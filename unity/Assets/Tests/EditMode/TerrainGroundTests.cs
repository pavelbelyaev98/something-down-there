using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    // Concept 03 §3–5: four zones of one main ground, short mixed edge bands, soft paths through
    // the rock and a few ground places per zone, all from the seed.
    public sealed class TerrainGroundTests
    {
        private static readonly Dictionary<int, byte[]> Sites = new Dictionary<int, byte[]>();
        private static byte[] Site(int seed)
        {
            if (!Sites.TryGetValue(seed, out var ids))
                Sites[seed] = ids = TerrainMaterialSnapshot.Generate(SiteLayout.Size, SiteLayout.CellSize, seed).ToArray();
            return ids;
        }

        private static int Index(int x, int y, int z) => x + y * (SiteLayout.Size.x + 1) + z * (SiteLayout.Size.x + 1) * (SiteLayout.Size.y + 1);
        private static float Depth(int y) => (SiteLayout.Size.y - y) * SiteLayout.CellSize;
        private static bool InPlot(int x, int z) => SiteLayout.BeyondFootprint(new Vector2(
            SiteLayout.Origin.x + x * SiteLayout.CellSize, SiteLayout.Origin.z + z * SiteLayout.CellSize)) < 0;

        [TestCase(2718)] [TestCase(90127)]
        public void EachZoneIsMostlyItsMainGroundWithMixedEdgeBands(int seed)
        {
            var ids = Site(seed);
            // Per zone, the share of each family away from the bands, sampled every 4th column.
            var counts = new int[4, (int)TerrainMaterialSnapshot.Last + 1];
            int placeCracks = 0;
            var band = new HashSet<byte>[3];
            for (int b = 0; b < 3; b++) band[b] = new HashSet<byte>();
            for (int z = 0; z <= SiteLayout.Size.z; z += 4)
            for (int x = 0; x <= SiteLayout.Size.x; x += 4)
            {
                if (!InPlot(x, z)) continue;
                for (int y = 0; y <= SiteLayout.Size.y; y++)
                {
                    float depth = Depth(y);
                    byte id = ids[Index(x, y, z)];
                    int border = System.Array.FindIndex(TerrainGround.ZoneBorders, d => Mathf.Abs(depth - d) < .6f);
                    if (border >= 0) band[border].Add(id);
                    bool near = TerrainGround.ZoneBorders.Any(d => Mathf.Abs(depth - d) < TerrainGround.EdgeBand * .5f + TerrainGround.BorderWarp);
                    if (depth > 2 && !near) counts[TerrainGround.ZoneAt(depth), id]++;
                }
            }
            float Share(int zone, params TerrainMaterialId[] ids2)
            {
                int total = 0, hits = 0;
                for (int m = 0; m <= (int)TerrainMaterialSnapshot.Last; m++) total += counts[zone, m];
                foreach (var id in ids2) hits += counts[zone, (int)id];
                return hits / (float)total;
            }
            Assert.That(Share(0, TerrainMaterialId.Soil), Is.GreaterThan(.8f), "Zone 1 is soil.");
            Assert.That(Share(0, TerrainMaterialId.Gravel), Is.InRange(.02f, .15f), "Zone 1 holds gravel lenses.");
            Assert.That(Share(1, TerrainMaterialId.Clay), Is.GreaterThan(.8f), "Zone 2 is clay.");
            Assert.That(Share(1, TerrainMaterialId.PondClay), Is.GreaterThan(0), "Zone 2 holds clay basins.");
            for (int zone = 2; zone < 4; zone++)
            {
                Assert.That(Share(zone, TerrainMaterialId.Rock, TerrainMaterialId.FracturedRock, TerrainMaterialId.Crack), Is.GreaterThan(.65f), $"Zone {zone + 1} is rock.");
                Assert.That(Share(zone, TerrainMaterialId.FracturedRock, TerrainMaterialId.Crack), Is.InRange(.01f, .12f), $"Zone {zone + 1} rock carries cracks.");
                Assert.That(Share(zone, TerrainMaterialId.Clay, TerrainMaterialId.Gravel), Is.InRange(.08f, .3f), $"Zone {zone + 1} is veined.");
            }
            Assert.That(Share(2, TerrainMaterialId.Concrete), Is.GreaterThan(0), "Zone 3 holds waterworks concrete.");
            // Each band meets both neighbouring grounds within a metre of the border.
            Assert.That(band[0], Is.SupersetOf(new[] { (byte)TerrainMaterialId.Soil, (byte)TerrainMaterialId.Clay }));
            Assert.That(band[1], Is.SupersetOf(new[] { (byte)TerrainMaterialId.Clay, (byte)TerrainMaterialId.Rock }));
        }

        [TestCase(2718)] [TestCase(12)] [TestCase(991)]
        public void SoftPathLeadsThroughTheRockZone(int seed)
        {
            // Never a wall: soft ground (not rock, not concrete) connects the top of the rock zone
            // to its bottom under the plot, sampled every other lattice point.
            var ids = Site(seed);
            int top = SiteLayout.Size.y - Mathf.CeilToInt((TerrainGround.ZoneBorders[1] + TerrainGround.EdgeBand) / SiteLayout.CellSize);
            int bottom = SiteLayout.Size.y - Mathf.FloorToInt((TerrainGround.ZoneBorders[2] - TerrainGround.EdgeBand) / SiteLayout.CellSize);
            int sx = SiteLayout.Size.x / 2 + 1, sz = SiteLayout.Size.z / 2 + 1, sy = (top - bottom) / 2 + 1;
            var seen = new bool[sx * sy * sz];
            var queue = new Queue<int>();
            bool Soft(int x, int y, int z)
            {
                if (!InPlot(x * 2, z * 2)) return false;
                var id = (TerrainMaterialId)ids[Index(x * 2, top - y * 2, z * 2)];
                return id != TerrainMaterialId.Rock && id != TerrainMaterialId.Concrete;
            }
            for (int z = 0; z < sz; z++) for (int x = 0; x < sx; x++)
                if (Soft(x, 0, z)) { seen[x + sx * sz * 0 + z * sx] = true; queue.Enqueue(x + z * sx); }
            bool reached = false;
            while (queue.Count > 0 && !reached)
            {
                int node = queue.Dequeue(), y = node / (sx * sz), rest = node % (sx * sz), z = rest / sx, x = rest % sx;
                if (y == sy - 1) reached = true;
                foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
                {
                    int nx = x + dx, ny = y + dy, nz = z + dz;
                    if (nx < 0 || ny < 0 || nz < 0 || nx >= sx || ny >= sy || nz >= sz) continue;
                    int next = nx + nz * sx + ny * sx * sz;
                    if (seen[next] || !Soft(nx, ny, nz)) continue;
                    seen[next] = true; queue.Enqueue(next);
                }
            }
            Assert.That(reached, Is.True, $"Seed {seed}: no soft path through the rock zone.");
        }

        // Rock masses are criss-crossed by cracks and every concrete body carries some (concept 03 §5).
        [TestCase(2718)] [TestCase(12)]
        public void RockMassesAndConcreteCarryCracks(int seed)
        {
            var ids = Site(seed);
            foreach (var place in TerrainGround.Places(SiteLayout.Size, SiteLayout.CellSize, seed))
            {
                if (place.Kind == TerrainGround.PlaceKind.Basin || place.Kind == TerrainGround.PlaceKind.Rubble) continue;
                int cracked = 0, body = 0;
                var min = Vector3Int.FloorToInt((Vector3)place.Min / SiteLayout.CellSize);
                var max = Vector3Int.CeilToInt((Vector3)place.Max / SiteLayout.CellSize);
                for (int z = Mathf.Max(0, min.z); z <= Mathf.Min(SiteLayout.Size.z, max.z); z += 2)
                for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(SiteLayout.Size.y, max.y); y += 2)
                for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(SiteLayout.Size.x, max.x); x += 2)
                {
                    var id = (TerrainMaterialId)ids[Index(x, y, z)];
                    bool broken = id == TerrainMaterialId.FracturedRock || id == TerrainMaterialId.FracturedConcrete || id == TerrainMaterialId.Crack;
                    if (broken || id == (place.Kind == TerrainGround.PlaceKind.RockMass ? TerrainMaterialId.Rock : TerrainMaterialId.Concrete)) body++;
                    if (broken) cracked++;
                }
                Assert.That(cracked, Is.GreaterThan(0), $"Seed {seed}: {place.Kind} at depth {SiteLayout.Extent.y - place.Centre.y:F1} has no cracks.");
                Assert.That(cracked, Is.LessThan(body * .45f), $"Seed {seed}: {place.Kind} is more crack than body.");
            }
            // Cracks stay inside rock and concrete: never in the recent fill's soil or gravel.
            for (int z = 0; z <= SiteLayout.Size.z; z += 5)
            for (int x = 0; x <= SiteLayout.Size.x; x += 5)
            for (int y = SiteLayout.Size.y - Mathf.FloorToInt(TerrainGround.PlaceTop / SiteLayout.CellSize); y <= SiteLayout.Size.y; y++)
                Assert.That(ids[Index(x, y, z)], Is.Not.EqualTo((byte)TerrainMaterialId.Crack).And.Not.EqualTo((byte)TerrainMaterialId.FracturedRock));
        }

        [TestCase(2718)] [TestCase(12)] [TestCase(991)] [TestCase(5)]
        public void PlacesSitUnderThePlotInTheirZonesWithoutOverlapping(int seed)
        {
            var places = TerrainGround.Places(SiteLayout.Size, SiteLayout.CellSize, seed);
            Assert.That(places, Is.EqualTo(TerrainGround.Places(SiteLayout.Size, SiteLayout.CellSize, seed)), "Deterministic.");
            var extent = SiteLayout.Extent;
            foreach (var place in places)
            {
                float depth = extent.y - place.Centre.y;
                Assert.That(TerrainGround.ZoneAt(depth), Is.EqualTo(place.Zone));
                Assert.That(depth - place.HalfSize.y, Is.GreaterThanOrEqualTo(TerrainGround.PlaceTop - .001f));
                var centre = new Vector2(SiteLayout.Origin.x + place.Centre.x, SiteLayout.Origin.z + place.Centre.z);
                Assert.That(SiteLayout.BeyondFootprint(centre), Is.LessThan(0), place.Kind + " inside the plot");
                // Never a wall across the plot.
                Assert.That(Mathf.Max(place.HalfSize.x, place.HalfSize.z) * 2, Is.LessThan(10));
            }
            for (int i = 0; i < places.Length; i++)
            for (int j = i + 1; j < places.Length; j++)
                Assert.That(Unity.Mathematics.math.any(places[i].Min > places[j].Max) || Unity.Mathematics.math.any(places[j].Min > places[i].Max), Is.True);
            int Count(int zone, TerrainGround.PlaceKind kind) => places.Count(p => p.Zone == zone && p.Kind == kind);
            Assert.That(Count(0, TerrainGround.PlaceKind.Rubble), Is.GreaterThanOrEqualTo(5));
            Assert.That(Count(1, TerrainGround.PlaceKind.Structure), Is.GreaterThanOrEqualTo(1));
            Assert.That(Count(1, TerrainGround.PlaceKind.RockMass), Is.GreaterThanOrEqualTo(1));
            Assert.That(Count(1, TerrainGround.PlaceKind.Basin), Is.GreaterThanOrEqualTo(2));
            Assert.That(Count(2, TerrainGround.PlaceKind.Structure), Is.GreaterThanOrEqualTo(1));
            Assert.That(Count(2, TerrainGround.PlaceKind.RockMass), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void FullSiteGenerationStaysFastAndRepeatable()
        {
            TerrainMaterialSnapshot.Generate(new Vector3Int(16, 16, 16), .125f, 1); // compile the job
            var random = Random.state;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var first = TerrainMaterialSnapshot.Generate(SiteLayout.Size, SiteLayout.CellSize, 4242).ToArray();
            watch.Stop();
            Debug.Log($"Full-site ground generation: {watch.Elapsed.TotalMilliseconds:F0} ms.");
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(1.5), "Generation runs at every session start.");
            Assert.That(Random.state, Is.EqualTo(random));
            Assert.That(TerrainMaterialSnapshot.Generate(SiteLayout.Size, SiteLayout.CellSize, 4242).ToArray(), Is.EqualTo(first));
            Assert.That(first.Distinct().OrderBy(v => v), Is.EqualTo(Enumerable.Range(0, (int)TerrainMaterialSnapshot.Last + 1).Select(v => (byte)v)));
            // The first metre stays soil everywhere: first scrapes and the permanent bank.
            for (int z = 0; z <= SiteLayout.Size.z; z += 7)
            for (int x = 0; x <= SiteLayout.Size.x; x += 7)
            for (int y = SiteLayout.Size.y - 8; y <= SiteLayout.Size.y; y++)
                Assert.That(first[Index(x, y, z)], Is.EqualTo((byte)TerrainMaterialId.Soil));
        }
    }
}
