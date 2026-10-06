using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class ExcavationGrid
    {
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
            Vector3 center = (from + to) * .5f;
            half += Abs(inverse * (to - from) * .5f);
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

        // A C4 charge's blast (026): a ball of `radius` round `center` in every ground, geode shell out to `shellRadius`
        // (it cracks open further), its edge wobbling by at most BlastWobble of the radius with direction (seeded by
        // `seed`) so the crater is not a stamped sphere. The permanent bank holds as for any cut; the shared cleanup follows.
        public const float BlastWobble = .06f;
        public bool RemoveBlast(Vector3 center, float radius, float shellRadius, int seed, out BoundsInt changed)
        {
            changed = default;
            if (!WorldSnapshot.Valid(center) || !(radius > 0) || radius > 8 || !(shellRadius >= radius) || shellRadius > 12) return false;
            BeginRemoval();
            uint random = unchecked((uint)seed) ^ 0x51ed2701u;
            Vector3 phase = new Vector3(Next01(ref random), Next01(ref random), Next01(ref random)) * (2 * Mathf.PI);
            Vector3 influence = Vector3.one * (shellRadius * (1 + BlastWobble) + band);
            var first = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt((center - influence) / CellSize));
            var last = Vector3Int.Min(Size, Vector3Int.CeilToInt((center + influence) / CellSize));
            Vector3Int changedMin = Size + Vector3Int.one, changedMax = -Vector3Int.one;
            float volume = CellSize * CellSize * CellSize;
            for (int z = first.z; z <= last.z; z++) for (int y = first.y; y <= last.y; y++) for (int x = first.x; x <= last.x; x++)
            {
                int index = x + y * strideY + z * strideZ; float before = density[index]; if (before <= -band) continue;
                Vector3 delta = new Vector3(x, y, z) * CellSize - center;
                float distance = delta.magnitude;
                float reach = materials[index] == TerrainMaterialId.GeodeShell ? shellRadius : radius;
                if (distance - reach * (1 + BlastWobble) >= before) continue;
                Vector3 direction = distance > 1e-5f ? delta / distance : Vector3.up;
                float wobble = .6f * Mathf.Sin(3.1f * direction.x + phase.x) * Mathf.Sin(2.7f * direction.y + phase.y)
                    + .4f * Mathf.Sin(4.3f * direction.z + phase.z);
                float cut = distance - reach * (1 + BlastWobble * wobble);
                float after = Mathf.Max(-band, Mathf.Min(before, cut));
                if (bankBeyond != null) after = Mathf.Max(after, Mathf.Min(before, Bank(x, y, z)));
                if (before - after < .00001f) continue;
                density[index] = after;
                if (before > 0) remnantSeeds.Add(index);
                if (before > 0 && after <= 0) { severedSamples.Add(index); lowestCarvedY = Mathf.Min(lowestCarvedY, y); }
                float weight = (x == 0 || x == Size.x ? .5f : 1) * (y == 0 || y == Size.y ? .5f : 1) * (z == 0 || z == Size.z ? .5f : 1);
                LastRemovedVolume += (Mathf.Clamp01(.5f + before / CellSize) - Mathf.Clamp01(.5f + after / CellSize)) * volume * weight;
                var point = new Vector3Int(x, y, z); changedMin = Vector3Int.Min(changedMin, point); changedMax = Vector3Int.Max(changedMax, point);
            }
            return CompleteRemoval(changedMin, changedMax, out changed);
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
