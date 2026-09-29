using UnityEngine;

namespace SomethingDownThere
{
    // The smart rope's haul: a spring guide pulls the dynamic load along the accepted route, winding
    // up against jams until the retaining dirt breaks (SalvageCrane.Contacts).
    public sealed partial class SalvageCrane
    {
        private Rigidbody guide;
        private SpringJoint tether;
        private float stalledSeconds, tensionCharge;
        private Vector3 progressPosition;
        private Vector3 incomingVelocity;
        private Vector3 impactCarry;
        private FindPhysics loadPhysics;
        private Rigidbody LoadBody => payload.GetComponent<Rigidbody>();
        // Supporting the ordinary load already puts the cable under tension;
        // jam charge is extra motor effort, not the definition of a taut rope.
        private float CableTension => tether == null || guide == null ? 0
            : Mathf.Clamp01((Vector3.Distance(AttachWorld, guide.position) - tether.maxDistance) / .08f);

        private void CaptureMotion()
        {
            if(job==null || payload==null || LoadBody.isKinematic) return;
            job.LinearVelocity=terrain.transform.InverseTransformDirection(LoadBody.linearVelocity);
            job.AngularVelocity=terrain.transform.InverseTransformDirection(LoadBody.angularVelocity);
        }

        private void SuspendLoad()
        {
            ResetContactPressure(); ResetPullTension(); contactCount=0; stalledSeconds=0; incomingVelocity=Vector3.zero;
            if(job==null || payload==null || LoadBody.isKinematic) return;
            CaptureMotion();
            payload.GetComponent<FindPhysics>().ClaimForRecovery();
        }

        private void ReleaseRig()
        {
            if(loadPhysics!=null) loadPhysics.RecoveryContact-=RecordLoadContacts;
            loadPhysics=null; ResetContactPressure(); tensionCharge=0; contactCount=0; incomingVelocity=Vector3.zero;
            if(tether!=null) Destroy(tether);
            if(guide!=null) Destroy(guide.gameObject);
            tether=null; guide=null; stalledSeconds=0;
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
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.maxLinearVelocity=settings.MaximumLoadSpeed;
            body.maxAngularVelocity=settings.MaximumSpin;
            body.angularDamping=.28f;
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
            impactCarry=Vector3.zero;
            float lag=Vector3.Distance(AttachWorld,currentGuide);
            float drive=Mathf.Clamp01(tensionCharge);
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
            body.angularDamping=.28f;

            // Net progress toward the guide counts; jitter and sideways rocking
            // cannot keep restarting a jam forever. The body always stays dynamic.
            Vector3 pull=nextGuide-AttachWorld;
            bool progress=Vector3.Dot(AttachWorld-progressPosition,pull.normalized)>terrain.CellSize*.5f;
            if(progress || lag<.15f)
            {
                progressPosition=AttachWorld; stalledSeconds=0;
                if(job.Phase==ExtractionPhase.Retensioning) SetPhase(ExtractionPhase.Hauling);
            }
            else stalledSeconds+=dt;
            if(stalledSeconds>=settings.RetensionSeconds && job.Phase!=ExtractionPhase.Retensioning)
                SetPhase(ExtractionPhase.Retensioning);

            // At rest, speculative CCD can hold the hull off the surface and
            // report only future contacts. Reuse ordinary find collision mode
            // hysteresis so slow jams get exact contacts; fast release keeps CCD.
            // Sweep CCD reaches the actual impact. Speculative CCD can brake a
            // fast load at a future contact, discarding its momentum before the
            // real-contact rupture gate is ever allowed to see it.
            loadPhysics.UpdateCollisionMode(CollisionDetectionMode.ContinuousDynamic);
            bool brokeSoil=RelieveBlockedContact(dt,currentGuide-AttachWorld);
            UpdatePullTension(dt,brokeSoil,nextGuide,(nextGuide-currentGuide)/dt);
            contactCount=0;
            // PhysX can report an already-resolved velocity in its contact
            // callback. Keep this step's incoming momentum for rupture decisions.
            incomingVelocity=Vector3.ClampMagnitude(body.linearVelocity+impactCarry, settings.MaximumBurstSpeed);
            // The crane takes the load only once the route has brought it out of the ground.
            if(distance>=end-.00001f && lag<.22f && Anchor.y>terrain.SurfaceHeight) { Handoff(); return; }
            DrawAttachedRope();
        }

