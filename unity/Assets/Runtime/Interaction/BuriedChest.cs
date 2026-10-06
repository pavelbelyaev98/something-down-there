using System;
using UnityEngine;

namespace SomethingDownThere
{
    // An old chest at the bottom of a stash pit (106, 109): a container opened where it lies. Its hollow is seeded air,
    // so what it holds lies loose inside from New Game, behind its walls and lid. Once the lid has room to swing,
    // holding Interact forces the rusted lock and the lid swings up; the contents are ordinary finds from then on.
    // Never sold, bagged or craned. Anchored while soil holds it up, it falls once undercut. Once nothing is left
    // inside, it goes the next time it is out of the player's sight and untouched, so it never pops away in front of them.
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(Animation))]
    public sealed class BuriedChest : MonoBehaviour, IInteractionTarget, IHoldTarget
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
        // Forcing a lock rusted shut takes a little longer than bolting on the crane's lifting eye.
        public const float LockSeconds = 1.5f;
        // How often an emptied chest looks whether it can go, and how much past its hollow a find still counts as inside.
        private const float GoneCheckSeconds = .25f, InsideMargin = .05f;

        public string DisplayName => displayName;
        public Bounds Hollow => new Bounds(hollowCentre, hollowHalf * 2);
        public Vector3[] ContentSeats => contentSeats;
        public float Radius => radius;
        public bool Opened { get; private set; }
        public bool Released { get; private set; }
        public Rigidbody Body => body;

        private TerrainVolume terrain;
        private DiscoveryField field;
        private FpsPlayer player;
        private Rigidbody body;
        private Animation opening;
        private Renderer[] renderers;
        private bool supportDirty;
        private float goneCheck;
        private Vector3 observedPosition;
        private Quaternion observedRotation;
        private readonly Plane[] frustum = new Plane[6];
        private readonly Collider[] touching = new Collider[16];

        public void Initialize(TerrainVolume owner, DiscoveryField population)
        {
            terrain = owner; field = population;
            body = GetComponent<Rigidbody>(); opening = GetComponent<Animation>();
            renderers = GetComponentsInChildren<Renderer>(true);
            opening.playAutomatically = false;
            opening.cullingType = AnimationCullingType.AlwaysAnimate;
            if (owner.TryGetComponent<ExcavationDaylight>(out var daylight))
                foreach (var renderer in renderers) daylight.Register(renderer);
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

        public bool CanHold(FpsPlayer holder) => isActiveAndEnabled && terrain != null && !terrain.IsRestoring && !Opened && LidHasRoom();
        public float HoldSeconds(FpsPlayer holder) => LockSeconds;

        // The rusted lock gives and the lid swings open.
        public bool CompleteHold(FpsPlayer holder, RaycastHit hit)
        {
            if (!CanHold(holder)) return false;
            Opened = true;
            player = holder;
            opening.Play();
            field?.NotifyMotion();
            return true;
        }

        public string GetPrompt(FpsPlayer viewer)
        {
            if (Opened) return displayName;
            if (!LidHasRoom()) return $"{displayName}  |  Clear the soil above its lid";
            return $"{displayName}  |  Hold {viewer.InputSettings.Display(PlayerBinding.Interact)} to force the rusted lock";
        }

        public bool TryInteract(FpsPlayer viewer) => false;

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

        // An opened, emptied chest goes once out of sight and untouched (user, 2026-10-05).
        private void Update()
        {
            if (!Opened || field == null || terrain == null || terrain.IsRestoring || opening.isPlaying) return;
            if ((goneCheck -= Time.deltaTime) > 0) return;
            goneCheck = GoneCheckSeconds;
            if (player == null) player = FindViewer();
            if (player == null || HoldsAnything() || Seen() || Touched()) return;
            field.RemoveChest(this);
        }

        private FpsPlayer FindViewer()
        {
            foreach (var candidate in FindObjectsByType<FpsPlayer>())
                if (candidate.gameObject.scene == gameObject.scene) return candidate;
            return null;
        }

        // Any uncollected find whose current pose lies in the hollow (in the chest's own frame, so a fallen chest works too).
        public bool HoldsAnything()
        {
            var hollow = Hollow;
            hollow.Expand(InsideMargin * 2);
            float reach = radius * radius;
            foreach (var find in field.Finds)
            {
                if (find == null || find.Collected) continue;
                var pose = find.TryGetComponent<FindPhysics>(out var physics) && physics.Released ? physics.Body.position : find.transform.position;
                if ((pose - transform.position).sqrMagnitude > reach) continue;
                if (hollow.Contains(transform.InverseTransformPoint(pose))) return true;
            }
            return false;
        }

        // In the camera's frustum with a clear line to it; soil in between counts as out of sight.
        private bool Seen()
        {
            var camera = player.ViewCamera;
            if (camera == null || renderers.Length == 0) return false;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            bounds.Expand(.3f);
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            if (!GeometryUtility.TestPlanesAABB(frustum, bounds)) return false;
            var eye = camera.transform.position;
            return Clear(eye, bounds.center) || Clear(eye, new Vector3(bounds.center.x, bounds.max.y - .3f, bounds.center.z));
        }

        private static bool Clear(Vector3 eye, Vector3 point)
            => !Physics.Linecast(eye, point, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
               || hit.collider.GetComponentInParent<TerrainVolume>() == null;

        // The player's capsule, a little inflated, overlaps the chest: standing on it, leaning on it.
        private bool Touched()
        {
            if (!player.TryGetComponent<CharacterController>(out var motor)) return false;
            var up = motor.transform.up;
            var centre = motor.transform.TransformPoint(motor.center);
            float half = Mathf.Max(0, motor.height * .5f - motor.radius);
            int count = Physics.OverlapCapsuleNonAlloc(centre - up * half, centre + up * half, motor.radius + motor.skinWidth + .1f,
                touching, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (touching[i].transform.IsChildOf(transform)) return true;
            return false;
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
