using System;
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
        [TestCase(TerrainMaterialId.Soil)] [TestCase(TerrainMaterialId.Clay)] [TestCase(TerrainMaterialId.Rock)]
        public void UniformDepositPublishesOnlyItsOwnSurfaceWeight(TerrainMaterialId material)
        {
            var grid = new ExcavationGrid(new Vector3Int(16, 16, 16), .2f);
            var saved = grid.Capture(); saved.Materials = TerrainMaterialSnapshot.Uniform(saved.Materials.Length, material);
            grid.Restore(saved); grid.RemoveSphere(new Vector3(1.6f, 3.1f, 1.6f), .8f, out _);
            var mesh = new Mesh();
            try
            {
                TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 16);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(mesh.uv3.Length, Is.EqualTo(mesh.vertexCount));
                var expected = material == TerrainMaterialId.Clay ? Vector2.right
                    : material == TerrainMaterialId.Rock ? Vector2.up : Vector2.zero;
                foreach (var weight in mesh.uv3) Assert.That((weight - expected).sqrMagnitude, Is.LessThan(1e-10f));
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
                materials[index++] = (byte)(x < 12 ? TerrainMaterialId.Soil : z < 6 ? TerrainMaterialId.Clay : TerrainMaterialId.Rock);
            saved.Materials = TerrainMaterialSnapshot.CopyFrom(materials); grid.Restore(saved);
            grid.RemoveSphere(new Vector3(2.4f, 3.7f, 1.3f), .9f, out _);
            var left = new Mesh(); var right = new Mesh(); var restored = new Mesh();
            try
            {
                TerrainChunkMesh.Rebuild(left, grid, new Vector3Int(0, 12, 0), 12);
                TerrainChunkMesh.Rebuild(right, grid, new Vector3Int(12, 12, 0), 12);
                int shared = 0, blended = 0;
                var lv = left.vertices; var rv = right.vertices; var lw = left.uv3; var rw = right.uv3;
                foreach (var w in lw.Concat(rw))
                {
                    Assert.That(w.x, Is.InRange(0, 1)); Assert.That(w.y, Is.InRange(0, 1));
                    Assert.That(w.x + w.y, Is.LessThanOrEqualTo(1.00001f));
                    if ((w.x > .001f && w.x < .999f) || (w.y > .001f && w.y < .999f)) blended++;
                }
                for (int a = 0; a < lv.Length; a++)
                for (int b = 0; b < rv.Length; b++)
                    if ((lv[a] - rv[b]).sqrMagnitude < 1e-12f)
                    { Assert.That(lw[a], Is.EqualTo(rw[b])); shared++; }
                Assert.That(shared, Is.GreaterThan(8)); Assert.That(blended, Is.GreaterThan(0));
                var clone = new ExcavationGrid(grid.Size, grid.CellSize); clone.Restore(grid.Capture());
                TerrainChunkMesh.Rebuild(restored, clone, new Vector3Int(0, 12, 0), 12);
                CollectionAssert.AreEqual(lw, restored.uv3);
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
                var rock = grid.Capture(); rock.Materials = TerrainMaterialSnapshot.Uniform(rock.Materials.Length, TerrainMaterialId.Rock);
                grid.Restore(rock);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 12, workspace, cache), Is.True);
                CollectionAssert.AreEqual(vertices, mesh.vertices);
                Assert.That(mesh.uv3.All(w => w == Vector2.up), Is.True);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 12, workspace, cache), Is.False);
                grid.Restore(original);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 12, workspace, cache), Is.True);
                Assert.That(mesh.uv3.All(w => w == Vector2.zero), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void SeededDepositsAreRepeatableContainAllFamiliesAndDoNotConsumeGlobalRandom()
        {
            var size = new Vector3Int(48, 96, 48);
            var random = UnityEngine.Random.state;
            var first = TerrainMaterialSnapshot.Generate(size, .125f, 2718).ToArray();
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
            Assert.That(TerrainMaterialSnapshot.Generate(size, .125f, 2718).ToArray(), Is.EqualTo(first));
            Assert.That(TerrainMaterialSnapshot.Generate(size, .125f, 853).ToArray(), Is.Not.EqualTo(first));
            Assert.That(first.Distinct().OrderBy(v => v), Is.EqualTo(new byte[] { 0, 1, 2 }));
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
            float[] previous = new float[3];
            foreach (var profile in EquipmentProgression.ToolProfiles())
            {
                float softerRate = float.MaxValue;
                foreach (TerrainMaterialId material in Enum.GetValues(typeof(TerrainMaterialId)))
                {
                    var grid = Homogeneous(material);
                    Vector3 top = new Vector3(1.5f, grid.Extent.y, 1.5f);
                    bool cut = scoop
                        ? grid.RemoveScoop(top - Vector3.up * profile.Radius * .12f, profile.Radius, Vector3.up, 62, .1f, out _, true)
                        : grid.RemoveShave(top, profile.Radius, Vector3.up, profile.Radius * EquipmentProgression.ShavingDepthRatio, out _, true, 62);
                    Assert.That(cut, Is.True);
                    float rate = grid.LastRemovedVolume / (profile.CadenceMultiplier * EquipmentProgression.MaterialResponse(material).Interval);
                    Assert.That(rate, Is.GreaterThan(previous[(int)material]), $"{material} must improve with each tier.");
                    Assert.That(rate, Is.LessThan(softerRate), $"{material} must retain its resistance.");
                    previous[(int)material] = rate; softerRate = rate;
                }
            }
        }

        [TestCase(TerrainMaterialId.Soil)] [TestCase(TerrainMaterialId.Clay)] [TestCase(TerrainMaterialId.Rock)]
        public void AutomaticMotionImprovesFreshAndSustainedOutputAcrossTheDrillMilestone(TerrainMaterialId material)
        {
            float previousFresh = 0, previousSustained = 0;
            var profiles = EquipmentProgression.ToolProfiles();
            Assert.That(profiles.Length, Is.EqualTo(10));
            for (int level = 1; level <= profiles.Length; level++)
            {
                var profile = profiles[level - 1];
                var grid = new ExcavationGrid(new Vector3Int(48, 192, 48), .0625f);
                var saved = grid.Capture();
                saved.Materials = TerrainMaterialSnapshot.Uniform(saved.Density.Length, material);
                grid.Restore(saved);
                bool drill = EquipmentProgression.UsesDrill(level);
                float interval = .35f * profile.CadenceMultiplier * EquipmentProgression.MaterialResponse(material).Interval
                    * (drill ? EquipmentProgression.ShavingIntervalScale : 1);
                float first = 0;
                for (int cut = 0; cut < 12; cut++)
                {
                    float low = 0, high = grid.Extent.y;
                    for (int step = 0; step < 18; step++)
                    {
                        float middle = (low + high) * .5f;
                        if (grid.Sample(new Vector3(1.5f, middle, 1.5f)) > 0) low = middle; else high = middle;
                    }
                    var surface = new Vector3(1.5f, (low + high) * .5f, 1.5f);
                    Assert.That(drill
                        ? grid.RemoveShave(surface, profile.Radius, Vector3.up, profile.Radius * EquipmentProgression.ShavingDepthRatio, out _, true, 62 + cut)
                        : grid.RemoveScoop(surface - Vector3.up * profile.Radius * .12f, profile.Radius, Vector3.up, 62 + cut, .1f, out _, true), Is.True);
                    if (cut == 0) first = grid.LastRemovedVolume / interval;
                }
                float sustained = grid.RemovedVolume / (12 * interval);
                TestContext.WriteLine($"{material} level {level}: fresh {first:F3}, sustained {sustained:F3} m3/s");
                Assert.That(first, Is.GreaterThan(previousFresh), $"{material} level {level} fresh-ground output");
                Assert.That(sustained, Is.GreaterThan(previousSustained), $"{material} level {level} sustained output");
                previousFresh = first; previousSustained = sustained;
            }
        }

        [Test]
        public void MixedBoundaryCutsHardSamplesWithTheirOwnResponse()
        {
            var mixed = Homogeneous(TerrainMaterialId.Soil);
            var snapshot = mixed.Capture(); var ids = snapshot.Materials.ToArray();
            int i = 0;
            for (int z = 0; z <= mixed.Size.z; z++)
            for (int y = 0; y <= mixed.Size.y; y++)
            for (int x = 0; x <= mixed.Size.x; x++) ids[i++] = (byte)(x >= 24 ? TerrainMaterialId.Rock : TerrainMaterialId.Soil);
            snapshot.Materials = TerrainMaterialSnapshot.CopyFrom(ids); mixed.Restore(snapshot);
            var soil = Homogeneous(TerrainMaterialId.Soil); var rock = Homogeneous(TerrainMaterialId.Rock);
            foreach (var grid in new[] { mixed, soil, rock })
                Assert.That(grid.RemoveShave(new Vector3(1.5f, 2, 1.5f), .56f, Vector3.up, .028f, out _, true, 53), Is.True);
            Assert.That(mixed.Sample(26, 32, 24), Is.EqualTo(rock.Sample(26, 32, 24)).Within(.00001f));
            Assert.That(mixed.Sample(26, 32, 24), Is.GreaterThan(soil.Sample(26, 32, 24)));
            Assert.That(mixed.Sample(22, 32, 24), Is.EqualTo(soil.Sample(22, 32, 24)).Within(.00001f));
        }

        [Test]
        public void MaterialValidationRejectsUnknownIdsMismatchedCountsAndOversizeDimensions()
        {
            Assert.Throws<InvalidDataException>(() => TerrainMaterialSnapshot.CopyFrom(new byte[] { 0, 1, 255 }));
            var grid = Homogeneous(TerrainMaterialId.Soil).Capture();
            grid.Materials = TerrainMaterialSnapshot.Uniform(grid.Density.Length - 1);
            Assert.Throws<InvalidDataException>(() => grid.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExcavationGrid(new Vector3Int(2048, 2048, 2048), .125f));
        }

        [TestCase(TerrainMaterialId.Soil, false, false)]
        [TestCase(TerrainMaterialId.Clay, false, true)]
        [TestCase(TerrainMaterialId.Rock, true, false)]
        public void HeldShavingRetainsDistinctContoursInsteadOfAveragingIntoRoundHoles(TerrainMaterialId material, bool solidX, bool solidZ)
        {
            var grid = Homogeneous(material);
            var surface = new Vector3(1.5f, 2, 1.5f);
            for (int i = 0; i < 30; i++)
            {
                Assert.That(grid.RemoveShave(surface, .56f, Vector3.up, .028f, out _, true, i * 486187739), Is.True);
                surface.y -= .028f * EquipmentProgression.MaterialResponse(material).Penetration;
            }
            var lip = new Vector3(1.5f, 1.93f, 1.5f);
            Assert.That(grid.IsSolid(lip + Vector3.right * .5f), Is.EqualTo(solidX));
            Assert.That(grid.IsSolid(lip + Vector3.forward * .5f), Is.EqualTo(solidZ));
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

        private static ExcavationGrid Homogeneous(TerrainMaterialId material)
        {
            var grid = new ExcavationGrid(new Vector3Int(48, 32, 48), .0625f);
            var snapshot = grid.Capture(); snapshot.Materials = TerrainMaterialSnapshot.Uniform(snapshot.Density.Length, material);
            grid.Restore(snapshot); return grid;
        }
    }
}
