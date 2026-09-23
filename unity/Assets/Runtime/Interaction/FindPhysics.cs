using UnityEngine;

namespace SomethingDownThere
{
    // Soil attachment owns the kinematic state; pickup exposure is an independent rule.
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BuriedFind))]
    public sealed class FindPhysics : MonoBehaviour
    {
        [SerializeField, Range(1f, 12f)] private float throwSpeed = 8f;
        private Rigidbody body;
        private MeshCollider shape;
        private Collider[] holdOverlaps;
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
        public bool Held { get; private set; }
        public float ThrowSpeed => Mathf.Clamp(throwSpeed, 1f, 12f);
        public Rigidbody Body => body;

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
            Held = false;
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
            if (terrain == null || find.Collected || Held) return;
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
            if (Held || terrain == null || terrain.IsRestoring || find.State != FindState.World || !find.CanReleaseFromSoil()) return false;
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
            // Winch owns the body until release/storage; terrain notifications must
            // not freeze it again when its rotating hull clears another patch of soil.
            transform.SetPositionAndRotation(body.position, body.rotation);
            StopMotion();
            Held = false; Released = true; enabled = false;
            ResetSettling();
        }

        internal void BeginHold()
        {
            enabled = true;
            Held = Released = true; recoveryHeld = suspended = supportDirty = false;
            ResetSettling();
            body.isKinematic = false; body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            safePosition = observedPosition = body.position; safeRotation = observedRotation = body.rotation;
            body.WakeUp(); field?.NotifyMotion();
        }

        // A held find remains a real rigid body. Contacts stop it at walls/soil;
        // bounded velocity draws it into view without teleporting through blockers.
        internal bool MoveHeld(Vector3 target, Quaternion rotation, Vector3 carrierVelocity, float deltaTime)
        {
            if (!Held || terrain == null || terrain.IsRestoring) return false;
            if (!ValidPose())
            {
                StopMotion(); body.position = safePosition; body.rotation = safeRotation;
                transform.SetPositionAndRotation(safePosition, safeRotation);
                EndHold(Vector3.zero); return false;
            }
            safePosition = observedPosition = body.position; safeRotation = observedRotation = body.rotation;
            target = ClearHoldTarget(target, rotation);
            // The hand travels with the player, independently of item throw strength.
            // Feed forward actual controller motion so flight/falling does not build
            // a speed-dependent gap. Contact response still owns the resulting pose.
            body.maxLinearVelocity = Mathf.Max(freeSpeedLimit, carrierVelocity.magnitude + 12f);
            float response = Mathf.Min(20f, 1f / Mathf.Max(.001f, deltaTime));
            body.linearVelocity = carrierVelocity + Vector3.ClampMagnitude((target - body.position) * response, 12f);
            Quaternion delta = rotation * Quaternion.Inverse(body.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            body.angularVelocity = angle > -.01f && angle < .01f ? Vector3.zero
                : Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * 14f), 8f);
            find.RefreshExposure(false); field?.NotifyMotion();
            return true;
        }

        internal void EndHold(Vector3 velocity)
        {
            if (!Held) return;
            Held = false; Released = true; supportDirty = recoveryHeld = false;
            body.maxLinearVelocity = freeSpeedLimit; ResetSettling();
            body.isKinematic = false; body.useGravity = true;
            body.linearVelocity = velocity; body.angularVelocity = Vector3.zero;
            UpdateCollisionMode();
            safePosition = observedPosition = body.position; safeRotation = observedRotation = body.rotation;
            body.WakeUp(); find.RefreshExposure(false); field?.NotifyMotion();
        }

        private Vector3 ClearHoldTarget(Vector3 target, Quaternion rotation)
        {
            // Keep the desired rotated hull out of soil/props when pitching down.
            // This only adjusts the goal: the dynamic body still travels through
            // normal contact solving, never teleports to the far side of a wall.
            if (holdOverlaps == null) holdOverlaps = new Collider[32];
            float radius = Vector3.Scale(shape.sharedMesh.bounds.extents, transform.lossyScale).magnitude + .03f;
            for (int pass = 0; pass < 3; pass++)
            {
                Vector3 center = target + rotation * Vector3.Scale(shape.sharedMesh.bounds.center, transform.lossyScale);
                int count = Physics.OverlapSphereNonAlloc(center, radius, holdOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == holdOverlaps.Length) return body.position; // Crowded query: retain the known pose.
                bool adjusted = false;
                for (int i = 0; i < count; i++)
                {
                    var other = holdOverlaps[i];
                    if (other == shape || other == field?.PlayerCollider
                        || Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer)
                        || Physics.GetIgnoreCollision(shape, other)) continue;
                    if (!Physics.ComputePenetration(shape, target, rotation, other, other.transform.position, other.transform.rotation,
                        out Vector3 direction, out float distance)) continue;
                    target += direction * (distance + .015f); adjusted = true;
                }
                if (!adjusted) break;
            }
            return target;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (Held || !Released) return;
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

        private void UpdateCollisionMode()
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
                if (body.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative)
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
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
            // the next bounce. Airborne, held and fast-moving bodies are unaffected.
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
