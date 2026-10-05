using System;
using UnityEngine;

namespace SomethingDownThere
{
    // An old chest at the bottom of a stash pit (106): a container opened where it lies. Its hollow is
    // seeded air, so what it holds lies loose inside from New Game, behind its walls and lid. Once the lid
    // has room to swing, one tool strike breaks the rusted lock and opens it; the contents are ordinary
    // finds from then on. Never sold, bagged or craned. Anchored while soil holds it up, it falls once undercut.
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(Animation))]
    public sealed class BuriedChest : MonoBehaviour, IDigTarget, IInteractionTarget
    {
        [SerializeField] private string displayName = "Old chest";
        // Measured from the closed model by the discovery sync (ChestSetup), in the chest's own frame: its hollow,
        // the seats on its floor, the space its lid sweeps opening and points just under its base.
        [SerializeField] private Vector3 hollowCentre, hollowHalf;
        [SerializeField] private Vector3[] contentSeats = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] lidSpace = Array.Empty<Vector3>(), footing = Array.Empty<Vector3>();
        // Reach of the whole chest from its pivot; ordinary finds keep out of it.
        [SerializeField, Min(.1f)] private float radius = .85f;
        // The lid opens with this share of its space still soil (crumbs and slivers), and the chest falls
        // once no more than FootingHeld of its footing points rest on soil.
        public const float LidSoilAllowance = .15f;
        public const int FootingHeld = 2;

        public string DisplayName => displayName;
        public Bounds Hollow => new Bounds(hollowCentre, hollowHalf * 2);
        public Vector3[] ContentSeats => contentSeats;
        public float Radius => radius;
        public bool Opened { get; private set; }
        public bool Released { get; private set; }
        public Rigidbody Body => body;

        private TerrainVolume terrain;
        private DiscoveryField field;
        private Rigidbody body;
        private Animation opening;
        private bool supportDirty;
        private Vector3 observedPosition;
        private Quaternion observedRotation;

        public void Initialize(TerrainVolume owner, DiscoveryField population)
        {
            terrain = owner; field = population;
            body = GetComponent<Rigidbody>(); opening = GetComponent<Animation>();
            opening.playAutomatically = false;
            opening.cullingType = AnimationCullingType.AlwaysAnimate;
            if (owner.TryGetComponent<ExcavationDaylight>(out var daylight))
                foreach (var renderer in GetComponentsInChildren<Renderer>(true)) daylight.Register(renderer);
            owner.Changed += HandleExcavationChanged;
            Anchor();
            Pose(false);
            supportDirty = true;
        }

        private void OnDestroy() { if (terrain != null) terrain.Changed -= HandleExcavationChanged; }

        public ChestSnapshot Capture() => new ChestSnapshot {
            Position = terrain.transform.InverseTransformPoint(Released ? body.position : transform.position),
            Rotation = Quaternion.Inverse(terrain.transform.rotation) * (Released ? body.rotation : transform.rotation),
            Released = Released, Opened = Opened };

        public void Restore(ChestSnapshot state)
        {
            Anchor();
            transform.SetPositionAndRotation(terrain.transform.TransformPoint(state.Position), terrain.transform.rotation * state.Rotation);
            body.position = transform.position; body.rotation = transform.rotation;
            Opened = state.Opened;
            Pose(Opened);
            if (state.Released) Release();
            observedPosition = transform.position; observedRotation = transform.rotation;
            supportDirty = true;
        }

        // Closed, or held at the end of the opening (a restored open chest skips the animation).
        private void Pose(bool open)
        {
            if (opening.clip == null) return;
            opening.Stop();
            opening.clip.SampleAnimation(gameObject, open ? opening.clip.length : 0);
        }

        public bool LidHasRoom()
        {
            if (terrain == null || lidSpace.Length == 0) return false;
            int solid = 0;
            foreach (var point in lidSpace) if (terrain.IsSolid(transform.TransformPoint(point))) solid++;
            return solid <= lidSpace.Length * LidSoilAllowance;
        }

        public bool CanDig => isActiveAndEnabled && terrain != null && !terrain.IsRestoring && !Opened && LidHasRoom();
        public string DigPrompt => Opened ? "The chest is open" : "Dig out the space above its lid first";

        // The tool strikes the lock: it breaks and the lid swings open.
        public bool TryDig(RaycastHit hit)
        {
            if (!CanDig) return false;
            Opened = true;
            opening.Play();
            field?.NotifyMotion();
            return true;
        }

        public string GetPrompt(FpsPlayer player)
        {
            if (Opened) return displayName;
            if (!LidHasRoom()) return $"{displayName}  |  Dig out the space above its lid";
            return $"{displayName}  |  {player.InputSettings.Display(PlayerBinding.Dig)} to break the rusted lock";
        }

        public bool TryInteract(FpsPlayer player) => false;

        private void HandleExcavationChanged(Bounds changed)
        {
            var reach = new Bounds(transform.position, Vector3.one * (radius * 2 + .5f));
            if (changed.Intersects(reach)) supportDirty = true;
        }

        private void FixedUpdate()
        {
            if (terrain == null || terrain.IsRestoring) return;
            if (supportDirty)
            {
                supportDirty = false;
                if (!Released)
                {
                    int held = 0;
                    foreach (var point in footing) if (terrain.IsSolid(transform.TransformPoint(point))) held++;
                    if (held <= FootingHeld) Release();
                }
                // The administrative reset can bury a fallen chest where it lies.
                else if (terrain.IsSolid(body.position)) { Anchor(); field?.NotifyMotion(); }
            }
            if (!Released) return;
            if ((body.position - observedPosition).sqrMagnitude < 1e-8f && Quaternion.Angle(body.rotation, observedRotation) < .05f) return;
            observedPosition = body.position; observedRotation = body.rotation;
            field?.NotifyMotion();
        }

        private void Release()
        {
            Released = true;
            body.isKinematic = false; body.useGravity = true; body.WakeUp();
            field?.NotifyMotion();
        }

        private void Anchor()
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true; body.useGravity = false;
            Released = false;
        }
    }

    public sealed class ChestSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public bool Released, Opened;
    }
}
