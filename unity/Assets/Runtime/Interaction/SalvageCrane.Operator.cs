using UnityEngine;

namespace SomethingDownThere
{
    // The automatic operator: it works the pack's levers the way a person at the controls would,
    // full lever while far off and easing off early enough for the crane's own inertia and drag.
    public sealed partial class SalvageCrane
    {
        // The hook tip is "over" a point within this many metres while swinging slower than this (m/s):
        // loosely to enter a hole, closely to set a load down on its spot.
        private const float Alignment = .15f, SettledSpeed = .2f, SpotAlignment = .06f, SpotSettledSpeed = .1f;
        // Close enough to the set-down spot to start lowering; the load still settles a metre up first.
        private const float Approach = .3f;

        private void Park() => rig.Step(0, 0, Hoist(restRope), 0);

        // Over toward a find before its route is known: the hook at park height above it.
        private void HeadFor(Vector3 point)
        {
            Vector3 park = ParkPoint(point);
            Steer(park, rig.RopeFor(park.y));
        }

        // The hook over the hole mouth at park height.
        private void HoldHook() => Steer(Anchor, rig.RopeFor(Anchor.y));

        // The trolley held over the hole while the hook is off the hoist on the smart rope.
        private void HoldOver() => Steer(Anchor, rig.Rope);

        private void Operate(float dt)
        {
            job.PhaseSeconds = Mathf.Min(60, job.PhaseSeconds + dt);
            float ground = terrain.SurfaceHeight, travel = ground + travelClearance;
            switch (job.Phase)
            {
                case ExtractionPhase.Reaching:
                    // Swing over the hole mouth, the hook dropping to park height on the way.
                    HoldHook();
                    if (Over(Anchor)) SetPhase(ExtractionPhase.Lowering);
                    Revision++;
                    return;
                case ExtractionPhase.Lowering:
                {
                    // Straight down the shaft on the crane's cable, entering the ground only while the hook
                    // hangs over the mouth; then the hook leaves the hoist to ride the smart rope's end.
                    Vector3 drop = terrain.transform.TransformPoint(ExtractionSnapshot.Point(job.Route, DropDistance, out _));
                    bool clear = rig.Tip.y < ground || Over(Anchor);
                    Steer(Anchor, rig.RopeFor(clear ? drop.y : Mathf.Max(drop.y, ground + .5f)));
                    if (Mathf.Abs(rig.Rope - rig.RopeFor(drop.y)) < .01f)
                    {
                        rig.ReleaseHook();
                        ropeView.Carry(rig.Seat, Vector3.up, 0, true);
                        SetPhase(ExtractionPhase.Deploying);
                    }
                    Revision++;
                    return;
                }
                case ExtractionPhase.Lifting:
                {
                    // Straight up from the hole mouth until the load's underside is at travel height.
                    float rope = rig.RopeForSeat(travel + BelowGrab());
                    Steer(Anchor, rope);
                    if (Mathf.Abs(rig.Rope - rope) < .05f) SetPhase(ExtractionPhase.Carrying);
                    break;
                }
                case ExtractionPhase.Carrying:
                {
                    Vector3 aim = SpotAim(Spot(job.Spot));
                    Steer(aim, rig.RopeForSeat(travel + BelowGrab()));
                    if (Horizontal(rig.Seat - aim).magnitude < Approach && Straight) SetPhase(ExtractionPhase.SettingDown);
                    break;
                }
                case ExtractionPhase.SettingDown:
                {
                    // Hold a metre up until the load hangs still over its spot, then lower it onto the ground
                    // and let go once the hook has come to rest there.
                    Vector3 spot = Spot(job.Spot), aim = SpotAim(spot);
                    float touch = spot.y + BelowGrab() + .01f, rest = rig.RopeForSeat(touch);
                    // Already on the way down from the hold height (not merely level with it).
                    bool low = rig.Rope > rig.RopeForSeat(touch + 1) + .05f;
                    Steer(aim, low || Over(aim, SpotAlignment, SpotSettledSpeed, true) ? rest : rig.RopeForSeat(touch + 1));
                    if (Mathf.Abs(rig.Rope - rest) < .01f && Mathf.Abs(rig.Seat.y - touch) < .01f && Mathf.Abs(rig.TipVelocity.y) < .05f)
                    {
                        Follow(0); Ground(spot.y); Deliver();
                        return;
                    }
                    break;
                }
            }
            Follow(dt);
            Revision++;
        }

        private static Vector3 Horizontal(Vector3 v) { v.y = 0; return v; }

        // The hook (or a load on its seat) hangs over the point and has nearly stopped swinging.
        private bool Over(Vector3 point, float within = Alignment, float speed = SettledSpeed, bool seat = false)
            => Horizontal((seat ? rig.Seat : rig.Tip) - point).magnitude < within && Horizontal(rig.TipVelocity).magnitude < speed;

