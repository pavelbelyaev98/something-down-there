using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SomethingDownThere
{
    // The seeded ground: soil, with backfill pits someone dug and refilled in the recent fill and a few geodes deeper
    // down. Written once per session into the one-byte material field; saves store the bytes, so nothing here is saved
    // separately.
    public static class TerrainGround
    {
        // Metres below the surface: even quarters of the 150 m site. Absolute depths, so a
        // shallow grid (fixtures, tests) is all zone 1.
        public static readonly float[] ZoneBorders = { 37.5f, 75f, 112.5f };
        // The first metre stays soil (first scrapes, the permanent bank). Pits start PitTop down and end
        // PitMargin above the first zone border.
        public const float SurfaceSoil = 1.1f, PitTop = 3f, PitMargin = 3f;

        // What the generator lays down. The site admits grounds one at a time (SiteLayout.Ground).
        [Flags] public enum Features { None = 0, Pits = 1, Geodes = 2, All = Pits | Geodes }
        private static bool Has(Features features, Features feature) => (features & feature) != 0;

        // Disturbed ground (099, 106): a column of backfill someone dug and refilled, from Top down to the old chest
        // they buried at Bottom. Mostly steep, some leaning sideways. Backfill holds only chests (user, 2026-10-06):
        // rubbish such as old TVs lies loose in the soil like any find.
        public struct Pit
        {
            public float3 Top, Bottom, Min, Max;
            public float Radius;
        }
        // Only the recent fill (user, 2026-10-05): a refilled hole far down needs a story.
        public const int PitCount = 3;

        // A stash pit's chest: its pivot StashLift above the pit's bottom, turned to a seeded yaw. Its pocket
        // (grid-local box in the chest's frame, from the chest prefab via TerrainVolume) is seeded air around and inside
        // the chest, from its base up past the lid's swing (user, 2026-10-06): the fill settled away from the chest, so a
        // shaft breaks into an open space with the chest standing in it, and what it holds lies loose inside from New Game.
        public struct Stash
        {
            public float3 Centre, PocketCentre, PocketHalf, Min, Max;
            // The pocket's dome (PocketDistance): its centre's offset across the pocket and its width, as shares of the
            // pocket's half extents (x, z offsets; x, z widths).
            public float4 Dome;
            public float3x3 ToLocal;
            public quaternion Rotation;
            public bool HasPocket => math.all(PocketHalf > 0);
        }
        public const float StashLift = .12f;

        // A geode (110): a hollow of air inside a shell of hard stone Shell thick, sealed until the player breaks in; its
        // crystals line the hollow. The hollow is an ellipsoid (Radii, in its own frame) smoothly joined to up to two side
        // lobes (LobeA, LobeB: their centres in its frame, with their radii; zero radii: none) and bent by a broad warp, so
        // it bulges and pinches like a real one instead of an egg (user, 2026-10-06: "larger ... weirder shape"). The
        // shell follows it, its outer face lumpy, the inner one nearly smooth.
        public struct Geode
        {
            public float3 Centre, Radii, Min, Max;
            public float3 LobeA, LobeRadiiA, LobeB, LobeRadiiB;
            public float Shell;
            // The farthest the shell reaches from the centre, lobes, warp and lumps included.
            public float Reach;
            public float3x3 ToLocal;
            public quaternion Rotation;
        }
        // Two in the old lake sediment, three in the old riverbed (110); the deep stone's hollow is the crystal cavern (115).
        public static readonly int[] GeodesPerZone = { 0, 2, 3, 0 };
        // The warp's and the lumps' heights, how softly a lobe joins the hollow, and slack on the reach for the
        // approximate distances.
        public const float GeodeWarp = .22f, GeodeOuterLumps = .15f, GeodeInnerLumps = .05f, GeodeBlend = .4f, GeodeSlack = .1f;
        // Clearance from pits and stashes, from uniques' spaces, and between geodes (beyond their shells).
        private const float GeodePitClearance = 1.5f, GeodeSpotClearance = 1f, GeodeSpacing = 6f;

        // A unique's space: its reserved envelope plus OddSpotReach sideways and OddSpotRise up and down,
        // which pits keep clear of.
        public const float OddSpotReach = .9f, OddSpotRise = .6f;
        public struct OddSpot
        {
            public float3 Centre, Half, Min, Max;
        }

        public static OddSpot[] OddSpots(Vector4[] spots)
        {
            if (spots == null) return Array.Empty<OddSpot>();
            var result = new OddSpot[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var centre = new float3(spots[i].x, spots[i].y, spots[i].z);
                var half = new float3(spots[i].w + OddSpotReach, spots[i].w + OddSpotRise, spots[i].w + OddSpotReach);
                result[i] = new OddSpot { Centre = centre, Half = half, Min = centre - half * 1.2f, Max = centre + half * 1.2f };
            }
            return result;
        }

        public static Pit[] Pits(Vector3Int size, float cellSize, int seed, OddSpot[] spots = null, Features features = Features.All)
        {
            if (!Has(features, Features.Pits)) return Array.Empty<Pit>();
            var extent = (Vector3)size * cellSize;
            var footprint = SiteLayout.FindFootprint(extent);
            uint state = unchecked((uint)seed * 3432918353u ^ 0x2545f491u);
            float Next() => TerrainMaterialSnapshot.NextUnit(ref state);
            float Range(float a, float b) => a + (b - a) * Next();
            var pits = new List<Pit>();
            float top = PitTop, bottom = Mathf.Min(ZoneBorders[0] - PitMargin, extent.y - 1);
            if (bottom - top < 4) return Array.Empty<Pit>();
            for (int n = 0; n < PitCount; n++)
                for (int attempt = 0; attempt < 120; attempt++)
                {
                    // The first lies a few metres under the plot centre, where a first shaft meets it in daylight; each
                    // pit is wide enough for the chest to lie in loose fill.
                    bool first = n == 0;
                    float radius = Range(1f, 1.2f), length = Range(2.5f, 6f);
                    var low = first ? new float3(extent.x * .5f + Range(-3, 3), extent.y - Range(top + 1, Mathf.Min(top + 3, bottom)), extent.z * .5f + Range(-3, 3))
                        : new float3(Range(2, extent.x - 2), extent.y - Range(top + 1, bottom), Range(2, extent.z - 2));
                    // Most pits were dug straight down; some lean well over to the side.
                    float lean = first ? Range(0, .3f) : Next() < .7f ? Range(0, .45f) : Range(.7f, 1.2f), heading = Range(0, 2 * math.PI);
                    var up = new float3(math.sin(lean) * math.sin(heading), math.cos(lean), math.sin(lean) * math.cos(heading));
                    var high = low + up * length;
                    if (extent.y - high.y < SurfaceSoil + .6f) high = low + up * ((extent.y - SurfaceSoil - .6f - low.y) / math.max(up.y, .2f));
                    if (math.distance(high, low) < 2) continue;
                    if (footprint != null && (!Inside(footprint, low, radius + .5f) || !Inside(footprint, high, radius + .5f))) continue;
                    var pit = new Pit { Top = high, Bottom = low, Radius = radius,
                        Min = math.min(low, high) - radius - .2f, Max = math.max(low, high) + radius + .2f };
                    bool clear = true;
                    foreach (var other in pits) clear &= math.any(pit.Min > other.Max + 1) || math.any(other.Min > pit.Max + 1);
                    if (spots != null) foreach (var spot in spots) clear &= math.any(pit.Min > spot.Max + 1) || math.any(spot.Min > pit.Max + 1);
                    if (!clear) continue;
                    pits.Add(pit);
                    break;
                }
            return pits.ToArray();
        }

        // One chest per pit, level, at a yaw seeded per pit. pocket: the chest's pocket in its own frame (size zero: none
        // carved).
        public static Stash[] Stashes(Pit[] pits, int seed, Bounds pocket = default)
        {
            var stashes = new Stash[pits.Length];
            for (int i = 0; i < pits.Length; i++)
            {
                uint h = unchecked((uint)seed * 2654435761u ^ (uint)i * 2246822519u ^ 0x9e3779b9u);
                stashes[i] = MakeStash(pits[i].Bottom + new float3(0, StashLift, 0), quaternion.RotateY(TerrainMaterialSnapshot.NextUnit(ref h) * 2 * math.PI), pocket);
            }
            return stashes;
        }

        // A chest at centre (grid-local), turned by rotation, in its pocket, its dome set from where it lies.
        public static Stash MakeStash(float3 centre, quaternion rotation, Bounds pocket)
        {
            float reach = math.length(pocket.extents) + math.length(pocket.center) + PocketDome + PocketWarp + .1f;
            uint state = math.hash(centre) | 1u;
            float Next() => TerrainMaterialSnapshot.NextUnit(ref state);
            var dome = new float4(Next() * .4f - .2f, Next() * .4f - .2f, .85f + Next() * .2f, .85f + Next() * .2f);
            return new Stash { Centre = centre, Rotation = rotation, ToLocal = math.transpose(new float3x3(rotation)), Dome = dome,
                PocketCentre = pocket.center, PocketHalf = pocket.extents, Min = centre - reach, Max = centre + reach };
        }

        // A chest's pocket of air (user, 2026-10-06: "more space vertically and a bit more random, not a square block"):
        // the box measured from the chest (its hollow, the lid's swing and a margin) with well-rounded edges, joined to a
        // dome over it, PocketDome higher, set off-centre and sized per stash, and pushed out by up to PocketWarp of broad
        // bulges, so it reads as a hollow the fill settled out of; small lumps pull walls and roof in by up to PocketRough.
        // Its floor stays flat under the chest (bulges and lumps fade in over PocketFloorBand above it). Signed distance,
        // negative in the air; grid-local metres.
        public const float PocketDome = .7f, PocketWarp = .22f, PocketRough = .08f, PocketFloorBand = .3f, PocketRound = .3f;
        public static float PocketDistance(Stash stash, float3 p)
        {
            var local = math.mul(stash.ToLocal, p - stash.Centre) - stash.PocketCentre;
            var half = stash.PocketHalf;
            float round = math.min(PocketRound, math.cmin(half) * .5f);
            var d = math.abs(local) - (half - round);
            float box = math.length(math.max(d, 0)) + math.min(math.cmax(d), 0) - round;
            var domeCentre = new float3(stash.Dome.x * half.x, half.y - .15f, stash.Dome.y * half.z);
            var domeRadii = new float3(half.x * stash.Dome.z, PocketDome + .15f, half.z * stash.Dome.w);
            float shape = math.max(SmoothMin(box, Ellipsoid(local - domeCentre, domeRadii), .35f), -(local.y + half.y));
            float above = math.saturate((local.y + half.y) / PocketFloorBand);
            return shape - above * PocketWarp * (.5f + .5f * noise.snoise(p * .7f + stash.Centre * .29f))
                + above * PocketRough * (.5f + .5f * noise.snoise(p * 2.2f + stash.Centre * .37f));
        }

        // Where finds keep out of a pocket's dome (grid-local centre and radius), beside the chest's own reserves.
        public static (float3 centre, float radius) PocketDomeReserve(Stash stash)
        {
            var half = stash.PocketHalf;
            var local = stash.PocketCentre + new float3(stash.Dome.x * half.x, half.y + PocketDome * .4f, stash.Dome.y * half.z);
            return (stash.Centre + math.mul(stash.Rotation, local), math.max(half.x * stash.Dome.z, half.z * stash.Dome.w) + PocketWarp);
        }

        // Seeded geodes, each wholly inside its zone and the find footprint, clear of pits, uniques' spaces and each other.
        public static Geode[] Geodes(Vector3Int size, float cellSize, int seed, Pit[] pits, OddSpot[] spots = null, Features features = Features.All)
        {
            if (!Has(features, Features.Geodes)) return Array.Empty<Geode>();
            var extent = (Vector3)size * cellSize;
            var footprint = SiteLayout.FindFootprint(extent);
            uint state = unchecked((uint)seed * 2246822519u ^ 0x7feb352du);
            float Next() => TerrainMaterialSnapshot.NextUnit(ref state);
            float Range(float a, float b) => a + (b - a) * Next();
            var geodes = new List<Geode>();
            // A side lobe off a hollow of this radius along heading, rising or dipping a little, always overlapping it.
            float3 Lobe(float radius, float heading, out float3 lobeRadii)
            {
                float rise = Range(-.5f, .4f), size = radius * Range(.5f, .75f);
                lobeRadii = new float3(size, size * Range(.7f, .9f), size * Range(.8f, 1.1f));
                return new float3(math.cos(rise) * math.cos(heading), math.sin(rise), math.cos(rise) * math.sin(heading)) * radius * Range(.6f, .9f);
            }
            for (int zone = 0; zone < GeodesPerZone.Length; zone++)
            {
                float zoneTop = zone == 0 ? 0 : ZoneBorders[zone - 1], zoneBottom = zone < ZoneBorders.Length ? ZoneBorders[zone] : extent.y;
                for (int n = 0; n < GeodesPerZone[zone]; n++)
                    for (int attempt = 0; attempt < 200; attempt++)
                    {
                        float radius = Range(1.5f, 1.85f), heading = Range(0, 2 * math.PI);
                        var radii = new float3(radius, radius * Range(.7f, .85f), radius * Range(.85f, 1.05f));
                        var lobeA = Lobe(radius, heading, out var lobeRadiiA);
                        var lobeRadiiB = float3.zero;
                        var lobeB = Next() < .6f ? Lobe(radius, heading + Range(1.6f, 4.7f), out lobeRadiiB) : float3.zero;
                        var geode = Make(float3.zero, radii, Range(.6f, .8f), Range(0, 2 * math.PI), Range(-.26f, .26f), lobeA, lobeRadiiA, lobeB, lobeRadiiB);
                        float reach = geode.Reach;
                        float top = zoneTop + reach + .5f, bottom = math.min(zoneBottom, extent.y - 1) - reach - .5f;
                        if (bottom <= top) break;
                        var centre = new float3(Range(reach + 1, extent.x - reach - 1), extent.y - Range(top, bottom), Range(reach + 1, extent.z - reach - 1));
                        geode = At(geode, centre);
                        if (footprint != null && !Inside(footprint, centre, reach + .5f)) continue;
                        bool clear = true;
                        foreach (var pit in pits) clear &= math.any(geode.Min > pit.Max + GeodePitClearance) || math.any(pit.Min > geode.Max + GeodePitClearance);
                        if (spots != null) foreach (var spot in spots) clear &= math.any(geode.Min > spot.Max + GeodeSpotClearance) || math.any(spot.Min > geode.Max + GeodeSpotClearance);
                        foreach (var other in geodes) clear &= math.distance(other.Centre, centre) > other.Reach + reach + GeodeSpacing;
                        if (!clear) continue;
                        geodes.Add(geode);
                        break;
                    }
            }
            return geodes.ToArray();
        }

        // A geode at centre (grid-local metres), turned by yaw about up and tipped by tilt about its own x, with its side
        // lobes (zero radii: none).
        public static Geode Make(float3 centre, float3 radii, float shell, float yaw, float tilt,
            float3 lobeA = default, float3 lobeRadiiA = default, float3 lobeB = default, float3 lobeRadiiB = default)
        {
            var rotation = math.mul(quaternion.RotateY(yaw), quaternion.RotateX(tilt));
            float span = math.cmax(radii);
            if (math.cmin(lobeRadiiA) > 0) span = math.max(span, math.length(lobeA) + math.cmax(lobeRadiiA));
            if (math.cmin(lobeRadiiB) > 0) span = math.max(span, math.length(lobeB) + math.cmax(lobeRadiiB));
            var geode = new Geode { Radii = radii, Shell = shell, Rotation = rotation, ToLocal = math.transpose(new float3x3(rotation)),
                LobeA = lobeA, LobeRadiiA = lobeRadiiA, LobeB = lobeB, LobeRadiiB = lobeRadiiB,
                Reach = span + GeodeBlend * .25f + GeodeWarp + shell + GeodeOuterLumps + GeodeSlack };
            return At(geode, centre);
        }

        private static Geode At(Geode geode, float3 centre)
        {
            geode.Centre = centre; geode.Min = centre - geode.Reach; geode.Max = centre + geode.Reach;
            return geode;
        }

        // Signed distance (approximate, metres) from a geode-local point to an ellipsoid of these radii: negative inside.
        public static float Ellipsoid(float3 local, float3 radii)
        {
            float k0 = math.length(local / radii), k1 = math.length(local / (radii * radii));
            return k1 > 1e-6f ? k0 * (k0 - 1) / k1 : -math.cmin(radii);
        }

        // Polynomial smooth minimum: the nearer of two distances, rounded where they come within k of each other.
        private static float SmoothMin(float a, float b, float k)
        {
            float h = math.max(k - math.abs(a - b), 0) / k;
            return math.min(a, b) - h * h * k * .25f;
        }

        // Signed distance (approximate) from a geode-local point to the hollow's shape: its lobes joined, warped.
        private static float Shape(Geode geode, float3 local)
        {
            float d = Ellipsoid(local, geode.Radii);
            if (math.cmin(geode.LobeRadiiA) > 0) d = SmoothMin(d, Ellipsoid(local - geode.LobeA, geode.LobeRadiiA), GeodeBlend);
            if (math.cmin(geode.LobeRadiiB) > 0) d = SmoothMin(d, Ellipsoid(local - geode.LobeB, geode.LobeRadiiB), GeodeBlend);
            return d + GeodeWarp * noise.snoise(local * .55f + geode.Centre);
        }

        // Signed distance to a geode's hollow (negative in its air) and to its shell's outer face (negative inside the
        // stone or the hollow), lumps included. Grid-local metres.
        public static float HollowDistance(Geode geode, float3 p)
        {
            var local = math.mul(geode.ToLocal, p - geode.Centre);
            return Shape(geode, local) + GeodeInnerLumps * noise.snoise(local * 2.6f + geode.Centre);
        }

        public static float OuterDistance(Geode geode, float3 p)
        {
            var local = math.mul(geode.ToLocal, p - geode.Centre);
            return Shape(geode, local) - geode.Shell + GeodeOuterLumps * noise.snoise(local * .9f + geode.Centre * .37f);
        }

        // Where the ray from a geode's centre along a direction (its own frame) first meets its hollow's face, and the
        // face's outward normal there, grid-local: marched out HollowStep at a time, then halved to a millimetre or so.
        private const float HollowStep = .08f;
        public static (float3 surface, float3 outward) HollowFace(Geode geode, float3 direction)
        {
            var way = math.mul(new float3x3(geode.Rotation), math.normalizesafe(direction, new float3(0, -1, 0)));
            float inside = 0, outside = geode.Reach;
            for (float t = HollowStep; t < geode.Reach; t += HollowStep)
            {
                if (HollowDistance(geode, geode.Centre + way * t) >= 0) { outside = t; break; }
                inside = t;
            }
            for (int i = 0; i < 6; i++)
            {
                float middle = (inside + outside) * .5f;
                if (HollowDistance(geode, geode.Centre + way * middle) >= 0) outside = middle; else inside = middle;
            }
            var surface = geode.Centre + way * outside;
            const float h = .03f;
            var gradient = new float3(
                HollowDistance(geode, surface + new float3(h, 0, 0)) - HollowDistance(geode, surface - new float3(h, 0, 0)),
                HollowDistance(geode, surface + new float3(0, h, 0)) - HollowDistance(geode, surface - new float3(0, h, 0)),
                HollowDistance(geode, surface + new float3(0, 0, h)) - HollowDistance(geode, surface - new float3(0, 0, h)));
            return (surface, math.normalizesafe(gradient, way));
        }

        // A geode's signed distances, to its hollow or to its shell's outer face, at every sample from first to last
        // (inclusive, x fastest). Burst-compiled: a geode's box holds a few hundred thousand samples.
        public static NativeArray<float> GeodeField(Geode geode, float cellSize, int3 first, int3 last, bool outer, Allocator allocator)
        {
            var count = math.max(last - first + 1, 0);
            var field = new NativeArray<float>(count.x * count.y * count.z, allocator, NativeArrayOptions.UninitializedMemory);
            new GeodeJob { Geode = geode, CellSize = cellSize, First = first, Count = count, Outer = outer, Output = field }
                .Schedule(count.z, 1).Complete();
            return field;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
        private struct GeodeJob : IJobParallelFor
        {
            public Geode Geode;
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
                    Output[i] = Outer ? OuterDistance(Geode, p) : HollowDistance(Geode, p);
                }
            }
        }

        // What find placement needs from the seeded ground: the pits and their chests, and the geodes.
        public sealed class GroundLayout
        {
            public static readonly GroundLayout Empty = new GroundLayout(Array.Empty<Pit>(), Array.Empty<Stash>(), Array.Empty<Geode>());
            public readonly Pit[] Pits; public readonly Stash[] Stashes; public readonly Geode[] Geodes;
            public GroundLayout(Pit[] pits, Stash[] stashes, Geode[] geodes) { Pits = pits; Stashes = stashes; Geodes = geodes; }
        }

        // Pits and geodes keep clear of every unique's space.
        public static GroundLayout Layout(Vector3Int size, float cellSize, int seed, Vector4[] oddSpots = null, Features features = Features.All,
            Bounds stashPocket = default)
        {
            var spots = OddSpots(oddSpots);
            var pits = Pits(size, cellSize, seed, spots, features);
            return new GroundLayout(pits, Stashes(pits, seed, stashPocket), Geodes(size, cellSize, seed, pits, spots, features));
        }

        private static bool Inside(Func<Vector2, bool> footprint, float3 centre, float reach)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * math.PI / 4;
                if (!footprint(new Vector2(centre.x + math.cos(angle) * reach, centre.z + math.sin(angle) * reach))) return false;
            }
            return footprint(new Vector2(centre.x, centre.z));
        }

        internal static byte[] Generate(Vector3Int size, float cellSize, int seed, Vector4[] oddSpots = null, Features features = Features.All,
            Bounds stashPocket = default)
        {
            int stride = size.x + 1, plane = stride * (size.y + 1);
            var spots = OddSpots(oddSpots);
            var pits = Pits(size, cellSize, seed, spots, features);
            uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
            var offsets = new float4(hash & 1023, (hash >> 10) & 1023, (hash >> 20) & 1023, (hash >> 5) & 1023) * .37f;
            using var output = new NativeArray<byte>(plane * (size.z + 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            using var nativePits = new NativeArray<Pit>(pits, Allocator.TempJob);
            new GroundJob { Size = new int3(size.x, size.y, size.z), CellSize = cellSize, Offsets = offsets, Pits = nativePits, Output = output }
                .Schedule(size.z + 1, 1).Complete();
            var ids = output.ToArray();
            foreach (var stash in Stashes(pits, seed, stashPocket)) if (stash.HasPocket) FillShell(ids, size, cellSize, stash, offsets);
            foreach (var geode in Geodes(size, cellSize, seed, pits, spots, features)) FillGeode(ids, size, cellSize, geode);
            return ids;
        }

        // A geode's stone: everything inside its outer face, its hollow's samples too, so the hollow's walls read as
        // shell. On the main thread after the ground job, like FillShell.
        public static void FillGeode(byte[] ids, Vector3Int size, float cellSize, Geode geode)
        {
            int stride = size.x + 1, plane = stride * (size.y + 1);
            var first = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt((Vector3)geode.Min / cellSize));
            var last = Vector3Int.Min(size, Vector3Int.CeilToInt((Vector3)geode.Max / cellSize));
            using var field = GeodeField(geode, cellSize, new int3(first.x, first.y, first.z), new int3(last.x, last.y, last.z), true, Allocator.TempJob);
            int i = 0;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++, i++)
                if (field[i] < 0) ids[x + y * stride + z * plane] = (byte)TerrainMaterialId.GeodeShell;
        }

        // The fill around a chest (user, 2026-10-06: "all ground around it"): its pocket's walls, floor and roof are
        // backfill to ChestShell beyond it, with a lumpy edge, wherever the pit itself does not reach.
        public const float ChestShell = .6f;
        public static void FillShell(byte[] ids, Vector3Int size, float cellSize, Stash stash, float4 offsets)
        {
            int stride = size.x + 1, plane = stride * (size.y + 1);
            float reach = math.length(stash.PocketHalf) + math.length(stash.PocketCentre) + PocketDome + PocketWarp + ChestShell + .3f;
            var first = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt((Vector3)(stash.Centre - reach) / cellSize));
            var last = Vector3Int.Min(size, Vector3Int.CeilToInt((Vector3)(stash.Centre + reach) / cellSize));
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            {
                if ((size.y - y) * cellSize < SurfaceSoil) continue;
                for (int x = first.x; x <= last.x; x++)
                {
                    var p = new float3(x, y, z) * cellSize;
                    float outside = PocketDistance(stash, p) - ChestShell + .15f * noise.snoise(p * 1.4f + offsets.xzw + 19.7f);
                    if (outside < 0) ids[x + y * stride + z * plane] = (byte)TerrainMaterialId.Backfill;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
        private struct GroundJob : IJobParallelFor
        {
            public int3 Size;
            public float CellSize;
            public float4 Offsets;
            [ReadOnly] public NativeArray<Pit> Pits;
            [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<byte> Output;

            public void Execute(int z)
            {
                int stride = Size.x + 1, plane = stride * (Size.y + 1);
                float pz = z * CellSize;
                var dug = new FixedList128Bytes<int>();
                for (int i = 0; i < Pits.Length && dug.Length < dug.Capacity; i++)
                    if (pz >= Pits[i].Min.z && pz <= Pits[i].Max.z) dug.Add(i);
                for (int x = 0; x <= Size.x; x++)
                {
                    float px = x * CellSize;
                    for (int y = 0; y <= Size.y; y++)
                    {
                        float depth = (Size.y - y) * CellSize;
                        var p = new float3(px, y * CellSize, pz);
                        Output[x + y * stride + z * plane] = (byte)Material(p, depth, dug);
                    }
                }
            }

            private TerrainMaterialId Material(float3 p, float depth, FixedList128Bytes<int> dug)
            {
                if (depth < SurfaceSoil) return TerrainMaterialId.Soil;
                for (int i = 0; i < dug.Length; i++)
                    if (InPit(Pits[dug[i]], p)) return TerrainMaterialId.Backfill;
                return TerrainMaterialId.Soil;
            }

            // Inside a refilled pit: a capsule from top to bottom with a ragged, lumpy edge.
            private bool InPit(Pit pit, float3 p)
            {
                if (math.any(p < pit.Min) || math.any(p > pit.Max)) return false;
                float3 axis = pit.Top - pit.Bottom;
                float t = math.saturate(math.dot(p - pit.Bottom, axis) / math.max(1e-6f, math.dot(axis, axis)));
                float edge = pit.Radius * (1 + .18f * noise.snoise(p * 1.4f + Offsets.xzw + 19.7f));
                return math.lengthsq(p - (pit.Bottom + axis * t)) < edge * edge;
            }
        }
    }
}
