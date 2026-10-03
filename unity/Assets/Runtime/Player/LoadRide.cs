using UnityEngine;

namespace SomethingDownThere
{
    // Unique finds are solid to the player (FindPhysics; commons still let the player through). Standing on
    // one, the player moves with it as a rigid part of it, so a load the salvage crane hauls carries a rider
    // up the shaft and over to camp; one moving into the player's side pushes the player aside (user, 2026-10-03).
    // A carried player is not pushed down onto the load (FpsPlayer): its collider runs up to a physics step
    // ahead of the drawn, interpolated load, so a rider resting on the collider dropped after it in steps.
    internal sealed class LoadRide
    {
        // A carrier that jumps further than this in one frame was teleported (restore, set-down), not moved.
        private const float MaximumStep = 2f;
        // The player stands on a carrier that touches the capsule this far (share of its radius) below the
        // centre of its bottom sphere; contact counts within this gap (metres) beyond the controller's skin.
        private const float StandingDepth = .5f, ContactGap = .02f;
        // A rider lifted more than this (metres) off the drawn load (a jolt pushing the controller out) sinks
        // back to it at this speed (m/s); held just clear, the rider never presses on the collider running ahead.
        private const float SettleMargin = .05f, SettleSpeed = 1.5f;
        private readonly CharacterController motor;
        private readonly int mask;
        private readonly Collider[] overlaps = new Collider[16];
        private Collider carrier;
        private bool standing;
        private Vector3 lastPosition, pushOut;
        private Quaternion lastRotation;

        public LoadRide(CharacterController motor, int mask) { this.motor = motor; this.mask = mask; }

        // The player stands on a moving unique, which carries them.
        public bool Standing => carrier != null && standing;

        public void Clear() => carrier = null;

        // Moves the player by how far the carrier moved since the last frame where the player stands: all of
        // it when standing on the carrier (and down onto it if lifted off), otherwise only the part that
        // pushes into the player. The controller's own collision keeps the player out of walls.
        public void Carry(float deltaTime)
        {
            if (carrier == null || !carrier.gameObject.activeInHierarchy) { carrier = null; return; }
            Transform load = carrier.transform;
            Vector3 feet = motor.transform.position;
            Vector3 moved = load.position + load.rotation * Quaternion.Inverse(lastRotation) * (feet - lastPosition) - feet;
            if (moved.sqrMagnitude > MaximumStep * MaximumStep) return;
            if (!standing) moved = pushOut * Mathf.Max(0, Vector3.Dot(moved, pushOut));
            else moved += Vector3.down * Mathf.Clamp(Floating(moved) - SettleMargin, 0, SettleSpeed * deltaTime);
            if (moved.sqrMagnitude > 1e-8f) motor.Move(moved);
        }

        // How far the player's capsule, moved by `offset`, floats above the drawn load beneath its middle,
        // beyond the controller's skin. The ray hits the collider at its physics pose, carried back to the
        // drawn pose.
        private float Floating(Vector3 offset)
        {
            Vector3 up = motor.transform.up;
            Vector3 bottom = motor.transform.TransformPoint(motor.center) + offset - up * (motor.height * .5f - motor.radius);
            float reach = motor.radius + motor.skinWidth + ContactGap * 2;
            if (!carrier.Raycast(new Ray(bottom, -up), out var hit, reach)) return 0;
            Vector3 point = hit.point;
            var body = carrier.attachedRigidbody;
            if (body != null)
                point = body.transform.position + body.transform.rotation * (Quaternion.Inverse(body.rotation) * (point - body.position));
            return Mathf.Max(0, Vector3.Dot(bottom - point, up) - motor.radius - motor.skinWidth);
        }

        // Finds the unique the player stands on or is pressed against, and remembers its pose.
        public void Track()
        {
            carrier = null;
            Vector3 up = motor.transform.up, centre = motor.transform.TransformPoint(motor.center);
            float reach = motor.radius + motor.skinWidth + ContactGap, half = Mathf.Max(0, motor.height * .5f - motor.radius);
            Vector3 bottom = centre - up * half;
            int count = Physics.OverlapCapsuleNonAlloc(bottom - up * ContactGap, centre + up * half, reach,
                overlaps, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (overlaps[i] == motor) continue;
                var find = overlaps[i].GetComponentInParent<BuriedFind>();
                if (find == null || find.Kind != DiscoveryKind.Unique) continue;
                Vector3 touch = overlaps[i].ClosestPoint(bottom);
                standing = Vector3.Dot(touch - bottom, up) < -motor.radius * StandingDepth;
                pushOut = Vector3.ProjectOnPlane(bottom - touch, up);
                // Inside the carrier's hull the closest point is the bottom itself: push away from its centre.
                if (pushOut.sqrMagnitude < 1e-6f) pushOut = Vector3.ProjectOnPlane(bottom - overlaps[i].bounds.center, up);
                pushOut = pushOut.sqrMagnitude > 1e-6f ? pushOut.normalized : Vector3.zero;
                carrier = overlaps[i];
                lastPosition = carrier.transform.position;
                lastRotation = carrier.transform.rotation;
                if (standing) return;
            }
        }
    }
}