        // One operator step toward the hook hanging over a point at a hoist length. The rig's anti-sway
        // assist settles the hook under the trolley, so the trolley is steered straight at the point.
        private void Steer(Vector3 point, float rope)
        {
            rig.Aim(point, out float yaw, out float reach);
            reach = Mathf.Clamp(reach, rig.MinimumReach, rig.MaximumReach);
            float slew = Lever(Mathf.DeltaAngle(rig.Yaw, yaw) * Mathf.Deg2Rad, rig.YawSpeed * Mathf.Deg2Rad, rig.SlewAcceleration, rig.SlewDrag);
            float trolley = Lever(reach - rig.Reach, rig.ReachSpeed, rig.TrolleyAcceleration, rig.TrolleyDrag);
            rig.Step(slew, trolley, Hoist(rope), 0);
        }

        // Lever for a pack axis whose full lever accelerates by `acceleration` against `drag`:
        // critically damped near the target, saturating to the pack's top speed far from it.
        private static float Lever(float error, float velocity, float acceleration, float drag)
        {
            const float frequency = 3f;
            float gain = frequency * frequency / acceleration, lead = (2 * frequency - drag) / (frequency * frequency);
            return Mathf.Clamp(gain * (error - lead * velocity), -1, 1);
        }

        // The hoist moves a fixed length per step at full lever, so ease off to land exactly.
        private float Hoist(float rope) => Mathf.Clamp((Mathf.Clamp(rope, 0, rig.MaximumRope) - rig.Rope) / rig.HoistStep, -1, 1);

        // The pack's cargo attach: the load follows the hook point, held by its lifting eye. Once the
        // whole object is out of the ground it turns upright.
        private void Follow(float dt, bool straighten = true)
        {
            var body = payload.GetComponent<FindPhysics>().Body;
            Quaternion rotation = body.rotation;
            if (straighten && dt > 0 && Lowest(rotation, job.GrabLocal) > terrain.SurfaceHeight + .3f)
            {
                rotation = Quaternion.RotateTowards(rotation, Upright(rotation), straightenDegrees * dt);
            }
            Vector3 seat = rig.Seat + rig.TipVelocity * dt;
            Vector3 position = seat - rotation * Vector3.Scale(payload.transform.lossyScale, job.GrabLocal);
            if (dt > 0) { body.MovePosition(position); body.MoveRotation(rotation); }
            else payload.MoveRecovered(position, rotation);
            discoveries.NotifyMotion();
        }

        // The load's lowest corner rests exactly on the ground.
        private void Ground(float height)
        {
            var body = payload.GetComponent<FindPhysics>().Body;
            float lowest = body.position.y + LowestOffset(body.rotation);
            payload.MoveRecovered(body.position + Vector3.up * (height - lowest), body.rotation);
        }

        private bool Straight
        {
            get
            {
                var rotation = payload.GetComponent<FindPhysics>().Body.rotation;
                return Quaternion.Angle(rotation, Upright(rotation)) < .5f;
            }
        }

        // Where the hook hangs for the upright load's centre, not its eye, to come down on the spot.
        private Vector3 SpotAim(Vector3 spot)
        {
            var rotation = Upright(payload.GetComponent<FindPhysics>().Body.rotation);
            return spot + Horizontal(rotation * Vector3.Scale(payload.transform.lossyScale, job.GrabLocal - payload.LocalHull.center));
        }

        // Same heading, standing on its authored base (catalog uniques are authored upright).
        private static Quaternion Upright(Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(rotation * Vector3.down, Vector3.up);
            return forward.sqrMagnitude < .0001f ? Quaternion.identity : Quaternion.LookRotation(forward, Vector3.up);
        }

        // How far the load's underside hangs below the hook: the upright pose it arrives in.
        private float BelowGrab()
        {
            var rotation = payload.GetComponent<FindPhysics>().Body.rotation;
            Quaternion pose = job.Phase == ExtractionPhase.Lifting ? rotation : Upright(rotation);
            return Vector3.Dot(pose * Vector3.Scale(payload.transform.lossyScale, job.GrabLocal), Vector3.up) - LowestOffset(pose);
        }

        // World height of the hull's lowest corner for a load hanging from the hook's seat.
        private float Lowest(Quaternion rotation, Vector3 grab)
        {
            Vector3 origin = rig.Seat - rotation * Vector3.Scale(payload.transform.lossyScale, grab);
            return origin.y + LowestOffset(rotation);
        }

        private float LowestOffset(Quaternion rotation)
        {
            var hull = payload.LocalHull;
            Vector3 scale = payload.transform.lossyScale;
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? hull.min.x : hull.max.x, i / 2 % 2 == 0 ? hull.min.y : hull.max.y, i / 4 == 0 ? hull.min.z : hull.max.z);
                lowest = Mathf.Min(lowest, Vector3.Dot(rotation * Vector3.Scale(scale, corner), Vector3.up));
            }
            return lowest;
        }
    }
}
