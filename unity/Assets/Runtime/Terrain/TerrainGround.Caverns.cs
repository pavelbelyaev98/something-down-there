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
        // A cavern (115, user 2026-10-06: "areas where the player can walk, like caves at some places"): a sealed chamber
        // the player breaks into like a geode and walks around in, never a passage. A short chain of overlapping
        // ellipsoids along a spine (Centres, Radii; grid-local metres) smoothly joined and warped, its floor flattened
        // at Floor (grid-local y, rolling a little) and its roof a dome, a few stone pillars left standing (Pillars: x,
        // z, radius), all in a shell of the geode's hard stone Shell thick, so the same tell says "a hollow is in
        // there". Crystal: the deep crystal cavern, dressed and lit from the Crystal Caverns demo (CavernScenery).
        public struct Cavern
        {
            public FixedList128Bytes<float3> Centres, Radii, Pillars;
            public float3 Min, Max, Seed;
            public float Floor, Shell;
            public bool Crystal;
            // The deepest point of its floor below the surface, for depth bands.
            public float Depth(float extentY) => extentY - Floor;
        }

        // One cavern per zone, the zone-4 one the crystal cavern: depth bands (metres below the surface) for the floor.
        public static readonly Vector2[] CavernDepths = { new Vector2(20, 32), new Vector2(46, 70), new Vector2(84, 106), new Vector2(126, 144) };
        // How softly chambers join, the warp and lumps of the walls, the floor's roll, and slack on the reach.
        public const float CavernBlend = 1f, CavernWarp = .35f, CavernLumps = .07f, CavernFloorRoll = .15f, CavernSlack = .2f;
        // A cavern's near side reaches CavernInset under the plot's edge, so tunnels along the edge meet its stone; the
        // rest lies out under the permanent ground. Clearance from pits and uniques' spaces, and from the grid's walls.
        private const float CavernInset = 2f, CavernClearance = 1.5f, CavernWallMargin = 1f;

        public static Cavern[] Caverns(Vector3Int size, float cellSize, int seed, Pit[] pits, OddSpot[] spots = null, Features features = Features.All)
        {
            if (!Has(features, Features.Caverns)) return Array.Empty<Cavern>();
            var extent = (Vector3)size * cellSize;
            if (extent != SiteLayout.Extent) return Array.Empty<Cavern>();
            uint state = unchecked((uint)seed * 1597334677u ^ 0x3c6ef372u);
            float Next() => TerrainMaterialSnapshot.NextUnit(ref state);
            float Range(float a, float b) => a + (b - a) * Next();
            var corner = new float2(SiteLayout.Origin.x, SiteLayout.Origin.z);
            var caverns = new List<Cavern>();
            // East and west by turns, so the caverns are spread round the plot.
            float firstSide = Next() < .5f ? -1 : 1;
            for (int zone = 0; zone < CavernDepths.Length; zone++)
            {
                var band = CavernDepths[zone];
                if (band.y > extent.y - 2) continue;
                bool crystal = zone == CavernDepths.Length - 1;
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    float side = zone % 2 == 0 ? firstSide : -firstSide, floorDepth = Range(band.x, band.y);
                    int count = crystal ? 6 : Next() < .5f ? 4 : 5;
                    float spacing = Range(2.6f, 3.1f), length = (count - 1) * spacing;
                    float middle = Range(-extent.z * .5f + length * .5f + 4, extent.z * .5f - length * .5f - 4);
                    var cavern = new Cavern { Crystal = crystal, Shell = Range(.6f, .8f), Seed = new float3(Range(0, 500), Range(0, 500), Range(0, 500)) };
                    float floor = extent.y - floorDepth;
                    // The spine runs along the plot's east or west edge, following its curve.
                    for (int n = 0; n < count; n++)
                    {
                        float z = middle + (n - (count - 1) * .5f) * spacing + Range(-.3f, .3f);
                        var radii = crystal ? new float3(Range(2.8f, 3.5f), Range(2.3f, 2.9f), Range(2.2f, 2.7f))
                            : new float3(Range(2.1f, 2.8f), Range(1.6f, 2.1f), Range(1.9f, 2.4f));
                        float edge = EdgeAlong(side, z);
                        float x = side * (edge + radii.x - CavernInset) + Range(-.4f, .4f);
                        float y = floor + radii.y * .45f + Range(0, crystal ? .7f : .5f);
                        var site = new float2(x, z);
                        cavern.Centres.Add(new float3(site.x - corner.x, y, site.y - corner.y));
                        cavern.Radii.Add(radii);
                    }
                    cavern.Floor = floor;
                    int pillars = crystal ? 3 : Next() < .5f ? 1 : 0;
                    for (int n = 0; n < pillars; n++)
                    {
                        int at = 1 + (int)(Next() * (count - 2));
                        var c = cavern.Centres[at]; var r = cavern.Radii[at];
                        cavern.Pillars.Add(new float3(c.x + Range(-.55f, .55f) * r.x, c.z + Range(-.4f, .4f) * r.z, crystal ? Range(.45f, .75f) : Range(.35f, .55f)));
                    }
                    Bound(ref cavern);
                    if (cavern.Min.x < CavernWallMargin || cavern.Max.x > extent.x - CavernWallMargin
                        || cavern.Min.z < CavernWallMargin || cavern.Max.z > extent.z - CavernWallMargin
                        || cavern.Min.y < 1 || extent.y - cavern.Max.y < SurfaceSoil + 2) continue;
                    bool clear = true;
                    foreach (var pit in pits) clear &= math.any(cavern.Min > pit.Max + CavernClearance) || math.any(pit.Min > cavern.Max + CavernClearance);
                    if (spots != null) foreach (var spot in spots) clear &= math.any(cavern.Min > spot.Max + CavernClearance) || math.any(spot.Min > cavern.Max + CavernClearance);
                    foreach (var other in caverns) clear &= math.any(cavern.Min > other.Max + CavernClearance) || math.any(other.Min > cavern.Max + CavernClearance);
                    if (!clear) continue;
                    caverns.Add(cavern);
                    break;
                }
            }
            return caverns.ToArray();
        }

        // The plot's edge east (side 1) or west (-1) of the centre at site-local z: metres out along x.
        private static float EdgeAlong(float side, float z)
        {
            float x = 0;
            while (x < SiteLayout.Extent.x * .5f && SiteLayout.BeyondOpening(new Vector2(side * x, z)) < 0) x += .25f;
            return x;
        }

        // A cavern at hand (the Ground Lab's): chambers along a spine, its floor, pillars and shell.
        public static Cavern MakeCavern(float3[] centres, float3[] radii, float floor, float3[] pillars, float shell, bool crystal, float3 seed)
        {
            var cavern = new Cavern { Floor = floor, Shell = shell, Crystal = crystal, Seed = seed };
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

        // Where a cavern's air stands at a chamber, a little above the floor: rays for seats and scenery start here.
        public static float3 CavernHeart(in Cavern cavern, int chamber)
        {
            var c = cavern.Centres[chamber];
            return new float3(c.x, math.max(c.y, cavern.Floor + 1.1f), c.z);
        }

        // A crystal cavern chamber's grove (user, 2026-10-06, after the Crystal Caverns demo: one colour to a grove, its
        // crystals crowded round one spot by a wall, never scattered): GroveColumns tall columns standing by the wall,
        // GroveSprays sprays on the wall beside them pointing into the air, and GroveShards small crystals half-buried in
        // the floor round them. Columns and sprays are scenery the player breaks (CavernCrystal), each sealing
        // GroveColumnPieces or GroveSprayPieces finds that fall out when it breaks; shards are finds from the start. Glow:
        // one colour to GroveGlowRun chambers in turn, so the cavern goes blue, then green, then red, as the demo's areas
        // do; one colour a chamber read as random where neighbours met. Seeded from the cavern, so scenery and find seats
        // agree.
        public enum GroveKind : byte { Column, Spray, Shard }
        public struct GroveCrystal
        {
            // Grid-local: where it stands, the way it points and how far (metres), its turn about that way.
            public float3 Foot, Up;
            public float Size, Turn;
            public GroveKind Kind;
            public int Glow, Variant;
            public float3 Centre => Foot + Up * (Size * .5f);
            public int Pieces => Kind == GroveKind.Column ? GroveColumnPieces : Kind == GroveKind.Spray ? GroveSprayPieces : 1;
        }
        public const int GroveColumns = 3, GroveSprays = 5, GroveShards = 5, GroveColumnPieces = 3, GroveSprayPieces = 1, GroveGlows = 3, GroveGlowRun = 2;
        private const int GroveTries = 10;

        public static List<GroveCrystal> Grove(Cavern cavern, int chamber)
        {
            var grove = new List<GroveCrystal>();
            uint state = math.hash(new float4(cavern.Seed, chamber)) | 1u;
            float Next() => TerrainMaterialSnapshot.NextUnit(ref state);
            float Range(float a, float b) => a + (b - a) * Next();
            int glow = chamber / GroveGlowRun % GroveGlows;
            var heart = CavernHeart(cavern, chamber);
            // The grove's wall: where a seeded level ray from the chamber's heart meets the stone.
            float bearing = Next() * 2 * math.PI;
            var level = new float3(math.cos(bearing), 0, math.sin(bearing));
            var (wall, wallOut) = CavernFace(cavern, heart, level);
            var inward = math.normalizesafe(new float3(-wallOut.x, 0, -wallOut.z), -level);
            var side = math.cross(new float3(0, 1, 0), inward);
            float3 FloorAt(float3 near)
            {
                var start = new float3(near.x, heart.y, near.z);
                if (CavernHollow(cavern, start) > -.15f) return float3.zero;
                return CavernFace(cavern, start, new float3(0, -1, 0)).surface;
            }
            float Room(float3 from, float3 way) => math.distance(from, CavernFace(cavern, from, way).surface);
            bool Clear(float3 at, float gap)
            {
                foreach (var other in grove) if (math.distance(other.Foot, at) < gap) return false;
                return true;
            }
            var centre = FloorAt(wall + inward * 1.2f);
            if (math.all(centre == float3.zero)) return grove;
            for (int n = 0; n < GroveColumns; n++)
                for (int attempt = 0; attempt < GroveTries; attempt++)
                {
                    var foot = FloorAt(centre + side * ((n - 1) * 1f + Range(-.3f, .3f)) + inward * Range(-.4f, .3f));
                    if (math.all(foot == float3.zero) || !Clear(foot, .8f)) continue;
                    var up = math.normalize(new float3(0, 1, 0) + inward * Range(.05f, .35f) + side * Range(-.2f, .2f));
                    float size = math.min(Range(1.8f, 2.7f), Room(foot + up * .2f, up) * .85f);
                    if (size < 1f) continue;
                    grove.Add(new GroveCrystal { Foot = foot, Up = up, Size = size, Turn = Range(0, 360), Kind = GroveKind.Column, Glow = glow, Variant = (int)(Next() * 1000) });
                    break;
                }
            for (int n = 0; n < GroveSprays; n++)
                for (int attempt = 0; attempt < GroveTries; attempt++)
                {
                    float turn = bearing + Range(-.6f, .6f), rise = math.radians(Range(5f, 50f));
                    var way = new float3(math.cos(turn) * math.cos(rise), math.sin(rise), math.sin(turn) * math.cos(rise));
                    var (foot, outward) = CavernFace(cavern, heart, way);
                    if (!Clear(foot, .6f)) continue;
                    var up = math.normalize(-outward + new float3(0, .35f, 0));
                    float size = math.min(Range(.8f, 1.3f), Room(foot + up * .15f, up) * .8f);
                    if (size < .5f) continue;
                    grove.Add(new GroveCrystal { Foot = foot, Up = up, Size = size, Turn = Range(0, 360), Kind = GroveKind.Spray, Glow = glow, Variant = (int)(Next() * 1000) });
                    break;
                }
            for (int n = 0; n < GroveShards; n++)
                for (int attempt = 0; attempt < GroveTries; attempt++)
                {
                    float angle = Range(0, 2 * math.PI), reach = Range(.5f, 1.6f);
                    var foot = FloorAt(centre + new float3(math.cos(angle), 0, math.sin(angle)) * reach);
                    if (math.all(foot == float3.zero) || !Clear(foot, .45f)) continue;
                    var up = math.normalize(new float3(Range(-.3f, .3f), 1, Range(-.3f, .3f)));
                    grove.Add(new GroveCrystal { Foot = foot, Up = up, Size = .35f, Turn = Range(0, 360), Kind = GroveKind.Shard, Glow = glow, Variant = (int)(Next() * 1000) });
                    break;
                }
            return grove;
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
