using UnityEngine;

namespace SomethingDownThere
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class WorkLamp : MonoBehaviour, IInteractionTarget
    {
        [SerializeField] private Light workLight;
        private WorksiteTools owner;
        private Rigidbody body;
        private Vector3 supportPoint, supportNormal, previousPosition;
        private Quaternion previousRotation;
        private Vector3 suspendedVelocity, suspendedAngularVelocity;
        private bool suspended;
        private Collider shape, playerBody;
        private float authoredIntensity, shining;
        private Vector3 domeLocal;
        // How far out the lamp shines from, and the clear space its light keeps (see AimLight).
        private const float LightThrow = .3f, LightClearance = .08f;
        private static readonly Collider[] crowding = new Collider[8];
        // Set by the kit each frame: 1 for a lamp inside the lit budget, fading near the cull distance.
        public float Shine { get; set; }
        public int Slot { get; private set; }
        public bool Anchored { get; private set; }
        public Light WorkLight => workLight;
        public Rigidbody Body => body;
        public Vector3 SupportPoint => supportPoint;

        public void Initialize(WorksiteTools tools, LampSnapshot state)
        {
            owner = tools; body = GetComponent<Rigidbody>(); Slot = state.Slot;
            // Initialize the physics pose as well as the Transform. Interpolation can
            // otherwise restore the freshly-instantiated prefab pose on the next step.
            body.isKinematic = state.Anchored;
            body.position = state.Position; body.rotation = state.Rotation;
            transform.SetPositionAndRotation(state.Position, state.Rotation);
            previousPosition = state.Position; previousRotation = state.Rotation;
            supportPoint = state.SupportPoint; supportNormal = state.SupportNormal;
            Anchored = state.Anchored;
            if (!Anchored) { body.linearVelocity = state.LinearVelocity; body.angularVelocity = state.AngularVelocity; }
            foreach (var renderer in GetComponentsInChildren<Renderer>()) owner.RegisterRenderer(renderer);
            shape = GetComponent<Collider>(); playerBody = tools.Player.GetComponent<CharacterController>();
            if (workLight != null)
            {
                authoredIntensity = workLight.intensity; domeLocal = workLight.transform.localPosition;
                workLight.enabled = false; AimLight();
            }
        }

        // A lamp this small sits almost on the ground, where its light would only skim the floor
        // and catch on every bump. It shines from a point a little way out, like a lantern held off
        // the surface: along its axis when mounted, straight up when it lies loose (it may have
        // tumbled). The point keeps clear space around it, so the light never sits inside or against
        // ground or a find resting on the lamp; that turned rocks black with huge broken shadows.
        private void AimLight()
        {
            if (workLight == null) return;
            Vector3 dome = transform.TransformPoint(domeLocal), axis = Anchored ? transform.up : Vector3.up;
            float reach = LightThrow;
            if (Physics.SphereCast(dome, LightClearance, axis, out var hit, LightThrow,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) reach = hit.distance;
            while (reach > 0 && Crowded(dome + axis * reach)) reach = Mathf.Max(0, reach - .05f);
            workLight.transform.position = dome + axis * reach;
        }

        private bool Crowded(Vector3 point)
        {
            int count = Physics.OverlapSphereNonAlloc(point, LightClearance, crowding, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (crowding[i] != shape) return true;
            return false;
        }

        public string GetPrompt(FpsPlayer player) => $"{player.InputSettings.Display(PlayerBinding.Interact)}  Pick up work lamp";
        public bool TryInteract(FpsPlayer player) => owner != null && owner.Player == player && owner.Retrieve(this);

        public void CheckSupport()
        {
            AimLight();
            // A loose lamp may be asleep on ground that was just dug away.
            if (!Anchored) { if (!body.isKinematic) body.WakeUp(); return; }
            if (owner.HasLampSupport(supportPoint, supportNormal)) return;
            ReleaseFromSupport();
        }

        internal bool ReleaseFromSupport()
        {
            if (!Anchored) return false;
            Anchored = false;
            if (!suspended) { body.isKinematic = false; body.WakeUp(); }
            owner.Dirty();
            return true;
        }

        public void Tick(bool playing, float deltaTime)
        {
            // Like finds, lamps never collide with the player. Toggling the character controller
            // (restore, rescue) clears the pair, so it is re-applied when lost.
            if (playerBody != null && !Physics.GetIgnoreCollision(shape, playerBody)) Physics.IgnoreCollision(shape, playerBody, true);
            if (!Anchored && suspended == playing)
            {
                if (!playing)
                {
                    suspendedVelocity = body.linearVelocity; suspendedAngularVelocity = body.angularVelocity;
                    body.isKinematic = true;
                }
                else
                {
                    body.isKinematic = false; body.linearVelocity = suspendedVelocity; body.angularVelocity = suspendedAngularVelocity;
                }
                suspended = !playing;
            }
            if (!Anchored && (previousPosition != body.position || previousRotation != body.rotation))
            {
                previousPosition = body.position; previousRotation = body.rotation; owner.Dirty();
            }
            // Lamps outside the lit budget keep their glowing dome but submit no light or shadows;
            // entering or leaving the budget fades instead of popping.
            if (workLight == null) return;
            shining = Mathf.MoveTowards(shining, Shine, deltaTime / .35f);
            bool on = shining > .001f;
            if (workLight.enabled != on) workLight.enabled = on;
            // Finds and loose lamps can come to rest against a lamp at any time: re-aim every
            // frame, which the lit budget keeps to a handful of lamps.
            if (on) { workLight.intensity = authoredIntensity * shining; AimLight(); }
        }

        public LampSnapshot Capture() => new LampSnapshot
        {
            Slot = Slot, Position = transform.position, Rotation = transform.rotation, Anchored = Anchored,
            SupportPoint = supportPoint, SupportNormal = supportNormal,
            LinearVelocity = Anchored ? Vector3.zero : suspended ? suspendedVelocity : body.linearVelocity,
            AngularVelocity = Anchored ? Vector3.zero : suspended ? suspendedAngularVelocity : body.angularVelocity
        };
    }
}
