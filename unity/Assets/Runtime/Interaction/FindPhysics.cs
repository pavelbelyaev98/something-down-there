using UnityEngine;

namespace SomethingDownThere
{
    // Soil attachment owns the kinematic state; pickup exposure is an independent rule.
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BuriedFind))]
    public sealed class FindPhysics : MonoBehaviour
    {
        private Rigidbody body;
        private MeshCollider shape;
        private float freeSpeedLimit, quietSeconds;
        private bool supported;
        private Vector3 quietPosition;
        private Quaternion quietRotation;
        private BuriedFind find;
        private TerrainVolume terrain;
        private DiscoveryField field;
        private bool supportDirty, suspended, recoveryHeld;
        private Vector3 observedPosition, safePosition;
        private Quaternion observedRotation, safeRotation;
        public bool Released { get; private set; }
        public Rigidbody Body => body;
        internal event System.Action<Collision> RecoveryContact;

        public void Initialize(TerrainVolume owner, DiscoveryField population)
        {
            terrain = owner; field = population; find = GetComponent<BuriedFind>(); body = GetComponent<Rigidbody>();
            shape = GetComponent<MeshCollider>(); freeSpeedLimit = body.maxLinearVelocity;
            if (field != null && field.PlayerCollider != null)
                Physics.IgnoreCollision(shape, field.PlayerCollider, true);
            Restore(false);
        }

        public void Restore(bool released)
        {
            body.maxLinearVelocity = freeSpeedLimit; ResetSettling();
            StopMotion();
            body.position = transform.position; body.rotation = transform.rotation;
            observedPosition = safePosition = body.position; observedRotation = safeRotation = body.rotation;
            Released = released; supportDirty = true; suspended = false; recoveryHeld = false;
            enabled = true;
            // Release in FixedUpdate after the terrain restore and collider rebuild finish.
        }

        public void TerrainChanged()
        {
            if (find != null && find.State != FindState.World) { enabled = false; return; }
            enabled = true;
            supportDirty = true;
            recoveryHeld = false;
            ResetSettling();
            if (Released && !body.isKinematic) body.WakeUp();
        }

        private void FixedUpdate()
        {
            using var profile = PhysicsMarker.Auto();
            if (find != null && find.State != FindState.World) { enabled = false; return; }
            if (terrain == null || find.Collected) return;
            if (terrain.IsRestoring)
            {
                if (!suspended) { StopMotion(); suspended = true; }
                return;
            }
            if (suspended) { suspended = false; supportDirty = true; }
            if (supportDirty)
            {
                supportDirty = false;
                // Administrative ground reset can rebury a released object at its current pose.
                if (Released && find.Exposure < .6f && terrain.IsSolid(body.position))
                {
                    Released = false; StopMotion(); field?.NotifyMotion();
                }
                if (!Released) TryReleaseFromSoil();
            }
            // An anchored find cannot move until a terrain event or explicit restore.
            // Remove it from Unity's fixed-update list instead of polling every buried item.
            if (!Released) { enabled = false; return; }
            if (recoveryHeld) return;
            if (body.isKinematic)
            {
                body.isKinematic = false; body.useGravity = true; body.WakeUp();
            }
            UpdateCollisionMode();
            SettleSupportedBody();
            bool moved = (body.position - observedPosition).sqrMagnitude > .00000001f
                || Quaternion.Angle(body.rotation, observedRotation) > .05f;
            if (!moved) return;
            if (!ValidPose())
            {
                // Preserve the same find at its last clear pose; never reroll or sell it.
                StopMotion(); body.position = safePosition; body.rotation = safeRotation;
                transform.SetPositionAndRotation(safePosition, safeRotation);
                recoveryHeld = true;
            }
            else { safePosition = body.position; safeRotation = body.rotation; }
            observedPosition = body.position; observedRotation = body.rotation;
            find.RefreshExposure(false); field?.NotifyMotion();
        }

        private bool ValidPose()
        {
            Vector3 p = terrain.transform.InverseTransformPoint(body.position);
            Vector3 extent = (Vector3)terrain.Dimensions * terrain.CellSize;
            return ExcavationGrid.Finite(p.x) && ExcavationGrid.Finite(p.y) && ExcavationGrid.Finite(p.z)
                && p.x >= 0 && p.z >= 0 && p.x <= extent.x && p.z <= extent.z
                && p.y >= 0
                // Contact near the rendered isosurface is valid; deep penetration is not.
                && terrain.SignedDensity(body.position) < terrain.CellSize * .6f;
        }

        internal bool TryReleaseFromSoil()
        {
            if (terrain == null || terrain.IsRestoring || find.State != FindState.World || !find.CanReleaseFromSoil()) return false;
            Released = true; supportDirty = recoveryHeld = false; enabled = true;
            safePosition = observedPosition = body.position; safeRotation = observedRotation = body.rotation;
            ResetSettling();
            body.isKinematic = false; body.useGravity = true; body.WakeUp();
            field?.NotifyMotion();
            return true;
        }

        private static readonly Unity.Profiling.ProfilerMarker PhysicsMarker = new Unity.Profiling.ProfilerMarker("Discovery.Physics");

        private void StopMotion()
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true; body.useGravity = false;
        }

        internal void ClaimForRecovery()
        {
            // The crane's rope owns the body until the crane sets it down; terrain notifications
            // must not freeze it again when its rotating hull clears another patch of soil.
            transform.SetPositionAndRotation(body.position, body.rotation);
            StopMotion();
            Released = true; enabled = false;
            ResetSettling();
        }

        private void OnCollisionEnter(Collision collision) => RecoveryContact?.Invoke(collision);

        private void OnCollisionStay(Collision collision)
        {
            // Unity sends contact callbacks even while normal find motion is
            // disabled for recovery. The crane's rope, not this component, owns the pull.
            RecoveryContact?.Invoke(collision);
            if (!Released) return;
            var supportBody = collision.rigidbody;
            if (supportBody != null && (supportBody.linearVelocity.sqrMagnitude > .0025f
                || supportBody.angularVelocity.sqrMagnitude > .01f)) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                // Require real upward support, not a speculative near-contact or wall.
                if (contact.separation <= .012f && Vector3.Dot(contact.normal, Vector3.up) > .65f)
                { supported = true; break; }
            }
        }

        private void ResetSettling() { quietSeconds = 0; supported = false; }

        internal void UpdateCollisionMode(CollisionDetectionMode continuous = CollisionDetectionMode.ContinuousSpeculative)
        {
            // Speculative CCD adds distant predicted contacts on the irregular hull.
            // Near rest those contacts can sustain rocking instead of letting it sleep.
            // Use exact discrete contacts at low speed; restore CCD before fast travel.
            // Hysteresis avoids switching modes on every contact impulse.
            float speedSquared = body.linearVelocity.sqrMagnitude, spinSquared = body.angularVelocity.sqrMagnitude;
            if (speedSquared < .0625f && spinSquared < 1f)
            {
                if (body.collisionDetectionMode != CollisionDetectionMode.Discrete)
                    body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
            else if (speedSquared > .5625f || spinSquared > 4f)
            {
                if (body.collisionDetectionMode != continuous)
                    body.collisionDetectionMode = continuous;
            }
        }

        private void SettleSupportedBody()
        {
            if (body.IsSleeping()) { ResetSettling(); return; }
            // Small contact oscillations can have sharp velocity peaks even though the
            // rock stays within the quiet pose envelope below. That envelope rejects real
            // sliding/tipping; strict instantaneous speed gates kept some poses rocking.
            // Dampen modest supported contact impulses before the stricter sleep gate.
            // Otherwise a peak just above that gate receives no damping and perpetuates
            // the next bounce. Airborne and fast-moving bodies are unaffected.
            if (supported && body.linearVelocity.sqrMagnitude <= .5625f && body.angularVelocity.sqrMagnitude <= 4f)
            {
                float damping = Mathf.Exp(-30f * Time.fixedDeltaTime);
                body.linearVelocity *= damping;
                body.angularVelocity *= damping;
            }
            if (!supported || body.linearVelocity.sqrMagnitude > .0625f || body.angularVelocity.sqrMagnitude > 1f)
                quietSeconds = 0;
            else
            {
                if (quietSeconds == 0) { quietPosition = body.position; quietRotation = body.rotation; }
                // A lump resting on a slope creeps at a steady millimetre speed forever:
                // gravity and the support damping cancel out, so the pose envelope below
                // never fills and the find rolls away instead of settling. Creep this slow
                // counts as quiet - real sliding and tipping are well above it.
                bool creeping = body.linearVelocity.sqrMagnitude <= .0225f && body.angularVelocity.sqrMagnitude <= .36f;
                if (!creeping && (Vector3.Distance(quietPosition, body.position) > .008f || Quaternion.Angle(quietRotation, body.rotation) > 2f))
                    quietSeconds = 0;
                else quietSeconds += Time.fixedDeltaTime;
                if (quietSeconds >= .65f)
                {
                    // Convex rocks can rock forever between nearby contact points.
                    // Sleep only after a bounded quiet window on actual support;
                    // keep the body dynamic so impacts and dug-away soil wake it.
                    body.linearVelocity = body.angularVelocity = Vector3.zero;
                    body.Sleep(); quietSeconds = 0;
                }
            }
            supported = false;
        }
    }
}
