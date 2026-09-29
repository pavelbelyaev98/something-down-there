using System;
using UnityEngine;

namespace SomethingDownThere
{
    // Slew and hook turn in degrees from the crane's authored heading, trolley reach from the mast
    // axis and hoist length in metres: everything needed to put the rig back after a load.
    [Serializable]
    public struct CranePose
    {
        public float Yaw, Reach, Rope, HookYaw;
        public bool Finite => WorldSnapshot.Finite(Yaw) && WorldSnapshot.Finite(Reach) && WorldSnapshot.Finite(Rope) && WorldSnapshot.Finite(HookYaw);
    }

    // The tower crane pack's own control model (its Controller_TC) on the pack's own rig: the same
    // bodies, joints, drags and per-step speeds, moved through four levers instead of key reads.
    // The vendor script cannot run here: it reads legacy Input (the project is Input System only),
    // its keys are the player's, and its hook stops at world y 0.3 while finds lie below ground.
    // SalvageCraneSetup strips it from the project variant; the vendor files stay untouched.
    [DisallowMultipleComponent]
    public sealed class TowerCraneRig : MonoBehaviour
    {
        [SerializeField] private Rigidbody cabin;          // point_cabine: slews about its Y axis
        [SerializeField] private Rigidbody truck;          // point_truck: runs along the jib (local Z)
        [SerializeField] private Rigidbody hook;           // point_hook: hangs on the hoist joint
        [SerializeField] private Rigidbody swivel;         // Point_Rotation_Hook: turns the hook block
        [SerializeField] private ConfigurableJoint hoist;  // point_hook's joint: its linear limit is the rope
        // Controller_TC's speeds: each is applied once per physics step, times speedGeneral (the pack's
        // overall speed knob, which SalvageCraneSetup raises for a brisk salvage crane).
        [SerializeField] private float speedGeneral = .01f, speedBoomRotation = .25f, speedTruck = 5f, speedHook = 5f, speedHookRotation = .25f;
        // The anti-sway assist (1/s²): pulls the hook back under the trolley, critically damped, so it
        // trails the trolley's accelerations and settles without swinging on.
        [SerializeField, Min(0)] private float swayStiffness;
        // Measured from the vendor rig by SalvageCraneSetup: trolley stops and the hanging offsets
        // below the trolley at zero rope (hook body, swivel, hook tip).
        [SerializeField] private float minimumReach = 2.5f, maximumReach = 33.5f, maximumRope = 250f;
        [SerializeField] private float hookDrop = 1.3f, swivelDrop = .25f, tipDrop = .5f;
        // Where the hoist cable leaves the trolley and enters the hook block, and the inside bottom of the
        // small hook's bowl, where a load or lifting eye hangs, in their bodies' local spaces.
        [SerializeField] private Vector3 sheave, ropeEntry, seat;
        private float rope, truckHeight;
        private bool initialized, released;
        private ConfigurableJointMotion[] hoistMotions;
        private RigidbodyInterpolation interpolation;
        private Quaternion swivelTurn;

