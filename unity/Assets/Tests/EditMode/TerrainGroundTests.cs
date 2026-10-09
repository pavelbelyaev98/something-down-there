using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    // Concept 03 §4: soil with backfill pits someone dug and refilled, from the seed.
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

        // Concept 03 §4 disturbed ground (106): pits of backfill only in the recent fill, each holding a chest (user,
        // 2026-10-06); the first a few metres under the plot centre.
        [TestCase(2718)] [TestCase(12)] [TestCase(991)]
        public void BackfillPitsHoldOnlyChestsInTheRecentFill(int seed)
        {
            var layout = TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, seed);
            var pits = layout.Pits;
            Assert.That(pits.Length, Is.EqualTo(TerrainGround.PitCount), $"Seed {seed}: every pit placed.");
            Assert.That(pits.All(p => SiteLayout.Extent.y - p.Bottom.y < TerrainGround.ZoneBorders[0]), Is.True, "Only the recent fill.");
            var first = pits[0];
            Assert.That(SiteLayout.Extent.y - first.Bottom.y, Is.InRange(4f, 6f), "The first stash lies a few metres down.");
            Assert.That(new Vector2(first.Bottom.x - SiteLayout.Extent.x * .5f, first.Bottom.z - SiteLayout.Extent.z * .5f).magnitude,
                Is.LessThanOrEqualTo(3 * Mathf.Sqrt(2) + .01f), "Under the plot centre.");
            Assert.That(layout.Stashes.Length, Is.EqualTo(pits.Length), "A chest in every pit.");
            var ids = Site(seed);
            foreach (var pit in pits)
            {
                Assert.That(SiteLayout.Extent.y - pit.Top.y, Is.GreaterThanOrEqualTo(TerrainGround.SurfaceSoil), "Below the first scrapes.");
                Assert.That(pit.Top.y, Is.GreaterThan(pit.Bottom.y), "Dug from above.");
                var middle = Vector3Int.RoundToInt((Vector3)((pit.Top + pit.Bottom) * .5f) / SiteLayout.CellSize);
                Assert.That(ids[Index(middle.x, middle.y, middle.z)], Is.EqualTo((byte)TerrainMaterialId.Backfill), "The pit is backfill.");
            }
        }

        // The site admits grounds one at a time (106, 110, 111, 116): the zones' main grounds with backfill pits, geodes and
        // caves, nothing else.
        [Test]
        public void TheSiteHoldsZoneGroundsBackfillPitsGeodesAndCaves()
        {
            Assert.That(SiteLayout.GroundFor(SiteLayout.Size, SiteLayout.CellSize), Is.EqualTo(SiteLayout.Ground));
            Assert.That(SiteLayout.GroundFor(new Vector3Int(64, 64, 64), SiteLayout.CellSize), Is.EqualTo(TerrainGround.Features.None), "Fixtures stay plain soil.");
            var ids = TerrainMaterialSnapshot.Generate(SiteLayout.Size, SiteLayout.CellSize, 2718, null, SiteLayout.Ground).ToArray();
            Assert.That(ids.Distinct().OrderBy(v => v), Is.EqualTo(new[] { (byte)TerrainMaterialId.Soil, (byte)TerrainMaterialId.Backfill,
                (byte)TerrainMaterialId.GeodeShell, (byte)TerrainMaterialId.CaveRock, (byte)TerrainMaterialId.LakeSediment, (byte)TerrainMaterialId.Riverbed }));
            var layout = TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, 2718, null, SiteLayout.Ground);
            Assert.That(layout.Pits.Length, Is.EqualTo(3));
            Assert.That(layout.Pits.All(p => SiteLayout.Extent.y - p.Bottom.y < TerrainGround.ZoneBorders[0]), Is.True);
            Assert.That(layout.Geodes.Length, Is.EqualTo(TerrainGround.GeodesPerZone.Sum()));
        }

        // Concept 03 §3 zone grounds (111): each zone is mostly its own main ground (outside pits, geodes and caves); away from
        // a border by more than its warp and band only that ground lies; across a border the two mix in patches, and the
        // border's depth wanders from column to column, never a flat line.
        [TestCase(2718)] [TestCase(991)]
        public void EachZoneIsMostlyItsMainGroundWithPatchyWarpedBorders(int seed)
        {
            var ids = Site(seed);
            var borders = TerrainGround.ZoneBorders;
            int[] main = new int[4], total = new int[4];
            var near = new HashSet<byte>[borders.Length];
            for (int b = 0; b < near.Length; b++) near[b] = new HashSet<byte>();
            float reach = TerrainGround.BorderWarp + TerrainGround.BorderBand;
            for (int z = 0; z <= SiteLayout.Size.z; z += 6)
            for (int x = 0; x <= SiteLayout.Size.x; x += 6)
            for (int y = 0; y <= SiteLayout.Size.y; y += 2)
            {
                float depth = (SiteLayout.Size.y - y) * SiteLayout.CellSize;
                var id = (TerrainMaterialId)ids[Index(x, y, z)];
                if (depth < TerrainGround.SurfaceSoil || id == TerrainMaterialId.Backfill || id == TerrainMaterialId.GeodeShell
                    || id == TerrainMaterialId.CaveRock) continue;
                int zone = borders.Count(b => depth >= b);
                total[zone]++;
                if (id == TerrainGround.ZoneGround(zone)) main[zone]++;
                float nearest = borders.Min(b => Mathf.Abs(depth - b));
                if (nearest > reach) Assert.That(id, Is.EqualTo(TerrainGround.ZoneGround(zone)), $"Zone {zone + 1} at {depth:F1} m");
                for (int b = 0; b < borders.Length; b++) if (Mathf.Abs(depth - borders[b]) < .5f) near[b].Add((byte)id);
            }
            for (int zone = 0; zone < total.Length; zone++)
                Assert.That(main[zone] / (float)total[zone], Is.GreaterThan(.9f), $"Zone {zone + 1} is mostly {TerrainGround.ZoneGround(zone)}");
            for (int b = 0; b < borders.Length; b++)
                if (TerrainGround.ZoneGround(b) != TerrainGround.ZoneGround(b + 1))
                    Assert.That(near[b], Is.EquivalentTo(new[] { (byte)TerrainGround.ZoneGround(b), (byte)TerrainGround.ZoneGround(b + 1) }), $"Border {b + 1} mixes.");
            var offsets = TerrainGround.NoiseOffsets(seed);
            var levels = new List<float>();
            for (float x = 0; x < SiteLayout.Extent.x; x += 2)
            for (float z = 0; z < SiteLayout.Extent.z; z += 2)
                levels.Add(TerrainGround.WarpedBorders(new float2(x, z), new float3(borders[0], borders[1], borders[2]), offsets).x);
            Assert.That(levels.Max() - levels.Min(), Is.InRange(1f, 2 * TerrainGround.BorderWarp), "The border wanders.");
        }

        // Concept 03 §5 geodes (110): two in the old lake sediment and three in the old riverbed, each wholly inside its
        // zone and the find footprint, clear of the pits, the uniques' spaces and each other; the same from the same seed.
        [TestCase(2718)] [TestCase(12)] [TestCase(991)]
        public void GeodesLieInTheMiddleZonesClearOfPitsSpotsAndEachOther(int seed)
        {
            var spots = TerrainGround.OddSpots(new[] { new Vector4(20, SiteLayout.Extent.y - 50, 20, 1) });
            var layout = TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, seed, new[] { new Vector4(20, SiteLayout.Extent.y - 50, 20, 1) }, SiteLayout.Ground);
            var geodes = layout.Geodes;
            Assert.That(geodes.Length, Is.EqualTo(TerrainGround.GeodesPerZone.Sum()), $"Seed {seed}: every geode placed.");
            var footprint = SiteLayout.FindFootprint(SiteLayout.Extent);
            for (int zone = 0; zone < TerrainGround.GeodesPerZone.Length; zone++)
            {
                float top = zone == 0 ? 0 : TerrainGround.ZoneBorders[zone - 1];
                float bottom = zone < TerrainGround.ZoneBorders.Length ? TerrainGround.ZoneBorders[zone] : SiteLayout.Extent.y;
                var inZone = geodes.Where(g => SiteLayout.Extent.y - g.Centre.y >= top && SiteLayout.Extent.y - g.Centre.y < bottom).ToArray();
                Assert.That(inZone.Length, Is.EqualTo(TerrainGround.GeodesPerZone[zone]), $"Zone {zone + 1}");
                foreach (var g in inZone)
                {
                    Assert.That(SiteLayout.Extent.y - g.Centre.y - g.Reach, Is.GreaterThan(top), "Wholly inside its zone.");
                    Assert.That(SiteLayout.Extent.y - g.Centre.y + g.Reach, Is.LessThan(bottom), "Wholly inside its zone.");
                    Assert.That(footprint(new Vector2(g.Centre.x, g.Centre.z)), Is.True, "Under the dig plot.");
                }
            }
            foreach (var g in geodes)
            {
                foreach (var pit in layout.Pits) Assert.That(math.any(g.Min > pit.Max) || math.any(pit.Min > g.Max), Is.True, "Clear of the pits.");
                foreach (var spot in spots) Assert.That(math.any(g.Min > spot.Max) || math.any(spot.Min > g.Max), Is.True, "Clear of the uniques.");
                foreach (var other in geodes.Where(o => !o.Centre.Equals(g.Centre)))
                    Assert.That(math.distance(g.Centre, other.Centre), Is.GreaterThan(g.Reach + other.Reach), "Apart.");
            }
            var again = TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, seed, new[] { new Vector4(20, SiteLayout.Extent.y - 50, 20, 1) }, SiteLayout.Ground).Geodes;
            Assert.That(again.Select(g => g.Centre), Is.EqualTo(geodes.Select(g => g.Centre)), "From the seed.");
        }

        // The catalog's three computers (envelope 0.9 m) in the recent fill, plus one in the lake sediment and one in the
        // riverbed so every lens ground is laid.
        private static readonly Vector4[] Uniques =
        {
            new Vector4(26.5f, 142, 4.7f, .9f), new Vector4(20, 136.5f, 11, .9f), new Vector4(31, 129, 18, .9f),
            new Vector4(25, 100, 13, .9f), new Vector4(18, 62, 9, .9f)
        };

        // Concept 03 §4 lenses (112): every unique lies in a lens of another zone's ground, the odd one out: its envelope and a
        // margin wholly in it, the lens several metres across and thickest at the unique; pits and geodes keep clear of it;
        // the same from the same seed.
        [TestCase(2718)] [TestCase(12)] [TestCase(991)]
        public void EachUniqueLiesInALensOfAnotherZonesGround(int seed)
        {
            var ids = TerrainMaterialSnapshot.Generate(SiteLayout.Size, SiteLayout.CellSize, seed, Uniques, SiteLayout.Ground).ToArray();
            var layout = TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, seed, Uniques, SiteLayout.Ground);
            Assert.That(layout.Lenses.Length, Is.EqualTo(Uniques.Length), "A lens round every unique.");
            var offsets = TerrainGround.NoiseOffsets(seed);
            var borders = new float3(TerrainGround.ZoneBorders[0], TerrainGround.ZoneBorders[1], TerrainGround.ZoneBorders[2]);
            const float cell = SiteLayout.CellSize;
            var grounds = new HashSet<TerrainMaterialId>();
            for (int i = 0; i < Uniques.Length; i++)
            {
                var lens = layout.Lenses[i];
                var centre = (float3)(Vector3)Uniques[i];
                var host = TerrainGround.ZoneGround(TerrainGround.ZoneAt(centre, SiteLayout.Extent.y - centre.y, borders, offsets));
                Assert.That(lens.Ground, Is.Not.EqualTo(host), $"Unique {i}: another zone's ground.");
                grounds.Add(lens.Ground);
                float reach = Uniques[i].w + .25f;
                var middle = Vector3Int.RoundToInt((Vector3)centre / cell);
                int n = Mathf.CeilToInt(reach / cell), outside = 0;
                for (int z = -n; z <= n; z++)
                for (int y = -n; y <= n; y++)
                for (int x = -n; x <= n; x++)
                    if (new Vector3(x, y, z).magnitude * cell <= reach && ids[Index(middle.x + x, middle.y + y, middle.z + z)] != (byte)lens.Ground) outside++;
                Assert.That(outside, Is.Zero, $"Unique {i}: its envelope lies wholly in its lens.");
                // Its thickness along the long axis, out to the side that stays inside the grid.
                float side = centre.x + lens.Axis.x * lens.Radii.x > 1 && centre.x + lens.Axis.x * lens.Radii.x < SiteLayout.Extent.x - 1
                    && centre.z + lens.Axis.y * lens.Radii.x > 1 && centre.z + lens.Axis.y * lens.Radii.x < SiteLayout.Extent.z - 1 ? 1 : -1;
                float Thickness(float share)
                {
                    var at = Vector3Int.RoundToInt(new Vector3(centre.x + side * lens.Axis.x * lens.Radii.x * share, 0, centre.z + side * lens.Axis.y * lens.Radii.x * share) / cell);
                    int count = 0;
                    for (int y = Mathf.FloorToInt(lens.Min.y / cell); y <= Mathf.CeilToInt(lens.Max.y / cell); y++)
                        if (ids[Index(at.x, y, at.z)] == (byte)lens.Ground) count++;
                    return count * cell;
                }
                float thickest = Thickness(0);
                Assert.That(thickest, Is.GreaterThan(2 * Uniques[i].w + .5f), $"Unique {i}: ground left above and below it.");
                Assert.That(Thickness(.6f), Is.GreaterThan(0), $"Unique {i}: the lens reaches metres sideways.");
                Assert.That(Thickness(.8f), Is.LessThan(thickest * .7f), $"Unique {i}: thinner toward the rim.");
                foreach (var pit in layout.Pits) Assert.That(math.any(lens.Min > pit.Max) || math.any(pit.Min > lens.Max), Is.True, "Pits keep clear.");
                foreach (var geode in layout.Geodes) Assert.That(math.any(lens.Min > geode.Max) || math.any(geode.Min > lens.Max), Is.True, "Geodes keep clear.");
            }
            Assert.That(grounds, Is.EquivalentTo(new[] { TerrainMaterialId.Riverbed, TerrainMaterialId.LakeSediment }), "River gravel, and lake silt in the riverbed.");
            var again = TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, seed, Uniques, SiteLayout.Ground).Lenses;
            Assert.That(again.Select(l => (l.Centre, l.Radii, l.Axis, l.Dip)), Is.EqualTo(layout.Lenses.Select(l => (l.Centre, l.Radii, l.Axis, l.Dip))), "From the seed.");
        }

        // Concept 03 §5 (116): a great cave in every zone, the pits standing clear of its chambers. The halls are laid before
        // the pits; laid after them, the first zone's hall was missing in most seeds.
        [Test]
        public void EveryZoneGetsAGreatCaveWithThePitsClearOfIt()
        {
            var spots = new[] { Uniques[0], Uniques[1], Uniques[2] };
            for (int seed = 100; seed < 112; seed++)
            {
                var layout = TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, seed, spots, SiteLayout.Ground);
                var halls = layout.Caverns.Where(c => c.Great).ToArray();
                Assert.That(halls.Length, Is.EqualTo(TerrainGround.ZoneBorders.Length + 1), $"Seed {seed}: a great cave in every zone.");
                Assert.That(layout.Pits.Length, Is.EqualTo(TerrainGround.PitCount), $"Seed {seed}: every pit placed.");
                foreach (var pit in layout.Pits)
                    Assert.That(halls.Any(h => TerrainGround.NearHall(h, pit.Min, pit.Max, 0)), Is.False, $"Seed {seed}: pits clear of the halls.");
            }
        }

        // A geode's hollow is sealed in its shell: everything inside its outer face is shell stone (its hollow's samples
        // too, so the hollow's walls read as shell), and every point a little outside the hollow lies in that stone.
        [Test]
        public void GeodeHollowsAreSealedInTheirShell()
        {
            const float cell = .125f;
            var size = new Vector3Int(48, 48, 48);
            var geode = TerrainGround.Make(new float3(3, 3, 3), new float3(1.3f, 1f, 1.2f), .6f, .7f, .26f);
            var ids = new byte[(size.x + 1) * (size.y + 1) * (size.z + 1)];
            TerrainGround.FillGeode(ids, size, cell, geode);
            int hollow = 0, wall = 0;
            for (int z = 0; z <= size.z; z++)
            for (int y = 0; y <= size.y; y++)
            for (int x = 0; x <= size.x; x++)
            {
                var p = new float3(x, y, z) * cell;
                float inside = TerrainGround.HollowDistance(geode, p);
                var id = (TerrainMaterialId)ids[x + y * (size.x + 1) + z * (size.x + 1) * (size.y + 1)];
                if (inside < 0) { hollow++; Assert.That(id, Is.EqualTo(TerrainMaterialId.GeodeShell), "The hollow's samples read as shell."); }
                else if (inside < .3f) { wall++; Assert.That(id, Is.EqualTo(TerrainMaterialId.GeodeShell), $"Shell all round the hollow at {p}."); }
                if (math.length(p - geode.Centre) > geode.Reach + cell) Assert.That(id, Is.EqualTo(TerrainMaterialId.Soil), "Nothing beyond its reach.");
            }
            Assert.That(hollow, Is.GreaterThan(1000), "Room to crouch in.");
            Assert.That(wall, Is.GreaterThan(1000));
        }

        // A stash chest's pocket is seeded air, closed on every side, with a flat floor of ground under the chest's base.
        [Test]
        public void StashPocketsAreClosedSeededAir()
        {
            var pocket = new Bounds(new Vector3(0, .5f, 0), new Vector3(1.4f, 1.04f, 2f));
            var grid = new ExcavationGrid(new Vector3Int(112, 320, 112), .125f, 77, null, TerrainGround.Features.Pits, pocket);
            var stashes = grid.Layout.Stashes;
            Assert.That(stashes.Length, Is.GreaterThan(0));
            Assert.That(grid.RemovedVolume, Is.Zero, "Seeded air is not a cut.");
            foreach (var stash in stashes)
            {
                Assert.That(stash.HasPocket, Is.True);
                var turn = (Quaternion)stash.Rotation;
                var centre = (Vector3)stash.Centre + turn * pocket.center;
                Assert.That(grid.IsSolid(centre), Is.False, "The chest's pocket holds air.");
                Assert.That(grid.IsSolid(centre - Vector3.up * (pocket.extents.y + .1f)), Is.True, "Ground under the chest.");
                foreach (var corner in new[] { new Vector3(-.35f, 0, -.6f), new Vector3(.35f, 0, .6f), new Vector3(.35f, 0, -.6f) })
                {
                    var floor = (Vector3)stash.Centre + turn * (corner + Vector3.up * pocket.min.y);
                    Assert.That(grid.IsSolid(floor - Vector3.up * .05f), Is.True, "The chest's footing stays ground.");
                    Assert.That(grid.IsSolid(floor + Vector3.up * .08f), Is.False, "A flat floor under the chest.");
                }
                // Its walls, floor and roof are fill all round (user, 2026-10-06).
                foreach (var side in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back, Vector3.up, Vector3.down })
                {
                    var wall = Vector3Int.RoundToInt(((Vector3)stash.Centre + turn * (pocket.center + Vector3.Scale(side, pocket.extents + Vector3.one * .25f))) / .125f);
                    Assert.That(grid.MaterialAt(wall.x, wall.y, wall.z), Is.EqualTo(TerrainMaterialId.Backfill), $"Fill {side} of the pocket.");
                }
                var start = Vector3Int.RoundToInt(centre / .125f);
                var seen = new HashSet<Vector3Int> { start };
                var queue = new Queue<Vector3Int>(); queue.Enqueue(start);
                var low = Vector3Int.FloorToInt((Vector3)stash.Min / .125f) - Vector3Int.one * 2;
                var high = Vector3Int.CeilToInt((Vector3)stash.Max / .125f) + Vector3Int.one * 2;
                while (queue.Count > 0)
                {
                    var s = queue.Dequeue();
                    Assert.That(s.x > low.x && s.y > low.y && s.z > low.z && s.x < high.x && s.y < high.y && s.z < high.z, Is.True, "The pocket leaks.");
                    foreach (var step in new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) })
                    {
                        var n = s + step;
                        if (seen.Contains(n) || grid.Sample(n.x, n.y, n.z) > 0) continue;
                        seen.Add(n); queue.Enqueue(n);
                    }
                }
                Assert.That(seen.Count, Is.GreaterThan(100), "A chest's worth of air.");
            }
            // Above the pocket's uneven dome (113), in the fill.
            var dome = TerrainGround.PocketDomeReserve(stashes[0]);
            var above = (Vector3)dome.centre + Vector3.up * (dome.radius + .4f);
            Assert.That(grid.IsSolid(above), Is.True, "Fill above the pocket's dome.");
            grid.RemoveSphere(above, .3f, out _);
            Assert.That(grid.IsSolid(above), Is.False);
            grid.Reset();
            Assert.That(grid.IsSolid(above), Is.True, "Reset refills above the chest.");
            Assert.That(grid.IsSolid((Vector3)stashes[0].Centre + (Quaternion)stashes[0].Rotation * pocket.center), Is.False, "Reset carves the pocket again.");
        }

        // The Ground Lab lays every bay out in its own ground on the shipped grid; unused slots and the ground below stay soil.
        [Test]
        public void GroundLabLaysOutEveryBay()
        {
            var ids = GroundLab.Materials(SiteLayout.Size, SiteLayout.CellSize).ToArray();
            int y = SiteLayout.Size.y - Mathf.RoundToInt(2f / SiteLayout.CellSize);
            for (int bay = 0; bay < GroundLab.Bays.Length; bay++)
            {
                var centre = GroundLab.BayCentre(bay);
                int x = Mathf.RoundToInt((centre.x - SiteLayout.Origin.x) / SiteLayout.CellSize);
                int z = Mathf.RoundToInt((centre.y - SiteLayout.Origin.z) / SiteLayout.CellSize);
                float u = SiteLayout.Origin.x + x * SiteLayout.CellSize - centre.x, v = SiteLayout.Origin.z + z * SiteLayout.CellSize - centre.y;
                // The geode bay's middle at 2 m is its geode's stone.
                var expected = GroundLab.Bays[bay].Name == "Geode" ? TerrainMaterialId.GeodeShell : GroundLab.Bays[bay].Ground(u, 2f, v);
                Assert.That((TerrainMaterialId)ids[Index(x, y, z)], Is.EqualTo(expected), GroundLab.Bays[bay].Name);
            }
            Assert.That(GroundLab.Bays.Select(b => b.Slot).Distinct().Count(), Is.EqualTo(GroundLab.Bays.Length), "One bay a slot.");
            // The zone borders bay runs from soil through lake sediment into the riverbed.
            var borders = System.Array.Find(GroundLab.Bays, b => b.Name == "Zone borders");
            Assert.That(borders.Ground(0, 1.5f, 0), Is.EqualTo(TerrainMaterialId.Soil));
            Assert.That(borders.Ground(0, 5.75f, 0), Is.EqualTo(TerrainMaterialId.LakeSediment));
            Assert.That(borders.Ground(0, 11.5f, 0), Is.EqualTo(TerrainMaterialId.Riverbed));
            var unused = GroundLab.SlotCentre(12);
            Assert.That((TerrainMaterialId)ids[Index(Mathf.RoundToInt((unused.x - SiteLayout.Origin.x) / SiteLayout.CellSize), y,
                Mathf.RoundToInt((unused.y - SiteLayout.Origin.z) / SiteLayout.CellSize))], Is.EqualTo(TerrainMaterialId.Soil));
        }

        [Test]
        public void FullSiteGenerationStaysFastAndRepeatable()
        {
            TerrainMaterialSnapshot.Generate(new Vector3Int(16, 16, 16), .125f, 1); // compile the job
            var random = UnityEngine.Random.state;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var first = TerrainMaterialSnapshot.Generate(SiteLayout.Size, SiteLayout.CellSize, 4242).ToArray();
            watch.Stop();
            Debug.Log($"Full-site ground generation: {watch.Elapsed.TotalMilliseconds:F0} ms.");
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(1.5), "Generation runs at every session start.");
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
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
