using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // A small, derived lighting cache. Only the open top admits sky light; the route through
    // connected air attenuates it: descent slowly, sideways travel at once.
    // It is rebuilt from density (signed, about the distance to air, open at or below OpenDensity), never persisted in
    // excavation checkpoints.
    public sealed class ExcavationDaylightGrid
    {
        // Route choice weights in quarter cells. Straight down is cheapest; a slope (one down, one
        // aside) stays close to it; flat sideways travel and climbing cost twice as much, and a
        // rising diagonal both (so zigzagging along a tunnel never beats going straight along it).
        private const int Down = 4, DownDiagonal = 5, Across = 8, Up = 8, UpDiagonal = 16;
        private const float CostsPerCell = 4;
        // Transport stops after 40 m of weighted route; both falloffs are black well before that.
        private const int MaximumDistance = 320;
        // Two tallies along the chosen route set the light (user, 2026-09-30: natural, lamps needed
        // from ~12-15 m). Descent, straight or on a walked slope, fades from the first metre: about
        // 85% at 5 m, 53% at 10 m, 24% at 15 m, 8% at 20 m. Every metre sideways or back up loses
        // light at once, like light turning a corner, so a side tunnel dims faster at any depth.
        public const float DaylightReach = 12.5f, DaylightShoulder = 2f, SidewaysReach = 3f;
        private const float OpenDensity = .001f;
        // Where in its cell a node may stand (fractions of the node spacing): its own point first. A hole narrower than
        // the spacing can pass between nodes, leaving no air node to carry light down it; a buried node with air within
        // AnchorReach stands at the most open of these points instead (user, 2026-10-05: a narrow hole went black).
        private const float AnchorReach = .4f;
        private static readonly Vector3[] Anchors =
        {
            Vector3.zero,
            new Vector3(.4f, 0, 0), new Vector3(-.4f, 0, 0), new Vector3(0, 0, .4f), new Vector3(0, 0, -.4f),
            new Vector3(.28f, 0, .28f), new Vector3(-.28f, 0, .28f), new Vector3(.28f, 0, -.28f), new Vector3(-.28f, 0, -.28f),
            new Vector3(0, .4f, 0), new Vector3(0, -.4f, 0)
        };
        private readonly bool[] air;
        private readonly byte[] links, anchor;
        private readonly ushort[] distance, descent, aside;
        private readonly float[] descentFalloff = new float[MaximumDistance + 1], asideFalloff = new float[MaximumDistance + 1];
        private readonly List<int>[] buckets = new List<int>[MaximumDistance + 1];
        // light is what receivers see; a rebuild writes computed and publishes it when complete, so
        // provisional patches stay visible while it runs.
        private readonly byte[] light, computed;
        // The faint light an opened hollow holds (116): light never falls below it there (Floor).
        private readonly byte[] floor;
        private readonly List<bool> patchOpen = new List<bool>(), patchFresh = new List<bool>();
        private readonly List<byte> patchAnchor = new List<byte>();
        // Nodes the last rebuild reached, and the cells its final pass covered. Only these can hold
        // routes or light, so a rebuild resets and recomputes just this part of the grid.
        private Vector3Int reachedMin, reachedMax, litMin, litMax;
        private bool anyReached, anyLit;
        public Vector3Int Size { get; }
        public Vector3 Step { get; }
        public Vector3 Extent { get; }
        public byte[] Light => light;
        public int Count => light.Length;

        public ExcavationDaylightGrid(Vector3 extent, float spacing = 0.5f)
        {
            if (spacing <= 0 || extent.x <= 0 || extent.y <= 0 || extent.z <= 0)
                throw new ArgumentOutOfRangeException(nameof(extent));
            Extent = extent;
            Size = new Vector3Int(Mathf.CeilToInt(extent.x / spacing) + 1,
                Mathf.CeilToInt(extent.y / spacing) + 1, Mathf.CeilToInt(extent.z / spacing) + 1);
            Step = new Vector3(extent.x / (Size.x - 1), extent.y / (Size.y - 1), extent.z / (Size.z - 1));
            int count = Size.x * Size.y * Size.z;
            air = new bool[count]; links = new byte[count]; anchor = new byte[count]; light = new byte[count]; computed = new byte[count];
            floor = new byte[count];
            distance = new ushort[count]; descent = new ushort[count]; aside = new ushort[count];
            for (int i = 0; i < count; i++) distance[i] = MaximumDistance + 1;
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<int>();
            // One value per tally instead of an Exp per cell.
            for (int tally = 0; tally <= MaximumDistance; tally++)
            {
                descentFalloff[tally] = Daylight(tally / CostsPerCell * Step.y, 0);
                asideFalloff[tally] = Daylight(0, tally / CostsPerCell * Step.y);
            }
        }

        public static float Daylight(float descentMetres, float asideMetres = 0) =>
            Mathf.Exp(-Mathf.Pow(Mathf.Max(0, descentMetres) / DaylightReach, DaylightShoulder) - Mathf.Max(0, asideMetres) / SidewaysReach);

        private int Index(int x, int y, int z) => x + Size.x * (y + Size.y * z);

        // Grid nodes touched by a change, expanded by one node on every side.
        private void Range(Bounds changed, out Vector3Int min, out Vector3Int max)
        {
            Vector3 lo = changed.min - Step, hi = changed.max + Step;
            min = new Vector3Int(Mathf.Clamp(Mathf.FloorToInt(lo.x / Step.x), 0, Size.x - 1),
                Mathf.Clamp(Mathf.FloorToInt(lo.y / Step.y), 0, Size.y - 1), Mathf.Clamp(Mathf.FloorToInt(lo.z / Step.z), 0, Size.z - 1));
            max = new Vector3Int(Mathf.Clamp(Mathf.CeilToInt(hi.x / Step.x), 0, Size.x - 1),
                Mathf.Clamp(Mathf.CeilToInt(hi.y / Step.y), 0, Size.y - 1), Mathf.Clamp(Mathf.CeilToInt(hi.z / Step.z), 0, Size.z - 1));
        }

        // A cut shows new ground at once, but the route rebuild takes several frames. Until it
        // lands, only the fresh nodes (open now but not air at the last rebuild, or still unlit) are
        // filled from their lit neighbours: nearly all of the light from above, less from the side,
        // as the rebuild would, so a fresh cut neither flashes dark nor brightens the ground around
        // it. Wall samples beside fresh air take its light. The next rebuild replaces these values. Light passes only where
    // the rebuild's links would, through clear air between the nodes' standing points: a hole too small for them stays
    // dark rather than flashing bright until the rebuild lands (user, 2026-10-06, breaking into a geode).
        public void Patch(Bounds changed, Func<Vector3, float> density)
        {
            Range(changed, out var min, out var max);
            int sx = max.x - min.x + 1, sy = max.y - min.y + 1, sz = max.z - min.z + 1;
            patchOpen.Clear(); patchFresh.Clear(); patchAnchor.Clear();
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int i = Index(x, y, z);
                bool open = Locate(x, y, z, density, out var which), fresh = open && (!air[i] || light[i] == 0);
                patchOpen.Add(open); patchFresh.Add(fresh); patchAnchor.Add(which);
                if (fresh) light[i] = 0;
            }
            int layer = Size.x * Size.y;
            int Local(int x, int y, int z) => (x - min.x) + sx * ((y - min.y) + sy * (z - min.z));
            bool Inside(int x, int y, int z) => x >= min.x && y >= min.y && z >= min.z && x <= max.x && y <= max.y && z <= max.z;
            bool Open(int x, int y, int z) => Inside(x, y, z) ? patchOpen[Local(x, y, z)] : air[Index(x, y, z)];
            Vector3 At(int x, int y, int z) => Point(x, y, z)
                + Vector3.Scale(Anchors[Inside(x, y, z) ? patchAnchor[Local(x, y, z)] : anchor[Index(x, y, z)]], Step);
            // Node (x, y, z) takes its neighbour (nx, ny, nz)'s light by `share` where the air between them is clear.
            float From(int x, int y, int z, int nx, int ny, int nz, int j, float share)
            {
                if (!Open(nx, ny, nz)) return 0;
                var p = At(x, y, z);
                return Clear(p, At(nx, ny, nz) - p, density) ? light[j] * share : 0;
            }
            float side = Mathf.Exp(-Step.x / SidewaysReach);
            // A tool cut is a few nodes deep; large releases settle with the rebuild instead.
            for (int pass = Math.Min(6, Math.Max(sx, Math.Max(sy, sz))); pass > 0; pass--)
            {
                bool brightened = false;
                for (int z = min.z; z <= max.z; z++)
                for (int y = min.y; y <= max.y; y++)
                for (int x = min.x; x <= max.x; x++)
                {
                    if (!patchFresh[Local(x, y, z)]) continue;
                    int i = Index(x, y, z);
                    float best = 0;
                    if (y < Size.y - 1) best = Math.Max(best, From(x, y, z, x, y + 1, z, i + Size.x, .97f));
                    if (y > 0) best = Math.Max(best, From(x, y, z, x, y - 1, z, i - Size.x, side));
                    if (x > 0) best = Math.Max(best, From(x, y, z, x - 1, y, z, i - 1, side));
                    if (x < Size.x - 1) best = Math.Max(best, From(x, y, z, x + 1, y, z, i + 1, side));
                    if (z > 0) best = Math.Max(best, From(x, y, z, x, y, z - 1, i - layer, side));
                    if (z < Size.z - 1) best = Math.Max(best, From(x, y, z, x, y, z + 1, i + layer, side));
                    int value = (int)best;
                    if (value > light[i]) { light[i] = (byte)value; brightened = true; }
                }
                if (!brightened) break;
            }
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                if (patchOpen[Local(x, y, z)]) continue;
                int i = Index(x, y, z), best = light[i];
                if (x > 0 && Inside(x - 1, y, z) && patchFresh[Local(x - 1, y, z)]) best = Math.Max(best, light[i - 1]);
                if (x < Size.x - 1 && Inside(x + 1, y, z) && patchFresh[Local(x + 1, y, z)]) best = Math.Max(best, light[i + 1]);
                if (y > 0 && Inside(x, y - 1, z) && patchFresh[Local(x, y - 1, z)]) best = Math.Max(best, light[i - Size.x]);
                if (y < Size.y - 1 && Inside(x, y + 1, z) && patchFresh[Local(x, y + 1, z)]) best = Math.Max(best, light[i + Size.x]);
                if (z > 0 && Inside(x, y, z - 1) && patchFresh[Local(x, y, z - 1)]) best = Math.Max(best, light[i - layer]);
                if (z < Size.z - 1 && Inside(x, y, z + 1) && patchFresh[Local(x, y, z + 1)]) best = Math.Max(best, light[i + layer]);
                light[i] = (byte)best;
            }
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int i = Index(x, y, z);
                if (light[i] < floor[i]) light[i] = floor[i];
            }
        }

        // An opened hollow (a cave, a geode) is never pitch black (116; user, 2026-10-07: "there is a hole above me and the
        // crystals and ground technically reflect light"): its stone and crystals throw back a little light, `level` at
        // least through the box, whatever the route brings. ClearFloors forgets them (a new population).
        public void Floor(Bounds changed, byte level)
        {
            Range(changed, out var min, out var max);
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int i = Index(x, y, z);
                if (floor[i] < level) floor[i] = level;
                if (light[i] < level) light[i] = level;
                if (computed[i] < level) computed[i] = level;
            }
        }

        public void ClearFloors() => Array.Clear(floor, 0, floor.Length);

        private Vector3 Point(int x, int y, int z) => new Vector3(x * Step.x, y * Step.y, z * Step.z);

        // Where node i (at x, y, z) stands: its own point, or its anchor in a narrow hole.
        private Vector3 Stand(int i, int x, int y, int z) => Point(x, y, z) + Vector3.Scale(Anchors[anchor[i]], Step);

        // Whether the node's cell is open at its point or, failing that, at the most open anchor within reach (`which`).
        private bool Locate(int x, int y, int z, Func<Vector3, float> density, out byte which)
        {
            which = 0;
            var p = Point(x, y, z);
            float best = density(p);
            if (best <= OpenDensity) return true;
            // Density is about the distance to air: deep in the ground nothing within reach is open.
            if (best >= Step.x * AnchorReach) return false;
            for (byte k = 1; k < Anchors.Length; k++)
            {
                float value = density(p + Vector3.Scale(Anchors[k], Step));
                if (value < best) { best = value; which = k; }
            }
            return best <= OpenDensity;
        }

        // Caller timeslices this iterator. Expand the dirty area by one cell so
        // a newly opened/closed connection also invalidates its neighbour's link.
        // Links point up/positive from each air node: +x 1, +y 2, +z 4, and the
        // rising diagonals (+x,+y) 8, (-x,+y) 16, (+z,+y) 32, (-z,+y) 64.
        public IEnumerator Rebuild(Bounds changed, Func<Vector3, float> density)
        {
            Range(changed, out var first, out var last);
            int work = 0;
            // Where every node stands first: links run between those points.
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
            {
                int i = Index(x, y, z);
                air[i] = Locate(x, y, z, density, out anchor[i]);
                if (++work % 128 == 0) yield return null;
            }
            int plane = Size.x * Size.y;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
            {
                int i = Index(x, y, z);
                links[i] = 0;
                if (air[i])
                {
                    Vector3 p = Stand(i, x, y, z);
                    bool To(int j, int jx, int jy, int jz) => Clear(p, Stand(j, jx, jy, jz) - p, density);
                    if (x < Size.x - 1 && To(i + 1, x + 1, y, z)) links[i] |= 1;
                    if (y < Size.y - 1 && To(i + Size.x, x, y + 1, z)) links[i] |= 2;
                    if (z < Size.z - 1 && To(i + plane, x, y, z + 1)) links[i] |= 4;
                    if (y < Size.y - 1)
                    {
                        if (x < Size.x - 1 && To(i + 1 + Size.x, x + 1, y + 1, z)) links[i] |= 8;
                        if (x > 0 && To(i - 1 + Size.x, x - 1, y + 1, z)) links[i] |= 16;
                        if (z < Size.z - 1 && To(i + plane + Size.x, x, y + 1, z + 1)) links[i] |= 32;
                        if (z > 0 && To(i - plane + Size.x, x, y + 1, z - 1)) links[i] |= 64;
                    }
                }
                if (++work % 128 == 0) yield return null;
            }
            foreach (var bucket in buckets) bucket.Clear();
            if (anyReached)
                for (int z = reachedMin.z; z <= reachedMax.z; z++)
                for (int y = reachedMin.y; y <= reachedMax.y; y++)
                for (int x = reachedMin.x; x <= reachedMax.x; x++) distance[Index(x, y, z)] = MaximumDistance + 1;
            reachedMin = Size; reachedMax = -Vector3Int.one; anyReached = false;
            for (int z = 0; z < Size.z; z++)
            for (int x = 0; x < Size.x; x++)
            {
                int i = Index(x, Size.y - 1, z);
                if (air[i]) Visit(i, 0, 0, 0);
            }
            int layer = Size.x * Size.y;
            for (int cost = 0; cost <= MaximumDistance; cost++)
            {
                var bucket = buckets[cost];
                for (int b = 0; b < bucket.Count; b++)
                {
                    int i = bucket[b];
                    if (distance[i] != cost) continue;
                    int x = i % Size.x, y = i / Size.x % Size.y, z = i / layer;
                    byte own = links[i];
                    int d = descent[i], s = aside[i];
                    // Sideways and climbing moves add to the aside tally (a rising diagonal counts both its
                    // climb and its sideways step), descending ones to descent.
                    if ((own & 1) != 0) Visit(i + 1, cost + Across, d, s + 4);
                    if ((own & 2) != 0) Visit(i + Size.x, cost + Up, d, s + 4);
                    if ((own & 4) != 0) Visit(i + layer, cost + Across, d, s + 4);
                    if ((own & 8) != 0) Visit(i + 1 + Size.x, cost + UpDiagonal, d, s + 8);
                    if ((own & 16) != 0) Visit(i - 1 + Size.x, cost + UpDiagonal, d, s + 8);
                    if ((own & 32) != 0) Visit(i + layer + Size.x, cost + UpDiagonal, d, s + 8);
                    if ((own & 64) != 0) Visit(i - layer + Size.x, cost + UpDiagonal, d, s + 8);
                    if (x > 0 && (links[i - 1] & 1) != 0) Visit(i - 1, cost + Across, d, s + 4);
                    if (z > 0 && (links[i - layer] & 4) != 0) Visit(i - layer, cost + Across, d, s + 4);
                    if (y > 0)
                    {
                        int below = i - Size.x;
                        if ((links[below] & 2) != 0) Visit(below, cost + Down, d + 4, s);
                        if (x < Size.x - 1 && (links[below + 1] & 16) != 0) Visit(below + 1, cost + DownDiagonal, d + 5, s);
                        if (x > 0 && (links[below - 1] & 8) != 0) Visit(below - 1, cost + DownDiagonal, d + 5, s);
                        if (z < Size.z - 1 && (links[below + layer] & 64) != 0) Visit(below + layer, cost + DownDiagonal, d + 5, s);
                        if (z > 0 && (links[below - layer] & 32) != 0) Visit(below - layer, cost + DownDiagonal, d + 5, s);
                    }
                    if (++work % 128 == 0) yield return null;
                }
            }
            // Recompute where light is now or was before; everything else stays dark. Extend into
            // the immediately adjacent solid samples for smooth wall interpolation. This extension
            // never transports light.
            Vector3Int lo = anyReached ? Vector3Int.Max(Vector3Int.zero, reachedMin - Vector3Int.one) : Size;
            Vector3Int hi = anyReached ? Vector3Int.Min(Size - Vector3Int.one, reachedMax + Vector3Int.one) : -Vector3Int.one;
            Vector3Int from = anyLit ? Vector3Int.Min(lo, litMin) : lo, to = anyLit ? Vector3Int.Max(hi, litMax) : hi;
            litMin = lo; litMax = hi; anyLit = anyReached;
            for (int z = from.z; z <= to.z; z++)
            for (int y = from.y; y <= to.y; y++)
            for (int x = from.x; x <= to.x; x++)
            {
                int i = Index(x, y, z);
                float value = Lit(i);
                if (!air[i])
                {
                    if (x > 0) value = Math.Max(value, Lit(i - 1));
                    if (x < Size.x - 1) value = Math.Max(value, Lit(i + 1));
                    if (y > 0) value = Math.Max(value, Lit(i - Size.x));
                    if (y < Size.y - 1) value = Math.Max(value, Lit(i + Size.x));
                    if (z > 0) value = Math.Max(value, Lit(i - layer));
                    if (z < Size.z - 1) value = Math.Max(value, Lit(i + layer));
                }
                // Only connected air transports this light: sealed air (a chest's hollow) stays unlit.
                computed[i] = (byte)Math.Max(Mathf.RoundToInt(255 * value), floor[i]);
                if (++work % 512 == 0) yield return null;
            }
            Buffer.BlockCopy(computed, 0, light, 0, light.Length);
        }

        private static bool Clear(Vector3 p, Vector3 delta, Func<Vector3, float> density)
        {
            // Quarter-cell samples reject thin earth partitions between nodes.
            for (int s = 1; s <= 4; s++) if (density(p + delta * (s * 0.25f)) > OpenDensity) return false;
            return true;
        }

        private float Lit(int i) => air[i] && distance[i] <= MaximumDistance ? descentFalloff[descent[i]] * asideFalloff[aside[i]] : 0;

        private void Visit(int i, int cost, int down, int side)
        {
            if (!air[i] || cost > MaximumDistance || cost >= distance[i]) return;
            distance[i] = (ushort)cost; descent[i] = (ushort)down; aside[i] = (ushort)side;
            buckets[cost].Add(i);
            // Every node given a route is inside these bounds, even if a rebuild is abandoned.
            var node = new Vector3Int(i % Size.x, i / Size.x % Size.y, i / (Size.x * Size.y));
            reachedMin = Vector3Int.Min(reachedMin, node); reachedMax = Vector3Int.Max(reachedMax, node); anyReached = true;
        }

        public float Sample(Vector3 p)
        {
            if (p.y >= Extent.y || p.x < 0 || p.z < 0 || p.x > Extent.x || p.z > Extent.z) return 1;
            Vector3 q = new Vector3(p.x / Step.x, Mathf.Max(0, p.y) / Step.y, p.z / Step.z);
            int x = Mathf.Min((int)q.x, Size.x - 2), y = Mathf.Min((int)q.y, Size.y - 2), z = Mathf.Min((int)q.z, Size.z - 2);
            float a = q.x - x, b = q.y - y, c = q.z - z;
            float v0 = Mathf.Lerp(Mathf.Lerp(light[Index(x,y,z)], light[Index(x+1,y,z)], a),
                Mathf.Lerp(light[Index(x,y+1,z)], light[Index(x+1,y+1,z)], a), b);
            float v1 = Mathf.Lerp(Mathf.Lerp(light[Index(x,y,z+1)], light[Index(x+1,y,z+1)], a),
                Mathf.Lerp(light[Index(x,y+1,z+1)], light[Index(x+1,y+1,z+1)], a), b);
            return Mathf.Lerp(v0, v1, c) / 255f;
        }
    }
}
