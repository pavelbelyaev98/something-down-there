using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SomethingDownThere
{
    // The yard's salvage machine. Marking a unique bolts a lifting eye onto it and plans a rope route
    // through the player's own excavation; the tower crane swings over the hole mouth and lowers its hook
    // straight down the shaft on its cable. From the bottom of the shaft the cable is the smart rope: the
    // hook rides its end along the rest of the route into the eye, and the crane reels the load in
    // through bends and jams, tearing out retaining dirt. At the top the hook goes back on the hoist and
    // the crane carries the load to a free spot beside the camp for good. The crane is driven by an
    // automatic operator (SalvageCrane.Operator) through the pack's own levers (TowerCraneRig).
    public sealed partial class SalvageCrane : MonoBehaviour
    {
        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private DiscoveryField discoveries;
        [SerializeField] private FpsPlayer player;
        [SerializeField] private TowerCraneRig rig;
        [SerializeField] private SalvageRopeSettings settings;
        [SerializeField] private CraneRopeView ropeView;
        [SerializeField] private Transform[] setDownSpots;
        [SerializeField] private Material markMaterial;
        // The lifting eye the accepted mark bolts on (art/lifting-eye).
        [SerializeField] private GameObject liftingEye;
        // Where the idle crane waits: slew from its authored heading (toward the set-down spots), trolley
        // reach, hoist length.
        [SerializeField] private float restYaw, restReach = 8f, restRope = 12f;
        // The hook swings over the hole mouth this high before it goes down.
        [SerializeField, Min(1f)] private float parkHeight = 3f;
        // Height of a carried load's underside above the ground while travelling.
        [SerializeField, Min(1f)] private float travelClearance = 5f;
        [SerializeField, Min(1f)] private float straightenDegrees = 40f;
        private ExtractionSnapshot job;
        private BuriedFind payload;
        private RecoveryMarkView mark;
        private ExtractionRoutePlanner planner;
        private IEnumerator search;
        private int planningFrame = -1;
        private readonly Collider[] overlaps = new Collider[256];
        private readonly RaycastHit[] hits = new RaycastHit[256];
        public long Revision { get; private set; }
        public SalvageRopeSettings Settings => settings;
        public bool Busy => job != null;
        public TowerCraneRig Rig => rig;
        public int SpotCount => setDownSpots == null ? 0 : setDownSpots.Length;
        public Vector3 Spot(int index) => setDownSpots[index].position;
        public bool Configured => terrain != null && discoveries != null && player != null && rig != null && rig.Configured
            && settings != null && settings.Valid && ropeView != null && ropeView.Configured
            && setDownSpots != null && setDownSpots.Length > 0 && setDownSpots.Length <= ExtractionSnapshot.MaximumSpots
            && Array.TrueForAll(setDownSpots, s => s != null) && markMaterial != null && liftingEye != null
            && soilChipsMaterial != null && soilDustMaterial != null;
        public string Prompt => job == null ? "" : job.Phase switch
        {
            ExtractionPhase.Planning => "Preparing rope route…",
            ExtractionPhase.Reaching => "Crane on its way",
            ExtractionPhase.Lowering => "Hook coming down",
            ExtractionPhase.Deploying => "Hook on its way",
            ExtractionPhase.Attaching => "Hooking on",
            ExtractionPhase.Retensioning => "Pulling through the obstruction",
            ExtractionPhase.Hauling => "Hauling to the surface",
            ExtractionPhase.Lifting => "Lifting it clear",
            ExtractionPhase.Carrying => "Carrying it to camp",
            _ => "Setting it down at camp"
        };
        private CranePose RestPose => new CranePose { Yaw = restYaw, Reach = restReach, Rope = restRope };
        private Vector3 Anchor => terrain.transform.TransformPoint(job.Route[job.AnchorIndex]);
        private Vector3[] dropRoute;
        private float dropDistance;

        // How far the hoist lowers the hook on the route: the bottom of the straight shaft under the
        // trolley. Beyond it the hook rides the smart rope's end.
        private float DropDistance
        {
            get
            {
                if (dropRoute == job.Route) return dropDistance;
                dropRoute = job.Route;
                Vector3 top = job.Route[job.AnchorIndex];
                int bottom = job.AnchorIndex;
                while (bottom > 0 && Horizontal(job.Route[bottom - 1] - top).magnitude < .35f) bottom--;
                float full = ExtractionSnapshot.Length(job.Route);
                dropDistance = Mathf.Clamp(ExtractionSnapshot.Length(job.Route, bottom), 0, full);
                return dropDistance;
            }
        }

        // Only the rope below the straight shaft is simulated (or from just ahead of a load already coming
        // up it); above, it hangs straight down the shaft along the route. A cable simulated from above the
        // mouth had too many particles for its solver and stretched through corner soil.
        private const float SimulatedLead = 1.2f;
        private float ReelDistance => job.Phase == ExtractionPhase.Hauling || job.Phase == ExtractionPhase.Retensioning
            ? Mathf.Clamp(job.Progress + SimulatedLead, DropDistance, ExtractionSnapshot.Length(job.Route)) : DropDistance;

        private void Start()
        {
            if (!Configured) return;
            ropeView.Initialize(terrain, settings); InitializeBreakFeedback();
            if (job == null) rig.Apply(RestPose);
        }
        private void FixedUpdate()
        {
            // A slow frame can contain several physics steps. Give planning at
            // most one slice per rendered frame, and start the crane on a later one.
            if (planningFrame == Time.frameCount) return;
            if (job != null && job.Phase == ExtractionPhase.Planning) planningFrame = Time.frameCount;
            Tick(Time.fixedDeltaTime);
        }
        private void Update()
        {
            if (player == null || !player.GameplayActive || terrain == null || terrain.IsRestoring) SuspendLoad();
            DrawAttachedRope();
        }
        private void LateUpdate()
        {
            RefreshMark();
            if (ropeView != null) ropeView.Render();
            AimEye();
        }
        private void OnDisable() { SuspendLoad(); mark?.Hide(); }
        private void OnDestroy() { (search as IDisposable)?.Dispose(); ReleaseRig(); mark?.Dispose(); }

        public bool TryMark(BuriedFind find, Vector3 hit, Vector3 normal)
        {
            if (!Configured || !player.GameplayActive || find == null || !find.CanMark) return false;
            if (Busy) { player.ShowFeedback("The crane is already recovering a find"); return false; }
            if (!player.TryGetTarget(player.Tuning.InteractReach, out var visible) || visible.collider.GetComponentInParent<BuriedFind>() != find) return false;
            int spot = FreeSpot();
            if (spot < 0) { player.ShowFeedback("No room left beside the camp"); return false; }
            find.ObserveDiscovery();
            if (!find.Transition(FindState.World, FindState.Extracting)) return false;
            payload = find;
            payload.GetComponent<FindPhysics>().ClaimForRecovery();
            job = new ExtractionSnapshot { FindId = find.Item.InstanceId, Phase = ExtractionPhase.Planning,
                AttachLocal = find.transform.InverseTransformPoint(hit), Outward = terrain.transform.InverseTransformDirection(normal).normalized,
                Spot = spot, Pose = rig.Capture() };
            ShowMark(normal);
            BeginSearch(); Checkpoint();
            return true;
        }

        private Vector3 AttachWorld => LoadBody.position + Offset;
        private Vector3 Offset => LoadBody.rotation * Vector3.Scale(payload.transform.lossyScale, job.AttachLocal);
        private Vector3 HullHalf => Vector3.Scale(payload.LocalHull.extents, payload.transform.lossyScale);
        private Vector3 HullCenter => LoadBody.rotation * Vector3.Scale(payload.transform.lossyScale, payload.LocalHull.center);
        private Vector3 PayloadPosition(Vector3 localHook) => terrain.transform.TransformPoint(localHook) - Offset;

        private void BeginSearch()
        {
            (search as IDisposable)?.Dispose();
            var snapshot = terrain.Capture();
            planner = new ExtractionRoutePlanner(snapshot, settings.RopeRadius, settings.SearchNodesPerFrame, settings.MaximumSearchNodes,
                (a, b) => PayloadClear(PayloadPosition(a), PayloadPosition(b)));
            search = planner.Search(terrain.transform.InverseTransformPoint(AttachWorld), job.Outward);
            ropeView.Hide(); player.ShowFeedback("Marked for the crane");
        }

        public void Tick(float deltaTime)
        {
            if (!Configured || deltaTime <= 0 || !float.IsFinite(deltaTime)) return;
            if (!ropeView.Initialized) ropeView.Initialize(terrain, settings);
            if (!player.GameplayActive || terrain.IsRestoring)
            {
                // Levers released: the rig coasts to a stop and a load on the hook stays with it.
                SuspendLoad();
                if (job != null && payload != null && ExtractionSnapshot.Craning(job.Phase)) Follow(deltaTime, false);
                return;
            }
            if (job == null || payload == null) { Park(); return; }
            // A hitch cannot bank a large terrain edit or an uncontrolled load jump.
            float dt = Mathf.Min(deltaTime, .1f);
            if (job.Phase == ExtractionPhase.Planning)
            {
                HeadFor(payload.transform.position);
                using var profile = PlanningMarker.Auto();
                if (search == null) BeginSearch();
                if (search.MoveNext()) return;
                search = null;
                if (planner.Route == null) { PlanningFailed(planner.Error); return; }
                var path = new List<Vector3>(planner.Route);
                // The crane parks its hook over the hole mouth, or as near as its trolley reaches; the
                // load comes straight up out of the hole first, then across to the hook.
                Vector3 mouth = terrain.transform.TransformPoint(path[path.Count - 1]);
                Vector3 park = ParkPoint(mouth);
                Vector3 exit = new Vector3(mouth.x, park.y, mouth.z);
                if (Vector3.Distance(exit, park) > .1f) path.Add(terrain.transform.InverseTransformPoint(exit));
                path.Add(terrain.transform.InverseTransformPoint(park));
                bool valid = path.Count <= ExtractionSnapshot.MaximumWaypoints;
                for (int i = 1; i < path.Count && valid; i++)
                    valid = PayloadClear(PayloadPosition(path[i - 1]), PayloadPosition(path[i]));
                if (!valid) { PlanningFailed("The load cannot clear the rim. Open another route and try again."); return; }
                job.Route = path.ToArray(); job.Progress = job.PhaseSeconds = 0;
                SetPhase(ExtractionPhase.Reaching);
                planner = null;
                return;
            }
            float haulLength = ExtractionSnapshot.Length(job.Route);
            switch (job.Phase)
            {
                case ExtractionPhase.Deploying:
                    HoldOver();
                    job.Progress = Mathf.Min(DropDistance, job.Progress + settings.RopeSpeed * dt); Revision++;
                    RideHook(dt, false);
                    if (job.Progress >= DropDistance) { job.Progress = 0; SetPhase(ExtractionPhase.Attaching); }
                    break;
                case ExtractionPhase.Attaching:
                    HoldOver();
                    job.PhaseSeconds += dt; Revision++;
                    RideHook(dt, false);
                    if (job.PhaseSeconds >= settings.AttachSeconds) { job.Attached = true; job.PhaseSeconds = 0; SetPhase(ExtractionPhase.Hauling); }
                    break;
                case ExtractionPhase.Hauling:
                case ExtractionPhase.Retensioning:
                    HoldOver();
                    HaulPhysics(dt, haulLength);
                    break;
                default:
                    Operate(dt);
                    return;
            }
            if (job != null && !ExtractionSnapshot.Craning(job.Phase)) ropeView.Simulate(dt, payload.HitCollider, CableTension);
        }

        // Straight above the hole mouth at park height, or as near as the trolley reaches.
        private Vector3 ParkPoint(Vector3 mouth)
        {
            Vector3 park = new Vector3(mouth.x, terrain.SurfaceHeight + parkHeight, mouth.z);
            rig.Aim(park, out _, out float reach);
            float allowed = Mathf.Clamp(reach, rig.MinimumReach + .3f, rig.MaximumReach - .3f);
            if (Mathf.Abs(allowed - reach) < .001f) return park;
            Vector3 offset = Horizontal(park - rig.Mast).normalized * allowed;
            return new Vector3(rig.Mast.x + offset.x, park.y, rig.Mast.z + offset.z);
        }

        // The crane's hook riding the smart rope's free end along the route, seat first, then into the
        // lifting eye. The rope's end stays on the route a hook's length behind the seat (the drawn rope
        // ends at the block): a straight hook cuts bends, and an end inside the corner soil would drag
        // the cable into it.
        private void RideHook(float dt, bool snap)
        {
            float remaining = job.Phase == ExtractionPhase.Deploying ? DropDistance - job.Progress : 0;
            float behind = Mathf.Min(DropDistance, remaining + rig.HookLength);
            Vector3 outward = terrain.transform.TransformDirection(job.Outward);
            Vector3 seat = terrain.transform.TransformPoint(ExtractionSnapshot.Point(job.Route, remaining, out _))
                + outward * (RecoveryMarkView.HookReach * Mathf.Clamp01(1 - remaining));
            Vector3 end = terrain.transform.TransformPoint(ExtractionSnapshot.Point(job.Route, behind, out _));
            ropeView.Carry(seat, end - seat, dt, snap);
            ropeView.Draw(terrain.transform, job.Route, behind, DropDistance, end, false);
        }

        // Once hooked on, the lifting eye swivels toward the hook.
        private void AimEye()
        {
            if (mark == null || job == null || payload == null || !job.Attached) return;
            if (ExtractionSnapshot.Craning(job.Phase))
                mark.Aim(payload.transform.TransformPoint(job.GrabLocal) - payload.transform.TransformPoint(job.AttachLocal));
            else mark.Aim(ropeView.HookDirection);
        }

        // At the top of the route the load hangs in the hook: the hook goes back on the hoist and the
        // crane holds the load from here on.
        private void Handoff()
        {
            ReleaseRig(); ropeView.Hide(); rig.SeatHook();
            payload.GetComponent<FindPhysics>().ClaimForRecovery();
            job.LinearVelocity = job.AngularVelocity = Vector3.zero;
            job.GrabLocal = payload.transform.InverseTransformPoint(rig.Seat);
            SetPhase(ExtractionPhase.Lifting);
        }

        private void PlanningFailed(string error)
        {
            search = null; planner = null;
            payload.Transition(FindState.Extracting, FindState.World);
            payload.GetComponent<FindPhysics>().Restore(false); job = null; payload = null; Checkpoint();
            mark?.Hide();
            player.ShowFeedback(error);
        }

        private int FreeSpot()
        {
            for (int i = 0; i < setDownSpots.Length; i++)
            {
                Vector3 spot = setDownSpots[i].position;
                bool taken = false;
                foreach (var find in discoveries.Finds)
                {
                    if (find == null || find.State != FindState.Stored) continue;
                    Vector3 offset = find.WorldBounds.center - spot; offset.y = 0;
                    if (offset.magnitude < 1.5f) { taken = true; break; }
                }
                if (!taken) return i;
            }
            return -1;
        }

        private void Deliver()
        {
            var body = payload.GetComponent<FindPhysics>().Body;
            payload.MoveRecovered(body.position, body.rotation);
            if (!payload.Transition(FindState.Extracting, FindState.Stored)) throw new InvalidOperationException("Recovery lost its owner.");
            payload.GetComponent<FindPhysics>().Restore(false);
            player.ShowFeedback($"The crane set the {payload.DisplayName} down at camp");
            job = null; payload = null; mark?.Hide();
            Checkpoint();
        }

        private void SetPhase(ExtractionPhase phase) { job.Phase = phase; job.PhaseSeconds = 0; Checkpoint(); }
        private void Checkpoint() { Revision++; player.Persistence?.RequestCheckpoint(); }

        private static readonly Unity.Profiling.ProfilerMarker PlanningMarker = new Unity.Profiling.ProfilerMarker("Crane.Planning");

        private bool PayloadClear(Vector3 from, Vector3 to)
        {
            Vector3 half = HullHalf + Vector3.one * (settings.Clearance * .25f);
            Vector3 a = from + HullCenter, b = to + HullCenter;
            int count = Physics.OverlapBoxNonAlloc(a, half, overlaps, payload.transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (Blocker(overlaps[i])) return false;
            count = Physics.OverlapBoxNonAlloc(b, half, overlaps, payload.transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (Blocker(overlaps[i])) return false;
            Vector3 delta = b - a; if (delta.sqrMagnitude < .00000001f) return true;
            count = Physics.BoxCastNonAlloc(a, half, delta.normalized, hits, payload.transform.rotation, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++) if (Blocker(hits[i].collider)) return false;
            return true;
        }
        private bool Blocker(Collider collider)
        {
            if (collider.GetComponentInParent<PermanentTerrainBoundary>() != null) return true;
            if (collider.GetComponentInParent<TerrainVolume>() == terrain || collider.GetComponentInParent<FpsPlayer>() == player) return false;
            if (collider.GetComponentInParent<WorkLamp>() != null) return false;
            var find = collider.GetComponentInParent<BuriedFind>();
            if (find != null) return find != payload && find.Kind == DiscoveryKind.Unique;
            return true;
        }

        // The accepted mark stays on the load; a zero normal is recovered from the load's surface.
        private void ShowMark(Vector3 normal)
        {
            if (markMaterial == null) return;
            mark ??= new RecoveryMarkView(transform, markMaterial, liftingEye);
            mark.Show(payload, job.AttachLocal, normal, 1, true);
        }

        internal void RefreshMark()
        {
            if (markMaterial == null) return;
            if (terrain == null || terrain.IsRestoring) { mark?.Hide(); return; }
            mark ??= new RecoveryMarkView(transform, markMaterial, liftingEye);
            if (job != null && payload != null) mark.Show(payload, job.AttachLocal, Vector3.zero, 1, true);
            else if (player != null && player.TryGetRecoveryMark(out var find, out var hit))
                mark.Show(find, find.transform.InverseTransformPoint(hit.point), hit.normal, player.ExtractionMarkProgress, false);
            else mark.Hide();
        }

        public ExtractionSnapshot Capture()
        {
            if (job == null) return null;
            CaptureMotion();
            job.Pose = rig.Capture();
            return job.Copy();
        }

        public void ValidateRestore(WorldSnapshot snapshot)
        {
            if (!Configured) throw new InvalidDataException("Salvage crane scene is incomplete.");
            var saved = snapshot.Extraction;
            if (saved == null) return;
            if (saved.Spot >= setDownSpots.Length) throw new InvalidDataException("Unknown set-down spot.");
            if (!rig.Holds(saved.Pose)) throw new InvalidDataException("Crane pose is outside its rig.");
        }

        public void Restore(ExtractionSnapshot snapshot)
        {
            if (!ropeView.Initialized) ropeView.Initialize(terrain, settings);
            ReleaseRig();
            ClearBreakFeedback();
            (search as IDisposable)?.Dispose(); search = null; planner = null;
            job = snapshot?.Copy(); payload = null; Revision++;
            ropeView.Hide(); mark?.Hide();
            if (job == null) { rig.Apply(RestPose); return; }
            payload = discoveries.Find(job.FindId);
            if (payload == null || payload.State != FindState.Extracting) throw new InvalidDataException("Recovery owner is missing.");
            payload.GetComponent<FindPhysics>().ClaimForRecovery();
            rig.Apply(job.Pose);
            ShowMark(Vector3.zero);
            if (job.Phase <= ExtractionPhase.Lowering) return;
            if (ExtractionSnapshot.Craning(job.Phase)) { Follow(0); return; }
            rig.ReleaseHook();
            if (job.Attached) DrawAttachedRope(); else RideHook(0, true);
        }
    }
}
