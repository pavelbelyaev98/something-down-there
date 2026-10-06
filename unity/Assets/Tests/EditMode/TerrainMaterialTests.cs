using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class TerrainMaterialTests
    {
        // Mesh weights are (free, free, free, 1 - backfill); soil is the remainder.
        private static readonly Vector4 SoilWeight = new Vector4(0, 0, 0, 1);

        [TestCase(TerrainMaterialId.Soil, 0)] [TestCase(TerrainMaterialId.Backfill, 1)]
        public void UniformDepositPublishesOnlyItsOwnSurfaceWeight(TerrainMaterialId material, float backfill)
        {
            var grid = new ExcavationGrid(new Vector3Int(16, 16, 16), .2f);
            var saved = grid.Capture(); saved.Materials = TerrainMaterialSnapshot.Uniform(saved.Materials.Length, material);
            grid.Restore(saved); grid.RemoveSphere(new Vector3(1.6f, 3.1f, 1.6f), .8f, out _);
            var mesh = new Mesh();
            try
            {
                TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 16);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                var weights = Weights(mesh);
                Assert.That(weights.Count, Is.EqualTo(mesh.vertexCount));
                var expected = new Vector4(0, 0, 0, 1 - backfill);
                foreach (var weight in weights) Assert.That((weight - expected).sqrMagnitude, Is.LessThan(1e-10f));
                var second = new List<Vector4>(); mesh.GetUVs(3, second);
                Assert.That(second, Is.Empty, "One weight stream.");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void DepositWeightsBlendAndMatchAcrossChunkSeamsAndRestore()
        {
            var grid = new ExcavationGrid(new Vector3Int(24, 20, 24), .2f);
            var saved = grid.Capture(); var materials = saved.Materials.ToArray();
            int index = 0;
            for (int z = 0; z <= grid.Size.z; z++)
            for (int y = 0; y <= grid.Size.y; y++)
            for (int x = 0; x <= grid.Size.x; x++)
                materials[index++] = (byte)(x < 12 == z < 6 ? TerrainMaterialId.Soil : TerrainMaterialId.Backfill);
            saved.Materials = TerrainMaterialSnapshot.CopyFrom(materials); grid.Restore(saved);
            grid.RemoveSphere(new Vector3(2.4f, 3.7f, 1.3f), .9f, out _);
            var left = new Mesh(); var right = new Mesh(); var restored = new Mesh();
            try
            {
                TerrainChunkMesh.Rebuild(left, grid, new Vector3Int(0, 12, 0), 12);
                TerrainChunkMesh.Rebuild(right, grid, new Vector3Int(12, 12, 0), 12);
                int shared = 0, blended = 0;
                var lv = left.vertices; var rv = right.vertices; var lw = Weights(left); var rw = Weights(right);
                foreach (var w in lw.Concat(rw))
                {
                    for (int c = 0; c < 4; c++) Assert.That(w[c], Is.InRange(0, 1));
                    Assert.That(w.x + w.y + w.z + 1 - w.w, Is.LessThanOrEqualTo(1.00001f));
                    for (int c = 0; c < 4; c++) if (w[c] > .001f && w[c] < .999f) { blended++; break; }
                }
                for (int a = 0; a < lv.Length; a++)
                for (int b = 0; b < rv.Length; b++)
                    if ((lv[a] - rv[b]).sqrMagnitude < 1e-12f)
                    { Assert.That(lw[a], Is.EqualTo(rw[b])); shared++; }
                Assert.That(shared, Is.GreaterThan(8)); Assert.That(blended, Is.GreaterThan(0));
                var clone = new ExcavationGrid(grid.Size, grid.CellSize); clone.Restore(grid.Capture());
                TerrainChunkMesh.Rebuild(restored, clone, new Vector3Int(0, 12, 0), 12);
                CollectionAssert.AreEqual(lw, Weights(restored));
            }
            finally
            { UnityEngine.Object.DestroyImmediate(left); UnityEngine.Object.DestroyImmediate(right); UnityEngine.Object.DestroyImmediate(restored); }
        }

        [Test]
        public void MaterialOnlyRestoreInvalidatesAnOtherwiseMatchingChunkCache()
        {
            var grid = new ExcavationGrid(new Vector3Int(12, 12, 12), .2f);
            var original = grid.Capture(); var mesh = new Mesh();
            using var workspace = new TerrainChunkMesh.Workspace(); var cache = new TerrainChunkMesh.DensityCache();
            try
            {
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 12, workspace, cache), Is.True);
                var vertices = mesh.vertices;
                var backfill = grid.Capture(); backfill.Materials = TerrainMaterialSnapshot.Uniform(backfill.Materials.Length, TerrainMaterialId.Backfill);
                grid.Restore(backfill);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 12, workspace, cache), Is.True);
                CollectionAssert.AreEqual(vertices, mesh.vertices);
                Assert.That(Weights(mesh).All(w => w == Vector4.zero), Is.True);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 12, workspace, cache), Is.False);
                grid.Restore(original);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 12, workspace, cache), Is.True);
                Assert.That(Weights(mesh).All(w => w == SoilWeight), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void CapturesShareImmutableIdentitiesAcrossCutsResetAndRestore()
        {
            var grid = new ExcavationGrid(new Vector3Int(24, 48, 24), .125f, 51);
            var saved = grid.Capture();
            var original = saved.Materials.ToArray();
            grid.RemoveSphere(new Vector3(1.5f, 5, 1.5f), 1, out _);
            grid.Reset();
            Assert.That(grid.Capture().Materials, Is.SameAs(saved.Materials));
            var exported = saved.Materials.ToArray(); exported[0] = 255;
            Assert.That(saved.Materials.ToArray(), Is.EqualTo(original));
            var loaded = new ExcavationGrid(grid.Size, grid.CellSize, 999);
            loaded.Restore(saved);
            Assert.That(loaded.Capture().Materials, Is.SameAs(saved.Materials));
            Assert.That(loaded.MaterialAt(-1, -1, -1), Is.EqualTo(loaded.MaterialAt(0, 0, 0)));
            Assert.That(loaded.MaterialAt(100, 100, 100), Is.EqualTo(loaded.MaterialAt(24, 48, 24)));
        }

        [TestCase(false)] [TestCase(true)]
        public void AllTiersCutEveryMaterialAndUpgradesImproveFamiliarGround(bool scoop)
        {
            float[] previous = new float[(int)TerrainMaterialSnapshot.Last + 1];
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(TerrainMaterialId)), EquipmentProgression.HardnessOrder);
            foreach (var profile in EquipmentProgression.ToolProfiles())
            {
                // Grounds sharing one response form one class: each class stays below the slowest member of the softer class.
                float softerRate = float.MaxValue, classMinimum = float.MaxValue;
                MaterialToolResponse? classResponse = null;
                foreach (TerrainMaterialId material in EquipmentProgression.HardnessOrder)
                {
                    var response = EquipmentProgression.MaterialResponse(material);
                    if (!classResponse.HasValue || !response.Equals(classResponse.Value))
                    { softerRate = classMinimum; classMinimum = float.MaxValue; classResponse = response; }
                    var grid = Homogeneous(material);
                    Vector3 top = new Vector3(1.5f, grid.Extent.y, 1.5f);
                    // A scoop's first bite; a drill bores tip first, so its first moments (a dozen cuts) count.
                    int cuts = scoop ? 1 : 12;
                    for (int cut = 0; cut < cuts; cut++)
                        Assert.That(scoop
                            ? grid.RemoveScoop(top - Vector3.up * profile.Radius * .12f, profile.Radius, Vector3.up, 62, .1f, out _, true)
                            : Drill(grid, new Vector3(top.x, SurfaceAt(grid, top), top.z), profile.Radius, 62 + cut), Is.True);
                    float rate = grid.RemovedVolume / (cuts * profile.CadenceMultiplier * EquipmentProgression.MaterialResponse(material).Interval);
                    Assert.That(rate, Is.GreaterThan(previous[(int)material]), $"{material} must improve with each tier.");
                    Assert.That(rate, Is.LessThan(softerRate), $"{material} must retain its resistance.");
                    previous[(int)material] = rate; classMinimum = Mathf.Min(classMinimum, rate);
                }
            }
        }

        [TestCase(TerrainMaterialId.Soil)] [TestCase(TerrainMaterialId.Backfill)]
        public void AutomaticMotionImprovesFreshAndSustainedOutputAcrossTheDrillMilestone(TerrainMaterialId material)
        {
            float previousFresh = 0, previousSustained = 0;
            Assert.That(EquipmentProgression.ToolProfiles().Length, Is.EqualTo(EquipmentProgression.LevelCount));
            for (int level = 1; level <= EquipmentProgression.LevelCount; level++)
            {
                var (first, sustained) = Output(material, level);
                TestContext.WriteLine($"{material} level {level}: fresh {first:F3}, sustained {sustained:F3} m3/s");
                Assert.That(first, Is.GreaterThan(previousFresh), $"{material} level {level} fresh-ground output");
                Assert.That(sustained, Is.GreaterThan(previousSustained), $"{material} level {level} sustained output");
                previousFresh = first; previousSustained = sustained;
            }
        }

        // Rubble backfill (108): about three quarters of soil's rate at every tool level, shovel and drill, felt at once and
        // never a wall.
        [Test]
        public void RubbleBackfillDigsAboutThreeQuartersOfSoilsRateAtEveryLevel()
        {
            for (int level = 1; level <= EquipmentProgression.LevelCount; level++)
            {
                float ratio = Output(TerrainMaterialId.Backfill, level).sustained / Output(TerrainMaterialId.Soil, level).sustained;
                TestContext.WriteLine($"Level {level}: backfill digs {ratio:P0} of soil's rate");
                Assert.That(ratio, Is.InRange(.6f, .88f), $"Level {level}");
            }
        }

        // Fresh (the first second) and sustained (four seconds) output (m3/s) of the automatic motion held straight down.
        private static (float first, float sustained) Output(TerrainMaterialId material, int level)
        {
            var profile = EquipmentProgression.ToolProfiles()[level - 1];
            var grid = new ExcavationGrid(new Vector3Int(48, 192, 48), .0625f);
            var saved = grid.Capture();
            saved.Materials = TerrainMaterialSnapshot.Uniform(saved.Density.Length, material);
            grid.Restore(saved);
            bool drill = EquipmentProgression.UsesDrill(level);
            float interval = .35f * profile.CadenceMultiplier * EquipmentProgression.MaterialResponse(material).Interval
                * (drill ? EquipmentProgression.ShavingIntervalScale : 1);
            int perSecond = Mathf.CeilToInt(1f / interval), cuts = Mathf.CeilToInt(4f / interval);
            var axis = new Vector3(1.5f, 0, 1.5f);
            float first = 0;
            for (int cut = 0; cut < cuts; cut++)
            {
                var surface = new Vector3(axis.x, SurfaceAt(grid, axis), axis.z);
                Assert.That(drill
                    ? Drill(grid, surface, profile.Radius, 62 + cut)
                    : grid.RemoveScoop(surface - Vector3.up * profile.Radius * .12f, profile.Radius, Vector3.up, 62 + cut, .1f, out _, true), Is.True);
                if (cut == perSecond - 1) first = grid.RemovedVolume / (perSecond * interval);
            }
            return (first, grid.RemovedVolume / (cuts * interval));
        }

        // One drill cut straight down from `surface`, as TerrainVolume.TryToolCut makes it.
        private static bool Drill(ExcavationGrid grid, Vector3 surface, float radius, int seed)
        {
            var ground = EquipmentProgression.MaterialResponse(grid.MaterialAt(surface - Vector3.up * .03f));
            float advance = radius * EquipmentProgression.DrillAdvanceRatio * ground.Penetration;
            float engaged = EquipmentProgression.DrillEngagedShare * Mathf.PI * radius * radius * advance * ground.Width * ground.Length;
            return grid.RemoveBore(surface - Vector3.up * advance, radius, Vector3.up, radius * EquipmentProgression.DrillBoreLengthRatio, out _, true, seed,
                advance, EquipmentProgression.DrillPushes, engaged);
        }

        // The drill bores tip first (user, 2026-10-05): held, it opens the middle and widens around it into a cone, with no
        // pit at its tip, then takes a layer a cut.
        [Test]
        public void DrillOpensTheMiddleFirstIntoAConeThenTakesALayerACut()
        {
            var profile = EquipmentProgression.ToolProfiles()[EquipmentProgression.DrillLevel - 1];
            float radius = profile.Radius, layer = radius * EquipmentProgression.DrillAdvanceRatio;
            var grid = new ExcavationGrid(new Vector3Int(48, 192, 48), .0625f);
            var saved = grid.Capture();
            saved.Materials = TerrainMaterialSnapshot.Uniform(saved.Density.Length, TerrainMaterialId.Soil);
            grid.Restore(saved);
            var axis = new Vector3(1.5f, 0, 1.5f);
            float top = SurfaceAt(grid, axis), last = 0;
            int cuts = Mathf.CeilToInt(EquipmentProgression.DrillBoreLengthRatio / EquipmentProgression.DrillAdvanceRatio) + 6;
            for (int cut = 0; cut < cuts; cut++)
            {
                Assert.That(Drill(grid, new Vector3(axis.x, SurfaceAt(grid, axis), axis.z), radius, 62 + cut), Is.True);
                last = grid.LastRemovedVolume;
                if (cut == 0)
                    Assert.That(grid.IsSolid(new Vector3(axis.x + radius * .6f, top - layer * .5f, axis.z)), Is.True, "Middle first");
            }
            Assert.That(grid.IsSolid(new Vector3(axis.x + radius * .9f, top - layer * .5f, axis.z)), Is.False, "Then the full bite");
            // The floor falls steadily to the tip, like a cone, rather than dropping into a pit at the middle.
            float Rise(float r) => SurfaceAt(grid, axis + Vector3.right * radius * r) - SurfaceAt(grid, axis);
            Assert.That(Rise(.5f) / Rise(.25f), Is.InRange(1.5f, 2.6f), "Cone, not a pit");
            float flatLayer = Mathf.PI * radius * radius * layer;
            Assert.That(last, Is.EqualTo(flatLayer).Within(flatLayer * .15f), "A layer a cut once bored in");
        }

        // The ground's height under x/z, by bisection down the column.
        private static float SurfaceAt(ExcavationGrid grid, Vector3 at)
        {
            float low = 0, high = grid.Extent.y;
            for (int step = 0; step < 18; step++)
            {
                float middle = (low + high) * .5f;
                if (grid.Sample(new Vector3(at.x, middle, at.z)) > 0) low = middle; else high = middle;
            }
            return (low + high) * .5f;
        }

        [Test]
        public void MixedBoundaryCutsHardSamplesWithTheirOwnResponse()
        {
            var mixed = Homogeneous(TerrainMaterialId.Soil);
            var snapshot = mixed.Capture(); var ids = snapshot.Materials.ToArray();
            int i = 0;
            for (int z = 0; z <= mixed.Size.z; z++)
            for (int y = 0; y <= mixed.Size.y; y++)
            for (int x = 0; x <= mixed.Size.x; x++) ids[i++] = (byte)(x >= 24 ? TerrainMaterialId.Backfill : TerrainMaterialId.Soil);
            snapshot.Materials = TerrainMaterialSnapshot.CopyFrom(ids); mixed.Restore(snapshot);
            var soil = Homogeneous(TerrainMaterialId.Soil); var backfill = Homogeneous(TerrainMaterialId.Backfill);
            // The bit deep enough that its cone is wide at the surface.
            foreach (var grid in new[] { mixed, soil, backfill })
                Assert.That(grid.RemoveBore(new Vector3(1.5f, 1.6f, 1.5f), .56f, Vector3.up, .56f * EquipmentProgression.DrillBoreLengthRatio, out _, true, 53), Is.True);
            // Each side of the boundary is cut exactly as its own ground alone, and the two grounds cut differently.
            int differing = 0;
            for (int y = 16; y <= 32; y++)
            for (int x = 0; x <= 40; x++)
            {
                if (x > 20 && x < 27) continue; // Clean-up of thin remnants may reach across the boundary.
                var own = x >= 24 ? backfill : soil;
                Assert.That(mixed.Sample(x, y, 24), Is.EqualTo(own.Sample(x, y, 24)).Within(.00001f), $"Sample {x},{y}");
                if (x >= 24 && Mathf.Abs(backfill.Sample(x, y, 24) - soil.Sample(x, y, 24)) > .0001f) differing++;
            }
            Assert.That(differing, Is.GreaterThan(0), "Backfill cuts differently from soil.");
        }

        [Test]
        public void MaterialValidationRejectsUnknownIdsMismatchedCountsAndOversizeDimensions()
        {
            Assert.Throws<InvalidDataException>(() => TerrainMaterialSnapshot.CopyFrom(new byte[] { 0, 1, (byte)(TerrainMaterialSnapshot.Last + 1) }));
            var grid = Homogeneous(TerrainMaterialId.Soil).Capture();
            grid.Materials = TerrainMaterialSnapshot.Uniform(grid.Density.Length - 1);
            Assert.Throws<InvalidDataException>(() => grid.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExcavationGrid(new Vector3Int(2048, 2048, 2048), .125f));
        }

        [TestCase("id")] [TestCase("count")] [TestCase("truncated")]
        public void ChecksummedButInvalidMaterialPayloadIsRejected(string damage)
        {
            var grid = new ExcavationGrid(new Vector3Int(8, 8, 8), .125f);
            var saved = new WorldSnapshot { Sequence = 1, UtcTicks = DateTime.UtcNow.Ticks, Terrain = grid.Capture(),
                TerrainRotation = Quaternion.identity, PlayerRotation = Quaternion.identity,
                InventoryCapacity = 10, ShovelLevel = 1, BatteryCapacity = 100, Finds = Array.Empty<FindSnapshot>(), Inventory = Array.Empty<ItemSnapshot>() };
            using var valid = new MemoryStream(); WorldSaveCodec.Write(valid, saved);
            byte[] bytes = valid.ToArray();
            using var compressed = new MemoryStream(bytes, 48, bytes.Length - 48);
            using var zip = new GZipStream(compressed, CompressionMode.Decompress);
            using var unpacked = new MemoryStream(); zip.CopyTo(unpacked);
            byte[] payload = unpacked.ToArray();
            // This empty-world fixture has a fixed current-format tail: stance, two
            // progression levels, no extraction, and two empty equipment counts.
            int materialStart = payload.Length - 21 - saved.Terrain.Materials.Length;
            if (damage == "id") payload[materialStart] = 255;
            else if (damage == "count") Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, payload, materialStart - 4, 4);
            else Array.Resize(ref payload, materialStart + 3);
            using var packed = new MemoryStream();
            using (var encode = new GZipStream(packed, System.IO.Compression.CompressionLevel.Fastest, true)) encode.Write(payload, 0, payload.Length);
            byte[] corrupt = packed.ToArray();
            using var rebuilt = new MemoryStream(); using var writer = new BinaryWriter(rebuilt);
            writer.Write(bytes, 0, 12); writer.Write(corrupt.Length);
            using var sha = SHA256.Create(); writer.Write(sha.ComputeHash(corrupt)); writer.Write(corrupt); writer.Flush();
            rebuilt.Position = 0;
            Assert.Throws<InvalidDataException>(() => WorldSaveCodec.Read(rebuilt));
        }

        private static List<Vector4> Weights(Mesh mesh)
        {
            var weights = new List<Vector4>(); mesh.GetUVs(2, weights); return weights;
        }

        private static ExcavationGrid Homogeneous(TerrainMaterialId material)
        {
            var grid = new ExcavationGrid(new Vector3Int(48, 32, 48), .0625f);
            var snapshot = grid.Capture(); snapshot.Materials = TerrainMaterialSnapshot.Uniform(snapshot.Density.Length, material);
            grid.Restore(snapshot); return grid;
        }
    }
}
