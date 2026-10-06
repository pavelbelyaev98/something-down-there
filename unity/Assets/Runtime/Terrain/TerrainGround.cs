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
            public float3x3 ToLocal;
            public quaternion Rotation;
            public bool HasPocket => math.all(PocketHalf > 0);
        }
        public const float StashLift = .12f;

        // A geode (110): a hollow ellipsoid of air (Radii, in its own frame) inside a shell of hard stone Shell thick, its
        // outer face lumpy and its inner one nearly smooth. Sealed until the player breaks in; its crystals line the hollow.
        public struct Geode
        {
            public float3 Centre, Radii, Min, Max;
            public float Shell;
            public float3x3 ToLocal;
            public quaternion Rotation;
            // The farthest the shell reaches from the centre, lumps included.
            public float Reach => math.cmax(Radii) + Shell + GeodeOuterLumps;
        }
        // Two in the old lake sediment, three in the old riverbed (110); the deep stone's hollow is the crystal cavern (115).
        public static readonly int[] GeodesPerZone = { 0, 2, 3, 0 };
        public const float GeodeOuterLumps = .15f, GeodeInnerLumps = .04f;
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

        // A chest at centre (grid-local), turned by rotation, in its pocket.
        public static Stash MakeStash(float3 centre, quaternion rotation, Bounds pocket)
        {
            float reach = math.length(pocket.extents) + math.length(pocket.center);
            return new Stash { Centre = centre, Rotation = rotation, ToLocal = math.transpose(new float3x3(rotation)),
                PocketCentre = pocket.center, PocketHalf = pocket.extents, Min = centre - reach, Max = centre + reach };
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
            for (int zone = 0; zone < GeodesPerZone.Length; zone++)
            {
                float zoneTop = zone == 0 ? 0 : ZoneBorders[zone - 1], zoneBottom = zone < ZoneBorders.Length ? ZoneBorders[zone] : extent.y;
                for (int n = 0; n < GeodesPerZone[zone]; n++)
                    for (int attempt = 0; attempt < 200; attempt++)
                    {
                        float radius = Range(.9f, 1.3f);
                        var geode = Make(float3.zero, new float3(radius, radius * Range(.75f, .9f), radius * Range(.9f, 1.1f)), Range(.6f, .9f),
                            Range(0, 2 * math.PI), Range(-.26f, .26f));
                        float reach = geode.Reach;
                        float top = zoneTop + reach + .5f, bottom = math.min(zoneBottom, extent.y - 1) - reach - .5f;
                        if (bottom <= top) break;
                        var centre = new float3(Range(reach + 1, extent.x - reach - 1), extent.y - Range(top, bottom), Range(reach + 1, extent.z - reach - 1));
                        geode = Make(centre, geode.Radii, geode.Shell, geode.Rotation);
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

        // A geode at centre (grid-local metres), turned by yaw about up and tipped by tilt about its own x.
        public static Geode Make(float3 centre, float3 radii, float shell, float yaw, float tilt)
            => Make(centre, radii, shell, math.mul(quaternion.RotateY(yaw), quaternion.RotateX(tilt)));

        private static Geode Make(float3 centre, float3 radii, float shell, quaternion rotation)
        {
            var geode = new Geode { Centre = centre, Radii = radii, Shell = shell, Rotation = rotation, ToLocal = math.transpose(new float3x3(rotation)) };
            float reach = geode.Reach;
            geode.Min = centre - reach; geode.Max = centre + reach;
            return geode;
        }

        // Signed distance (approximate, metres) from a geode-local point to an ellipsoid of these radii: negative inside.
        public static float Ellipsoid(float3 local, float3 radii)
        {
            float k0 = math.length(local / radii), k1 = math.length(local / (radii * radii));
            return k1 > 1e-6f ? k0 * (k0 - 1) / k1 : -math.cmin(radii);
        }

        // Signed distance to a geode's hollow (negative in its air) and to its shell's outer face (negative inside the
        // stone or the hollow), lumps included.
        public static float HollowDistance(Geode geode, float3 p)
            => Ellipsoid(math.mul(geode.ToLocal, p - geode.Centre), geode.Radii) + GeodeInnerLumps * noise.snoise(p * 2.6f + geode.Centre);

        public static float OuterDistance(Geode geode, float3 p)
            => Ellipsoid(math.mul(geode.ToLocal, p - geode.Centre), geode.Radii + geode.Shell) + GeodeOuterLumps * noise.snoise(p * .9f + geode.Centre * .37f);

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
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
                if (OuterDistance(geode, new float3(x, y, z) * cellSize) < 0) ids[x + y * stride + z * plane] = (byte)TerrainMaterialId.GeodeShell;
        }

        // The fill around a chest (user, 2026-10-06: "all ground around it"): its pocket's walls, floor and roof are
        // backfill to ChestShell beyond it, with a lumpy edge, wherever the pit itself does not reach.
        public const float ChestShell = .6f;
        public static void FillShell(byte[] ids, Vector3Int size, float cellSize, Stash stash, float4 offsets)
        {
            int stride = size.x + 1, plane = stride * (size.y + 1);
            float reach = math.length(stash.PocketHalf) + math.length(stash.PocketCentre) + ChestShell + .2f;
            var first = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt((Vector3)(stash.Centre - reach) / cellSize));
            var last = Vector3Int.Min(size, Vector3Int.CeilToInt((Vector3)(stash.Centre + reach) / cellSize));
            var half = stash.PocketHalf + ChestShell;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            {
                if ((size.y - y) * cellSize < SurfaceSoil) continue;
                for (int x = first.x; x <= last.x; x++)
                {
                    var p = new float3(x, y, z) * cellSize;
                    var d = math.abs(math.mul(stash.ToLocal, p - stash.Centre) - stash.PocketCentre) - (half - .3f);
                    float outside = math.length(math.max(d, 0)) + math.min(math.cmax(d), 0) - .3f
                        + .15f * noise.snoise(p * 1.4f + offsets.xzw + 19.7f);
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
