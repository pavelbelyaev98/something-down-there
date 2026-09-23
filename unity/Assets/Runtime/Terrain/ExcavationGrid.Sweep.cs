using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class ExcavationGrid
    {
        public readonly struct SweptBox
        {
            public readonly Vector3 Center, Half;
            public readonly Quaternion Rotation;

            public SweptBox(Vector3 from, Vector3 to, Quaternion rotation, Vector3 half)
            {
                Center = (from + to) * .5f;
                Rotation = rotation;
                Half = half + Abs(Quaternion.Inverse(rotation) * (to - from) * .5f);
            }

            // Project every axis of the inner box into this box's frame. Checking
            // the full support extents also covers rotating corners, not just centers.
            public bool Contains(SweptBox other)
            {
                var inverse = Quaternion.Inverse(Rotation);
                var relative = inverse * other.Rotation;
                Vector3 extent = Abs(inverse * (other.Center - Center))
                    + Abs(relative * Vector3.right) * other.Half.x
                    + Abs(relative * Vector3.up) * other.Half.y
                    + Abs(relative * Vector3.forward) * other.Half.z;
                return extent.x <= Half.x && extent.y <= Half.y && extent.z <= Half.z;
            }
        }

        // Conservative extrusion of a fixed-orientation load over a bounded substep.
        // Inflating each local axis by half its travel covers every intermediate pose.
        public bool RemoveBoxSweep(Vector3 from, Vector3 to, Quaternion rotation, Vector3 half, out BoundsInt changed)
        {
            changed = default;
            if (!WorldSnapshot.Valid(from) || !WorldSnapshot.Valid(to) || !WorldSnapshot.Valid(rotation)
                || !WorldSnapshot.Valid(half) || half.x <= 0 || half.y <= 0 || half.z <= 0
                || half.magnitude > 8 || Vector3.Distance(from,to) > 1) return false;
            BeginRemoval();
            var inverse = Quaternion.Inverse(rotation);
            var sweep = new SweptBox(from, to, rotation, half);
            Vector3 center = sweep.Center;
            half = sweep.Half;
            Vector3 axes = Abs(rotation*Vector3.right)*half.x + Abs(rotation*Vector3.up)*half.y + Abs(rotation*Vector3.forward)*half.z;
            Vector3 influence=axes+Vector3.one*band;
            var first=Vector3Int.Max(Vector3Int.zero,Vector3Int.FloorToInt((center-influence)/CellSize));
            var last=Vector3Int.Min(Size,Vector3Int.CeilToInt((center+influence)/CellSize));
            Vector3Int changedMin=Size+Vector3Int.one, changedMax=-Vector3Int.one;
            float volume=CellSize*CellSize*CellSize;
            for(int z=first.z;z<=last.z;z++) for(int y=first.y;y<=last.y;y++) for(int x=first.x;x<=last.x;x++)
            {
                int index=x+y*strideY+z*strideZ; float before=density[index]; if(before<=-band) continue;
                Vector3 q=Abs(inverse*(new Vector3(x,y,z)*CellSize-center))-half;
                float cut=Vector3.Max(q,Vector3.zero).magnitude+Mathf.Min(0,Mathf.Max(q.x,Mathf.Max(q.y,q.z)));
                float after=Mathf.Max(-band,Mathf.Min(before,cut)); if(before-after<.00001f) continue;
                density[index]=after;
                if(before>0) remnantSeeds.Add(index);
                if(before>0&&after<=0) { severedSamples.Add(index); lowestCarvedY=Mathf.Min(lowestCarvedY,y); }
                float weight=(x==0||x==Size.x?.5f:1)*(y==0||y==Size.y?.5f:1)*(z==0||z==Size.z?.5f:1);
                LastRemovedVolume+=(Mathf.Clamp01(.5f+before/CellSize)-Mathf.Clamp01(.5f+after/CellSize))*volume*weight;
                var point=new Vector3Int(x,y,z); changedMin=Vector3Int.Min(changedMin,point); changedMax=Vector3Int.Max(changedMax,point);
            }
            return CompleteRemoval(changedMin,changedMax,out changed);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        private void BeginRemoval()
        {
            LastRemovedVolume=LastDetachedVolume=LastRemnantVolume=0;
            LastDetachedSamples=LastSupportVisitedSamples=LastRemnantSamples=LastRemnantCheckedSamples=0;
            severedSamples.Clear(); remnantSeeds.Clear();
        }
        private bool CompleteRemoval(Vector3Int changedMin, Vector3Int changedMax, out BoundsInt changed)
        {
            using var profile = CleanupMarker.Auto();
            changed=default;
            if(changedMax.x<0) return false;
            RemoveDetachedSoil(ref changedMin,ref changedMax);
            RemovePaperThinSlivers(ref changedMin,ref changedMax);
            RemoveTinyRemnants(ref changedMin,ref changedMax);
            if(LastRemnantSamples>0) RemoveDetachedSoil(ref changedMin,ref changedMax);
            changed=new BoundsInt(changedMin,changedMax-changedMin+Vector3Int.one);
            RemovedVolume+=LastRemovedVolume;
            Revision++;
            return true;
        }

        private static readonly Unity.Profiling.ProfilerMarker CleanupMarker = new Unity.Profiling.ProfilerMarker("Excavation.Cleanup");
    }
}
