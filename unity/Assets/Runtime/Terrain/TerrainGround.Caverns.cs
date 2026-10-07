using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SomethingDownThere
{
    public static partial class TerrainGround
    {
        // A cave (115, user 2026-10-07: "caves on much higher levels, many more of those, and only one rock type per
        // cave"): a small sealed grotto the player breaks into and walks around in, never a passage. Two or three
        // overlapping ellipsoids along a seeded heading (Centres, Radii; grid-local metres) smoothly joined and warped, its
        // floor flattened at Floor (grid-local y, rolling a little) and its roof a dome, in a shell of the geode's hard
        // stone Shell thick, so the same tell says "a hollow is in there". Its crystals, one kind to a cave, line its walls
        // and light it (DiscoveryCatalog.SeatCaves, CavernScenery). Pillars: x, z, radius (the site's caves have none).
        public struct Cavern
        {
            public FixedList128Bytes<float3> Centres, Radii, Pillars;
            public float3 Min, Max, Seed;
            public float Floor, Shell;
            // The deepest point of its floor below the surface, for depth bands.
            public float Depth(float extentY) => extentY - Floor;
        }

        // CavesPerZone caves in each zone, spread down it in turn, the first CaveTop or more below the surface, all under
        // the dig plot so digging down meets them.
        public static readonly int[] CavesPerZone = { 3, 3, 3, 3 };
        public const float CaveTop = 7.5f;
        // How softly chambers join, the warp and lumps of the walls, the floor's roll, and slack on the reach.
        public const float CavernBlend = 1f, CavernWarp = .3f, CavernLumps = .06f, CavernFloorRoll = .12f, CavernSlack = .2f;
        // Clearance from pits, uniques' spaces and each other, and from the grid's walls.
        private const float CavernClearance = 1.5f, CavernWallMargin = 1f;

        public static Cavern[] Caverns(Vector3Int size, float cellSize, int seed, Pit[] pits, OddSpot[] spots = null, Features features = Features.All)
        {
            if (!Has(features, Features.Caverns)) return Array.Empty<Cavern>();
            var extent = (Vector3)size * cellSize;
            if (extent != SiteLayout.Extent) return Array.Empty<Cavern>();
            var footprint = SiteLayout.FindFootprint(extent);
            uint state = unchecked((uint)seed * 1597334677u ^ 0x3c6ef372u);
            float Next() => TerrainMaterialSnapshot.NextUnit(ref state);
            float Range(float a, float b) => a + (b - a) * Next();
            var caverns = new List<Cavern>();
            for (int zone = 0; zone < CavesPerZone.Length; zone++)
            {
                float zoneTop = Mathf.Max(CaveTop, zone == 0 ? 0 : ZoneBorders[zone - 1] + 1.5f);
                float zoneBottom = (zone < ZoneBorders.Length ? ZoneBorders[zone] : extent.y - 3) - 1.5f;
                int count = CavesPerZone[zone];
                for (int n = 0; n < count; n++)
                {
                    // Each cave its own slice of the zone, so they are spread down it.
                    float top = Mathf.Lerp(zoneTop, zoneBottom, n / (float)count), bottom = Mathf.Lerp(zoneTop, zoneBottom, (n + 1) / (float)count);
                    for (int attempt = 0; attempt < 200; attempt++)
                    {
                        int chambers = Next() < .5f ? 2 : 3;
                        float spacing = Range(2.2f, 2.8f), heading = Range(0, 2 * math.PI), length = (chambers - 1) * spacing;
                        var along = new float3(math.cos(heading), 0, math.sin(heading));
                        float floor = extent.y - Range(top, bottom);
                        var middle = new float3(Range(length * .5f + 4, extent.x - length * .5f - 4), 0, Range(length * .5f + 4, extent.z - length * .5f - 4));
                        var cavern = new Cavern { Shell = Range(.5f, .7f), Seed = new float3(Range(0, 500), Range(0, 500), Range(0, 500)), Floor = floor };
                        bool inside = true;
                        for (int c = 0; c < chambers; c++)
                        {
                            var radii = new float3(Range(1.9f, 2.5f), Range(1.3f, 1.8f), Range(1.7f, 2.2f));
                            var centre = middle + along * ((c - (chambers - 1) * .5f) * spacing + Range(-.25f, .25f));
                            centre.y = floor + radii.y * .45f + Range(0, .4f);
                            cavern.Centres.Add(centre);
                            cavern.Radii.Add(radii);
                            inside &= footprint == null || Inside(footprint, centre, math.cmax(radii) + cavern.Shell + .5f);
                        }
                        if (!inside) continue;
                        Bound(ref cavern);
                        if (cavern.Min.x < CavernWallMargin || cavern.Max.x > extent.x - CavernWallMargin
                            || cavern.Min.z < CavernWallMargin || cavern.Max.z > extent.z - CavernWallMargin
                            || cavern.Min.y < 1 || extent.y - cavern.Max.y < SurfaceSoil + 1.5f) continue;
                        bool clear = true;
                        foreach (var pit in pits) clear &= math.any(cavern.Min > pit.Max + CavernClearance) || math.any(pit.Min > cavern.Max + CavernClearance);
                        if (spots != null) foreach (var spot in spots) clear &= math.any(cavern.Min > spot.Max + CavernClearance) || math.any(spot.Min > cavern.Max + CavernClearance);
                        foreach (var other in caverns) clear &= math.any(cavern.Min > other.Max + CavernClearance) || math.any(other.Min > cavern.Max + CavernClearance);
                        if (!clear) continue;
                        caverns.Add(cavern);
                        break;
                    }
                }
            }
            return caverns.ToArray();
        }

        // A cave at hand (the Ground Lab's): chambers along a line, its floor, pillars and shell.
        public static Cavern MakeCavern(float3[] centres, float3[] radii, float floor, float3[] pillars, float shell, float3 seed)
        {
            var cavern = new Cavern { Floor = floor, Shell = shell, Seed = seed };
            for (int i = 0; i < centres.Length; i++) { cavern.Centres.Add(centres[i]); cavern.Radii.Add(radii[i]); }
            foreach (var pillar in pillars) cavern.Pillars.Add(pillar);
            Bound(ref cavern);
            return cavern;
        }

        private static void Bound(ref Cavern cavern)
        {
            float pad = cavern.Shell + CavernWarp + GeodeOuterLumps + CavernBlend * .25f + CavernSlack;
            var min = new float3(float.MaxValue); var max = new float3(float.MinValue);
            for (int i = 0; i < cavern.Centres.Length; i++) { min = math.min(min, cavern.Centres[i] - cavern.Radii[i]); max = math.max(max, cavern.Centres[i] + cavern.Radii[i]); }
            min.y = math.max(min.y, cavern.Floor - CavernFloorRoll);
            cavern.Min = min - pad; cavern.Max = max + pad;
        }

        // The chambers joined and warped, solid below the floor: the hollow's shape before pillars and lumps, which the
        // shell follows.
        private static float CavernShape(in Cavern cavern, float3 p)
        {
            float d = Ellipsoid(p - cavern.Centres[0], cavern.Radii[0]);
            for (int i = 1; i < cavern.Centres.Length; i++) d = SmoothMin(d, Ellipsoid(p - cavern.Centres[i], cavern.Radii[i]), CavernBlend);
            d += CavernWarp * noise.snoise(p * .3f + cavern.Seed);
            float floor = cavern.Floor + CavernFloorRoll * noise.snoise(new float3(p.x, 0, p.z) * .4f + cavern.Seed.zxy);
            return math.max(d, floor - p.y);
        }

        // Signed distance to a cavern's air (negative inside) and to its shell's outer face (negative inside the stone
        // or the air). Pillars are stone left standing: wider at the foot.
        public static float CavernHollow(in Cavern cavern, float3 p)
        {
            float d = CavernShape(cavern, p);
            for (int i = 0; i < cavern.Pillars.Length; i++)
            {
                var pillar = cavern.Pillars[i];
                float foot = math.saturate(1 - (p.y - cavern.Floor) / 1.4f);
                float column = math.length(p.xz - pillar.xy) - pillar.z * (1 + .6f * foot * foot);
                d = -SmoothMin(-d, column, .4f);
            }
            return d + CavernLumps * noise.snoise(p * 1.6f + cavern.Seed.yzx);
        }

        public static float CavernOuter(in Cavern cavern, float3 p)
            => CavernShape(cavern, p) - cavern.Shell + GeodeOuterLumps * noise.snoise(p * .9f + cavern.Seed * .37f);

        // Where the ray from a point in a cavern's air along a direction first meets its hollow's face, and the face's
        // outward normal there (grid-local).
        public static (float3 surface, float3 outward) CavernFace(Cavern cavern, float3 from, float3 direction)
        {
            float limit = math.cmax(cavern.Max - cavern.Min);
            return Face(p => CavernHollow(cavern, p), from, math.normalizesafe(direction, new float3(0, -1, 0)), limit);
        }

        // Where a cave's air stands at a chamber, a little above the floor: rays for seats and its light start here.
        public static float3 CavernHeart(in Cavern cavern, int chamber)
        {
            var c = cavern.Centres[chamber];
            return new float3(c.x, math.max(c.y, cavern.Floor + 1.1f), c.z);
        }

        // A cavern's stone: everything inside its shell's outer face, its air's samples too, so its walls read as stone.
        public static void FillCavern(byte[] ids, Vector3Int size, float cellSize, Cavern cavern)
        {
            int stride = size.x + 1, plane = stride * (size.y + 1);
            var first = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt((Vector3)cavern.Min / cellSize));
            var last = Vector3Int.Min(size, Vector3Int.CeilToInt((Vector3)cavern.Max / cellSize));
            using var field = CavernField(cavern, cellSize, new int3(first.x, first.y, first.z), new int3(last.x, last.y, last.z), true, Allocator.TempJob);
            int i = 0;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++, i++)
                if (field[i] < 0) ids[x + y * stride + z * plane] = (byte)TerrainMaterialId.GeodeShell;
        }

        // A cavern's signed distances, to its air or to its shell's outer face, at every sample from first to last
        // (inclusive, x fastest), Burst-compiled like a geode's: a cavern's box holds a few million samples.
        public static NativeArray<float> CavernField(Cavern cavern, float cellSize, int3 first, int3 last, bool outer, Allocator allocator)
        {
            var count = math.max(last - first + 1, 0);
            var field = new NativeArray<float>(count.x * count.y * count.z, allocator, NativeArrayOptions.UninitializedMemory);
            new CavernJob { Cavern = cavern, CellSize = cellSize, First = first, Count = count, Outer = outer, Output = field }
                .Schedule(count.z, 1).Complete();
            return field;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
        private struct CavernJob : IJobParallelFor
        {
            public Cavern Cavern;
            public float CellSize;
            public int3 First, Count;
            public bool Outer;
            [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<float> Output;

            public void Execute(int z)
            {
                int i = z * Count.x * Count.y;
                for (int y = 0; y < Count.y; y++)
                for (int x = 0; x < Count.x; x++, i++)
                {
                    var p = (float3)(First + new int3(x, y, z)) * CellSize;
                    Output[i] = Outer ? CavernOuter(Cavern, p) : CavernHollow(Cavern, p);
                }
            }
        }

        // Where a ray from `from` along `way` first meets the surface of `distance` within `limit`: marched out
        // HollowStep at a time, then halved to a millimetre or so, with the outward normal from the distance's gradient.
        private static (float3 surface, float3 outward) Face(Func<float3, float> distance, float3 from, float3 way, float limit)
        {
            float inside = 0, outside = limit;
            for (float t = HollowStep; t < limit; t += HollowStep)
            {
                if (distance(from + way * t) >= 0) { outside = t; break; }
                inside = t;
            }
            for (int i = 0; i < 6; i++)
            {
                float middle = (inside + outside) * .5f;
                if (distance(from + way * middle) >= 0) outside = middle; else inside = middle;
            }
            var surface = from + way * outside;
            const float h = .03f;
            var gradient = new float3(
                distance(surface + new float3(h, 0, 0)) - distance(surface - new float3(h, 0, 0)),
                distance(surface + new float3(0, h, 0)) - distance(surface - new float3(0, h, 0)),
                distance(surface + new float3(0, 0, h)) - distance(surface - new float3(0, 0, h)));
            return (surface, math.normalizesafe(gradient, way));
        }
    }
}
