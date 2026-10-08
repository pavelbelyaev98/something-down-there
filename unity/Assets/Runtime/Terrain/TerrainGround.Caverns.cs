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
        // A cave (115, 116): a sealed hollow the player breaks into and walks around in, never a passage, in a shell of cave
        // rock (the Crystal Caverns demo's stone). Two kinds:
        // - a great cave, one a zone (user, 2026-10-07: "one cave that is actually very wide, like almost a whole level
        //   wide, with plenty of stuff ... like in the demo"; then "a bit smaller"): chambers on a jittered grid across the
        //   middle of the underground joined into one hall, with pillars and stalagmites of cave rock; crystals of one kind
        //   (user: "each cave MUST have only one colour") and its zone's crystal trophy (DiscoveryCatalog.SeatCaves,
        //   SeatTrophies);
        // - a mini cave, a few a zone (user: "small ones with just some pickables here and there"): one small chamber and
        //   a handful of crystals of its depth's kind.
        // Centres, Radii: the chambers (grid-local metres), smoothly joined and warped; Floor (grid-local y) flattens their
        // floor, rolling a little; the roof is their domes.
        public struct Cavern
        {
            public FixedList512Bytes<float3> Centres, Radii;
            // Rock left standing in the air: pillars (x, z, radius; floor to roof, wider at the foot) and stalagmites (x, z,
            // foot radius, height). (Arches went: a half ring on the floor read as a croissant, user 2026-10-07.)
            public FixedList128Bytes<float3> Pillars;
            public FixedList512Bytes<float4> Stalagmites;
            // Basins sunk into the floor (x, z, radius, depth), each holding a puddle (CavernScenery; user, 2026-10-08: "make
            // the puddles deeper"): a smooth bowl, deepest in its middle.
            public FixedList128Bytes<float4> Basins;
            public float3 Min, Max, Seed;
            public float Floor, Shell;
            public bool Great;
            // The deepest point of its floor below the surface, for depth bands.
            public float Depth(float extentY) => extentY - Floor;
        }

        // MiniCavesPerZone mini caves in each zone, spread down it in turn, the first CaveTop or more below the surface, all
        // under the dig plot so digging down meets them.
        public static readonly int[] MiniCavesPerZone = { 3, 3, 3, 3 };
        // A great cave's basins, and the share of mini caves with one; their radii and depths.
        public const int GreatBasins = 5;
        public const float MiniBasinShare = .5f;
        private const float BasinRadiusLeast = 1f, BasinRadiusMost = 1.6f, BasinDepthLeast = .35f, BasinDepthMost = .55f, BasinMargin = .4f;
        public const float CaveTop = 7.5f;
        // How softly chambers join, the warp and lumps of the walls, the floor's roll, and slack on the reach: a mini cave's,
        // then a great cave's (broader, slower folds).
        public const float CavernBlend = 1f, CavernWarp = .3f, CavernLumps = .06f, CavernFloorRoll = .12f, CavernSlack = .2f;
        public const float GreatBlend = 2f, GreatWarp = .8f, GreatFloorRoll = .35f;
        // A great cave: GreatColumns x GreatRows chambers GreatSpacing apart across the middle of the underground, GreatDrops
        // left out for bays; its floor GreatFloorShare of the way down its zone, never above GreatTopFloor (below the first
        // zone's uniques).
        private const int GreatColumns = 5, GreatRows = 3, GreatDrops = 3, GreatMinChambers = 8;
        private const float GreatSpacing = 6f, GreatFloorShare = .62f, GreatTopFloor = 31f;
        // Clearance from pits, uniques' spaces and each other, and from the grid's walls.
        private const float CavernClearance = 1.5f, CavernWallMargin = 1f;

        // Seeded draws for the generators.
        private sealed class Draws
        {
            private uint state;
            public Draws(uint state) => this.state = state;
            public float Next() => TerrainMaterialSnapshot.NextUnit(ref state);
            public float Range(float a, float b) => a + (b - a) * Next();
            public int Below(int count) => Mathf.Min(count - 1, (int)(Next() * count));
        }

        // Pits and uniques' spaces block a cave (with CavernClearance), and so do the geodes for a mini cave.
        private static Func<float3, float3, bool> Blocker(Pit[] pits, OddSpot[] spots, Geode[] geodes = null) => (min, max) =>
        {
            foreach (var pit in pits) if (!(math.any(min > pit.Max + CavernClearance) || math.any(pit.Min > max + CavernClearance))) return true;
            if (spots != null) foreach (var spot in spots) if (!(math.any(min > spot.Max + CavernClearance) || math.any(spot.Min > max + CavernClearance))) return true;
            if (geodes != null) foreach (var geode in geodes) if (!(math.any(min > geode.Max + CavernClearance) || math.any(geode.Min > max + CavernClearance))) return true;
            return false;
        };

        // A great cave in each zone (generated first: they need the room).
        public static Cavern[] GreatCaves(Vector3Int size, float cellSize, int seed, Pit[] pits, OddSpot[] spots = null, Features features = Features.All)
        {
            if (!Has(features, Features.Caverns)) return Array.Empty<Cavern>();
            var extent = (Vector3)size * cellSize;
            if (extent != SiteLayout.Extent) return Array.Empty<Cavern>();
            var footprint = SiteLayout.FindFootprint(extent);
            var draws = new Draws(unchecked((uint)seed * 1597334677u ^ 0x3c6ef372u));
            var caverns = new List<Cavern>();
            var blocked = Blocker(pits, spots);
            for (int zone = 0; zone <= ZoneBorders.Length; zone++)
            {
                float zoneTop = zone == 0 ? 0 : ZoneBorders[zone - 1], zoneBottom = zone < ZoneBorders.Length ? ZoneBorders[zone] : extent.y;
                float depth = Mathf.Max(GreatTopFloor, Mathf.Lerp(zoneTop, zoneBottom, GreatFloorShare)) + draws.Range(-1.5f, 1.5f);
                for (int attempt = 0; attempt < 12; attempt++)
                    if (TryGreatCave(extent, extent.y - depth, draws, blocked, footprint, out var cave)) { caverns.Add(cave); break; }
            }
            return caverns.ToArray();
        }

        // The mini caves, clear of the great caves and the geodes (placed after both: a small chamber fits almost anywhere).
        public static Cavern[] MiniCaves(Vector3Int size, float cellSize, int seed, Pit[] pits, OddSpot[] spots, Features features,
            Cavern[] great, Geode[] geodes)
        {
            if (!Has(features, Features.Caverns)) return Array.Empty<Cavern>();
            var extent = (Vector3)size * cellSize;
            if (extent != SiteLayout.Extent) return Array.Empty<Cavern>();
            var footprint = SiteLayout.FindFootprint(extent);
            var draws = new Draws(unchecked((uint)seed * 2654435761u ^ 0x5bd1e995u));
            var caverns = new List<Cavern>(great);
            var blocked = Blocker(pits, spots, geodes);
            for (int zone = 0; zone < MiniCavesPerZone.Length; zone++)
            {
                float zoneTop = Mathf.Max(CaveTop, zone == 0 ? 0 : ZoneBorders[zone - 1] + 1.5f);
                float zoneBottom = (zone < ZoneBorders.Length ? ZoneBorders[zone] : extent.y - 3) - 1.5f;
                // The zone's depths outside its great cave's band (with room for a mini cave's own height and clearance).
                float bandTop = zoneBottom, bandBottom = zoneBottom;
                foreach (var hall in great)
                {
                    float floorDepth = extent.y - hall.Floor;
                    if (floorDepth < zoneTop || floorDepth > zoneBottom + 1.5f) continue;
                    bandTop = Mathf.Max(zoneTop, extent.y - hall.Max.y - CavernClearance);
                    bandBottom = Mathf.Min(zoneBottom, extent.y - hall.Min.y + CavernClearance + 4);
                }
                float free = (bandTop - zoneTop) + (zoneBottom - bandBottom);
                float Depth(float u) => u < bandTop - zoneTop ? zoneTop + u : bandBottom + (u - (bandTop - zoneTop));
                int count = MiniCavesPerZone[zone];
                for (int n = 0; n < count; n++)
                {
                    // Each its own slice of the zone's free depths, so they are spread down it.
                    float top = free * n / count, bottom = free * (n + 1) / count;
                    for (int attempt = 0; attempt < 200; attempt++)
                    {
                        var radii = new float3(draws.Range(1.6f, 2f), draws.Range(1.3f, 1.6f), draws.Range(1.5f, 1.9f));
                        float floor = extent.y - Depth(draws.Range(top, bottom));
                        var centre = new float3(draws.Range(4, extent.x - 4), floor + radii.y * .45f + draws.Range(0, .3f), draws.Range(4, extent.z - 4));
                        var cavern = new Cavern { Shell = draws.Range(.5f, .7f), Seed = new float3(draws.Range(0, 500), draws.Range(0, 500), draws.Range(0, 500)), Floor = floor };
                        cavern.Centres.Add(centre); cavern.Radii.Add(radii);
                        if (draws.Next() < MiniBasinShare)
                            cavern.Basins.Add(new float4(centre.x + draws.Range(-.4f, .4f), centre.z + draws.Range(-.4f, .4f),
                                draws.Range(.75f, .95f), draws.Range(.3f, .4f)));
                        if (footprint != null && !Inside(footprint, centre, math.cmax(radii) + cavern.Shell + .5f)) continue;
                        Bound(ref cavern);
                        if (OutOfGrid(cavern, extent) || blocked(cavern.Min, cavern.Max)) continue;
                        bool clear = true;
                        foreach (var other in caverns) clear &= math.any(cavern.Min > other.Max + CavernClearance) || math.any(other.Min > cavern.Max + CavernClearance);
                        if (!clear) continue;
                        caverns.Add(cavern);
                        break;
                    }
                }
            }
            return caverns.GetRange(great.Length, caverns.Count - great.Length).ToArray();
        }

        private static bool OutOfGrid(in Cavern cavern, Vector3 extent)
            => cavern.Min.x < CavernWallMargin || cavern.Max.x > extent.x - CavernWallMargin
               || cavern.Min.z < CavernWallMargin || cavern.Max.z > extent.z - CavernWallMargin
               || cavern.Min.y < 1 || extent.y - cavern.Max.y < SurfaceSoil + 1.5f;

        // A great cave with its floor at `floor` (grid-local y) across the grid's width: chambers on a jittered grid, those
        // the blocker refuses (pits, uniques' spaces) and GreatDrops at random left out, then only the largest joined group
        // kept; then its pillars, arches and stalagmites where its hall has room. Fails with fewer than GreatMinChambers, or
        // fewer than three under the dig plot (footprint) to dig down into.
        public static bool TryGreatCave(Vector3 extent, float floor, int seed, Func<float3, float3, bool> blocked, Func<Vector2, bool> footprint,
            out Cavern cave)
            => TryGreatCave(extent, floor, new Draws(unchecked((uint)seed * 2246822519u ^ 0x27d4eb2fu)), blocked, footprint, out cave);

        private static bool TryGreatCave(Vector3 extent, float floor, Draws draws, Func<float3, float3, bool> blocked, Func<Vector2, bool> footprint,
            out Cavern cave)
        {
            cave = new Cavern { Great = true, Floor = floor, Shell = draws.Range(.6f, .8f),
                Seed = new float3(draws.Range(0, 500), draws.Range(0, 500), draws.Range(0, 500)) };
            float pad = cave.Shell + GreatWarp + GeodeOuterLumps + GreatBlend * .25f + CavernSlack;
            var centres = new float3[GreatColumns, GreatRows];
            var radii = new float3[GreatColumns, GreatRows];
            var kept = new bool[GreatColumns, GreatRows];
            float x0 = extent.x * .5f - GreatSpacing * (GreatColumns - 1) * .5f, x1 = extent.x * .5f + GreatSpacing * (GreatColumns - 1) * .5f;
            float z0 = 7.5f, z1 = extent.z - 7.5f;
            for (int c = 0; c < GreatColumns; c++)
            for (int r = 0; r < GreatRows; r++)
            {
                var radius = new float3(draws.Range(3.4f, 3.9f), draws.Range(3f, 3.8f), draws.Range(3.2f, 3.6f));
                var centre = new float3(Mathf.Lerp(x0, x1, c / (GreatColumns - 1f)) + draws.Range(-.8f, .8f), 0,
                    Mathf.Lerp(z0, z1, r / (GreatRows - 1f)) + draws.Range(-.6f, .6f));
                centre.y = floor + radius.y * .5f + draws.Range(0, .6f);
                centres[c, r] = centre; radii[c, r] = radius;
                var min = centre - radius - pad; var max = centre + radius + pad;
                min.y = math.max(min.y, floor - GreatFloorRoll - pad);
                kept[c, r] = !blocked(min, max) && min.x >= CavernWallMargin && max.x <= extent.x - CavernWallMargin
                    && min.z >= CavernWallMargin && max.z <= extent.z - CavernWallMargin && min.y >= 1 && extent.y - max.y >= SurfaceSoil + 1.5f;
            }
            for (int d = 0; d < GreatDrops; d++) kept[draws.Below(GreatColumns), draws.Below(GreatRows)] = false;
            // Only the largest group of chambers joined side by side: one hall, never two.
            var group = new int[GreatColumns, GreatRows];
            int groups = 0, best = 0, bestSize = 0;
            for (int c = 0; c < GreatColumns; c++)
            for (int r = 0; r < GreatRows; r++)
            {
                if (!kept[c, r] || group[c, r] != 0) continue;
                int id = ++groups, count = 0;
                var open = new Stack<(int c, int r)>(); open.Push((c, r)); group[c, r] = id;
                while (open.Count > 0)
                {
                    var (oc, or) = open.Pop(); count++;
                    foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nc = oc + dc, nr = or + dr;
                        if (nc < 0 || nr < 0 || nc >= GreatColumns || nr >= GreatRows || !kept[nc, nr] || group[nc, nr] != 0) continue;
                        group[nc, nr] = id; open.Push((nc, nr));
                    }
                }
                if (count > bestSize) { bestSize = count; best = id; }
            }
            if (bestSize < GreatMinChambers) return false;
            int underPlot = 0;
            for (int c = 0; c < GreatColumns; c++)
            for (int r = 0; r < GreatRows; r++)
            {
                if (group[c, r] != best) continue;
                cave.Centres.Add(centres[c, r]); cave.Radii.Add(radii[c, r]);
                if (footprint == null || footprint(new Vector2(centres[c, r].x, centres[c, r].z))) underPlot++;
            }
            if (underPlot < 3) return false;
            Bound(ref cave);
            Furnish(ref cave, draws);
            return true;
        }

        // A great cave's rock left standing, each where its hall has room: pillars, then stalagmites, clear of each other.
        private static void Furnish(ref Cavern cave, Draws draws)
        {
            var standing = new List<(float2 at, float reach)>();
            bool Clear(float2 at, float reach)
            {
                foreach (var (other, otherReach) in standing) if (math.distance(at, other) < reach + otherReach + 1.4f) return false;
                return true;
            }
            float2 Somewhere(in Cavern c, float spread)
            {
                int chamber = draws.Below(c.Centres.Length);
                var centre = c.Centres[chamber]; var radius = c.Radii[chamber];
                float angle = draws.Range(0, 2 * math.PI), distance = math.sqrt(draws.Next()) * spread;
                return new float2(centre.x + math.cos(angle) * radius.x * distance, centre.z + math.sin(angle) * radius.z * distance);
            }
            // Room at a height over the floor: the walls (and roof) at least margin away there.
            bool Open(in Cavern c, float2 at, float height, float margin) => CavernWalls(c, new float3(at.x, c.Floor + height, at.y)) < -margin;
            int pillars = 6 + draws.Below(4);
            for (int attempt = 0; attempt < 80 && cave.Pillars.Length < pillars; attempt++)
            {
                var at = Somewhere(cave, .6f); float radius = draws.Range(.45f, .9f);
                if (!Open(cave, at, 1.2f, radius + 1.2f) || !Clear(at, radius)) continue;
                cave.Pillars.Add(new float3(at, radius)); standing.Add((at, radius));
            }
            int stalagmites = 6 + draws.Below(5);
            for (int attempt = 0; attempt < 120 && cave.Stalagmites.Length < stalagmites; attempt++)
            {
                var at = Somewhere(cave, .85f); float foot = draws.Range(.25f, .55f), height = draws.Range(.7f, 2f);
                if (!Open(cave, at, height + 1f, .3f) || !Open(cave, at, .4f, foot + .3f) || !Clear(at, foot)) continue;
                cave.Stalagmites.Add(new float4(at, foot, height)); standing.Add((at, foot));
            }
            // Basins in the floor's open stretches: the bowl's middle well inside the hall (a bowl may meet a wall) and the
            // bowl clear of the standing rock by BasinMargin (the standing rock's own wide spacing left no room for one in a
            // crowded hall).
            for (int attempt = 0; attempt < 300 && cave.Basins.Length < GreatBasins; attempt++)
            {
                var at = Somewhere(cave, .8f); float radius = draws.Range(BasinRadiusLeast, BasinRadiusMost);
                if (!Open(cave, at, .4f, radius * .5f + BasinMargin)) continue;
                bool clear = true;
                foreach (var (other, otherReach) in standing) clear &= math.distance(at, other) > radius + otherReach + BasinMargin;
                if (!clear) continue;
                cave.Basins.Add(new float4(at, radius, draws.Range(BasinDepthLeast, BasinDepthMost))); standing.Add((at, radius));
            }
        }

        // A cave at hand (the Ground Lab's mini caves): chambers, its floor and shell, and its basins.
        public static Cavern MakeCavern(float3[] centres, float3[] radii, float floor, float shell, float3 seed, float4[] basins = null)
        {
            var cavern = new Cavern { Floor = floor, Shell = shell, Seed = seed };
            for (int i = 0; i < centres.Length; i++) { cavern.Centres.Add(centres[i]); cavern.Radii.Add(radii[i]); }
            if (basins != null) foreach (var basin in basins) cavern.Basins.Add(basin);
            Bound(ref cavern);
            return cavern;
        }

        private static void Bound(ref Cavern cavern)
        {
            float pad = cavern.Shell + (cavern.Great ? GreatWarp + GreatBlend * .25f : CavernWarp + CavernBlend * .25f) + GeodeOuterLumps + CavernSlack;
            var min = new float3(float.MaxValue); var max = new float3(float.MinValue);
            for (int i = 0; i < cavern.Centres.Length; i++) { min = math.min(min, cavern.Centres[i] - cavern.Radii[i]); max = math.max(max, cavern.Centres[i] + cavern.Radii[i]); }
            float deepest = 0;
            for (int i = 0; i < cavern.Basins.Length; i++) deepest = math.max(deepest, cavern.Basins[i].w);
            min.y = math.max(min.y, cavern.Floor - (cavern.Great ? GreatFloorRoll : CavernFloorRoll) - deepest);
            cavern.Min = min - pad; cavern.Max = max + pad;
        }

        // The chambers joined and warped, solid below the floor: the hollow's shape before its standing rock and lumps,
        // which the shell follows.
        private static float CavernShape(in Cavern cavern, float3 p)
        {
            float d = CavernWalls(cavern, p);
            float floor = cavern.Floor + (cavern.Great ? GreatFloorRoll * noise.snoise(new float3(p.x, 0, p.z) * .15f + cavern.Seed.zxy)
                : CavernFloorRoll * noise.snoise(new float3(p.x, 0, p.z) * .4f + cavern.Seed.zxy));
            return math.max(d, floor - BasinDip(cavern, p.xz) - p.y);
        }

        // How far the floor sinks into a basin at a grid-local x/z: a smooth bowl, its full depth in its middle.
        public static float BasinDip(in Cavern cavern, float2 xz)
        {
            float dip = 0;
            for (int i = 0; i < cavern.Basins.Length; i++)
            {
                var basin = cavern.Basins[i];
                float t = math.lengthsq(xz - basin.xy) / (basin.z * basin.z);
                if (t < 1) dip = math.max(dip, basin.w * (1 - t) * (1 - t));
            }
            return dip;
        }

        // The chambers joined and warped, without the floor: their walls and roof.
        private static float CavernWalls(in Cavern cavern, float3 p)
        {
            float blend = cavern.Great ? GreatBlend : CavernBlend;
            float d = Ellipsoid(p - cavern.Centres[0], cavern.Radii[0]);
            for (int i = 1; i < cavern.Centres.Length; i++) d = SmoothMin(d, Ellipsoid(p - cavern.Centres[i], cavern.Radii[i]), blend);
            return d + (cavern.Great ? GreatWarp * noise.snoise(p * .12f + cavern.Seed) + CavernWarp * noise.snoise(p * .35f + cavern.Seed.zxy)
                : CavernWarp * noise.snoise(p * .3f + cavern.Seed));
        }

        // Signed distance to a cavern's air (negative inside) and to its shell's outer face (negative inside the stone
        // or the air). Pillars and stalagmites are stone left standing in the air.
        public static float CavernHollow(in Cavern cavern, float3 p)
        {
            float d = CavernShape(cavern, p), height = p.y - cavern.Floor;
            for (int i = 0; i < cavern.Pillars.Length; i++)
            {
                var pillar = cavern.Pillars[i];
                float foot = math.saturate(1 - height / 1.4f);
                float column = math.length(p.xz - pillar.xy) - pillar.z * (1 + .6f * foot * foot) + .1f * noise.snoise(p * .9f + cavern.Seed);
                d = -SmoothMin(-d, column, .4f);
            }
            for (int i = 0; i < cavern.Stalagmites.Length; i++)
            {
                var stalagmite = cavern.Stalagmites[i];
                float t = math.saturate(height / stalagmite.w);
                float cone = math.max(math.length(p.xz - stalagmite.xy) - stalagmite.z * (1 - t), height - stalagmite.w) * .7f;
                d = -SmoothMin(-d, cone, .25f);
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

        // Where a cave's air stands at a chamber, a little above the floor: rays for seats start here. Where a great cave's
        // standing rock covers that point, the nearest open air round it (seat rays from inside rock would all stop at once).
        public static float3 CavernHeart(in Cavern cavern, int chamber)
        {
            var c = cavern.Centres[chamber];
            var heart = new float3(c.x, math.max(c.y, cavern.Floor + 1.1f), c.z);
            if (cavern.Pillars.Length + cavern.Stalagmites.Length == 0 || CavernHollow(cavern, heart) < -.3f) return heart;
            for (int ring = 1; ring <= 5; ring++)
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * math.PI / 4;
                    var p = heart + new float3(math.cos(angle), 0, math.sin(angle)) * (ring * .6f);
                    if (CavernHollow(cavern, p) < -.3f) return p;
                }
            return heart;
        }

        // The cave's floor and roof above a grid-local x/z (grid-local y), standing rock included: up from below the floor
        // to the first air, then on to the next stone. NaN floor when no air stands there.
        public static (float floor, float roof) CavernSpan(in Cavern cavern, float x, float z)
        {
            float floor = float.NaN;
            for (float y = cavern.Floor - .6f - BasinDip(cavern, new float2(x, z)); y < cavern.Max.y;)
            {
                float d = CavernHollow(cavern, new float3(x, y, z));
                bool air = d < 0;
                if (float.IsNaN(floor)) { if (air) floor = y; }
                else if (!air) return (floor, y);
                y += math.max(.05f, math.abs(d) * FaceStride);
            }
            return (floor, cavern.Max.y);
        }

        // A cavern's stone: everything inside its shell's outer face, its air's samples too, so its walls read as stone. A
        // great cave's is cave rock, the Crystal Caverns demo's; a mini cave's the geodes' shell (user, 2026-10-07: "why
        // did you switch it from the geode's ground type, which was fine?").
        public static void FillCavern(byte[] ids, Vector3Int size, float cellSize, Cavern cavern)
        {
            byte stone = (byte)(cavern.Great ? TerrainMaterialId.CaveRock : TerrainMaterialId.GeodeShell);
            int stride = size.x + 1, plane = stride * (size.y + 1);
            var first = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt((Vector3)cavern.Min / cellSize));
            var last = Vector3Int.Min(size, Vector3Int.CeilToInt((Vector3)cavern.Max / cellSize));
            using var field = CavernField(cavern, cellSize, new int3(first.x, first.y, first.z), new int3(last.x, last.y, last.z), true, Allocator.TempJob);
            int i = 0;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++, i++)
                if (field[i] < 0) ids[x + y * stride + z * plane] = stone;
        }

        // A cavern's signed distances, to its air or to its shell's outer face, at every sample from first to last
        // (inclusive, x fastest), Burst-compiled like a geode's: a great cave's box holds a few million samples.
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

        // Where a ray from `from` along `way` first meets the surface of `distance` within `limit`: marched out by a share of
        // the distance left (FaceStride, never under HollowStep: the distance is only roughly one), then halved to a
        // millimetre or so, with the outward normal from the distance's gradient.
        private const float FaceStride = .6f;
        private static (float3 surface, float3 outward) Face(Func<float3, float> distance, float3 from, float3 way, float limit)
        {
            float inside = 0, outside = limit;
            for (float t = HollowStep; t < limit;)
            {
                float d = distance(from + way * t);
                if (d >= 0) { outside = t; break; }
                inside = t;
                t += math.max(HollowStep, -d * FaceStride);
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
