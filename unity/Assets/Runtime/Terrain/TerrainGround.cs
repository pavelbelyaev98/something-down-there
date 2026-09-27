using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SomethingDownThere
{
    // The seeded ground: four zones of one main ground each, short mixed bands between them,
    // soft veins through the rock and a few places per zone. Written once per session into the
    // one-byte material field; saves store the bytes, so nothing here is saved separately.
    public static class TerrainGround
    {
        // Metres below the surface: even quarters of the 150 m site. Absolute depths, so a
        // shallow grid (fixtures, tests) is all zone 1.
        public static readonly float[] ZoneBorders = { 37.5f, 75f, 112.5f };
        // Borders undulate by up to this much; the mixed band is this thick around each border.
        public const float BorderWarp = 1.5f, EdgeBand = 3f;
        // The first metre stays soil (first scrapes, the permanent bank); places start lower.
        public const float SurfaceSoil = 1.1f, PlaceTop = 3f;

        public enum PlaceKind : byte { Rubble, Structure, RockMass, Basin }
        // Crack field (noise units): the line within CrackCore of a sheet, the fractured band within
        // CrackBand. Gate biases: how much of each body's sheets exist (rock masses are criss-crossed).
        public const float CrackCore = .017f, CrackBand = .055f;
        private const float MassCracks = .35f, ConcreteCracks = .25f, ZoneRockCracks = -.05f;

        // Grid-local metres. Rotation maps local offsets into the place's own axes.
        public struct Place
        {
            public PlaceKind Kind;
            public int Zone;
            public float3 Centre, HalfSize;
            public float3x3 ToLocal;
            public float Wall;
            public float3 Min, Max;
        }

        public static int ZoneAt(float depth)
        {
            int zone = 0;
            while (zone < ZoneBorders.Length && depth >= ZoneBorders[zone]) zone++;
            return zone;
        }

        // Places per zone: kind and count. Zone 4 waits for its own ground (039).
        private static readonly (PlaceKind kind, int count)[][] Quotas =
        {
            new[] { (PlaceKind.Rubble, 8) },
            new[] { (PlaceKind.Structure, 2), (PlaceKind.RockMass, 2), (PlaceKind.Basin, 3) },
            new[] { (PlaceKind.Structure, 2), (PlaceKind.RockMass, 3) },
            Array.Empty<(PlaceKind, int)>()
        };

        // Deterministic place list for a grid and seed. Places lie under the plot footprint
        // (SiteLayout) on the shipped grid, anywhere on other grids, and never overlap.
        public static Place[] Places(Vector3Int size, float cellSize, int seed)
        {
            var extent = (Vector3)size * cellSize;
            var footprint = SiteLayout.FindFootprint(extent);
            uint state = unchecked((uint)seed * 2654435761u ^ 0x5bd1e995u);
            float Next() => TerrainMaterialSnapshot.NextUnit(ref state);
            float Range(float a, float b) => a + (b - a) * Next();
            var places = new List<Place>();
            for (int zone = 0; zone < Quotas.Length; zone++)
            {
                float top = zone == 0 ? PlaceTop : ZoneBorders[zone - 1] + EdgeBand;
                float bottom = zone < ZoneBorders.Length ? ZoneBorders[zone] - EdgeBand : extent.y;
                foreach (var (kind, count) in Quotas[zone])
                    for (int n = 0; n < count; n++)
                        for (int attempt = 0; attempt < 200; attempt++)
                        {
                            float3 half = kind switch
                            {
                                PlaceKind.Rubble => new float3(Range(.3f, .75f), Range(.25f, .6f), Range(.3f, .75f)),
                                PlaceKind.Structure => zone == 1 ? new float3(Range(2f, 3.2f), Range(1.4f, 1.9f), Range(1.8f, 2.8f))
                                    : new float3(Range(3.2f, 4.5f), Range(1.2f, 1.6f), Range(1.5f, 2f)),
                                PlaceKind.RockMass => new float3(Range(2f, 3.5f), Range(1.6f, 2.8f), Range(2f, 3.5f)),
                                _ => new float3(Range(2.5f, 4.5f), Range(1.2f, 2.4f), Range(2.5f, 4.5f)),
                            };
                            float reach = math.length(half.xz);
                            float depth = Range(top + half.y, bottom - half.y);
                            if (bottom - top < half.y * 2 + .5f || depth + half.y > extent.y - .5f) break;
                            var centre = new float3(Range(reach + .5f, extent.x - reach - .5f), extent.y - depth, Range(reach + .5f, extent.z - reach - .5f));
                            if (footprint != null && !Inside(footprint, centre, reach)) continue;
                            float yaw = Range(0, 2 * math.PI);
                            float tilt = kind == PlaceKind.Rubble ? Range(-.5f, .5f) : 0, roll = kind == PlaceKind.Rubble ? Range(-.5f, .5f) : 0;
                            var toLocal = math.transpose(new float3x3(quaternion.EulerXYZ(tilt, yaw, roll)));
                            float3 bound = kind == PlaceKind.Rubble ? math.length(half) : new float3(reach, half.y, reach);
                            var place = new Place { Kind = kind, Zone = zone, Centre = centre, HalfSize = half, ToLocal = toLocal,
                                Wall = kind == PlaceKind.Structure ? Range(.4f, .5f) : 0, Min = centre - bound - .1f, Max = centre + bound + .1f };
                            bool clear = true;
                            foreach (var other in places)
                                clear &= math.any(place.Min > other.Max + 1) || math.any(other.Min > place.Max + 1);
                            if (!clear) continue;
                            places.Add(place);
                            break;
                        }
            }
            return places.ToArray();
        }

        // Old riverbeds (097): winding flattened tubes of gravel through the soil and clay zones,
        // sideways more than down. A segment runs A to B; its cross-section is HalfWidth across
        // and HalfHeight up.
        public struct ChannelSegment
        {
            public int Channel, Zone;
            public float3 A, B, Min, Max;
            public float HalfWidth, HalfHeight;
        }
        private static readonly int[] ChannelsPerZone = { 3, 3 };
        public const float ChannelStep = 1.5f;

        public static ChannelSegment[] Channels(Vector3Int size, float cellSize, int seed)
        {
            var extent = (Vector3)size * cellSize;
            var footprint = SiteLayout.FindFootprint(extent);
            uint state = unchecked((uint)seed * 1597334677u ^ 0x68e31da4u);
            float Next() => TerrainMaterialSnapshot.NextUnit(ref state);
            float Range(float a, float b) => a + (b - a) * Next();
            bool Under(float3 p, float reach) => footprint == null
                ? p.x > reach && p.z > reach && p.x < extent.x - reach && p.z < extent.z - reach : Inside(footprint, p, reach);
            var segments = new List<ChannelSegment>();
            int channel = 0;
            for (int zone = 0; zone < ChannelsPerZone.Length; zone++)
            {
                float top = zone == 0 ? PlaceTop : ZoneBorders[zone - 1] + EdgeBand, bottom = ZoneBorders[zone] - EdgeBand;
                bottom = Mathf.Min(bottom, extent.y - 1.5f);
                if (bottom - top < 3) break;
                for (int n = 0; n < ChannelsPerZone[zone]; n++, channel++)
                {
                    float halfWidth = Range(.8f, 1f), halfHeight = Range(.45f, .6f);
                    // The first channel meets the first shaft: near the plot centre, a few metres down.
                    bool first = zone == 0 && n == 0;
                    float3 p = default;
                    for (int attempt = 0; attempt < 64; attempt++)
                    {
                        p = first ? new float3(extent.x * .5f + Range(-3, 3), 0, extent.z * .5f + Range(-3, 3))
                            : new float3(Range(2, extent.x - 2), 0, Range(2, extent.z - 2));
                        p.y = extent.y - (first ? Range(4, Mathf.Min(7, bottom)) : Range(top + 1, bottom - 1));
                        if (Under(p, halfWidth + .5f)) break;
                    }
                    float heading = Range(0, 2 * math.PI), slope = Range(-.25f, .25f);
                    int steps = Mathf.CeilToInt(Range(18, 30) / ChannelStep);
                    for (int s = 0; s < steps; s++)
                    {
                        heading += Range(-.45f, .45f);
                        slope = math.clamp(slope + Range(-.15f, .15f), -.45f, .45f);
                        float3 next = default; bool ok = false;
                        for (int turn = 0; turn < 6 && !ok; turn++)
                        {
                            var direction = new float3(math.sin(heading) * math.cos(slope), -math.sin(slope), math.cos(heading) * math.cos(slope));
                            next = p + direction * ChannelStep;
                            float depth = extent.y - next.y;
                            if (depth < top || depth > bottom) { slope = -slope; continue; }
                            if (!Under(next, halfWidth + .5f)) { heading += math.PI * .6f; continue; }
                            ok = true;
                        }
                        if (!ok) break;
                        float3 reach = new float3(halfWidth, halfHeight, halfWidth) + .2f;
                        segments.Add(new ChannelSegment { Channel = channel, Zone = zone, A = p, B = next, HalfWidth = halfWidth,
                            HalfHeight = halfHeight, Min = math.min(p, next) - reach, Max = math.max(p, next) + reach });
                        p = next;
                    }
                }
            }
            return segments.ToArray();
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

        internal static byte[] Generate(Vector3Int size, float cellSize, int seed)
        {
            int stride = size.x + 1, plane = stride * (size.y + 1);
            var places = Places(size, cellSize, seed);
            var channels = Channels(size, cellSize, seed);
            uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
            var offsets = new float4(hash & 1023, (hash >> 10) & 1023, (hash >> 20) & 1023, (hash >> 5) & 1023) * .37f;
            using var output = new NativeArray<byte>(plane * (size.z + 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            using var nativePlaces = new NativeArray<Place>(places, Allocator.TempJob);
            using var borders = new NativeArray<float>(ZoneBorders, Allocator.TempJob);
            using var nativeChannels = new NativeArray<ChannelSegment>(channels, Allocator.TempJob);
            new GroundJob
            {
                Size = new int3(size.x, size.y, size.z), CellSize = cellSize, Offsets = offsets,
                Places = nativePlaces, Borders = borders, Channels = nativeChannels, Output = output
            }.Schedule(size.z + 1, 1).Complete();
            return output.ToArray();
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
        private struct GroundJob : IJobParallelFor
        {
            public int3 Size;
            public float CellSize;
            public float4 Offsets;
            [ReadOnly] public NativeArray<Place> Places;
            [ReadOnly] public NativeArray<float> Borders;
            [ReadOnly] public NativeArray<ChannelSegment> Channels;
            [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<byte> Output;

            public void Execute(int z)
            {
                int stride = Size.x + 1, plane = stride * (Size.y + 1);
                float pz = z * CellSize;
                // Places crossing this slice; a slice meets only a handful.
                var slice = new FixedList128Bytes<int>();
                for (int i = 0; i < Places.Length && slice.Length < slice.Capacity; i++)
                    if (pz >= Places[i].Min.z && pz <= Places[i].Max.z) slice.Add(i);
                var riverbeds = new FixedList512Bytes<int>();
                for (int i = 0; i < Channels.Length && riverbeds.Length < riverbeds.Capacity; i++)
                    if (pz >= Channels[i].Min.z && pz <= Channels[i].Max.z) riverbeds.Add(i);
                var warps = new float3(0);
                for (int x = 0; x <= Size.x; x++)
                {
                    float px = x * CellSize;
                    // Undulating borders, one warp per border and column.
                    for (int b = 0; b < 3; b++)
                        warps[b] = noise.snoise(new float2(px, pz) * .045f + Offsets.xy + b * 17.3f) * BorderWarp;
                    for (int y = 0; y <= Size.y; y++)
                    {
                        float depth = (Size.y - y) * CellSize;
                        var p = new float3(px, y * CellSize, pz);
                        Output[x + y * stride + z * plane] = (byte)Material(p, depth, warps, slice, riverbeds);
                    }
                }
            }

            private TerrainMaterialId Material(float3 p, float depth, float3 warps, FixedList128Bytes<int> slice, FixedList512Bytes<int> riverbeds)
            {
                if (depth < SurfaceSoil) return TerrainMaterialId.Soil;
                for (int i = 0; i < slice.Length; i++)
                {
                    var place = Places[slice[i]];
                    if (math.any(p < place.Min) || math.any(p > place.Max)) continue;
                    if (InPlace(place, p, out var material))
                        return material == TerrainMaterialId.Rock ? Cracked(p, material, MassCracks)
                            : material == TerrainMaterialId.Concrete ? Cracked(p, material, ConcreteCracks) : material;
                }
                int zone = 0;
                for (int b = 0; b < Borders.Length; b++)
                {
                    float rel = depth - (Borders[b] + warps[b]);
                    if (rel >= EdgeBand * .5f) { zone = b + 1; continue; }
                    if (rel > -EdgeBand * .5f)
                    {
                        // The mixed band: lumps of the next ground grow from none to all.
                        float t = rel / EdgeBand + .5f;
                        float lump = noise.snoise(p * .75f + Offsets.zwx + b * 31.7f) * .5f + .5f;
                        if (lump < t) zone = b + 1;
                    }
                    break;
                }
                if (zone <= 1 && InChannel(p, riverbeds)) return TerrainMaterialId.Gravel;
                if (zone == 0) return GravelLens(p, depth) ? TerrainMaterialId.Gravel : TerrainMaterialId.Soil;
                if (zone == 1) return TerrainMaterialId.Clay;
                var ground = Vein(p, zone == 2 ? .065f : .05f);
                return ground == TerrainMaterialId.Rock ? Cracked(p, ground, ZoneRockCracks) : ground;
            }

            // Two families of crack sheets, each a finite patch where its gate is open; sheets thin
            // toward the gate's edge, so cracks branch where families cross and fade out.
            private TerrainMaterialId Cracked(float3 p, TerrainMaterialId ground, float bias)
            {
                float distance = 1;
                float gate = noise.snoise(p * .055f + Offsets.xwz + 7.1f) + bias;
                if (gate > 0) distance = math.abs(noise.snoise(p * new float3(.13f, .1f, .13f) + Offsets.yxw + 21.3f)) / math.saturate(gate * 4);
                gate = noise.snoise(p * .06f + Offsets.zyw + 13.7f) + bias;
                if (gate > 0) distance = math.min(distance, math.abs(noise.snoise(p * new float3(.1f, .16f, .1f) + Offsets.wzx + 37.9f)) / math.saturate(gate * 4));
                if (distance < CrackCore) return TerrainMaterialId.Crack;
                if (distance < CrackBand) return ground == TerrainMaterialId.Concrete ? TerrainMaterialId.FracturedConcrete : TerrainMaterialId.FracturedRock;
                return ground;
            }

            // Inside a riverbed's flattened tube, with a slightly wandering bank.
            private bool InChannel(float3 p, FixedList512Bytes<int> riverbeds)
            {
                for (int i = 0; i < riverbeds.Length; i++)
                {
                    var s = Channels[riverbeds[i]];
                    if (math.any(p < s.Min) || math.any(p > s.Max)) continue;
                    float3 ab = s.B - s.A;
                    float t = math.saturate(math.dot(p - s.A, ab) / math.max(1e-6f, math.dot(ab, ab)));
                    float3 d = p - (s.A + ab * t);
                    float bank = 1 + .12f * noise.snoise(p * .6f + Offsets.wxy + 71.3f);
                    if (math.lengthsq(new float3(d.x / s.HalfWidth, d.y / s.HalfHeight, d.z / s.HalfWidth)) < bank * bank) return true;
                }
                return false;
            }

            // Flattened lenses of loose gravel in the recent fill, below the first scrapes.
            private bool GravelLens(float3 p, float depth)
            {
                if (depth < 2) return false;
                float lens = noise.snoise(new float3(p.x * .16f, p.y * .55f, p.z * .16f) + Offsets.wxz);
                return lens > .6f;
            }

            // Rock veined with clay: winding sheets of clay, pinching and swelling, in every
            // direction; a few stretches of gravel. These are the soft paths through the rock.
            private TerrainMaterialId Vein(float3 p, float width)
            {
                float swell = noise.snoise(p * .05f + Offsets.yzw) * .5f + .5f;
                float sheet = noise.snoise(p * .085f + Offsets.xzy);
                float second = noise.snoise(p * .07f + Offsets.wyx + 53.1f);
                float w = width * (.55f + swell * .9f);
                if (math.abs(sheet) >= w && math.abs(second) >= w * .7f) return TerrainMaterialId.Rock;
                return noise.snoise(p * .11f + Offsets.zxy + 91.3f) > .55f ? TerrainMaterialId.Gravel : TerrainMaterialId.Clay;
            }

            private bool InPlace(Place place, float3 p, out TerrainMaterialId material)
            {
                float3 q = math.mul(place.ToLocal, p - place.Centre);
                material = TerrainMaterialId.Soil;
                switch (place.Kind)
                {
                    case PlaceKind.Rubble:
                        material = TerrainMaterialId.Concrete;
                        return math.all(math.abs(q) <= place.HalfSize);
                    case PlaceKind.Structure:
                        if (math.any(math.abs(q) > place.HalfSize)) return false;
                        // Walls and floor are concrete; the open-topped interior is soil.
                        bool interior = math.all(math.abs(q.xz) < place.HalfSize.xz - place.Wall) && q.y > -place.HalfSize.y + place.Wall;
                        material = interior ? TerrainMaterialId.Soil : TerrainMaterialId.Concrete;
                        return true;
                    case PlaceKind.RockMass:
                        material = TerrainMaterialId.Rock;
                        float lumpy = 1 + .18f * noise.snoise(q * .45f + Offsets.xyz);
                        return math.lengthsq(q / place.HalfSize) < lumpy * lumpy;
                    default:
                        // A bowl of old pond clay with a flat top and a wavy rim.
                        if (q.y > 0) return false;
                        material = TerrainMaterialId.PondClay;
                        float rim = 1 + .12f * noise.snoise(q.xz * .4f + Offsets.yw);
                        return math.lengthsq(q / place.HalfSize) < rim * rim;
                }
            }
        }
    }
}
