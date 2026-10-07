using System;
using UnityEngine;

namespace SomethingDownThere
{
    // An old chest at the bottom of a stash pit (106, 109): a container opened where it lies. It stands in a pocket of
    // seeded air that takes in its hollow and the lid's swing, so what it holds lies loose inside from New Game, behind
    // its walls and lid, and a shaft breaks into open space around it. Once the lid has room to swing,
    // holding Interact forces the rusted lock and the lid swings up; the contents are ordinary finds from then on.
    // Never sold, bagged or craned. Anchored while soil holds it up, it falls once undercut. Once nothing is left
    // inside, it goes the next time it is out of the player's sight and untouched, so it never pops away in front of them.
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(Animation))]
    public sealed class BuriedChest : MonoBehaviour, IInteractionTarget, IHoldTarget
    {
        [SerializeField] private string displayName = "Old chest";
        // Measured from the closed model by the discovery sync (ChestSetup), in the chest's own frame: its hollow, the
        // pocket of air it stands in, the seats on its floor, the space its lid sweeps opening and points just under its base.
        [SerializeField] private Vector3 hollowCentre, hollowHalf, pocketCentre, pocketHalf;
        [SerializeField] private Vector3[] contentSeats = Array.Empty<Vector3>();
        // The way its lock faces (its lid's free edge), in its own frame, the height of its walls' top and the half size
        // (x, z) of its floor inside the walls: its contents heap no higher and no wider (DiscoveryCatalog.ChestHeap).
        [SerializeField] private Vector3 front = Vector3.right;
        [SerializeField] private float rim;
        [SerializeField] private Vector2 floorHalf;
        [SerializeField] private Vector3[] lidSpace = Array.Empty<Vector3>(), footing = Array.Empty<Vector3>();
        // Reach of the chest's pocket from its pivot (ordinary finds keep out of PocketReserves).
        [SerializeField, Min(.1f)] private float radius = .85f;
        // The lid opens with this share of its space still soil (crumbs and slivers), and the chest falls
        // once no more than FootingHeld of its footing points rest on soil.
        public const float LidSoilAllowance = .15f;
        public const int FootingHeld = 2;
        // Forcing a lock rusted shut takes a little longer than bolting on the crane's lifting eye; it is forced from in
        // front of the lock, the eye within FrontAngle of the way it faces (user, 2026-10-06).
        public const float LockSeconds = 1.5f, FrontAngle = 55f;
        // How often an emptied chest looks whether it can go, and how much past its hollow a find still counts as inside.
        private const float GoneCheckSeconds = .25f, InsideMargin = .05f;

        public string DisplayName => displayName;
        public Bounds Hollow => new Bounds(hollowCentre, hollowHalf * 2);
        public Bounds Pocket => new Bounds(pocketCentre, pocketHalf * 2);
        public float Rim => rim;
        public Vector2 FloorHalf => floorHalf;

        // The space ordinary finds keep out of, in the chest's frame: a sphere around each quarter of its pocket (split
        // across its floor), which hugs the wide, low pocket far closer than one sphere around it all, so the rock layer
        // over a shallow chest stays. PocketMargin more, as the carved surface can lie a voxel cell or two outside the box.
        public const float PocketMargin = .25f;
        public (Vector3 centre, float radius)[] PocketReserves()
        {
            var quarter = new Vector3(pocketHalf.x * .5f, pocketHalf.y, pocketHalf.z * .5f);
            var reserves = new (Vector3, float)[4];
            for (int i = 0; i < 4; i++)
                reserves[i] = (pocketCentre + new Vector3((i & 1) == 0 ? -quarter.x : quarter.x, 0, (i & 2) == 0 ? -quarter.z : quarter.z), quarter.magnitude + PocketMargin);
            return reserves;
        }
        public Vector3[] ContentSeats => contentSeats;
        public float Radius => radius;
        public bool Opened { get; private set; }
        public bool Released { get; private set; }
        // Its pocket has been broken into (saved, so it caves in once).
        public bool Breached { get; private set; }
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
            Released = Released, Opened = Opened, Breached = Breached };

        public void Restore(ChestSnapshot state)
        {
            Anchor();
            transform.SetPositionAndRotation(terrain.transform.TransformPoint(state.Position), terrain.transform.rotation * state.Rotation);
            body.position = transform.position; body.rotation = transform.rotation;
            Opened = state.Opened;
            Breached = state.Breached;
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

        public bool CanHold(FpsPlayer holder) => isActiveAndEnabled && terrain != null && !terrain.IsRestoring && !Opened && LidHasRoom()
            && FacesLock(holder);

        // Whether the player's eye is in front of the lock, seen from above.
        public bool FacesLock(FpsPlayer holder)
        {
            if (holder == null || holder.ViewCamera == null) return false;
            var eye = transform.InverseTransformPoint(holder.ViewCamera.transform.position);
            return Vector2.Angle(new Vector2(eye.x, eye.z), new Vector2(front.x, front.z)) < FrontAngle;
        }
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
            if (!FacesLock(viewer)) return $"{displayName}  |  Go round to its lock";
            return $"{displayName}  |  Hold {viewer.InputSettings.Display(PlayerBinding.Interact)} to force the rusted lock";
        }

        public bool TryInteract(FpsPlayer viewer) => false;

        private void HandleExcavationChanged(Bounds changed)
        {
            var reach = new Bounds(transform.position, Vector3.one * (radius * 2 + .5f));
            if (!changed.Intersects(reach)) return;
            supportDirty = true;
            if (!terrain.IsRestoring) CheckBreach(changed);
        }

        // Breaking into the pocket (user, 2026-10-06: "I like collapses"): the first cut whose open ground reaches the
        // pocket's air sets off the break-in, a part of the pocket's roof or wall caving in with clods and dust
        // (FpsPlayer.BreakInCollapses), or a fall of crumbs and dust only. The way from the cut to the nearest point of the
        // pocket must be open, so digging beside it does nothing.
        // The cave-in is a ball CollapseRadius across, CollapseDepth beyond the breach, rounded so it takes the thin rims a box
        // left between the way in and the pocket (user, 2026-10-06: "weird gaps, thin, hard to remove").
        private const float BreakInStep = .08f, CollapseDepth = .2f, CollapseRadius = .7f;

        // Every later cut that opens more of the pocket (user, 2026-10-07: "the animation as I continue to break in") drops
        // the ground it freed into it: clods, crumbs and dust from the cut's edge, at most every BreakAgainSeconds.
        private const float BreakAgainSeconds = .2f;
        private float lastBreak = float.NegativeInfinity;

        private void CheckBreach(Bounds changed)
        {
            var local = transform.InverseTransformPoint(changed.center);
            var pocket = Pocket;
            if (pocket.Contains(local) && !Breached) return;
            var nearest = pocket.ClosestPoint(local);
            if ((local - nearest).magnitude > changed.extents.magnitude + .3f) return;
            Vector3 from = changed.center, to = transform.TransformPoint(nearest);
            float length = Vector3.Distance(from, to);
            for (float t = 0; t <= length; t += BreakInStep)
                if (terrain.IsSolid(Vector3.Lerp(from, to, t / Mathf.Max(length, 1e-4f)))) return;
            var inward = length > 1e-4f ? (to - from) / length : Vector3.down;
            if (Breached)
            {
                if (Time.time - lastBreak < BreakAgainSeconds) return;
                lastBreak = Time.time;
                var crane = FindViewer()?.Crane;
                if (crane != null) crane.EmitGroundBreak(to, inward, Mathf.Max(terrain.LastRemovedVolume, .04f), .4f, 1.1f);
                return;
            }
            Breached = true;
            lastBreak = Time.time;
            // On the next frame: the cave-in cuts the ground, which this change event is still reporting.
            pendingBreak = (to, inward);
            field?.NotifyMotion();
        }

        private (Vector3 point, Vector3 inward)? pendingBreak;

        private void BreakIn(Vector3 point, Vector3 inward)
        {
            var viewer = FindViewer();
            bool collapse = viewer == null || viewer.BreakInCollapses;
            if (collapse)
            {
                // The fill around the breach gives way: a rounded piece of roof or wall drops into the pocket.
                terrain.ClearSphere(point - inward * CollapseDepth, CollapseRadius);
            }
            var crane = viewer != null ? viewer.Crane : null;
            if (crane == null) return;
            crane.EmitGroundBreak(point, inward, collapse ? .3f : .05f, collapse ? .7f : .35f, collapse ? 1.6f : .7f);
            if (collapse) crane.EmitGroundBreak(point + Vector3.up * .3f, inward, .2f, .5f, 1f);
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
            if (pendingBreak.HasValue && terrain != null && !terrain.IsRestoring)
            {
                var (point, inward) = pendingBreak.Value;
                pendingBreak = null;
                BreakIn(point, inward);
            }
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
        public bool Released, Opened, Breached;
    }
}
