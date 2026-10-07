using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SomethingDownThere
{
    // Seeded air (stash chests' pockets, geodes' hollows, caves, a lab's hollows and dug scenes) exists from the start, below the surface
    // layer, with no cut to create its chunks, so they are built with the session. A great cave (116) holds well over a
    // thousand chunks, so it is built as the view nears it instead: within GreatReach of its box, nearest first, GreatBudget
    // milliseconds a frame; all at once from inside the box. Until then it is sealed inside solid ground, unseen.
    public sealed partial class TerrainVolume
    {
        public TerrainGround.GroundLayout GroundLayout => grid?.Layout ?? TerrainGround.GroundLayout.Empty;
        private const float GreatReach = 15f, GreatBudget = 2.5f;
        private readonly List<int> greatPending = new List<int>();
        private readonly List<Vector3Int> greatQueue = new List<Vector3Int>();
        private int greatBuilding = -1;
        private Transform greatEye;
        // Whether every great cave has been built (tests, the lab).
        public bool GreatCavesBuilt => greatPending.Count == 0 && greatBuilding < 0;

        private void MaterializeSeededAir()
        {
            foreach (var stash in grid.Layout.Stashes) if (stash.HasPocket) MaterializeAround(stash.Min, stash.Max);
            foreach (var geode in grid.Layout.Geodes) MaterializeAround(geode.Min, geode.Max);
            greatPending.Clear(); greatQueue.Clear(); greatBuilding = -1;
            var caverns = grid.Layout.Caverns;
            for (int c = 0; c < caverns.Length; c++)
                if (caverns[c].Great) greatPending.Add(c); else MaterializeAround(caverns[c].Min, caverns[c].Max);
            foreach (var (min, max) in grid.LabBounds()) MaterializeAround(min, max);
        }

        private void MaterializeAround(Vector3 min, Vector3 max)
        {
            foreach (var key in ChunksAround(min, max)) Refresh(key);
        }

        private IEnumerable<Vector3Int> ChunksAround(Vector3 min, Vector3 max)
        {
            Vector3Int first = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt(min / cellSize) - Vector3Int.one * 2) / chunkSize;
            Vector3Int last = Vector3Int.Min(dimensions - Vector3Int.one, Vector3Int.CeilToInt(max / cellSize) + Vector3Int.one * 2) / chunkSize;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
                yield return new Vector3Int(x, y, z);
        }

        // Whether a chunk lies in a great cave still to be built: a load leaves it to the stream.
        private bool AwaitsGreatCave(Vector3Int key)
        {
            var caverns = grid.Layout.Caverns;
            Vector3 min = (Vector3)(key * chunkSize) * cellSize, max = min + Vector3.one * (chunkSize * cellSize);
            foreach (int c in greatPending)
                if (Overlaps(min, max, caverns[c].Min, caverns[c].Max)) return true;
            return greatBuilding >= 0 && Overlaps(min, max, caverns[greatBuilding].Min, caverns[greatBuilding].Max);
        }

        private static bool Overlaps(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
            => aMin.x <= bMax.x && aMax.x >= bMin.x && aMin.y <= bMax.y && aMax.y >= bMin.y && aMin.z <= bMax.z && aMax.z >= bMin.z;

        private void StreamGreatCaves()
        {
            if (grid == null || IsRestoring || GreatCavesBuilt) return;
            if (greatEye == null)
            {
                var viewer = FindAnyObjectByType<FpsPlayer>();
                greatEye = viewer != null && viewer.ViewCamera != null ? viewer.ViewCamera.transform : null;
                if (greatEye == null) return;
            }
            Vector3 eye = transform.InverseTransformPoint(greatEye.position);
            var caverns = grid.Layout.Caverns;
            if (greatBuilding < 0)
            {
                for (int i = 0; i < greatPending.Count && greatBuilding < 0; i++)
                {
                    var cave = caverns[greatPending[i]];
                    Vector3 nearest = Vector3.Max((Vector3)cave.Min, Vector3.Min((Vector3)cave.Max, eye));
                    if ((nearest - eye).sqrMagnitude > GreatReach * GreatReach) continue;
                    greatBuilding = greatPending[i];
                    greatPending.RemoveAt(i);
                    greatQueue.Clear();
                    greatQueue.AddRange(ChunksAround(cave.Min, cave.Max));
                    // Nearest last, so the queue is taken from its end.
                    greatQueue.Sort((a, b) => ChunkDistance(b, eye).CompareTo(ChunkDistance(a, eye)));
                }
                if (greatBuilding < 0) return;
            }
            var building = caverns[greatBuilding];
            bool inside = eye.x >= building.Min.x && eye.x <= building.Max.x && eye.y >= building.Min.y && eye.y <= building.Max.y
                && eye.z >= building.Min.z && eye.z <= building.Max.z;
            var watch = Stopwatch.StartNew();
            while (greatQueue.Count > 0 && (inside || watch.Elapsed.TotalMilliseconds < GreatBudget))
            {
                var key = greatQueue[greatQueue.Count - 1];
                greatQueue.RemoveAt(greatQueue.Count - 1);
                if (chunks.ContainsKey(key)) continue;
                var chunk = Materialize(key);
                Rebuild(key, chunk);
                // Chunks of solid ground or of air alone hold nothing: a cut recreates them when it reaches them.
                if (chunk.Mesh.GetIndexCount(0) == 0) Release(key);
            }
            if (greatQueue.Count == 0) greatBuilding = -1;
        }

        private float ChunkDistance(Vector3Int key, Vector3 eye)
            => ((Vector3)(key * chunkSize) * cellSize + Vector3.one * (chunkSize * cellSize * .5f) - eye).sqrMagnitude;

        // Every great cave built at once (tests and tools that look into one without walking there).
        public void BuildGreatCaves()
        {
            if (grid == null) return;
            var caverns = grid.Layout.Caverns;
            var all = new List<int>(greatPending);
            if (greatBuilding >= 0) all.Add(greatBuilding);
            greatPending.Clear(); greatQueue.Clear(); greatBuilding = -1;
            foreach (int c in all)
                foreach (var key in ChunksAround(caverns[c].Min, caverns[c].Max))
                {
                    if (chunks.ContainsKey(key)) continue;
                    var chunk = Materialize(key);
                    Rebuild(key, chunk);
                    if (chunk.Mesh.GetIndexCount(0) == 0) Release(key);
                }
        }
    }
}
