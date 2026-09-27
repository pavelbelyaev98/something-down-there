using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // Ground that lets go after a cut (concept 03 §4): undercut gravel pours, undercut backfill and
    // thin soil (roofs, shelves and lips under a metre) slump, and a cut into a crack's shattered band
    // breaks the connected band loose along the crack. Removal only, so none of it can bury the
    // player or close a route; ordinary cleanup clears anything left floating, and finds in the
    // released ground are simply exposed (their own release drops them).
    public enum GroundRelease { GravelPour, BackfillSlump, SoilSlump, CrackBreak }

    public sealed partial class ExcavationGrid
    {
        // Undercut samples (solid, air directly beneath) a cut must leave before loose ground lets
        // go: a real ceiling (~0.2 m² at 0.125 m cells), not a grazed edge.
        public const int PourSeeds = 12;
        // A crack breaks from a handful of freshly exposed band samples.
        public const int CrackSeeds = 4;
        // The gravel section stays within this reach of the undercut; backfill and soil slump less.
        public const float PourReach = 3f, BackfillSlumpReach = 2f, SoilSlumpReach = 1.5f;
        // Soil only slumps where it is thinner than this above the air it overhangs.
        public const float ThinSoilMetres = 1f;
        public static int MaximumSamples(GroundRelease kind) => kind switch
        {
            GroundRelease.GravelPour => 16000,
            GroundRelease.BackfillSlump => 10000,
            GroundRelease.SoilSlump => 6000,
            _ => 3000
        };
        private readonly List<int> releaseSamples = new List<int>(4096);
        private readonly HashSet<int> releaseVisited = new HashSet<int>();

        public static bool Releases(GroundRelease kind, TerrainMaterialId material) => kind switch
        {
            GroundRelease.GravelPour => material == TerrainMaterialId.Gravel,
            GroundRelease.BackfillSlump => material == TerrainMaterialId.Backfill,
            GroundRelease.SoilSlump => material == TerrainMaterialId.Soil,
            _ => material is TerrainMaterialId.FracturedRock or TerrainMaterialId.Crack or TerrainMaterialId.FracturedConcrete
        };

        public bool TryRelease(GroundRelease kind, BoundsInt cut, float reachMetres, out BoundsInt changed, out Vector3 centre)
        {
            changed = default; centre = default;
            releaseSamples.Clear(); releaseVisited.Clear();
            bool crack = kind == GroundRelease.CrackBreak;
            Vector3Int min = Vector3Int.Max(new Vector3Int(0, 1, 0), cut.min - Vector3Int.one);
            Vector3Int max = Vector3Int.Min(Size - new Vector3Int(0, 1, 0), cut.max + new Vector3Int(1, 2, 1));
            Vector3 sum = Vector3.zero;
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int index = x + y * strideY + z * strideZ;
                if (density[index] <= 0 || !Releases(kind, materials[index])) continue;
                // Loose ground seeds where the cut left air right beneath it; a crack where the cut
                // exposed its band.
                if (crack ? !Exposed(x, y, z) : density[index - strideY] > 0) continue;
                if (kind == GroundRelease.SoilSlump && !ThinSoil(x, y, z)) continue;
                releaseSamples.Add(index); releaseVisited.Add(index);
                sum += new Vector3(x, y, z);
            }
            if (releaseSamples.Count < (crack ? CrackSeeds : PourSeeds)) { releaseSamples.Clear(); releaseVisited.Clear(); return false; }
            Vector3 seed = sum / releaseSamples.Count;
            centre = seed * CellSize;
            float reach = reachMetres / CellSize, reachSquared = reach * reach;
            int limit = MaximumSamples(kind);
            // The connected released ground around the seeds, within its reach.
            for (int head = 0; head < releaseSamples.Count && releaseSamples.Count < limit; head++)
            {
                int index = releaseSamples[head];
                int x = index % strideY, y = index / strideY % (Size.y + 1), z = index / strideZ;
                for (int n = 0; n < 6; n++)
                {
                    int nx = x + (n == 0 ? 1 : n == 1 ? -1 : 0), ny = y + (n == 2 ? 1 : n == 3 ? -1 : 0), nz = z + (n == 4 ? 1 : n == 5 ? -1 : 0);
                    if (nx < 0 || ny < 1 || nz < 0 || nx > Size.x || ny > Size.y || nz > Size.z) continue;
                    int next = nx + ny * strideY + nz * strideZ;
                    if (!Releases(kind, materials[next]) || density[next] <= 0 || releaseVisited.Contains(next)) continue;
                    if ((new Vector3(nx, ny, nz) - seed).sqrMagnitude > reachSquared) continue;
                    if (kind == GroundRelease.SoilSlump && !ThinSoil(nx, ny, nz)) continue;
                    releaseVisited.Add(next); releaseSamples.Add(next);
                }
            }
            BeginRemoval();
            Vector3Int changedMin = Size + Vector3Int.one, changedMax = -Vector3Int.one;
            float unitVolume = CellSize * CellSize * CellSize;
            foreach (int index in releaseSamples)
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
            releaseSamples.Clear(); releaseVisited.Clear();
            return CompleteRemoval(changedMin, changedMax, out changed);
        }

        private bool Exposed(int x, int y, int z)
        {
            for (int n = 0; n < 6; n++)
            {
                int nx = x + (n == 0 ? 1 : n == 1 ? -1 : 0), ny = y + (n == 2 ? 1 : n == 3 ? -1 : 0), nz = z + (n == 4 ? 1 : n == 5 ? -1 : 0);
                if (nx < 0 || ny < 0 || nz < 0 || nx > Size.x || ny > Size.y || nz > Size.z) continue;
                if (density[nx + ny * strideY + nz * strideZ] <= 0) return true;
            }
            return false;
        }

        // Soil with air (or the open surface) less than ThinSoilMetres above it.
        private bool ThinSoil(int x, int y, int z)
        {
            int span = Mathf.CeilToInt(ThinSoilMetres / CellSize);
            for (int up = 1; up <= span; up++)
            {
                if (y + up > Size.y) return true;
                if (density[x + (y + up) * strideY + z * strideZ] <= 0) return true;
            }
            return false;
        }
    }
}
