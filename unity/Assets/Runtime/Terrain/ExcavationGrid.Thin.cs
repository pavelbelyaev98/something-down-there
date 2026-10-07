using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class ExcavationGrid
    {
        private readonly List<int> thinGround = new List<int>();

        // A hollow's roof gives way (116; user, 2026-10-07: "artifacts that don't disappear after digging"): ground thinner
        // than maxWidth across an axis within radius of a point (grid-local) falls in, so digging into a cave or geode
        // leaves no lace of hard shell or sheet hanging across the opening. The whole set is chosen before any of it goes,
        // so the order cannot thin a thick wall; the usual cleanup follows.
        public bool RemoveThin(Vector3 centre, float radius, float maxWidth, out BoundsInt changed)
        {
            changed = default;
            if (!WorldSnapshot.Valid(centre) || !Finite(radius) || radius <= 0 || radius > 4 || !Finite(maxWidth) || maxWidth <= 0) return false;
            BeginRemoval();
            var first = Vector3Int.Max(Vector3Int.one * 2, Vector3Int.FloorToInt((centre - Vector3.one * radius) / CellSize));
            var last = Vector3Int.Min(Size - Vector3Int.one * 2, Vector3Int.CeilToInt((centre + Vector3.one * radius) / CellSize));
            thinGround.Clear();
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
            {
                int index = x + y * strideY + z * strideZ;
                if (density[index] <= 0 || (new Vector3(x, y, z) * CellSize - centre).sqrMagnitude > radius * radius) continue;
                if (bankBeyond != null && Bank(x, y, z) > 0) continue;
                if (ThinAcross(index, 1, maxWidth) || ThinAcross(index, strideY, maxWidth, Size.y - y) || ThinAcross(index, strideZ, maxWidth))
                    thinGround.Add(index);
            }
            Vector3Int changedMin = Size + Vector3Int.one, changedMax = -Vector3Int.one;
            float volume = CellSize * CellSize * CellSize;
            foreach (int index in thinGround)
            {
                var p = SampleCoordinates(index);
                LastRemovedVolume += Mathf.Clamp01(.5f + density[index] / CellSize) * volume;
                density[index] = -band;
                severedSamples.Add(index); remnantSeeds.Add(index);
                lowestCarvedY = Mathf.Min(lowestCarvedY, p.y);
                changedMin = Vector3Int.Min(changedMin, p); changedMax = Vector3Int.Max(changedMax, p);
            }
            return CompleteRemoval(changedMin, changedMax, out changed);
        }

        // Seeded air (stash pockets, geodes, caves, the lab's carves) lies below the deepest cut without having been cut, so
        // ground beside it is not "untouched deep ground" for the support and sliver shortcuts: a piece left hanging into a
        // hollow below the deepest cut must still be found loose. Sample boxes, a little wider than each hollow.
        private readonly List<(Vector3Int min, Vector3Int max)> hollows = new List<(Vector3Int min, Vector3Int max)>();

        private void AddHollow(Vector3 min, Vector3 max)
            => hollows.Add((Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt(min / CellSize) - Vector3Int.one * 2),
                Vector3Int.Min(Size, Vector3Int.CeilToInt(max / CellSize) + Vector3Int.one * 2)));

        private void FindHollows()
        {
            hollows.Clear();
            foreach (var stash in Layout.Stashes) if (stash.HasPocket) AddHollow(stash.Min, stash.Max);
            foreach (var geode in Layout.Geodes) AddHollow(geode.Min, geode.Max);
            foreach (var cavern in Layout.Caverns) AddHollow(cavern.Min, cavern.Max);
            foreach (var (min, max) in LabBounds()) AddHollow(min, max);
        }

        // Below the deepest cut and beside no seeded air: ground that still joins the untouched depths.
        private bool Deep(Vector3Int p)
        {
            if (p.y >= lowestCarvedY) return false;
            for (int i = 0; i < hollows.Count; i++)
            {
                var (min, max) = hollows[i];
                if (p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y && p.z >= min.z && p.z <= max.z) return false;
            }
            return true;
        }
    }
}
