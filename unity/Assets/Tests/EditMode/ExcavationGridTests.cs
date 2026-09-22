using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class ExcavationGridTests
    {
        [TestCase(0f, 1f, 0f)] [TestCase(1f, 0f, 0f)] [TestCase(1f, 1f, 1f)]
        public void ShavingAdvancesBelowVoxelSizeAndPreservesNearbySupport(float x, float y, float z)
        {
            var normal = new Vector3(x, y, z).normalized;
            var origin = new Vector3(2, 2, 2);
            var grid = RemnantFixture(p => -Vector3.Dot(p - origin, normal));
            float previousVolume = 0;
            for (int cut = 0; cut < 20; cut++)
            {
                Vector3 inside = origin - normal * 1.5f, outside = origin + normal;
                for (int i = 0; i < 16; i++)
                {
                    Vector3 middle = (inside + outside) * .5f;
                    if (grid.Sample(middle) > 0) inside = middle; else outside = middle;
                }
                Vector3 surface = (inside + outside) * .5f;
                Assert.That(grid.RemoveShave(surface, .35f, normal, .035f, out _), Is.True);
                Assert.That(grid.RemovedVolume, Is.GreaterThan(previousVolume));
                previousVolume = grid.RemovedVolume;
                Assert.That(grid.RemoveShave(surface, .35f, normal, .035f, out _), Is.False,
                    "Replaying a fixed brush must not mine additional soil.");
            }
            Assert.That(grid.IsSolid(origin - normal * .3f), Is.False);
            Assert.That(grid.IsSolid(origin - normal * 1.2f), Is.True);
            Assert.That(grid.Revision, Is.EqualTo(20));
            AssertEverySolidSampleHasSupport(grid);
            var saved = grid.Capture();
            var restored = new ExcavationGrid(grid.Size, grid.CellSize);
            restored.Restore(saved);
            CollectionAssert.AreEqual(ReadSamples(grid), ReadSamples(restored));
        }

        [Test]
        public void InvalidShavingCannotChangeTheGrid()
        {
            var grid = new ExcavationGrid(new Vector3Int(16, 16, 16), .125f);
            foreach (float depth in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, 1f })
                Assert.That(grid.RemoveShave(new Vector3(1, 2, 1), .3f, Vector3.up, depth, out _), Is.False);
            Assert.That(grid.Revision, Is.Zero);
            Assert.That(grid.RemovedVolume, Is.Zero);
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(1f, 1f, 0f)]
        public void NarrowingAnAttachedSpikeClearsItsUncutTipInTheSameStroke(float x, float y, float z)
        {
            var normal = new Vector3(x, y, z).normalized;
            var tangent = Vector3.Cross(normal, Vector3.forward).normalized;
            var origin = new Vector3(2, 2, 2);
            var grid = RemnantFixture(p =>
            {
                Vector3 delta = p - origin;
                float h = Vector3.Dot(delta, normal), u = Vector3.Dot(delta, tangent);
                return Mathf.Max(-h, Mathf.Min(0.16f - Mathf.Abs(u), 0.16f - Mathf.Abs(delta.z), 0.375f - h, h + 0.05f));
            });
            var tip = origin + normal * 0.25f - tangent * 0.0625f;
            var cut = origin + tangent * 0.25f + normal * 0.2f;
            Assert.That(grid.IsSolid(tip), Is.True);
            Assert.That(Vector3.Distance(tip, cut), Is.GreaterThan(0.225f), "The brush itself cannot remove this tip.");
            float[] before = ReadSamples(grid);
            Assert.That(grid.RemoveSphere(cut, 0.225f, out var changed), Is.True);
            Assert.That(grid.IsSolid(tip), Is.False, "The attached remnant must disappear without another hit.");
            Assert.That(grid.LastRemnantSamples, Is.GreaterThan(0));
            Assert.That(grid.LastRemnantVolume, Is.GreaterThan(0));
            Assert.That(grid.LastDetachedSamples, Is.Zero, "This was connected soil, already outside Task 26's remit.");
            Assert.That(grid.IsSolid(origin - normal * 0.25f), Is.True);
            Assert.That(changed.Contains(Vector3Int.RoundToInt(tip / grid.CellSize)), Is.True);
            float measured = 0;
            int index = 0;
            for (int sz = 0; sz <= 32; sz++) for (int sy = 0; sy <= 32; sy++) for (int sx = 0; sx <= 32; sx++, index++)
            {
                float after = grid.Sample(sx, sy, sz);
                Assert.That(after, Is.LessThanOrEqualTo(before[index]), "Cleanup must never refill the excavation.");
                measured += (Mathf.Clamp01(0.5f + before[index] / 0.125f) - Mathf.Clamp01(0.5f + after / 0.125f))
                    * 0.125f * 0.125f * 0.125f;
            }
            Assert.That(grid.LastRemovedVolume, Is.EqualTo(measured).Within(0.00001f));
            Assert.That(grid.Revision, Is.EqualTo(1));
            float[] settled = ReadSamples(grid);
            Assert.That(grid.RemoveSphere(cut, 0.225f, out _), Is.False);
            CollectionAssert.AreEqual(settled, ReadSamples(grid), "The same cut cannot gradually erode retained soil.");
            Assert.That(grid.Revision, Is.EqualTo(1));
            AssertEverySolidSampleHasSupport(grid);
            grid.Reset();
            Assert.That(grid.LastRemnantSamples, Is.Zero);
            Assert.That(grid.RemovedVolume, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CleanupPreservesBroadThinLedgesAndAThinNeckBetweenLargerSupports(bool bridge)
        {
            var grid = RemnantFixture(p =>
            {
                Vector3 d = p - new Vector3(2, 2, 2);
                float floor = (bridge ? 1.75f : 1.5f) - p.y;
                if (!bridge)
                    return Mathf.Max(floor, Mathf.Min(0.08f - Mathf.Abs(d.x), 0.9f - Mathf.Abs(d.z), 0.5f - d.y));
                float neck = Mathf.Min(0.08f - Mathf.Abs(d.x), 0.08f - Mathf.Abs(d.z), 0.125f - d.y);
                float crown = Mathf.Min(0.5f - Mathf.Abs(d.x), 0.5f - Mathf.Abs(d.z), 0.5f - Mathf.Abs(d.y - 0.5f));
                return Mathf.Max(floor, neck, crown);
            });
            var witness = new Vector3(2, 2, 2);
            Assert.That(grid.IsSolid(witness), Is.True);
            Assert.That(grid.RemoveSphere(new Vector3(2.22f, 2, 2), 0.16f, out _), Is.True);
            Assert.That(grid.IsSolid(witness), Is.True, "A large thin sheet or two-support neck must not be classified as a tiny tip.");
            if (bridge) Assert.That(grid.IsSolid(new Vector3(2, 2.5f, 2)), Is.True, "Do not collapse a useful supported crown.");
            AssertEverySolidSampleHasSupport(grid);
        }

        [TestCase(false)] [TestCase(true)]
        public void AStrokeClearsLongPaperThinStripsIncludingBothEndAttachments(bool diagonal)
        {
            Vector3 normal=diagonal ? new Vector3(1,1,0).normalized : Vector3.up;
            var origin=new Vector3(2,2,2);
            var grid=RemnantFixture(p =>
            {
                var d=p-origin;
                float wall=Mathf.Abs(d.z)-.8f;
                float strip=Mathf.Min(.012f-Mathf.Abs(Vector3.Dot(d,normal)), .16f-Mathf.Abs(d.x), .85f-Mathf.Abs(d.z));
                return Mathf.Max(wall,strip);
            });
            Assert.That(grid.IsSolid(origin), Is.True);
            var witness=origin+Vector3.forward*.5f;
            float[] before=ReadSamples(grid);
            Assert.That(grid.RemoveSphere(origin+normal*.09f-Vector3.forward*.5f,.12f,out var changed), Is.True);
            Assert.That(grid.IsSolid(witness), Is.False, "The uncut length of a paper-thin strip must crumble too.");
            Assert.That(grid.LastRemnantSamples, Is.GreaterThan(0));
            Assert.That(changed.Contains(Vector3Int.RoundToInt(witness/grid.CellSize)), Is.True);
            Assert.That(grid.IsSolid(origin+Vector3.forward), Is.True, "Solid wall attachments remain.");
            var after=ReadSamples(grid);
            float volume=0;
            for(int i=0;i<after.Length;i++)
            {
                Assert.That(after[i], Is.LessThanOrEqualTo(before[i]));
                volume+=(Mathf.Clamp01(.5f+before[i]/grid.CellSize)-Mathf.Clamp01(.5f+after[i]/grid.CellSize))
                    *grid.CellSize*grid.CellSize*grid.CellSize;
            }
            Assert.That(grid.LastRemovedVolume, Is.EqualTo(volume).Within(.00001f));
            Assert.That(grid.Revision, Is.EqualTo(1));
            var restored=new ExcavationGrid(grid.Size,grid.CellSize);restored.Restore(grid.Capture());
            CollectionAssert.AreEqual(after,ReadSamples(restored));
            AssertEverySolidSampleHasSupport(grid);
        }

        // Controlled density fixtures represent already-excavated spaces, not new
        // runtime authoring APIs. Assertions concern clearance/support/accounting.
        private static ExcavationGrid RemnantFixture(Func<Vector3, float> shape)
        {
            var grid = new ExcavationGrid(new Vector3Int(32, 32, 32), 0.125f);
            var snapshot = grid.Capture();
            var samples = snapshot.Density.ToArray();
            snapshot.LowestCarvedY = 0;
            for (int z = 0; z <= 32; z++) for (int y = 0; y <= 32; y++) for (int x = 0; x <= 32; x++)
                samples[x + y * 33 + z * 33 * 33] = Mathf.Clamp(shape(new Vector3(x, y, z) * 0.125f), -0.25f, 0.25f);
            snapshot.Density = DensitySnapshot.CopyFrom(samples);
            grid.Restore(snapshot);
            return grid;
        }

        private static float[] ReadSamples(ExcavationGrid grid)
        {
            var samples = new float[33 * 33 * 33];
            for (int z = 0; z <= 32; z++) for (int y = 0; y <= 32; y++) for (int x = 0; x <= 32; x++)
                samples[x + y * 33 + z * 33 * 33] = grid.Sample(x, y, z);
            return samples;
        }

        [TestCase(4f, false)]
        [TestCase(0.2f, true)]
        public void CuttingTheLastSupportRemovesAnIslandButKeepsSideAnchoredSoil(float centerX, bool sideAnchored)
        {
            var grid = new ExcavationGrid(new Vector3Int(40, 40, 40), 0.2f);
            foreach (float y in new[] { 7.7f, 6.5f, 5.3f })
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                grid.RemoveSphere(new Vector3(centerX + Mathf.Cos(angle) * 1.5f, y,
                    4 + Mathf.Sin(angle) * 1.5f), 0.84f, out _);
            }
            var crown = new Vector3(centerX, 7.4f, 4);
            Assert.That(grid.IsSolid(crown), Is.True, "The remaining pillar still supports its crown.");
            float before = grid.RemovedVolume;
            int revision = grid.Revision;
            Assert.That(grid.RemoveSphere(new Vector3(centerX, 5, 4), 1.45f, out var changed), Is.True);
            Assert.That(grid.IsSolid(crown), Is.EqualTo(sideAnchored));
            Assert.That(grid.Revision, Is.EqualTo(revision + 1), "Detachment is part of the same stroke.");
            Assert.That(grid.RemovedVolume - before, Is.EqualTo(grid.LastRemovedVolume).Within(0.0001f));
            if (!sideAnchored)
            {
                Assert.That(grid.LastDetachedSamples, Is.GreaterThan(0));
                Assert.That(grid.LastDetachedVolume, Is.GreaterThan(0));
                Assert.That(changed.Contains(Vector3Int.RoundToInt(crown / grid.CellSize)), Is.True,
                    "The dirty region includes the crown well beyond the shovel brush.");
            }
            Assert.That(grid.IsSolid(new Vector3(4, 1, 4)), Is.True);
            AssertEverySolidSampleHasSupport(grid);
            grid.Reset();
            Assert.That(grid.IsSolid(crown), Is.True);
            Assert.That(grid.LastDetachedSamples, Is.Zero);
            Assert.That(grid.RemovedVolume, Is.Zero);
        }

        [Test]
        public void IrregularCutsNeverLeaveUnsupportedSamplesAndFreshCutsStayLocal()
        {
            var grid = new ExcavationGrid(new Vector3Int(40, 32, 40), 0.2f);
            grid.RemoveScoop(new Vector3(4, 6.3f, 4), 0.7f, 71, 0.12f, out _);
            Assert.That(grid.LastDetachedSamples, Is.Zero);
            Assert.That(grid.LastSupportVisitedSamples, Is.InRange(1, 6000),
                "A fresh shallow dig must not flood the entire site.");
            var random = new System.Random(2718);
            for (int i = 0; i < 80; i++)
            {
                grid.RemoveScoop(new Vector3(1 + (float)random.NextDouble() * 6,
                    1 + (float)random.NextDouble() * 5, 1 + (float)random.NextDouble() * 6),
                    0.5f + (float)random.NextDouble() * 0.7f, i, 0.12f, out _);
                AssertEverySolidSampleHasSupport(grid);
            }
        }

        // Independent whole-field oracle: flood from permanent anchors, rather than
        // using the runtime's incremental searches and untouched-layer shortcut.
        private static void AssertEverySolidSampleHasSupport(ExcavationGrid grid)
        {
            var supported = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            for (int z = 0; z <= grid.Size.z; z++)
            for (int y = 0; y <= grid.Size.y; y++)
            for (int x = 0; x <= grid.Size.x; x++)
            {
                if (x != 0 && x != grid.Size.x && z != 0 && z != grid.Size.z && y != 0) continue;
                var p = new Vector3Int(x, y, z);
                if (grid.Sample(x, y, z) > 0 && supported.Add(p)) queue.Enqueue(p);
            }
            var directions = new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up,
                Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var step in directions)
                {
                    var q = p + step;
                    if (q.x < 0 || q.y < 0 || q.z < 0 || q.x > grid.Size.x || q.y > grid.Size.y || q.z > grid.Size.z) continue;
                    if (grid.Sample(q.x, q.y, q.z) > 0 && supported.Add(q)) queue.Enqueue(q);
                }
            }
            for (int z = 0; z <= grid.Size.z; z++)
            for (int y = 0; y <= grid.Size.y; y++)
            for (int x = 0; x <= grid.Size.x; x++)
                if (grid.Sample(x, y, z) > 0 && !supported.Contains(new Vector3Int(x, y, z)))
                    Assert.Fail($"Unsupported soil remains at {x}, {y}, {z}.");
        }

        [Test]
        public void RoundedCutsSupportDownwardDiagonalAndLateralRemovalWithAnOverhang()
        {
            var grid = new ExcavationGrid(new Vector3Int(40, 30, 40), 0.2f);
            Assert.That(grid.RemovedVolume, Is.Zero);
            Vector3[] cuts = { new Vector3(2, 5.8f, 2), new Vector3(2, 4.8f, 2),
                new Vector3(3, 3.8f, 2), new Vector3(4, 3.8f, 2) };
            foreach (Vector3 cut in cuts)
            {
                Assert.That(grid.RemoveSphere(cut, 1.1f, out var changed), Is.True);
                Assert.That(changed.Contains(Vector3Int.FloorToInt(cut / grid.CellSize)), Is.True);
                Assert.That(grid.IsSolid(cut), Is.False);
            }
            Assert.That(grid.IsSolid(new Vector3(4, 5.8f, 2)), Is.True, "Roof stays solid.");
            Assert.That(grid.IsSolid(new Vector3(7.8f, 5.8f, 7.8f)), Is.True);
            Assert.That(grid.Revision, Is.EqualTo(4));
            float previous = grid.RemovedVolume;
            Assert.That(grid.RemoveSphere(cuts[3], 1.1f, out _), Is.False);
            Assert.That(grid.RemovedVolume, Is.EqualTo(previous));
        }

        [Test]
        public void InvalidRepeatedAndOutsideBrushesCannotMutateAndResetRestoresFreshSoil()
        {
            var grid = new ExcavationGrid(new Vector3Int(20, 20, 20), 0.2f);
            var center = new Vector3(2, 3.9f, 2);
            grid.RemoveSphere(center, 0.48f, out _);
            float removed = grid.RemovedVolume;
            Assert.That(removed, Is.GreaterThan(0));
            Assert.That(grid.RemoveSphere(center, 0.48f, out _), Is.False);
            foreach (float radius in new[] { -1f, 0f, float.NaN, float.PositiveInfinity })
                Assert.That(grid.RemoveSphere(center, radius, out _), Is.False);
            Assert.That(grid.RemoveSphere(Vector3.one * 1000, 1, out _), Is.False);
            Assert.That(grid.RemoveSphere(new Vector3(float.NaN, 0, 0), 1, out _), Is.False);
            Assert.That(grid.RemovedVolume, Is.EqualTo(removed));
            Assert.That(grid.Revision, Is.EqualTo(1));
            Assert.That(grid.IsSolid(new Vector3(-1, 0, 0)), Is.False);
            grid.Reset();
            Assert.That(grid.RemovedVolume, Is.Zero);
            Assert.That(grid.Revision, Is.Zero);
            Assert.That(grid.IsSolid(center), Is.True);
            Assert.That(grid.Sample(new Vector3(2, 4, 2)), Is.Zero);
        }

        [Test]
        public void EveryShovelLevelRemovesMoreFreshSoilAndUpgradesMustBeSequential()
        {
            var shovel = new ShovelState(ShovelProfile.Defaults());
            float previous = 0;
            for (int level = 1; level <= 6; level++)
            {
                float radius = shovel.Current.Radius;
                var grid = new ExcavationGrid(new Vector3Int(64, 64, 64), 0.125f);
                Assert.That(grid.RemoveScoop(new Vector3(4, 8 - radius * 0.12f, 4), radius, 2718, 0.12f, out _), Is.True);
                if (previous > 0) Assert.That(grid.RemovedVolume / previous, Is.InRange(1.2f, 2.4f),
                    "An upgrade should feel stronger without an explosive increase in volume.");
                // The starter is deliberately weak (048 follow-up): a level-1 stroke
                // still has to move soil, but far less than the old 0.06 m3 floor.
                Assert.That(grid.RemovedVolume, Is.InRange(0.02f, 2f), "Fresh strokes remain controlled at every level.");
                TestContext.WriteLine($"Level {level}: radius {radius:F2} m; scoop {grid.RemovedVolume:F3} m3");
                previous = grid.RemovedVolume;
                Assert.That(shovel.TryUpgradeTo(level), Is.False);
                Assert.That(shovel.TryUpgradeTo(level + 2), Is.False);
                Assert.That(shovel.TryUpgradeTo(level + 1), Is.EqualTo(level < 6));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => shovel.GetProfile(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => shovel.GetProfile(7));
            var invalid = ShovelProfile.Defaults(); invalid[3].Radius = invalid[2].Radius;
            Assert.Throws<ArgumentException>(() => new ShovelState(invalid));
            invalid = ShovelProfile.Defaults(); invalid[3].ReachBonus = invalid[2].ReachBonus;
            Assert.Throws<ArgumentException>(() => new ShovelState(invalid));
        }

        [Test]
        public void SurfaceHasInterpolatedPositionsOutwardWindingAndIdenticalSeamNormals()
        {
            var grid = new ExcavationGrid(new Vector3Int(24, 20, 24), 0.2f);
            grid.RemoveScoop(new Vector3(2.4f, 3.91f, 2.4f), 0.85f, 2718, 0.12f, out _);
            var left = new Mesh(); var right = new Mesh();
            try
            {
                TerrainChunkMesh.Rebuild(left, grid, new Vector3Int(0, 12, 0), 12);
                TerrainChunkMesh.Rebuild(right, grid, new Vector3Int(12, 12, 0), 12);
                int tilted = 0;
                foreach (Mesh mesh in new[] { left, right })
                {
                    Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                    int[] triangles = mesh.triangles;
                    Assert.That(triangles.Length, Is.GreaterThan(0));
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                        Assert.That(face.sqrMagnitude, Is.GreaterThan(1e-12f));
                        Assert.That(Vector3.Dot(face, normals[a] + normals[b] + normals[c]), Is.GreaterThan(0));
                    }
                    tilted += normals.Count(n => Mathf.Abs(n.y) > 0.1f && Mathf.Abs(n.y) < 0.95f);
                }
                Assert.That(tilted, Is.GreaterThan(15), "Curved normals, not cube faces.");
                int shared = 0;
                Vector3[] lv = left.vertices, rv = right.vertices, ln = left.normals, rn = right.normals;
                for (int a = 0; a < lv.Length; a++)
                for (int b = 0; b < rv.Length; b++)
                    if ((lv[a] - rv[b]).sqrMagnitude < 1e-12f)
                    { Assert.That((ln[a] - rn[b]).sqrMagnitude, Is.LessThan(1e-12f)); shared++; }
                Assert.That(shared, Is.GreaterThan(8));
            }
            finally { UnityEngine.Object.DestroyImmediate(left); UnityEngine.Object.DestroyImmediate(right); }
        }

        [Test]
        public void CachedChunkPreservesUnchangedGeometryAndRefreshesItsNormalHaloAfterEditsAndRestore()
        {
            var grid = new ExcavationGrid(new Vector3Int(24, 20, 24), 0.2f);
            var initial = grid.Capture();
            var mesh = new Mesh(); var expected = new Mesh();
            var workspace = new TerrainChunkMesh.Workspace(); var cache = new TerrainChunkMesh.DensityCache();
            var start = new Vector3Int(0, 12, 0); int writes = 0;
            try
            {
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, start, 12, workspace, cache, () => writes++), Is.True);
                grid.RemoveSphere(new Vector3(4.1f, 3.95f, 4.1f), 0.3f, out _);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, start, 12, workspace, cache, () => writes++), Is.False);
                Assert.That(writes, Is.EqualTo(1));
                grid.RemoveSphere(new Vector3(2.55f, 3.95f, 1.2f), 0.5f, out _);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, start, 12, workspace, cache, () => writes++), Is.True);
                TerrainChunkMesh.Rebuild(expected, grid, start, 12);
                CollectionAssert.AreEqual(expected.vertices, mesh.vertices);
                CollectionAssert.AreEqual(expected.normals, mesh.normals);
                CollectionAssert.AreEqual(expected.triangles, mesh.triangles);
                foreach (var normal in mesh.normals) Assert.That(normal.sqrMagnitude, Is.EqualTo(1).Within(0.00001));
                grid.Restore(initial);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, start, 12, workspace, cache, () => writes++), Is.True);
                TerrainChunkMesh.Rebuild(expected, grid, start, 12);
                CollectionAssert.AreEqual(expected.vertices, mesh.vertices);
                CollectionAssert.AreEqual(expected.normals, mesh.normals);
                Assert.That(writes, Is.EqualTo(3));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(expected); }
        }

        [Test]
        public void OrganicScoopVariesContoursWithinBoundsAndReplaysItsSeedExactly()
        {
            var a = new ExcavationGrid(new Vector3Int(40, 40, 40), 0.125f);
            var b = new ExcavationGrid(a.Size, a.CellSize);
            var c = new ExcavationGrid(a.Size, a.CellSize);
            var center = Vector3.one * 2.5f;
            a.RemoveScoop(center, 1, 2718, 0.12f, out _);
            b.RemoveScoop(center, 1, 2718, 0.12f, out _);
            c.RemoveScoop(center, 1, 2719, 0.12f, out _);
            Assert.That(a.RemovedVolume, Is.EqualTo(b.RemovedVolume));
            float smallest = float.MaxValue, largest = 0;
            int differing = 0;
            for (int i = 0; i < 32; i++)
            {
                float angle = 2 * Mathf.PI * i / 32;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                float low = 0.6f, high = 1.5f;
                for (int step = 0; step < 16; step++)
                {
                    float mid = (low + high) * 0.5f;
                    if (a.Sample(center + direction * mid) < 0) low = mid; else high = mid;
                }
                float radius = (low + high) * 0.5f;
                Assert.That(radius, Is.InRange(0.72f, 1.4f));
                smallest = Mathf.Min(smallest, radius); largest = Mathf.Max(largest, radius);
                Vector3 surface = center + direction * radius;
                Assert.That(a.Sample(surface), Is.EqualTo(b.Sample(surface)));
                if (Mathf.Abs(a.Sample(surface) - c.Sample(surface)) > 0.005f) differing++;
            }
            Assert.That(largest - smallest, Is.GreaterThan(0.15f), "The asymmetric bite has a visibly uneven outline.");
            Assert.That(differing, Is.GreaterThan(20), "Another stroke seed changes the contour.");
            Assert.That(a.RemoveScoop(center, 1, 2718, 0.12f, out _), Is.False);
            int revision = a.Revision;
            foreach (float invalid in new[] { float.NaN, -1f, 0.16f })
                Assert.That(a.RemoveScoop(center, 1, 0, invalid, out _), Is.False);
            Assert.That(a.Revision, Is.EqualTo(revision));
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(1f, 1f, 1f)]
        public void ShovelBiteHasABroadFloorAlignedToTheHitSurface(float x, float y, float z)
        {
            var grid = new ExcavationGrid(new Vector3Int(64, 64, 64), 0.08f);
            var center = Vector3.one * 2.56f;
            var normal = new Vector3(x, y, z).normalized;
            var tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
            var bitangent = Vector3.Cross(normal, tangent);
            Assert.That(grid.RemoveScoop(center, 1, normal, 2718, 0, out _), Is.True);
            float centerDepth = CutDepth(grid, center, normal);
            float ringDepth = 0;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                ringDepth += CutDepth(grid, center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * 0.55f, normal);
            }
            ringDepth /= 16;
            Assert.That(centerDepth, Is.InRange(0.6f, 0.9f), "Controlled penetration is shallower than a hemisphere.");
            Assert.That(Mathf.Abs(centerDepth - ringDepth), Is.LessThan(0.09f), "The inner floor stays broad; a sphere narrows into a deep bowl.");
            Assert.That(grid.IsSolid(center - normal * 1.2f), Is.True, "Soil behind the bite remains intact.");
            int revision = grid.Revision;
            foreach (var invalid in new[] { Vector3.zero, new Vector3(float.NaN, 0, 0), Vector3.one * float.MaxValue })
                Assert.That(grid.RemoveScoop(center, 1, invalid, 2718, 0, out _), Is.False);
            Assert.That(grid.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void DeepSiteGridCarriesCutsCaptureAndRestoreAndRejectsOversizedAxes()
        {
            var grid = new ExcavationGrid(new Vector3Int(32, 800, 32), 0.125f);
            Assert.That(grid.Extent.y, Is.EqualTo(100f).Within(.0001f));
            var deep = new Vector3(2, 95, 2);
            Assert.That(grid.RemoveScoop(deep, 1, Vector3.up, 2718, 0.12f, out _), Is.True);
            Assert.That(grid.IsSolid(deep), Is.False);
            Assert.That(grid.IsSolid(new Vector3(2, 99, 2)), Is.True, "Only the worked layer changed.");
            var restored = new ExcavationGrid(grid.Size, grid.CellSize);
            restored.Restore(grid.Capture());
            Assert.That(restored.Sample(deep), Is.EqualTo(grid.Sample(deep)));
            Assert.That(restored.RemovedVolume, Is.EqualTo(grid.RemovedVolume));
            Assert.That(grid.Sample(1, 799, 1), Is.GreaterThan(0), "Untouched soil stays solid above the cut.");
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ExcavationGrid(new Vector3Int(32, ExcavationGrid.MaximumCellsPerAxis + 1, 32), 0.125f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExcavationGrid(new Vector3Int(32, 0, 32), 0.125f));
        }

        [Test]
        public void UntouchedChunkBlocksReportNoModificationAndCornerCutsDirtyEveryNeighbour()
        {
            var grid = new ExcavationGrid(new Vector3Int(64, 64, 64), 0.125f);
            var corner = new Vector3Int(16, 16, 16);
            Assert.That(grid.AnyModified(corner, 16), Is.False);
            Assert.That(grid.AnyModified(new Vector3Int(48, 48, 48), 16), Is.False);
            // A wide cut through the shared corner of eight blocks must dirty all of them:
            // a sample feeds every cell touching it, including the neighbouring chunk's.
            Assert.That(grid.RemoveScoop(new Vector3(4, 4, 4), 1.5f, Vector3.up, 7, 0, out _), Is.True);
            for (int z = 1; z <= 2; z++)
            for (int y = 1; y <= 2; y++)
            for (int x = 1; x <= 2; x++)
                Assert.That(grid.AnyModified(new Vector3Int(x, y, z) * 16, 16), Is.True, $"{x},{y},{z}");
            Assert.That(grid.AnyModified(new Vector3Int(48, 48, 48), 16), Is.False, "Distant untouched ground stays untouched.");
            grid.Reset();
            Assert.That(grid.AnyModified(corner, 16), Is.False, "Reset restores the analytic base field.");
        }

        private static float CutDepth(ExcavationGrid grid, Vector3 origin, Vector3 normal)
        {
            float low = 0, high = 1.4f;
            Assert.That(grid.Sample(origin), Is.LessThan(0));
            for (int step = 0; step < 16; step++)
            {
                float middle = (low + high) * 0.5f;
                if (grid.Sample(origin - normal * middle) < 0) low = middle; else high = middle;
            }
            return (low + high) * 0.5f;
        }
    }
}
