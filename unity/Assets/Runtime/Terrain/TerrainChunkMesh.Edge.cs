using UnityEngine;

namespace SomethingDownThere
{
    // Every hole's mouth is rounded where it meets the untouched surface, the light cap fading into the soil
    // across the round (user, 2026-10-03: a straight vertical cut looked artificial; of a sloped lip, a rounded
    // edge, a colour fade and a clean cut, the rounded edge won). Only the surface mesh, and so its collider,
    // shapes the mouth; density, digging, exposure and saves stay as cut.
    public static partial class TerrainChunkMesh
    {
        // The quarter round's radius (metres); the ground shader fades the cap over the same depth (`_RimMix`).
        public const float MouthRound = .12f;

        // Cells beyond a cut whose surface mesh its mouth can change.
        public static int MouthReachCells(float cellSize) => Mathf.CeilToInt(MouthRound / cellSize) + 2;

        // How far the mouth reaches out from the cut's wall at this depth below the surface.
        private static float Widening(float depth)
        {
            float d = Mathf.Min(depth, MouthRound);
            return MouthRound - Mathf.Sqrt(MouthRound * MouthRound - (MouthRound - d) * (MouthRound - d));
        }

        // Lowers the samples' top layers round every opening in the untouched surface into the mouth. A column's
        // distance to the opening comes from the layer one cell down (so a tunnel under an intact roof never
        // opens the surface): the sample's own density where it is that close, or a nearby open sample's
        // distance less its depth into the air. Each sample depends only on the grid, so neighbouring chunks
        // shape their shared halo identically.
        private static void ShapeMouths(ExcavationGrid grid, Vector3Int origin, Vector3Int span, float[] samples)
        {
            float cell = grid.CellSize;
            int top = grid.Size.y, reference = top - 1;
            int yMin = Mathf.Max(origin.y, top - Mathf.CeilToInt(MouthRound / cell)), yMax = Mathf.Min(origin.y + span.y - 1, top);
            if (yMin > yMax) return;
            int reach = Mathf.CeilToInt(MouthRound / cell) + 1;
            for (int z = origin.z; z < origin.z + span.z; z++)
            for (int x = origin.x; x < origin.x + span.x; x++)
            {
                // Only untouched surface gets a mouth: a dug floor (even a shallow shave) keeps its depth.
                float own = grid.Sample(x, reference, z);
                if (own <= 0 || grid.Sample(x, top, z) < 0) continue;
                float distance = own < cell * .99f ? own : float.MaxValue;
                for (int dz = -reach; dz <= reach; dz++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    float open = grid.Sample(x + dx, reference, z + dz);
                    if (open > 0) continue;
                    distance = Mathf.Min(distance, Mathf.Max(0, Mathf.Sqrt(dx * dx + dz * dz) * cell + open));
                }
                if (distance >= MouthRound) continue;
                for (int y = yMin; y <= yMax; y++)
                {
                    float depth = (top - y) * cell;
                    if (depth >= MouthRound) continue;
                    int index = (x - origin.x) + (y - origin.y) * span.x + (z - origin.z) * span.x * span.y;
                    samples[index] = Mathf.Min(samples[index], distance - Widening(depth));
                }
            }
        }
    }
}
