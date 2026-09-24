using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // A small, derived lighting cache. Only the open top admits sky light;
    // distance through connected air attenuates it, with stronger lateral loss.
    // It is rebuilt from density, never persisted in excavation checkpoints.
    public sealed class ExcavationDaylightGrid
    {
        private const int MaximumDistance = 160;
        private readonly bool[] air;
        private readonly byte[] links, distance;
        private readonly List<int>[] buckets = new List<int>[MaximumDistance + 1];
        private readonly byte[] light;
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
            air = new bool[count]; links = new byte[count]; distance = new byte[count]; light = new byte[count];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<int>();
        }

        private int Index(int x, int y, int z) => x + Size.x * (y + Size.y * z);
        private Vector3 Point(int x, int y, int z) => new Vector3(x * Step.x, y * Step.y, z * Step.z);

        // Caller timeslices this iterator. Expand the dirty area by one cell so
        // a newly opened/closed connection also invalidates its neighbour's link.
        public IEnumerator Rebuild(Bounds changed, Func<Vector3, bool> isOpen)
        {
            Vector3 lo = changed.min - Step, hi = changed.max + Step;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(lo.x / Step.x), 0, Size.x - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(lo.y / Step.y), 0, Size.y - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(lo.z / Step.z), 0, Size.z - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(hi.x / Step.x), 0, Size.x - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(hi.y / Step.y), 0, Size.y - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt(hi.z / Step.z), 0, Size.z - 1);
            int work = 0;
            for (int z = z0; z <= z1; z++)
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = Index(x, y, z);
                Vector3 p = Point(x, y, z);
                air[i] = isOpen(p);
                links[i] = 0;
                if (air[i])
                {
                    if (x < Size.x - 1 && Clear(p, Vector3.right * Step.x, isOpen)) links[i] |= 1;
                    if (y < Size.y - 1 && Clear(p, Vector3.up * Step.y, isOpen)) links[i] |= 2;
                    if (z < Size.z - 1 && Clear(p, Vector3.forward * Step.z, isOpen)) links[i] |= 4;
                }
                if (++work % 128 == 0) yield return null;
            }
            foreach (var bucket in buckets) bucket.Clear();
            for (int i = 0; i < Count; i++) distance[i] = MaximumDistance + 1;
            for (int z = 0; z < Size.z; z++)
            for (int x = 0; x < Size.x; x++)
            {
                int i = Index(x, Size.y - 1, z);
                if (air[i]) { distance[i] = 0; buckets[0].Add(i); }
            }
            for (int cost = 0; cost <= MaximumDistance; cost++)
            {
                var bucket = buckets[cost];
                for (int b = 0; b < bucket.Count; b++)
                {
                    int i = bucket[b];
                    if (distance[i] != cost) continue;
                    int x = i % Size.x, y = i / Size.x % Size.y, z = i / (Size.x * Size.y);
                    if ((links[i] & 1) != 0) Visit(i + 1, cost + 2);
                    if ((links[i] & 2) != 0) Visit(i + Size.x, cost + 2);
                    if ((links[i] & 4) != 0) Visit(i + Size.x * Size.y, cost + 2);
                    if (x > 0 && (links[i - 1] & 1) != 0) Visit(i - 1, cost + 2);
                    if (y > 0 && (links[i - Size.x] & 2) != 0) Visit(i - Size.x, cost + 1);
                    if (z > 0 && (links[i - Size.x * Size.y] & 4) != 0) Visit(i - Size.x * Size.y, cost + 2);
                    if (++work % 128 == 0) yield return null;
                }
            }
            // Extend into the immediately adjacent solid samples for smooth
            // wall interpolation. This extension never transports light.
            for (int z = 0; z < Size.z; z++)
            for (int y = 0; y < Size.y; y++)
            for (int x = 0; x < Size.x; x++)
            {
                int i = Index(x, y, z), cost = distance[i];
                if (!air[i])
                {
                    if (x > 0) cost = Math.Min(cost, distance[i - 1]);
                    if (x < Size.x - 1) cost = Math.Min(cost, distance[i + 1]);
                    if (y > 0) cost = Math.Min(cost, distance[i - Size.x]);
                    if (y < Size.y - 1) cost = Math.Min(cost, distance[i + Size.x]);
                    if (z > 0) cost = Math.Min(cost, distance[i - Size.x * Size.y]);
                    if (z < Size.z - 1) cost = Math.Min(cost, distance[i + Size.x * Size.y]);
                }
                // Generous early/middle-depth fill, then a gradual fade into darkness.
                // Only connected air transports this light: sealed rooms stay unlit.
                float pathMetres = cost * Step.y;
                float daylight = cost > MaximumDistance ? 0
                    : Mathf.Exp(-0.018f * pathMetres - 0.0012f * pathMetres * pathMetres);
                light[i] = (byte)Mathf.RoundToInt(255 * daylight);
                if (++work % 512 == 0) yield return null;
            }
        }

        private static bool Clear(Vector3 p, Vector3 delta, Func<Vector3, bool> isOpen)
        {
            // Quarter-cell samples reject thin earth partitions between nodes.
            for (int s = 1; s <= 4; s++) if (!isOpen(p + delta * (s * 0.25f))) return false;
            return true;
        }

        private void Visit(int i, int cost)
        {
            if (!air[i] || cost > MaximumDistance || cost >= distance[i]) return;
            distance[i] = (byte)cost;
            buckets[cost].Add(i);
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
