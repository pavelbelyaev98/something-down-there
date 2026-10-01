using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // Route planning, the crane swinging over the hole, the hook riding the smart rope down the hole and
    // along the route into the lifting eye and the crane reeling the load in, then lifting it clear,
    // carrying it to camp, setting it down and letting it settle where it lands.
    public enum ExtractionPhase { Planning, Reaching, Deploying, Attaching, Hauling, Retensioning, Lifting, Carrying, SettingDown, Settling }

    public sealed class ExtractionSnapshot
    {
        public const int MaximumWaypoints = 8192, MaximumSpots = 16;
        public string FindId;
        public ExtractionPhase Phase;
        // Find-local: the marked rope attachment, where the lifting eye is bolted on.
        public Vector3 AttachLocal, Outward;
        public Vector3 LinearVelocity, AngularVelocity;
        // Site-local route from the attachment up the dug passage to the crane's park point over the mouth (the last point).
        public Vector3[] Route = Array.Empty<Vector3>();
        public float Progress, PhaseSeconds;
        public bool Attached;
        public int Spot;
        public CranePose Pose;
        public int AnchorIndex => Route.Length - 1;
        public static bool Craning(ExtractionPhase phase) => phase >= ExtractionPhase.Lifting;
        public ExtractionSnapshot Copy()
        {
            var copy = (ExtractionSnapshot)MemberwiseClone();
            copy.Route = (Vector3[])Route.Clone();
            return copy;
        }

        public void Validate(Dictionary<string,FindSnapshot> population, GridSnapshot terrain)
        {
            WorldSnapshot.Require(!string.IsNullOrEmpty(FindId) && population.TryGetValue(FindId,out var owner)
                && owner.State==FindState.Extracting && owner.Item.Kind==DiscoveryKind.Unique,"Extraction has no unique owner.");
            WorldSnapshot.Require(Enum.IsDefined(typeof(ExtractionPhase),Phase) && WorldSnapshot.Valid(AttachLocal)
                && AttachLocal.sqrMagnitude<100
                && WorldSnapshot.Valid(Outward) && Outward.sqrMagnitude>.5f && Outward.sqrMagnitude<1.5f
                && WorldSnapshot.Finite(Progress) && Progress>=0 && WorldSnapshot.Finite(PhaseSeconds) && PhaseSeconds>=0 && PhaseSeconds<=60,
                "Invalid extraction progress.");
            WorldSnapshot.Require(Spot>=0 && Spot<MaximumSpots, "Invalid set-down spot.");
            WorldSnapshot.Require(Pose.Finite && Mathf.Abs(Pose.Yaw)<=360 && Mathf.Abs(Pose.HookYaw)<=360
                && Pose.Reach>=0 && Pose.Reach<=200 && Pose.Rope>=0 && Pose.Rope<=1000, "Invalid crane pose.");
            WorldSnapshot.Require(Route!=null && Route.Length<=MaximumWaypoints,"Invalid extraction route size.");
            // Off-centre rupture/contact impulses can briefly exceed the body's
            // pre-solver spin cap. Preserve those real spins in the checkpoint.
            WorldSnapshot.Require(WorldSnapshot.Valid(LinearVelocity) && LinearVelocity.sqrMagnitude<=100
                && WorldSnapshot.Valid(AngularVelocity) && AngularVelocity.sqrMagnitude<=400, "Invalid extraction motion.");
            WorldSnapshot.Require(Attached || LinearVelocity==Vector3.zero && AngularVelocity==Vector3.zero,
                "Unattached extraction has rope motion.");
            bool hasRoute=Phase!=ExtractionPhase.Planning;
            WorldSnapshot.Require(!hasRoute || Route.Length>=3,"Extraction route is missing.");
            WorldSnapshot.Require(Route.Length==0 || Route.Length>=3,"Extraction route is incomplete.");
            Vector3 extent=(Vector3)terrain.Size*terrain.CellSize;
            foreach(var point in Route) WorldSnapshot.Require(WorldSnapshot.Valid(point)
                && point.y>=0 && point.y<=extent.y+60 && point.x>=-40 && point.x<=extent.x+40 && point.z>=-40 && point.z<=extent.z+40,
                "Extraction waypoint outside the site.");
            float haulLength = Route.Length < 3 ? 0 : Length(Route);
            WorldSnapshot.Require(Progress<=haulLength+.001f,"Extraction progress exceeds its route.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Planning || (!Attached && Route.Length == 0 && Progress == 0), "Invalid route planning.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Reaching || (!Attached && Progress == 0), "Invalid crane approach.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Deploying || !Attached, "Invalid rope deployment.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Attaching || (!Attached && Progress == 0), "Invalid rope attachment.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Hauling && Phase != ExtractionPhase.Retensioning || Attached, "Invalid haul progress.");
            WorldSnapshot.Require(!Craning(Phase) || (Attached && Progress >= haulLength - .001f), "Invalid crane lift.");
            if (Route.Length >= 3 && !Craning(Phase))
            {
                var find = population[FindId];
                Vector3 hook = find.Position + find.Rotation * Vector3.Scale(find.Scale, AttachLocal);
                Vector3 expected = Point(Route, Attached ? Progress : 0, out _);
                WorldSnapshot.Require(Vector3.Distance(hook, expected) < (Attached ? 2f : .005f), "Payload has left the rope guide.");
            }
        }
        public static float Length(Vector3[] points, int end=-1)
        {
            if(points==null) return 0;
            if(end<0) end=points.Length-1;
            float result=0; for(int i=1;i<=end;i++) result+=Vector3.Distance(points[i-1],points[i]); return result;
        }
        public static Vector3 Point(Vector3[] points,float distance,out int segment)
        {
            segment=0;
            for(int i=1;i<points.Length;i++)
            {
                float length=Vector3.Distance(points[i-1],points[i]);
                if(distance<=length) { segment=i-1; return Vector3.Lerp(points[i-1],points[i],length<=.00001f?1:distance/length); }
                distance-=length;
            }
            segment=Mathf.Max(0,points.Length-2); return points[points.Length-1];
        }
    }
}
