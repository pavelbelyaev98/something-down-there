using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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

        // The site admits grounds one at a time (106): today soil with backfill pits, nothing else.
        [Test]
        public void TheSiteIsSoilWithBackfillPits()
        {
            Assert.That(SiteLayout.GroundFor(SiteLayout.Size, SiteLayout.CellSize), Is.EqualTo(SiteLayout.Ground));
            Assert.That(SiteLayout.GroundFor(new Vector3Int(64, 64, 64), SiteLayout.CellSize), Is.EqualTo(TerrainGround.Features.None), "Fixtures stay plain soil.");
            var ids = TerrainMaterialSnapshot.Generate(SiteLayout.Size, SiteLayout.CellSize, 2718, null, SiteLayout.Ground).ToArray();
            Assert.That(ids.Distinct().OrderBy(v => v), Is.EqualTo(new[] { (byte)TerrainMaterialId.Soil, (byte)TerrainMaterialId.Backfill }));
            var layout = TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, 2718, null, SiteLayout.Ground);
            Assert.That(layout.Pits.Length, Is.EqualTo(3));
            Assert.That(layout.Pits.All(p => SiteLayout.Extent.y - p.Bottom.y < TerrainGround.ZoneBorders[0]), Is.True);
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
            var above = (Vector3)stashes[0].Centre + Vector3.up * (pocket.max.y + .3f);
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
                Assert.That((TerrainMaterialId)ids[Index(x, y, z)], Is.EqualTo(GroundLab.Bays[bay].Ground(0, 2f, 0)), GroundLab.Bays[bay].Name);
            }
            var unused = GroundLab.BayCentre(GroundLab.Bays.Length);
            Assert.That((TerrainMaterialId)ids[Index(Mathf.RoundToInt((unused.x - SiteLayout.Origin.x) / SiteLayout.CellSize), y,
                Mathf.RoundToInt((unused.y - SiteLayout.Origin.z) / SiteLayout.CellSize))], Is.EqualTo(TerrainMaterialId.Soil));
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
