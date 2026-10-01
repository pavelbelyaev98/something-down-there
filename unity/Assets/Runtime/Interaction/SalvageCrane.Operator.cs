using UnityEngine;

namespace SomethingDownThere
{
    // The automatic operator: it works the pack's levers the way a person at the controls would,
    // full lever while far off and easing off early enough for the crane's own inertia and drag.
    public sealed partial class SalvageCrane
    {
        // The crane is aligned with the hole once its trolley holds within this many metres of the park
        // point over the mouth, moving slower than this (m/s). The hook's own swing does not matter: from
        // there the hook is the smart rope's end and goes straight down.
        private const float TrolleyAlignment = .25f, TrolleySettledSpeed = .3f;
        // Over a set-down spot: the hook's seat within this many metres and slower than this (m/s), the
        // load swinging slower than LoadSettledSpeed.
        private const float SpotAlignment = .1f, SpotSettledSpeed = .15f, LoadSettledSpeed = .4f;
        // Close enough to the set-down spot to start lowering; the load still settles a metre up first.
        private const float Approach = .3f;
        // With a load on the hook the hoist moves it like an operator would: at most LoadHoistSpeed (m/s),
        // TouchdownSpeed for the last metre onto the ground, speeding up and slowing down at
        // LoadHoistAcceleration (m/s^2) instead of starting and stopping dead.
        private const float LoadHoistSpeed = 1.5f, TouchdownSpeed = .6f, LoadHoistAcceleration = 1.5f;
        // A load moving slower than RestSpeed (m/s) and RestSpin (rad/s) for RestSeconds has come to rest,
        // or after SettleSeconds at most.
        private const float RestSpeed = .05f, RestSpin = .1f, RestSeconds = .5f, SettleSeconds = 8f;
        private float restSeconds, hoistRate;

        private void Park() => rig.Step(0, 0, Hoist(restRope), 0);

        // Over toward a find before its route is known.
        private void HeadFor(Vector3 point)
        {
            Vector3 park = ParkPoint(point);
            Steer(park, HoldRope(park));
        }

        // The trolley held over the hole while the hook is off the hoist on the smart rope.
        private void HoldOver() => Steer(Anchor, rig.Rope);

        // While the crane swings round, the hook keeps its height (never lower than where the ride would
        // start below the park point).
        private float HoldRope(Vector3 park) => Mathf.Min(rig.Rope, rig.RopeForSeat(park.y - rig.HookLength));

        private bool Aligned(Vector3 point) => Horizontal(rig.Trolley - point).magnitude < TrolleyAlignment
            && Horizontal(rig.TrolleyVelocity).magnitude < TrolleySettledSpeed;

        private void Operate(float dt)
        {
            job.PhaseSeconds = Mathf.Min(60, job.PhaseSeconds + dt);
            float travel = terrain.SurfaceHeight + travelClearance + Hanging;
            switch (job.Phase)
            {
                case ExtractionPhase.Reaching:
                    // Swing over the hole mouth, the hook keeping its height; as soon as the crane is
                    // aligned the hook is the smart rope's end and goes straight on down from where it is.
                    Steer(Anchor, HoldRope(Anchor));
                    Revision++;
                    if (Aligned(Anchor))
                    {
                        rig.ReleaseHook();
                        ropeView.Carry(rig.Seat, Vector3.up, true);
                        SetPhase(ExtractionPhase.Deploying);
                        job.Progress = Mathf.Min(rig.HookLength, ExtractionSnapshot.Length(job.Route));
                        rideSpeed = 0;
                        BeginDescent();
                    }
                    return;
                case ExtractionPhase.Lifting:
                {
                    // Straight up from the hole mouth until the hanging load clears travel height.
                    float rope = rig.RopeForSeat(travel);
                    Steer(Anchor, rope, EasedHoist(rope, LoadHoistSpeed, dt));
                    if (Mathf.Abs(rig.Rope - rope) < .05f) SetPhase(ExtractionPhase.Carrying);
                    break;
                }
                case ExtractionPhase.Carrying:
                {
                    Vector3 spot = Spot(job.Spot);
                    float rope = rig.RopeForSeat(travel);
                    Steer(spot, rope, EasedHoist(rope, LoadHoistSpeed, dt));
                    if (Horizontal(rig.Seat - spot).magnitude < Approach) SetPhase(ExtractionPhase.SettingDown);
                    break;
                }
                case ExtractionPhase.SettingDown:
                {
                    // Ease down to a metre up and hold until the hook and its load hang still over the spot,
                    // then lower it slowly and let go as soon as the ground takes its weight: it lands as it
                    // hangs, never pushed into the ground.
                    Vector3 spot = Spot(job.Spot);
                    float hold = rig.RopeForSeat(spot.y + Hanging + 1), bottom = rig.RopeForSeat(spot.y);
                    bool lowering = rig.Rope > hold + .05f
                        || Mathf.Abs(rig.Rope - hold) < .05f && Over(spot, SpotAlignment, SpotSettledSpeed, true)
                            && LoadBody.linearVelocity.magnitude < LoadSettledSpeed;
                    float rope = lowering ? bottom : hold;
                    Steer(spot, rope, EasedHoist(rope, lowering ? TouchdownSpeed : LoadHoistSpeed, dt));
                    Hang(dt);
                    // Let go the moment it rests on the ground (read every step, so an earlier brush
                    // cannot count); a hole under the spot never takes the weight: let go at the bottom.
                    bool grounded = Grounded;
                    if (lowering && (grounded || Mathf.Abs(rig.Rope - bottom) < .01f)) { LetGo(); SetPhase(ExtractionPhase.Settling); }
                    Revision++;
                    return;
                }
                case ExtractionPhase.Settling:
                {
                    // The hook hoists clear while the load settles wherever it landed; there it stays.
                    Park();
                    Wake();
                    var body = LoadBody;
                    restSeconds = body.linearVelocity.magnitude < RestSpeed && body.angularVelocity.magnitude < RestSpin ? restSeconds + dt : 0;
                    Revision++;
                    if (restSeconds >= RestSeconds || job.PhaseSeconds >= SettleSeconds) Deliver();
                    return;
                }
            }
            Hang(dt);
            Revision++;
        }

