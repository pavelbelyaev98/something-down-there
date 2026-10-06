using UnityEngine;

namespace SomethingDownThere
{
    // Seeded air (stash chests' pockets, a lab's hollows and dug scenes) exists from the start, below the surface
    // layer, with no cut to create its chunks, so they are built with the session.
    public sealed partial class TerrainVolume
    {
        public TerrainGround.GroundLayout GroundLayout => grid?.Layout ?? TerrainGround.GroundLayout.Empty;

        private void MaterializeSeededAir()
        {
            foreach (var stash in grid.Layout.Stashes) if (stash.HasPocket) MaterializeAround(stash.Min, stash.Max);
            foreach (var (min, max) in grid.LabBounds()) MaterializeAround(min, max);
        }

        private void MaterializeAround(Vector3 min, Vector3 max)
        {
            Vector3Int first = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt(min / cellSize) - Vector3Int.one * 2) / chunkSize;
            Vector3Int last = Vector3Int.Min(dimensions - Vector3Int.one, Vector3Int.CeilToInt(max / cellSize) + Vector3Int.one * 2) / chunkSize;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
                Refresh(new Vector3Int(x, y, z));
        }
    }
}
