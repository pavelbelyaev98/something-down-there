using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // The gravel pour (concept 03 §4): undercut gravel lets go in one bounded rush. Removal only,
    // so it can never bury the player or close a route; ordinary cleanup clears anything left
    // floating, and finds in the section are simply exposed (their own release drops them).
    public sealed partial class ExcavationGrid
    {
        // Gravel samples with air directly beneath that a cut must leave before gravel lets go:
        // a real ceiling (~0.2 m² at 0.125 m cells), not a grazed edge.
        public const int PourSeeds = 12;
        // The section stays within this reach of the undercut and this many samples (~30 m³).
        public const float PourReach = 3f;
        public const int PourMaximumSamples = 16000;
        private readonly List<int> pourSamples = new List<int>(4096);
        private readonly HashSet<int> pourVisited = new HashSet<int>();

        public bool TryPourGravel(BoundsInt cut, out BoundsInt changed, out Vector3 centre)
        {
            changed = default; centre = default;
            pourSamples.Clear(); pourVisited.Clear();
            // Seeds: solid gravel in and just above the cut whose sample below is now air.
            Vector3Int min = Vector3Int.Max(new Vector3Int(0, 1, 0), cut.min - Vector3Int.one);
            Vector3Int max = Vector3Int.Min(Size, cut.max + new Vector3Int(1, 2, 1));
            Vector3 sum = Vector3.zero;
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int index = x + y * strideY + z * strideZ;
                if (materials[index] != TerrainMaterialId.Gravel || density[index] <= 0 || density[index - strideY] > 0) continue;
                pourSamples.Add(index); pourVisited.Add(index);
                sum += new Vector3(x, y, z);
            }
            if (pourSamples.Count < PourSeeds) { pourSamples.Clear(); pourVisited.Clear(); return false; }
            Vector3 seed = sum / pourSamples.Count;
            centre = seed * CellSize;
            float reach = PourReach / CellSize, reachSquared = reach * reach;
            // The connected loose section above and around the undercut, within its bounds.
            for (int head = 0; head < pourSamples.Count && pourSamples.Count < PourMaximumSamples; head++)
            {
                int index = pourSamples[head];
                int x = index % strideY, y = index / strideY % (Size.y + 1), z = index / strideZ;
                for (int n = 0; n < 6; n++)
                {
                    int nx = x + (n == 0 ? 1 : n == 1 ? -1 : 0), ny = y + (n == 2 ? 1 : n == 3 ? -1 : 0), nz = z + (n == 4 ? 1 : n == 5 ? -1 : 0);
                    if (nx < 0 || ny < 1 || nz < 0 || nx > Size.x || ny > Size.y || nz > Size.z) continue;
                    int next = nx + ny * strideY + nz * strideZ;
                    if (materials[next] != TerrainMaterialId.Gravel || density[next] <= 0 || pourVisited.Contains(next)) continue;
                    if ((new Vector3(nx, ny, nz) - seed).sqrMagnitude > reachSquared) continue;
                    pourVisited.Add(next); pourSamples.Add(next);
                }
            }
            BeginRemoval();
            Vector3Int changedMin = Size + Vector3Int.one, changedMax = -Vector3Int.one;
            float unitVolume = CellSize * CellSize * CellSize;
            foreach (int index in pourSamples)
            {
                int x = index % strideY, y = index / strideY % (Size.y + 1), z = index / strideZ;
                float before = density[index], after = -CellSize;
                if (bankBeyond != null) after = Mathf.Max(after, Mathf.Min(before, Bank(x, y, z)));
                if (before - after < 0.00001f) continue;
                density[index] = after;
                remnantSeeds.Add(index);
                if (after <= 0) { severedSamples.Add(index); lowestCarvedY = Mathf.Min(lowestCarvedY, y); }
                float weight = (x == 0 || x == Size.x ? 0.5f : 1f) * (z == 0 || z == Size.z ? 0.5f : 1f);
                LastRemovedVolume += (Mathf.Clamp01(0.5f + before / CellSize) - Mathf.Clamp01(0.5f + after / CellSize)) * unitVolume * weight;
                var sample = new Vector3Int(x, y, z);
                changedMin = Vector3Int.Min(changedMin, sample); changedMax = Vector3Int.Max(changedMax, sample);
            }
            pourSamples.Clear(); pourVisited.Clear();
            return CompleteRemoval(changedMin, changedMax, out changed);
        }
    }
}
