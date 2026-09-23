using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    public enum ExtractionPhase { Planning, Deploying, Attaching, Hauling, Delivering, Obstructed }

    public sealed class ExtractionSnapshot
    {
        public const int MaximumWaypoints = 8192;
        public string FindId;
        public ExtractionPhase Phase;
        public Vector3 AttachLocal, Outward;
        public Vector3 LinearVelocity, AngularVelocity;
        public Vector3[] Route = Array.Empty<Vector3>();
        public float Progress, PhaseSeconds;
        public bool Attached;
        public int AnchorIndex => Route.Length - 3;
        public ExtractionSnapshot Copy() => new ExtractionSnapshot { FindId=FindId, Phase=Phase, AttachLocal=AttachLocal,
            Outward=Outward, LinearVelocity=LinearVelocity, AngularVelocity=AngularVelocity,
            Route=(Vector3[])Route.Clone(), Progress=Progress, PhaseSeconds=PhaseSeconds, Attached=Attached };

        public void Validate(Dictionary<string,FindSnapshot> population, GridSnapshot terrain)
        {
            WorldSnapshot.Require(!string.IsNullOrEmpty(FindId) && population.TryGetValue(FindId,out var owner)
                && owner.State==FindState.Extracting && owner.Item.Kind==DiscoveryKind.Unique,"Extraction has no unique owner.");
            WorldSnapshot.Require(Enum.IsDefined(typeof(ExtractionPhase),Phase) && WorldSnapshot.Valid(AttachLocal)
                && AttachLocal.sqrMagnitude<100 && WorldSnapshot.Valid(Outward) && Outward.sqrMagnitude>.5f && Outward.sqrMagnitude<1.5f
                && WorldSnapshot.Finite(Progress) && Progress>=0 && WorldSnapshot.Finite(PhaseSeconds) && PhaseSeconds>=0 && PhaseSeconds<=60,
                "Invalid extraction progress.");
            WorldSnapshot.Require(Route!=null && Route.Length<=MaximumWaypoints,"Invalid extraction route size.");
            WorldSnapshot.Require(WorldSnapshot.Valid(LinearVelocity) && LinearVelocity.sqrMagnitude<=100
                && WorldSnapshot.Valid(AngularVelocity) && AngularVelocity.sqrMagnitude<=100, "Invalid extraction motion.");
            WorldSnapshot.Require(Attached || LinearVelocity==Vector3.zero && AngularVelocity==Vector3.zero,
                "Unattached extraction has rope motion.");
            bool hasRoute=Phase!=ExtractionPhase.Planning && Phase!=ExtractionPhase.Obstructed;
            WorldSnapshot.Require(!hasRoute || Route.Length>=4,"Extraction route is missing.");
            WorldSnapshot.Require(Route.Length==0 || Route.Length>=4,"Extraction route is incomplete.");
            Vector3 extent=(Vector3)terrain.Size*terrain.CellSize;
            foreach(var point in Route) WorldSnapshot.Require(WorldSnapshot.Valid(point)
                && point.y>=0 && point.y<=extent.y+20 && point.x>=-20 && point.x<=extent.x+20 && point.z>=-20 && point.z<=extent.z+20,
                "Extraction waypoint outside the site.");
            WorldSnapshot.Require(Progress<=Length(Route)+.001f,"Extraction progress exceeds its route.");
            float haulLength = Route.Length < 4 ? 0 : Length(Route, AnchorIndex);
            WorldSnapshot.Require(Phase != ExtractionPhase.Planning || (Route.Length == 0 && Progress == 0), "Planning has stale route progress.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Deploying || (!Attached && Progress <= haulLength), "Invalid rope deployment.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Attaching || (!Attached && Progress == 0), "Invalid rope attachment.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Hauling || (Attached && Progress <= haulLength + .001f), "Invalid haul progress.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Delivering || (Attached && Progress >= haulLength - .001f), "Invalid pad delivery.");
            WorldSnapshot.Require(Phase != ExtractionPhase.Obstructed || Attached, "An unattached failed mark must return to the world.");
            if (Route.Length >= 4)
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