        public bool Configured => cabin != null && truck != null && hook != null && swivel != null && hoist != null
            && hoist.GetComponent<Rigidbody>() == hook && minimumReach > 0 && maximumReach > minimumReach && maximumRope > 0;
        public float MinimumReach => minimumReach;
        public float MaximumReach => maximumReach;
        public float MaximumRope => maximumRope;
        public float Rope => rope;
        // Full-lever response per second and the bodies' own drag, for an operator that eases off.
        public float SlewAcceleration => speedBoomRotation * speedGeneral / Time.fixedDeltaTime;
        public float SlewDrag => cabin.angularDamping;
        public float TrolleyAcceleration => speedTruck * speedGeneral / Time.fixedDeltaTime;
        public float TrolleyDrag => truck.linearDamping;
        public float HoistStep => speedHook * speedGeneral;
        public float Yaw => Vector3.SignedAngle(transform.forward, Jib, transform.up);
        public float YawSpeed => Vector3.Dot(cabin.angularVelocity, transform.up) * Mathf.Rad2Deg;
        public float Reach => Vector3.Dot(truck.position - cabin.position, Jib);
        public float ReachSpeed => Vector3.Dot(truck.linearVelocity, Jib);
        public Vector3 Mast => cabin.position;
        public Vector3 Trolley => truck.position;
        // The hook's tip under the hook block, the rope's pendulum mass.
        public Vector3 Tip => hook.position - transform.up * (swivelDrop + tipDrop);
        public Vector3 TipVelocity => hook.linearVelocity;
        public Vector3 Sheave => truck.transform.TransformPoint(sheave);
        public Vector3 RopeEntry => hook.transform.TransformPoint(ropeEntry);
        public Vector3 Seat => swivel.transform.TransformPoint(seat);
        // From the hook's seat up to where the cable enters its block.
        public float HookLength => Vector3.Dot(RopeEntry - Seat, hook.transform.up);
        // Off the hoist joint, riding the smart rope's end.
        public bool Released => released;
        private Vector3 Jib => Vector3.ProjectOnPlane(cabin.rotation * Vector3.forward, transform.up).normalized;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized || !Configured) return;
            initialized = true;
            truckHeight = Vector3.Dot(truck.position - cabin.position, transform.up);
            rope = Mathf.Clamp(hoist.linearLimit.limit, 0, maximumRope);
            hoistMotions = new[] { hoist.xMotion, hoist.yMotion, hoist.zMotion, hoist.angularXMotion, hoist.angularYMotion, hoist.angularZMotion };
            interpolation = hook.interpolation;
        }

        // The hook block leaves the hoist joint to ride the smart rope's end down a tunnel: the cable is
        // drawn by CraneRopeView and both hook bodies are placed by hand.
        public void ReleaseHook()
        {
            Initialize();
            if (released) return;
            released = true;
            swivelTurn = Quaternion.Inverse(hook.rotation) * swivel.rotation;
            hoist.xMotion = hoist.yMotion = hoist.zMotion = ConfigurableJointMotion.Free;
            hoist.angularXMotion = hoist.angularYMotion = hoist.angularZMotion = ConfigurableJointMotion.Free;
            foreach (var body in new[] { hook, swivel }) { body.isKinematic = true; body.interpolation = RigidbodyInterpolation.None; }
        }

        // The released hook with its seat at `point`, its block toward the rope along `direction`.
        public void PlaceHook(Vector3 point, Vector3 direction)
        {
            if (!released) return;
            direction = direction.sqrMagnitude > .000001f ? direction.normalized : transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(hook.rotation * Vector3.forward, direction);
            if (forward.sqrMagnitude < .0001f) forward = Vector3.ProjectOnPlane(hook.rotation * Vector3.up, direction);
            Hang(point, Quaternion.LookRotation(forward, direction));
        }

        // Both hook bodies turned to `rotation`, the seat at `point`.
        private void Hang(Vector3 point, Quaternion rotation)
        {
            Quaternion turned = rotation * swivelTurn;
            Vector3 swivelAt = point - turned * Vector3.Scale(swivel.transform.lossyScale, seat);
            Place(hook, swivelAt + rotation * Vector3.up * swivelDrop, rotation);
            Place(swivel, swivelAt, turned);
        }

        // Back on the hoist joint, upright under its seat, the rope taking up the current drop.
        public void SeatHook()
        {
            Initialize();
            if (!released) return;
            Quaternion heading = Quaternion.LookRotation(Vector3.ProjectOnPlane(cabin.rotation * Vector3.forward, transform.up), transform.up);
            Hang(Seat, heading);
            SetRope(Vector3.Dot(truck.position - hook.position, transform.up) - hookDrop);
            released = false;
            foreach (var body in new[] { hook, swivel }) { body.isKinematic = false; body.interpolation = interpolation; }
            Place(hook, hook.position, heading);
            Place(swivel, swivel.position, heading * swivelTurn);
            hoist.xMotion = hoistMotions[0]; hoist.yMotion = hoistMotions[1]; hoist.zMotion = hoistMotions[2];
            hoist.angularXMotion = hoistMotions[3]; hoist.angularYMotion = hoistMotions[4]; hoist.angularZMotion = hoistMotions[5];
        }

        // Controller_TC.FixedUpdate, lever by lever. Slew and hook turn: arrows / E-Q; trolley: W-S;
        // hoist: down/up arrow (positive pays out). No world-height floor: the operator decides.
        public void Step(float slew, float trolley, float lower, float hookTurn)
        {
            Initialize();
            slew = Mathf.Clamp(slew, -1, 1); trolley = Mathf.Clamp(trolley, -1, 1);
            lower = Mathf.Clamp(lower, -1, 1); hookTurn = Mathf.Clamp(hookTurn, -1, 1);
            if (slew != 0) cabin.AddRelativeTorque(0, slew * speedBoomRotation * speedGeneral, 0, ForceMode.VelocityChange);
            if (trolley != 0) truck.AddRelativeForce(0, 0, trolley * speedTruck * speedGeneral, ForceMode.VelocityChange);
            if (hookTurn != 0) swivel.AddRelativeTorque(0, hookTurn * speedHookRotation * speedGeneral, 0, ForceMode.VelocityChange);
            if (lower != 0) SetRope(rope + lower * speedHook * speedGeneral);
            if (swayStiffness > 0)
            {
                Vector3 offset = Vector3.ProjectOnPlane(hook.position - truck.position, transform.up);
                Vector3 relative = Vector3.ProjectOnPlane(hook.linearVelocity - truck.linearVelocity, transform.up);
                float frequency = Mathf.Sqrt(Physics.gravity.magnitude / (hookDrop + rope) + swayStiffness);
                hook.AddForce(-offset * swayStiffness - relative * Mathf.Max(0, 2 * frequency - hook.linearDamping), ForceMode.Acceleration);
            }
            // The pack wakes both bodies each step; an idle crane may sleep until a lever moves.
            if (trolley != 0 || lower != 0) { truck.WakeUp(); hook.WakeUp(); }
        }

        private void SetRope(float length)
        {
            rope = Mathf.Clamp(length, 0, maximumRope);
            var limit = hoist.linearLimit; limit.limit = rope; hoist.linearLimit = limit;
        }

        // Hoist length that puts the hook tip at this world height, the trolley's height unchanged.
        public float RopeFor(float tipHeight) => Vector3.Dot(truck.position, transform.up) - hookDrop - swivelDrop - tipDrop - tipHeight;
        // Hoist length that hangs the hook's seat (a carried load's grab) at this world height.
        public float RopeForSeat(float seatHeight) => RopeFor(seatHeight) + tipDrop + Vector3.Dot(swivel.transform.TransformVector(seat), transform.up);

        // Slew angle and trolley reach that hang the hook straight above a world point.
        public void Aim(Vector3 point, out float yaw, out float reach)
        {
            Vector3 offset = Vector3.ProjectOnPlane(point - cabin.position, transform.up);
            yaw = Vector3.SignedAngle(transform.forward, offset, transform.up);
            reach = offset.magnitude;
        }

        public bool Reaches(Vector3 point, float margin = .3f)
        {
            Aim(point, out _, out float reach);
            return reach >= minimumReach + margin && reach <= maximumReach - margin;
        }

        public bool Holds(CranePose pose) => pose.Finite && Mathf.Abs(pose.Yaw) <= 360 && Mathf.Abs(pose.HookYaw) <= 360
            && pose.Reach >= minimumReach - .5f && pose.Reach <= maximumReach + .5f && pose.Rope >= 0 && pose.Rope <= maximumRope;

        public CranePose Capture() => new CranePose { Yaw = Yaw, Reach = Reach, Rope = rope,
            HookYaw = Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(swivel.rotation * Vector3.forward, transform.up), transform.up) };

        // Puts every moving body where the pose says, hanging still. Joints keep the anchors they
        // were created with: the trolley slides along its free axis and the hook along its limit.
        public void Apply(CranePose pose)
        {
            Initialize();
            SeatHook();
            SetRope(pose.Rope);
            Quaternion heading = transform.rotation * Quaternion.Euler(0, pose.Yaw, 0);
            Place(cabin, cabin.position, heading);
            Vector3 trolley = cabin.position + transform.up * truckHeight
                + heading * Vector3.forward * Mathf.Clamp(pose.Reach, minimumReach, maximumReach);
            Place(truck, trolley, heading);
            Vector3 hanging = trolley - transform.up * (hookDrop + rope);
            Place(hook, hanging, heading);
            Place(swivel, hanging - transform.up * swivelDrop, transform.rotation * Quaternion.Euler(0, pose.HookYaw, 0));
            Physics.SyncTransforms();
        }

        private static void Place(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            body.transform.SetPositionAndRotation(position, rotation);
            body.position = position; body.rotation = rotation;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
    }
}
