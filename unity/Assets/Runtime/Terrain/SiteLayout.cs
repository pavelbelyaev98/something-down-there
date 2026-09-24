using UnityEngine;

namespace SomethingDownThere
{
    // The shipped MainGame site in one place. The scene owns the serialized copy;
    // editor tooling, bounds checks and tests read these numbers from here so the
    // reservoir depth is a single authored value. Deepening appends soil below the
    // surface, which is why the origin moves down with the depth.
    public static class SiteLayout
    {
        public const float CellSize = 0.125f;
        public const int DepthCells = 800;
        public const int WidthCells = 192;
        public const int ChunkSize = 16;
        public const float OpeningRadius = WidthCells * CellSize * .5f;
        // Permanent lakebed ground around the opening sits just above the voxel top.
        public const float GroundTop = .03f;
        // The rim collar covers the terrain-hole edge with an exact circle; its closed
        // underside roofs the grid corners, which stay reachable by lateral digging.
        public const float RimTop = .045f;
        public const float RimOuterRadius = 12.9f;
        public const float RimRadius = 17.5f;
        // Keep the fixed surface above the shallowest saved finds.
        public const float RimBottom = -.01f;
        public static readonly Vector3Int Size = new Vector3Int(WidthCells, DepthCells, WidthCells);
        public static readonly Vector3 Origin = new Vector3(-12f, -DepthCells * CellSize, -12f);
        public static readonly Vector3 Extent = new Vector3(WidthCells * CellSize, DepthCells * CellSize, WidthCells * CellSize);
    }
}