        private static Vector3 Horizontal(Vector3 v) { v.y = 0; return v; }

        // The hook (or its seat) hangs over the point and has nearly stopped swinging.
        private bool Over(Vector3 point, float within, float speed, bool seat = false)
            => Horizontal((seat ? rig.Seat : rig.Tip) - point).magnitude < within && Horizontal(rig.TipVelocity).magnitude < speed;

        // One operator step toward the hook hanging over a point at a hoist length. The rig's anti-sway
        // assist settles the hook under the trolley, so the trolley is steered straight at the point.
        private void Steer(Vector3 point, float rope) => Steer(point, rope, Hoist(rope));

        private void Steer(Vector3 point, float rope, float hoistLever)
        {
            rig.Aim(point, out float yaw, out float reach);
            reach = Mathf.Clamp(reach, rig.MinimumReach, rig.MaximumReach);
            float slew = Lever(Mathf.DeltaAngle(rig.Yaw, yaw) * Mathf.Deg2Rad, rig.YawSpeed * Mathf.Deg2Rad, rig.SlewAcceleration, rig.SlewDrag);
            float trolley = Lever(reach - rig.Reach, rig.ReachSpeed, rig.TrolleyAcceleration, rig.TrolleyDrag);
            rig.Step(slew, trolley, hoistLever, 0);
        }

        // Hoist lever that eases a load toward a hoist length: speeding up and slowing down at the load's
        // acceleration, never faster than `speed`, landing on the length without overshoot.
        private float EasedHoist(float rope, float speed, float dt)
        {
            float error = Mathf.Clamp(rope, 0, rig.MaximumRope) - rig.Rope;
            float wanted = Mathf.Sign(error) * Mathf.Min(speed, Mathf.Sqrt(2 * LoadHoistAcceleration * Mathf.Abs(error)), Mathf.Abs(error) / Mathf.Max(dt, .0001f));
            hoistRate = Mathf.MoveTowards(hoistRate, wanted, LoadHoistAcceleration * dt);
            if (Mathf.Abs(hoistRate) * dt > Mathf.Abs(error)) hoistRate = error / Mathf.Max(dt, .0001f);
            return Mathf.Clamp(hoistRate / rig.HoistSpeed, -1, 1);
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

        // How far the load's lowest point can hang below the hook's seat, whichever way it turns: the
        // eye's link plus the hull corner farthest from the eye.
        private float Hanging
        {
            get
            {
                var hull = payload.LocalHull;
                Vector3 scale = payload.transform.lossyScale;
                float farthest = 0;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(i % 2 == 0 ? hull.min.x : hull.max.x, i / 2 % 2 == 0 ? hull.min.y : hull.max.y, i / 4 == 0 ? hull.min.z : hull.max.z);
                    farthest = Mathf.Max(farthest, Vector3.Scale(scale, corner - job.AttachLocal).magnitude);
                }
                return RecoveryMarkView.HookReach + farthest;
            }
        }
    }
}
