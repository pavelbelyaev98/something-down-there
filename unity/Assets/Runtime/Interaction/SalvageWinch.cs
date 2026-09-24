using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class SalvageWinch : MonoBehaviour
    {
        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private DiscoveryField discoveries;
        [SerializeField] private FpsPlayer player;
        [SerializeField] private SalvageWinchSettings settings;
        [SerializeField] private Transform liftAnchor;
        [SerializeField] private Transform[] padAnchors;
        [SerializeField] private WinchRopeView ropeView;
        [SerializeField] private UniqueDisplayStand[] displayStands;
        private ExtractionSnapshot job;
        private BuriedFind payload;
        private ExtractionRoutePlanner planner;
        private IEnumerator search;
        private int planningFrame = -1;
        private readonly Collider[] overlaps=new Collider[256];
        private readonly RaycastHit[] hits=new RaycastHit[256];
        public long Revision { get; private set; }
        public SalvageWinchSettings Settings => settings;
        public ExtractionSnapshot Capture() { CaptureMotion(); return job?.Copy(); }
        public bool Busy => job!=null;
        public bool Configured => terrain!=null && discoveries!=null && player!=null && settings!=null && settings.Valid
            && liftAnchor!=null && padAnchors!=null && padAnchors.Length>0 && Array.TrueForAll(padAnchors,p=>p!=null)
            && ropeView!=null && ropeView.Configured && displayStands!=null && displayStands.Length>0
            && Array.TrueForAll(displayStands,s=>s!=null && s.Configured)
            && soilChipsMaterial!=null && soilDustMaterial!=null;
        public string Prompt => job==null ? "" : job.Phase switch
        {
            ExtractionPhase.Planning => "Preparing rope route…",
            ExtractionPhase.Deploying => "Rope on its way",
            ExtractionPhase.Attaching => "Attaching rope",
            ExtractionPhase.Retensioning => "Pulling through the obstruction",
            _ => "Hauling to the surface"
        };
        private void Start() { if(Configured) { ropeView.Initialize(terrain, settings); InitializeBreakFeedback(); } }
        private void FixedUpdate()
        {
            // A slow frame can contain several physics steps. Give planning at
            // most one slice per rendered frame, and start hauling on a later one.
            if (planningFrame == Time.frameCount) return;
            if (job != null && job.Phase == ExtractionPhase.Planning) planningFrame = Time.frameCount;
            Tick(Time.fixedDeltaTime);
        }
        private void Update()
        {
            if(player==null || !player.GameplayActive || terrain==null || terrain.IsRestoring) SuspendLoad();
            DrawAttachedRope();
        }
        private void OnDisable() => SuspendLoad();
        private void OnDestroy() { (search as IDisposable)?.Dispose(); ReleaseRig(); }

        public bool TryMark(BuriedFind find,Vector3 hit,Vector3 normal)
        {
            if(!Configured || !player.GameplayActive || find==null || !find.CanMark) return false;
            if(Busy) { player.ShowFeedback("The winch is already recovering a find"); return false; }
            if(!player.TryGetTarget(player.Tuning.InteractReach,out var visible) || visible.collider.GetComponentInParent<BuriedFind>()!=find) return false;
            find.ObserveDiscovery();
            if(!find.Transition(FindState.World,FindState.Extracting)) return false;
            payload=find;
            payload.GetComponent<FindPhysics>().ClaimForRecovery();
            job=new ExtractionSnapshot { FindId=find.Item.InstanceId, Phase=ExtractionPhase.Planning,
                AttachLocal=find.transform.InverseTransformPoint(hit), Outward=terrain.transform.InverseTransformDirection(normal).normalized };
            BeginSearch(); Checkpoint(); return true;
        }
        private Vector3 AttachWorld => LoadBody.position + Offset;
        private Vector3 Offset => LoadBody.rotation * Vector3.Scale(payload.transform.lossyScale, job.AttachLocal);
        private Vector3 HullHalf => Vector3.Scale(payload.LocalHull.extents,payload.transform.lossyScale);
        private Vector3 HullCenter => LoadBody.rotation * Vector3.Scale(payload.transform.lossyScale, payload.LocalHull.center);
        private Vector3 PayloadPosition(Vector3 localHook) => terrain.transform.TransformPoint(localHook)-Offset;

        private void BeginSearch()
        {
            (search as IDisposable)?.Dispose();
            var snapshot=terrain.Capture();
            planner=new ExtractionRoutePlanner(snapshot,settings.RopeRadius,settings.SearchNodesPerFrame,settings.MaximumSearchNodes,
                (a,b)=>PayloadClear(PayloadPosition(a),PayloadPosition(b)));
            search=planner.Search(terrain.transform.InverseTransformPoint(AttachWorld),job.Outward);
            ropeView.Hide(); player.ShowFeedback("Marked for excavation");
        }

        public void Tick(float deltaTime)
        {
            if(job==null || payload==null || !Configured) return;
            if(!ropeView.Initialized) ropeView.Initialize(terrain, settings);
            if(!player.GameplayActive || terrain.IsRestoring) { SuspendLoad(); return; }
            if(deltaTime<=0 || !float.IsFinite(deltaTime)) return;
            // A hitch cannot bank a large terrain edit or an uncontrolled load jump.
            float dt=Mathf.Min(deltaTime,.1f);
            if(job.Phase==ExtractionPhase.Planning)
            {
                using var profile=PlanningMarker.Auto();
                if(search==null) BeginSearch();
                if(search.MoveNext()) return;
                search=null;
                if(planner.Route==null) { PlanningFailed(planner.Error); return; }
                var path=new List<Vector3>(planner.Route);
                Vector3 last=path[path.Count-1];
                // Clear the rim with the entire load before heading across the yard.
                Vector3 exit=last; exit.y=terrain.transform.InverseTransformPoint(liftAnchor.position).y;
                path.Add(exit);
                Vector3 lift=terrain.transform.InverseTransformPoint(liftAnchor.position);
                path.Add(lift);
                var pad=FreeReceivingPad();
                if(pad==null) { PlanningFailed("Place a recovered find on a display stand to free a receiving pad."); return; }
                Vector3 landing=terrain.transform.InverseTransformPoint(pad.position+Vector3.up*.08f);
                Vector3 abovePad=landing; abovePad.y=lift.y;
                path.Add(abovePad);
                path.Add(landing);
                bool valid=path.Count<=ExtractionSnapshot.MaximumWaypoints;
                // Final lowering deliberately meets the pad; contact solving owns arrival.
                for(int i=1;i<path.Count-1 && valid;i++)
                    valid=PayloadClear(PayloadPosition(path[i-1]),PayloadPosition(path[i]));
                if(!valid) { PlanningFailed("The load cannot clear the rim or receiving pad. Open another route and try again."); return; }
                job.Route=path.ToArray(); job.Progress=job.PhaseSeconds=0;
                SetPhase(job.Attached?ExtractionPhase.Hauling:ExtractionPhase.Deploying);
                planner=null;
                return;
            }
            float haulLength=ExtractionSnapshot.Length(job.Route,job.AnchorIndex);
            float fullLength=ExtractionSnapshot.Length(job.Route);
            switch(job.Phase)
            {
                case ExtractionPhase.Deploying:
                    job.Progress=Mathf.Min(haulLength,job.Progress+settings.RopeSpeed*dt); Revision++;
                    ropeView.Draw(terrain.transform,job.Route,haulLength-job.Progress,job.AnchorIndex);
                    if(job.Progress>=haulLength) { job.Progress=0; SetPhase(ExtractionPhase.Attaching); }
                    break;
                case ExtractionPhase.Attaching:
                    ropeView.Draw(terrain.transform,job.Route,0,job.AnchorIndex);
                    job.PhaseSeconds+=dt; Revision++;
                    if(job.PhaseSeconds>=settings.AttachSeconds) { job.Attached=true; job.PhaseSeconds=0; SetPhase(ExtractionPhase.Hauling); }
                    break;
                case ExtractionPhase.Hauling:
                case ExtractionPhase.Retensioning:
                case ExtractionPhase.Delivering:
                    HaulPhysics(dt, job.Phase==ExtractionPhase.Delivering?fullLength:haulLength);
                    break;
            }
            if (job != null) ropeView.Simulate(dt, payload.HitCollider, tensionCharge);
        }

        private void PlanningFailed(string error)
        {
            search=null; planner=null;
            payload.Transition(FindState.Extracting,FindState.World);
            payload.GetComponent<FindPhysics>().Restore(false); job=null; payload=null; Checkpoint();
            player.ShowFeedback(error);
        }
        private void Complete()
        {
            SuspendLoad(); ReleaseRig();
            if(!payload.Transition(FindState.Extracting,FindState.Stored)) throw new InvalidOperationException("Recovery lost its owner.");
            payload.GetComponent<FindPhysics>().Restore(false);
            player.ShowFeedback($"Recovered {payload.DisplayName}  |  Ready at the exhibit stand");
            ropeView.Hide(); job=null; payload=null; Checkpoint();
        }
        private void SetPhase(ExtractionPhase phase) { job.Phase=phase; job.PhaseSeconds=0; Checkpoint(); }
        private void Checkpoint() { Revision++; player.Persistence?.RequestCheckpoint(); }

        private static readonly Unity.Profiling.ProfilerMarker PlanningMarker = new Unity.Profiling.ProfilerMarker("Winch.Planning");

        private Transform FreeReceivingPad()
        {
            foreach(var pad in padAnchors)
            {
                // Arrival poses are authoritative saves; no extra pad ownership
                // state is needed. Reserve the whole load's horizontal envelope.
                bool occupied=false;
                foreach(var find in discoveries.Finds)
                {
                    if(find==null || find.State!=FindState.Stored) continue;
                    var bounds=find.WorldBounds;
                    Vector3 delta=bounds.center-pad.position;
                    if(Mathf.Abs(delta.x)<bounds.extents.x+HullHalf.magnitude
                        && Mathf.Abs(delta.z)<bounds.extents.z+HullHalf.magnitude) { occupied=true; break; }
                }
                if(!occupied) return pad;
            }
            return null;
        }

        private bool PayloadClear(Vector3 from,Vector3 to)
        {
            Vector3 half=HullHalf+Vector3.one*(settings.Clearance*.25f);
            Vector3 a=from+HullCenter, b=to+HullCenter;
            int count=Physics.OverlapBoxNonAlloc(a,half,overlaps,payload.transform.rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length) return false;
            for(int i=0;i<count;i++) if(Blocker(overlaps[i])) return false;
            count=Physics.OverlapBoxNonAlloc(b,half,overlaps,payload.transform.rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length) return false;
            for(int i=0;i<count;i++) if(Blocker(overlaps[i])) return false;
            Vector3 delta=b-a; if(delta.sqrMagnitude<.00000001f) return true;
            count=Physics.BoxCastNonAlloc(a,half,delta.normalized,hits,payload.transform.rotation,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==hits.Length) return false;
            for(int i=0;i<count;i++) if(Blocker(hits[i].collider)) return false;
            return true;
        }
        private bool Blocker(Collider collider)
        {
            if(collider.GetComponentInParent<PermanentTerrainBoundary>()!=null) return true;
            if(collider.GetComponentInParent<TerrainVolume>()==terrain || collider.GetComponentInParent<FpsPlayer>()==player) return false;
            if(collider.GetComponentInParent<WorkLamp>()!=null) return false;
            var find=collider.GetComponentInParent<BuriedFind>();
            if(find!=null) return find!=payload && find.Kind==DiscoveryKind.Unique;
            return true;
        }
        public void ValidateRestore(WorldSnapshot snapshot)
        {
            if(!Configured) throw new InvalidDataException("Recovery scene is incomplete.");
            foreach(var find in snapshot.Finds)
                if(find.State==FindState.Displayed && !Array.Exists(displayStands,s=>s.SocketId==find.DisplaySocket))
                    throw new InvalidDataException("Unknown exhibit socket.");
        }
        public void Restore(ExtractionSnapshot snapshot)
        {
            if(!ropeView.Initialized) ropeView.Initialize(terrain, settings);
            ReleaseRig();
            ClearBreakFeedback();
            (search as IDisposable)?.Dispose(); search=null; planner=null;
            job=snapshot?.Copy(); payload=job==null?null:discoveries.Find(job.FindId); Revision++;
            ropeView.Hide();
            if(job==null) return;
            if(payload==null || payload.State!=FindState.Extracting) throw new InvalidDataException("Recovery owner is missing.");
            payload.GetComponent<FindPhysics>().ClaimForRecovery();
            if(job.Phase==ExtractionPhase.Planning) return;
            if(job.Route.Length>=4)
            {
                float distance=job.Phase==ExtractionPhase.Deploying ? ExtractionSnapshot.Length(job.Route,job.AnchorIndex)-job.Progress : job.Progress;
                ropeView.Draw(terrain.transform,job.Route,distance,job.AnchorIndex,job.Attached?AttachWorld:null);
            }
        }
    }
}
