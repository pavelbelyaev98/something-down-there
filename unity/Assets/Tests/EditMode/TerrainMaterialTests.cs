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
        // Mesh weights are (clay, rock, concrete, 1 - gravel); soil is the remainder.
        private static readonly Vector4 SoilWeight = new Vector4(0, 0, 0, 1);

        [TestCase(TerrainMaterialId.Soil, 0, 0, 0, 1, 0, 0, 0, 0)] [TestCase(TerrainMaterialId.Clay, 1, 0, 0, 1, 0, 0, 0, 0)]
        [TestCase(TerrainMaterialId.Rock, 0, 1, 0, 1, 0, 0, 0, 0)] [TestCase(TerrainMaterialId.Gravel, 0, 0, 0, 0, 0, 0, 0, 0)]
        [TestCase(TerrainMaterialId.Concrete, 0, 0, 1, 1, 0, 0, 0, 0)] [TestCase(TerrainMaterialId.PondClay, 0, 0, 0, 1, 1, 0, 0, 0)]
        [TestCase(TerrainMaterialId.FracturedRock, 0, 1, 0, 1, 0, 1, 0, 0)] [TestCase(TerrainMaterialId.FracturedConcrete, 0, 0, 1, 1, 0, 1, 0, 0)]
        [TestCase(TerrainMaterialId.Crack, 0, 1, 0, 1, 0, 1, 1, 0)] [TestCase(TerrainMaterialId.Backfill, 0, 0, 0, 1, 0, 0, 0, 1)]
        public void UniformDepositPublishesOnlyItsOwnSurfaceWeight(TerrainMaterialId material, float clay, float rock, float concrete, float notGravel, float pond, float fractured, float crack, float backfill)
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
                var expected = new Vector4(clay, rock, concrete, notGravel);
                foreach (var weight in weights) Assert.That((weight - expected).sqrMagnitude, Is.LessThan(1e-10f));
                var second = new List<Vector4>(); mesh.GetUVs(3, second);
                foreach (var weight in second) Assert.That((weight - new Vector4(pond, fractured, crack, 1 - backfill)).sqrMagnitude, Is.LessThan(1e-10f));
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
                materials[index++] = (byte)(x < 12 ? (z < 6 ? TerrainMaterialId.Soil : TerrainMaterialId.Gravel)
                    : z < 6 ? TerrainMaterialId.Clay : z < 9 ? TerrainMaterialId.Rock : TerrainMaterialId.Concrete);
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
                var rock = grid.Capture(); rock.Materials = TerrainMaterialSnapshot.Uniform(rock.Materials.Length, TerrainMaterialId.Rock);
                grid.Restore(rock);
                Assert.That(TerrainChunkMesh.Rebuild(mesh, grid, Vector3Int.zero, 12, workspace, cache), Is.True);
                CollectionAssert.AreEqual(vertices, mesh.vertices);
                Assert.That(Weights(mesh).All(w => w == new Vector4(0, 1, 0, 1)), Is.True);
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
                // Families sharing one response (fractured rock and its crack line) form one class:
                // each class stays below the slowest member of the softer class.
                float softerRate = float.MaxValue, classMinimum = float.MaxValue;
                MaterialToolResponse? classResponse = null;
                foreach (TerrainMaterialId material in EquipmentProgression.HardnessOrder)
                {
                    var response = EquipmentProgression.MaterialResponse(material);
                    if (!classResponse.HasValue || !response.Equals(classResponse.Value))
                    { softerRate = classMinimum; classMinimum = float.MaxValue; classResponse = response; }
                    var grid = Homogeneous(material);
                    Vector3 top = new Vector3(1.5f, grid.Extent.y, 1.5f);
                    bool cut = scoop
                        ? grid.RemoveScoop(top - Vector3.up * profile.Radius * .12f, profile.Radius, Vector3.up, 62, .1f, out _, true)
                        : grid.RemoveShave(top, profile.Radius, Vector3.up, profile.Radius * EquipmentProgression.ShavingDepthRatio, out _, true, 62,
                            profile.Radius * EquipmentProgression.DrillPointDepthRatio);
                    Assert.That(cut, Is.True);
                    float rate = grid.LastRemovedVolume / (profile.CadenceMultiplier * EquipmentProgression.MaterialResponse(material).Interval);
                    Assert.That(rate, Is.GreaterThan(previous[(int)material]), $"{material} must improve with each tier.");
                    Assert.That(rate, Is.LessThan(softerRate), $"{material} must retain its resistance.");
                    previous[(int)material] = rate; classMinimum = Mathf.Min(classMinimum, rate);
                }
            }
        }

        [TestCase(TerrainMaterialId.Soil)] [TestCase(TerrainMaterialId.Clay)] [TestCase(TerrainMaterialId.Rock)]
        [TestCase(TerrainMaterialId.Gravel)] [TestCase(TerrainMaterialId.Concrete)] [TestCase(TerrainMaterialId.PondClay)]
        [TestCase(TerrainMaterialId.FracturedRock)] [TestCase(TerrainMaterialId.FracturedConcrete)] [TestCase(TerrainMaterialId.Backfill)]
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

        // Concept 03 zone rule: arriving in a zone at the level a player typically owns there never
        // feels like a restart. Clay is the working ground around levels 3-6 and rock arrives around
        // the drill (levels 6-8); at those levels two purchases outpace the previous zone's main ground.
        [Test]
        public void OneLevelOutpacesTheNextZonesMainGround()
        {
            float[] Sustained(TerrainMaterialId material) => Enumerable.Range(1, EquipmentProgression.LevelCount)
                .Select(level => Output(material, level).sustained).ToArray();
            float[] soil = Sustained(TerrainMaterialId.Soil), clay = Sustained(TerrainMaterialId.Clay), rock = Sustained(TerrainMaterialId.Rock);
            for (int level = 3; level <= 6; level++)
                Assert.That(clay[level - 1], Is.GreaterThanOrEqualTo(soil[level - 3]), $"Clay at level {level} vs soil at {level - 2}");
            for (int level = 6; level <= 8; level++)
                Assert.That(rock[level - 1], Is.GreaterThanOrEqualTo(clay[level - 3]), $"Rock at level {level} vs clay at {level - 2}");
            // Tells: basins bite clearly easier than the clay around them; the band beside a crack
            // clearly easier than the rock or concrete it breaks, at every level.
            float[] concrete = Sustained(TerrainMaterialId.Concrete);
            for (int level = 1; level <= EquipmentProgression.LevelCount; level++)
            {
                Assert.That(Output(TerrainMaterialId.PondClay, level).sustained, Is.GreaterThan(clay[level - 1] * 1.15f), $"Pond clay at level {level}");
                Assert.That(Output(TerrainMaterialId.FracturedRock, level).sustained, Is.GreaterThan(rock[level - 1] * 1.4f), $"Fractured rock at level {level}");
                Assert.That(Output(TerrainMaterialId.FracturedConcrete, level).sustained, Is.GreaterThan(concrete[level - 1] * 2f), $"Fractured concrete at level {level}");
                // Disturbed ground: the tool suddenly sinks in, even in the recent fill's soil.
                Assert.That(Output(TerrainMaterialId.Backfill, level).sustained, Is.GreaterThan(soil[level - 1] * 1.3f), $"Backfill at level {level}");
            }
        }

        // Fresh and sustained output (m3/s) of the automatic motion boring straight down.
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
                    ? grid.RemoveShave(surface, profile.Radius, Vector3.up, profile.Radius * EquipmentProgression.ShavingDepthRatio, out _, true, 62 + cut,
                        profile.Radius * EquipmentProgression.DrillPointDepthRatio)
                    : grid.RemoveScoop(surface - Vector3.up * profile.Radius * .12f, profile.Radius, Vector3.up, 62 + cut, .1f, out _, true), Is.True);
                if (cut == 0) first = grid.LastRemovedVolume / interval;
            }
            return (first, grid.RemovedVolume / (12 * interval));
        }

        // A drill cut leaves a pointed middle, and the point keeps its depth below the floor instead of sinking with
        // every cut: boring in place digs about as fast as a flat cut (concept 04).
        [Test]
        public void DrillCutsLeaveAPointedMiddleThatDoesNotOutdigAFlatCut()
        {
            var profile = EquipmentProgression.ToolProfiles()[EquipmentProgression.DrillLevel - 1];
            float point = profile.Radius * EquipmentProgression.DrillPointDepthRatio;
            var (pointed, pointedVolume) = Bore(profile.Radius, point);
            var (flat, flatVolume) = Bore(profile.Radius, 0);
            Assert.That(pointedVolume, Is.EqualTo(flatVolume).Within(flatVolume * .12f), "Boring rate");
            var axis = new Vector3(1.5f, 0, 1.5f);
            float Dip(ExcavationGrid grid) => SurfaceAt(grid, axis + Vector3.right * profile.Radius * .6f) - SurfaceAt(grid, axis);
            Assert.That(Dip(pointed), Is.GreaterThan(point * .5f), "Pointed middle");
            Assert.That(Dip(flat), Is.LessThan(point * .2f), "Flat floor");
        }

        // Drill cuts into soil until the point has fully bored in (DrillPointStep), each from the deepest point under
        // the axis: the grid and the volume removed.
        private static (ExcavationGrid grid, float volume) Bore(float radius, float point)
        {
            var grid = new ExcavationGrid(new Vector3Int(48, 192, 48), .0625f);
            var saved = grid.Capture();
            saved.Materials = TerrainMaterialSnapshot.Uniform(saved.Density.Length, TerrainMaterialId.Soil);
            grid.Restore(saved);
            var axis = new Vector3(1.5f, 0, 1.5f);
            for (int cut = 0; cut < 24; cut++)
                Assert.That(grid.RemoveShave(new Vector3(axis.x, SurfaceAt(grid, axis), axis.z), radius, Vector3.up,
                    radius * EquipmentProgression.ShavingDepthRatio, out _, true, 62 + cut, point), Is.True);
            return (grid, grid.RemovedVolume);
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
            Assert.Throws<InvalidDataException>(() => TerrainMaterialSnapshot.CopyFrom(new byte[] { 0, 1, (byte)(TerrainMaterialSnapshot.Last + 1) }));
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
            Assert.That(grid.IsSolid(lip + Vector3.right * .45f), Is.EqualTo(solidX));
            Assert.That(grid.IsSolid(lip + Vector3.forward * .45f), Is.EqualTo(solidZ));
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
