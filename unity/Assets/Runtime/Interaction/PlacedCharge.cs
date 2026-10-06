using UnityEngine;

namespace SomethingDownThere
{
    // An armed C4 charge (026): thrown from the tool in a short straight arc to where the preview showed, it sticks flat
    // to the ground there and blinks until it goes off (WorksiteTools.Detonate). No bounce: it lands exactly where aimed.
    // If its ground is dug away it falls and lies where it settles, still armed; Interact picks it back up. Like lamps
    // and finds it never collides with the player.
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class PlacedCharge : MonoBehaviour, IInteractionTarget
    {
        // How long the throw takes, how high its arc rises (of its length) and how the arming light blinks.
        private const float FlightSeconds = .22f, FlightArc = .08f, BlinkPeriod = .9f, BlinkOn = .18f;
        [SerializeField] private Light armedLight;
        [SerializeField] private Renderer armedLed;
        private WorksiteTools owner;
        private Rigidbody body;
        private Collider shape, playerBody;
        private Vector3 flightFrom, previousPosition;
        private float flight = -1, blink, ledIntensity;
        private Vector3 suspendedVelocity, suspendedAngularVelocity;
        private bool suspended;
        private MaterialPropertyBlock block;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        public bool Stuck { get; private set; }
        public bool Flying => flight >= 0;
        public Vector3 SupportPoint { get; private set; }
        public Vector3 SupportNormal { get; private set; }
        public Light ArmedLight => armedLight;

        internal void Initialize(WorksiteTools tools, ChargeSnapshot state, Vector3? thrownFrom = null)
        {
            owner = tools; body = GetComponent<Rigidbody>(); shape = GetComponent<Collider>();
            playerBody = tools.Player.GetComponent<CharacterController>();
            Stuck = state.Stuck;
            SupportPoint = state.Position; SupportNormal = state.Rotation * Vector3.up;
            body.isKinematic = true;
            body.position = state.Position; body.rotation = state.Rotation;
            transform.SetPositionAndRotation(state.Position, state.Rotation);
            previousPosition = state.Position;
            if (!Stuck) { body.isKinematic = false; body.WakeUp(); }
            else if (thrownFrom.HasValue)
            {
                flightFrom = thrownFrom.Value; flight = 0;
                shape.enabled = false;
                transform.position = flightFrom;
            }
            foreach (var renderer in GetComponentsInChildren<Renderer>()) owner.RegisterRenderer(renderer);
            if (armedLed != null) ledIntensity = 1;
            blink = Random.value * BlinkPeriod;
        }

        public string GetPrompt(FpsPlayer player) => Flying ? "" : $"{player.InputSettings.Display(PlayerBinding.Interact)}  Pick up C4 charge";
        public bool TryInteract(FpsPlayer player) => owner != null && owner.Player == player && !Flying && owner.RetrieveCharge(this);

        // Ground under a stuck charge was dug away: it comes loose and falls.
        public void CheckSupport()
        {
            if (Flying) return;
            if (!Stuck) { if (!body.isKinematic) body.WakeUp(); return; }
            if (owner.HasChargeSupport(SupportPoint, SupportNormal)) return;
            Stuck = false;
            if (!suspended) { body.isKinematic = false; body.WakeUp(); }
            owner.Dirty();
        }

        public void Tick(bool playing, float deltaTime)
        {
            if (playerBody != null && shape.enabled && !Physics.GetIgnoreCollision(shape, playerBody)) Physics.IgnoreCollision(shape, playerBody, true);
            if (Flying && playing)
            {
                flight = Mathf.Min(1, flight + deltaTime / FlightSeconds);
                Vector3 along = Vector3.Lerp(flightFrom, SupportPoint, flight);
                float height = Vector3.Distance(flightFrom, SupportPoint) * FlightArc * 4 * flight * (1 - flight);
                transform.position = along + Vector3.up * height;
                if (flight >= 1)
                {
                    flight = -1; shape.enabled = true;
                    body.position = SupportPoint; transform.position = SupportPoint;
                }
            }
            if (!Stuck && suspended == playing)
            {
                if (!playing)
                {
                    suspendedVelocity = body.linearVelocity; suspendedAngularVelocity = body.angularVelocity;
                    body.isKinematic = true;
                }
                else { body.isKinematic = false; body.linearVelocity = suspendedVelocity; body.angularVelocity = suspendedAngularVelocity; }
                suspended = !playing;
            }
            if (!Stuck && previousPosition != body.position) { previousPosition = body.position; owner.Dirty(); }
            // A short red blink, like an armed detonator.
            blink = Mathf.Repeat(blink + deltaTime, BlinkPeriod);
            bool on = blink < BlinkOn;
            if (armedLight != null && armedLight.enabled != on) armedLight.enabled = on;
            if (armedLed != null)
            {
                float target = on ? 1 : .08f;
                if (!Mathf.Approximately(target, ledIntensity))
                {
                    ledIntensity = target;
                    block ??= new MaterialPropertyBlock();
                    armedLed.GetPropertyBlock(block);
                    block.SetColor(EmissionColorId, armedLed.sharedMaterial.GetColor(EmissionColorId) * ledIntensity);
                    armedLed.SetPropertyBlock(block);
                }
            }
        }

        // A charge still in the air is saved where it lands.
        public ChargeSnapshot Capture() => new ChargeSnapshot
        {
            Position = Flying || Stuck ? SupportPoint : transform.position,
            Rotation = transform.rotation, Stuck = Stuck
        };
    }
}
