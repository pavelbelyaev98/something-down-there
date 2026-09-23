using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class SalvageWinch
    {
        private Rigidbody guide;
        private SpringJoint tether;
        private float stalledSeconds, settledSeconds;
        private Vector3 lastLoadPosition;
        private ExcavationGrid.SweptBox clearedLoad;
        private long clearedTerrainRevision = -1;
        private Rigidbody LoadBody => payload.GetComponent<Rigidbody>();

        private void CaptureMotion()
        {
            if(job==null || payload==null || LoadBody.isKinematic) return;
            job.LinearVelocity=terrain.transform.InverseTransformDirection(LoadBody.linearVelocity);
            job.AngularVelocity=terrain.transform.InverseTransformDirection(LoadBody.angularVelocity);
        }

        private void SuspendLoad()
        {
            if(job==null || payload==null || LoadBody.isKinematic) return;
            CaptureMotion();
            payload.GetComponent<FindPhysics>().ClaimForRecovery();
        }

        private void ReleaseRig()
        {
            if(tether!=null) Destroy(tether);
            if(guide!=null) Destroy(guide.gameObject);
            tether=null; guide=null; stalledSeconds=settledSeconds=0;
            clearedTerrainRevision=-1;
        }

        private void EnsureRig()
        {
            var body=LoadBody;
            if(guide==null)
            {
                var anchor=new GameObject("Recovery rope guide") { hideFlags=HideFlags.DontSave };
                anchor.transform.SetParent(transform, false);
                anchor.transform.position=terrain.transform.TransformPoint(ExtractionSnapshot.Point(job.Route,job.Progress,out _));
                guide=anchor.AddComponent<Rigidbody>();
                guide.isKinematic=true; guide.useGravity=false;
                tether=payload.gameObject.AddComponent<SpringJoint>();
                tether.autoConfigureConnectedAnchor=false;
                tether.anchor=job.AttachLocal; tether.connectedBody=guide; tether.connectedAnchor=Vector3.zero;
                tether.minDistance=0; tether.maxDistance=.02f; tether.tolerance=.005f;
                tether.spring=body.mass*settings.SpringAcceleration;
                tether.damper=body.mass*settings.SpringDamping;
                tether.enableCollision=false;
                lastLoadPosition=body.position;
            }
            if(!body.isKinematic) return;
            body.isKinematic=false; body.useGravity=true;
            body.constraints=RigidbodyConstraints.None;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            body.maxLinearVelocity=settings.MaximumLoadSpeed;
            body.maxAngularVelocity=settings.MaximumSpin;
            body.angularDamping=.6f;
            body.solverIterations=12; body.solverVelocityIterations=4;
            body.linearVelocity=terrain.transform.TransformDirection(job.LinearVelocity);
            body.angularVelocity=terrain.transform.TransformDirection(job.AngularVelocity);
            body.WakeUp();
        }

        private void HaulPhysics(float dt,float end)
        {
            using var profile=HaulMarker.Auto();
            EnsureRig();
            var body=LoadBody;
            Vector3 currentGuide=terrain.transform.TransformPoint(ExtractionSnapshot.Point(job.Route,job.Progress,out _));
            float lag=Vector3.Distance(AttachWorld,currentGuide);
            float distance=job.Progress;
            ExtractionSnapshot.Point(job.Route,job.Progress+.00001f,out int segment);
            float segmentStart=ExtractionSnapshot.Length(job.Route,segment);
            // Wait for the attachment to reach a bend before paying in the next span.
            // A spring may lag/stretch; neither the body nor its rotation is put on a rail.
            bool atBend=segment>0 && Mathf.Abs(job.Progress-segmentStart)<.0001f;
            if(lag<settings.GuideLead && (!atBend || lag<.22f))
            {
                distance=Mathf.Min(end,job.Progress+Mathf.Min(settings.MaximumMoveStep,settings.HaulSpeed*dt));
                distance=Mathf.Min(distance,ExtractionSnapshot.Length(job.Route,segment+1));
            }

            // A wedged load has no velocity. Clear towards the rope's intended pull
            // too, or the same cavity is cut forever while the spring stays stretched.
            Vector3 center=body.position+HullCenter;
            float angularPadding=HullHalf.magnitude*Mathf.Min(settings.MaximumSpin*dt,.2f);
            Vector3 nextGuide=terrain.transform.TransformPoint(ExtractionSnapshot.Point(job.Route,distance,out _));
            Vector3 pull=Vector3.ClampMagnitude(nextGuide-AttachWorld,settings.MaximumMoveStep);
            Vector3 travel=Vector3.ClampMagnitude(body.linearVelocity*dt+pull,settings.MaximumMoveStep);
            // Surface-net collider faces can lie inside the sampled box near angled
            // corners. Reserve a voxel diagonal, not only a sub-voxel visual margin.
            float skin=settings.Clearance+terrain.CellSize*1.75f;
            if(!ClearLoadSpace(center,center+travel,body.rotation,HullHalf+Vector3.one*(skin+angularPadding)))
            { SuspendLoad(); return; }
            LoosenContactFinds(body.position,body.position+travel,body.rotation,skin);
            // Contact clearance only removes soil, so the cached load volume is
            // still clear. Any external cut, reset or restore invalidates it.
            clearedTerrainRevision=terrain.StateRevision;
            guide.MovePosition(nextGuide);
            job.Progress=distance; Revision++;

            float movement=Vector3.Distance(body.position,lastLoadPosition);
            lastLoadPosition=body.position;
            bool onPad=job.Phase==ExtractionPhase.Delivering && Vector3.Distance(body.worldCenterOfMass,
                new Vector3(padAnchor.position.x,body.worldCenterOfMass.y,padAnchor.position.z))<.8f
                && payload.HitCollider.bounds.min.y<=padAnchor.position.y+.15f
                && payload.HitCollider.bounds.min.y>=padAnchor.position.y-.1f;
            if(onPad && body.linearVelocity.sqrMagnitude<.09f && body.angularVelocity.sqrMagnitude<1f)
                settledSeconds+=dt;
            else settledSeconds=0;
            if(settledSeconds>=.4f) { Complete(); return; }

            stalledSeconds=movement<.001f && lag>.3f && !onPad ? stalledSeconds+dt : 0;
            if(stalledSeconds>=3f)
            {
                SuspendLoad(); job.LinearVelocity=job.AngularVelocity=Vector3.zero;
                SetPhase(ExtractionPhase.Obstructed); player.ShowFeedback(Prompt); return;
            }
            if(distance>=end-.00001f && lag<.22f && job.Phase==ExtractionPhase.Hauling)
                SetPhase(ExtractionPhase.Delivering);
            DrawAttachedRope();
        }

        private bool ClearLoadSpace(Vector3 from,Vector3 to,Quaternion rotation,Vector3 half)
        {
            if(!terrain.CanDig || terrain.IsRestoring) return false;
            var required=new ExcavationGrid.SweptBox(from,to,rotation,half);
            if(clearedTerrainRevision==terrain.StateRevision && clearedLoad.Contains(required)) return true;
            // One cell of reusable clearance avoids remeshing almost identical
            // cavities at the physics rate. Keep checking the complete predicted
            // rotating hull each step; there is no timer or delayed collider update.
            half+=Vector3.one*terrain.CellSize;
            if(!terrain.ClearLoadSweep(from,to,rotation,half)) return false;
            clearedLoad=new ExcavationGrid.SweptBox(from,to,rotation,half);
            clearedTerrainRevision=terrain.StateRevision;
            return true;
        }

        private void LoosenContactFinds(Vector3 from,Vector3 to,Quaternion rotation,float skin)
        {
            using var profile=ContactsMarker.Auto();
            Vector3 localTravel=Quaternion.Inverse(rotation)*(to-from)*.5f;
            Vector3 half=HullHalf+new Vector3(Mathf.Abs(localTravel.x),Mathf.Abs(localTravel.y),Mathf.Abs(localTravel.z));
            int count=Physics.OverlapBoxNonAlloc((from+to)*.5f+HullCenter,half+Vector3.one*.02f,
                overlaps,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            int cleared=0;
            for(int i=0;i<count && cleared<4;i++)
            {
                var other=overlaps[i];
                var find=other.GetComponentInParent<BuriedFind>();
                if(find==null || find.Kind!=DiscoveryKind.Common || find.State!=FindState.World || find.IsHeld) continue;
                var physical=find.GetComponent<FindPhysics>();
                if(physical==null || !physical.Body.isKinematic) continue;
                // Broad-phase neighbours are not permission to excavate them. Only
                // a find touching the load or blocking its next pull is loosened.
                if(!Physics.ComputePenetration(payload.HitCollider,from,rotation,other,other.transform.position,other.transform.rotation,out _,out _)
                    && !Physics.ComputePenetration(payload.HitCollider,to,rotation,other,other.transform.position,other.transform.rotation,out _,out _)) continue;
                Vector3 findCenter=physical.Body.position+physical.Body.rotation*Vector3.Scale(find.LocalHull.center,find.transform.lossyScale);
                Vector3 findHalf=Vector3.Scale(find.LocalHull.extents,find.transform.lossyScale)+Vector3.one*skin;
                terrain.ClearLoadSweep(findCenter,findCenter,physical.Body.rotation,findHalf);
                find.RefreshExposure();
                physical.TryReleaseFromSoil();
                cleared++;
            }
        }

        private void DrawAttachedRope()
        {
            if(job==null || !job.Attached || payload==null || job.Route.Length<4) return;
            ropeView.Draw(terrain.transform,job.Route,job.Progress,job.AnchorIndex,
                payload.transform.TransformPoint(job.AttachLocal));
        }

        private static readonly Unity.Profiling.ProfilerMarker HaulMarker=new Unity.Profiling.ProfilerMarker("Winch.Haul");
        private static readonly Unity.Profiling.ProfilerMarker ContactsMarker=new Unity.Profiling.ProfilerMarker("Winch.Contacts");
    }
}