        private void UpdatePullTension(float dt, bool brokeSoil, Vector3 target, Vector3 reelVelocity)
        {
            float charge=brokeSoil ? 1f : Mathf.Clamp01(pressureSeconds/ContactDelay);
            if(job.Phase==ExtractionPhase.Retensioning) charge=2f;
            // Wind up only against a real blocked contact. Keep the spring loaded
            // as the soil gives way, then ease off while physics accelerates and
            // turns the body around the actual rope attachment. No velocity kick.
            tensionCharge=Mathf.Max(charge,Mathf.MoveTowards(tensionCharge,0,dt/settings.TensionReleaseSeconds));
            float strength=tensionCharge<=1 ? Mathf.Lerp(1f,settings.BlockedPullMultiplier,tensionCharge)
                : Mathf.Lerp(settings.BlockedPullMultiplier,settings.RetensionPullMultiplier,tensionCharge-1f);
            // Let stored spring energy become motion. Heavy constant damping and
            // a falling velocity cap used to swallow the surge between contacts.
            float damping=settings.SpringDamping*Mathf.Lerp(1f,.45f,Mathf.Clamp01(tensionCharge));
            tether.damper=LoadBody.mass*damping;
            LoadBody.maxLinearVelocity=settings.MaximumBurstSpeed;
            // maxLinearVelocity is applied before joint solving. Bound the motor
            // force as well, so a wound-up joint cannot launch beyond the motion
            // envelope used by physics and checkpoints in that same solver step.
            Vector3 pull=target-AttachWorld, direction=pull.normalized;
            Vector3 velocity=LoadBody.linearVelocity+impactCarry;
            float sideways=Vector3.ProjectOnPlane(velocity,direction).sqrMagnitude;
            float allowedSpeed=Mathf.Sqrt(Mathf.Max(0,LoadBody.maxLinearVelocity*LoadBody.maxLinearVelocity-sideways));
            float acceleration=(allowedSpeed-Vector3.Dot(velocity,direction))/dt-Vector3.Dot(Physics.gravity,direction);
            float dampingAcceleration=damping*Vector3.Dot(reelVelocity-LoadBody.GetPointVelocity(AttachWorld)-impactCarry,direction);
            float maximumSpring=Mathf.Max(0,acceleration-dampingAcceleration)/Mathf.Max(.001f,pull.magnitude-tether.maxDistance);
            tether.spring=LoadBody.mass*Mathf.Min(settings.SpringAcceleration*strength,maximumSpring);
        }

        private void ResetPullTension()
        {
            tensionCharge=0;
            if(tether!=null && payload!=null)
            {
                tether.spring=LoadBody.mass*settings.SpringAcceleration;
                tether.damper=LoadBody.mass*settings.SpringDamping;
                LoadBody.maxLinearVelocity=settings.MaximumLoadSpeed;
            }
        }

        private void DrawAttachedRope()
        {
            if(job==null || !job.Attached || payload==null || ExtractionSnapshot.Craning(job.Phase) || job.Route.Length<3) return;
            ropeView.Draw(terrain.transform,job.Route,job.Progress,ReelDistance,payload.transform.TransformPoint(job.AttachLocal),true);
        }

        private static readonly Unity.Profiling.ProfilerMarker HaulMarker=new Unity.Profiling.ProfilerMarker("Crane.Haul");
        private static readonly Unity.Profiling.ProfilerMarker ContactsMarker=new Unity.Profiling.ProfilerMarker("Crane.Contacts");
    }
}
