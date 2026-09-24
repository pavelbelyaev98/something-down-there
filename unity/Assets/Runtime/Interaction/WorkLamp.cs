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
        }

        public string GetPrompt(FpsPlayer player) => $"{player.InputSettings.Display(PlayerBinding.Interact)}  Pick up work lamp";
        public bool TryInteract(FpsPlayer player) => owner != null && owner.Player == player && owner.Retrieve(this);

        public void CheckSupport()
        {
            if (!Anchored || owner.HasLampSupport(supportPoint, supportNormal)) return;
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

        public void Tick(bool playing, Vector3 cameraPosition)
        {
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
            // Far lamps cannot illuminate a visible nearby surface. Keep their emissive lens,
            // but avoid submitting lights/shadows for the rest of a deep route.
            if (workLight != null) workLight.enabled = (cameraPosition - transform.position).sqrMagnitude < WorksiteTools.LightCullDistance * WorksiteTools.LightCullDistance;
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
