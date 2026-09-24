using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class SalvageWinch
    {
        private Rigidbody guide;
        private SpringJoint tether;
        private float stalledSeconds, settledSeconds, tensionCharge;
        private Vector3 progressPosition;
        private FindPhysics loadPhysics;
        private Rigidbody LoadBody => payload.GetComponent<Rigidbody>();

        private void CaptureMotion()
        {
            if(job==null || payload==null || LoadBody.isKinematic) return;
            job.LinearVelocity=terrain.transform.InverseTransformDirection(LoadBody.linearVelocity);
            job.AngularVelocity=terrain.transform.InverseTransformDirection(LoadBody.angularVelocity);
        }

        private void SuspendLoad()
        {
            ResetContactPressure(); ResetPullTension(); contactCount=0; stalledSeconds=0;
            if(job==null || payload==null || LoadBody.isKinematic) return;
            CaptureMotion();
            payload.GetComponent<FindPhysics>().ClaimForRecovery();
        }

        private void ReleaseRig()
        {
            if(loadPhysics!=null) loadPhysics.RecoveryContact-=RecordLoadContacts;
            loadPhysics=null; ResetContactPressure(); tensionCharge=0; contactCount=0;
            if(tether!=null) Destroy(tether);
            if(guide!=null) Destroy(guide.gameObject);
            tether=null; guide=null; stalledSeconds=settledSeconds=0;
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
                progressPosition=AttachWorld;
                if(job.Phase==ExtractionPhase.Retensioning) stalledSeconds=settings.RetensionSeconds;
                loadPhysics=payload.GetComponent<FindPhysics>();
                loadPhysics.RecoveryContact+=RecordLoadContacts;
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
            bool hauling=job.Phase!=ExtractionPhase.Delivering;
            float drive=hauling ? Mathf.Clamp01(tensionCharge) : 0;
            float lead=Mathf.Lerp(settings.GuideLead,settings.LoadedGuideLead,drive);
            float haulSpeed=settings.HaulSpeed*Mathf.Lerp(1f,settings.LoadedHaulMultiplier,drive);
            float distance=job.Progress;
            ExtractionSnapshot.Point(job.Route,job.Progress+.00001f,out int segment);
            float segmentStart=ExtractionSnapshot.Length(job.Route,segment);
            // Let physics reach a bend first. If it cannot, reel a short way into
            // the next span to change the pull direction instead of deadlocking
            // at the corner. The guide still follows every accepted route span.
            bool atBend=segment>0 && Mathf.Abs(job.Progress-segmentStart)<.0001f;
            if(lag<lead && (!atBend || lag<.22f || stalledSeconds>=settings.ContactStallSeconds))
            {
                distance=Mathf.Min(end,job.Progress+Mathf.Min(settings.MaximumMoveStep,haulSpeed*dt));
                distance=Mathf.Min(distance,ExtractionSnapshot.Length(job.Route,segment+1));
            }

            Vector3 nextGuide=terrain.transform.TransformPoint(ExtractionSnapshot.Point(job.Route,distance,out _));
            guide.MovePosition(nextGuide);
            body.WakeUp();
            job.Progress=distance; Revision++;

            bool onPad=job.Phase==ExtractionPhase.Delivering && Vector3.Distance(body.worldCenterOfMass,
                new Vector3(padAnchor.position.x,body.worldCenterOfMass.y,padAnchor.position.z))<.8f
                && payload.HitCollider.bounds.min.y<=padAnchor.position.y+.15f
                && payload.HitCollider.bounds.min.y>=padAnchor.position.y-.1f;
            if(onPad && body.linearVelocity.sqrMagnitude<.09f && body.angularVelocity.sqrMagnitude<1f)
                settledSeconds+=dt;
            else settledSeconds=0;
            if(settledSeconds>=.4f) { Complete(); return; }

            // Net progress toward the guide counts; jitter and sideways rocking
            // cannot keep restarting a jam forever. The body always stays dynamic.
            Vector3 pull=nextGuide-AttachWorld;
            bool progress=Vector3.Dot(AttachWorld-progressPosition,pull.normalized)>terrain.CellSize*.5f;
            if(progress || lag<.15f || onPad)
            {
                progressPosition=AttachWorld; stalledSeconds=0;
                if(job.Phase==ExtractionPhase.Retensioning) SetPhase(ExtractionPhase.Hauling);
            }
            else stalledSeconds+=dt;
            if(hauling && stalledSeconds>=settings.RetensionSeconds && job.Phase!=ExtractionPhase.Retensioning)
                SetPhase(ExtractionPhase.Retensioning);

            // At rest, speculative CCD can hold the hull off the surface and
            // report only future contacts. Reuse ordinary find collision mode
            // hysteresis so slow jams get exact contacts; fast release keeps CCD.
            loadPhysics.UpdateCollisionMode();
            if(onPad) ResetContactPressure();
            bool brokeSoil=!onPad && RelieveBlockedContact(dt,currentGuide-AttachWorld);
            UpdatePullTension(dt,brokeSoil);
            contactCount=0;
            if(distance>=end-.00001f && lag<.22f && hauling)
                SetPhase(ExtractionPhase.Delivering);
            DrawAttachedRope();
        }

        private void UpdatePullTension(float dt, bool brokeSoil)
        {
            float charge=brokeSoil ? 1f : Mathf.Clamp01(pressureSeconds/ContactDelay);
            if(job.Phase==ExtractionPhase.Retensioning) charge=2f;
            // Wind up only against a real blocked contact. Keep the spring loaded
            // as the soil gives way, then ease off while physics accelerates and
            // turns the body around the actual rope attachment. No velocity kick.
            tensionCharge=Mathf.Max(charge,Mathf.MoveTowards(tensionCharge,0,dt/settings.TensionReleaseSeconds));
            float strength=tensionCharge<=1 ? Mathf.Lerp(1f,settings.BlockedPullMultiplier,tensionCharge)
                : Mathf.Lerp(settings.BlockedPullMultiplier,settings.RetensionPullMultiplier,tensionCharge-1f);
            tether.spring=LoadBody.mass*settings.SpringAcceleration*strength;
            // A stalled motor pays in more cable and can accelerate the freed
            // load above cruising speed. Taper the burst; keep gentle pad arrival.
            float drive=job.Phase!=ExtractionPhase.Delivering ? Mathf.Clamp01(tensionCharge) : 0;
            LoadBody.maxLinearVelocity=Mathf.Lerp(settings.MaximumLoadSpeed,settings.MaximumBurstSpeed,drive);
        }

        private void ResetPullTension()
        {
            tensionCharge=0;
            if(tether!=null && payload!=null)
            {
                tether.spring=LoadBody.mass*settings.SpringAcceleration;
                LoadBody.maxLinearVelocity=settings.MaximumLoadSpeed;
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
