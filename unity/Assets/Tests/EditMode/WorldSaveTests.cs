using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class WorldSaveTests
    {
        private string directory;
        [SetUp] public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "SDT-save-tests", Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [TestCase(0f)]
        [TestCase(0.43f)]
        [TestCase(1f)]
        public void WholeWorldRoundTripPreservesExactDensityItemsAndDeterministicNextCut(float stance)
        {
            var grid = new ExcavationGrid(new Vector3Int(32, 64, 32), 0.125f, 2718);
            grid.RemoveScoop(new Vector3(2, 2.95f, 2), 0.8f, Vector3.up, 72, 0.12f, out _);
            grid.RemoveScoop(new Vector3(2.3f, 2.7f, 2), 0.65f, Vector3.left, 73, 0.12f, out _);
            var state = Snapshot(1);
            state.CrouchAmount = stance;
            state.InventoryLevel = 3; state.InventoryCapacity = 20;
            state.FuelLevel = 4; state.BatteryCapacity = 300;
            state.Terrain = grid.Capture();
            using var memory = new MemoryStream();
            WorldSaveCodec.Write(memory, state);
            memory.Position = 0;
            var restored = WorldSaveCodec.Read(memory);
            AssertSame(state, restored);
            var loadedGrid = new ExcavationGrid(grid.Size, grid.CellSize);
            loadedGrid.Restore(restored.Terrain);
            var center = new Vector3(2.4f, 2.5f, 2);
            grid.RemoveScoop(center, 0.7f, Vector3.left, 74, 0.12f, out _, true);
            loadedGrid.RemoveScoop(center, 0.7f, Vector3.left, 74, 0.12f, out _, true);
            Assert.That(loadedGrid.Capture().Density.ToArray(), Is.EqualTo(grid.Capture().Density.ToArray()));
            Assert.That(loadedGrid.RemovedVolume, Is.EqualTo(grid.RemovedVolume));
        }

        [TestCase(SaveWriteStage.BeforeWrite, 1)]
        [TestCase(SaveWriteStage.TemporaryFlushed, 1)]
        [TestCase(SaveWriteStage.BeforeReplace, 1)]
        [TestCase(SaveWriteStage.Replaced, 2)]
        public void InterruptionAtCommitBoundaryLoadsOneCompleteWorld(SaveWriteStage interruption, int expected)
        {
            bool interrupt = false;
            var first = Snapshot(1);
            var second = Snapshot(2);
            second.Credits = 123;
            second.Inventory = Array.Empty<ItemSnapshot>(); // Sold: identity remains collected.
            var changedDensity = second.Terrain.Density.ToArray(); changedDensity[42] = -0.25f;
            second.Terrain.Density = DensitySnapshot.CopyFrom(changedDensity);
            second.Terrain.LowestCarvedY = 0;
            second.ShovelLevel = 3;
            second.CrouchAmount = 0.6f;
            using (var store = new WorldSaveStore(directory, stage => { if (interrupt && stage == interruption) throw new IOException("Simulated process interruption"); }))
            {
                store.Load(); store.Commit(first); interrupt = true;
                Assert.Throws<IOException>(() => store.Commit(second));
            }
            using var reopened = new WorldSaveStore(directory);
            var result = reopened.Load();
            AssertSame(expected == 1 ? first : second, result.Snapshot);
            Assert.That(result.Recovered, Is.False);
        }

        [Test]
        public void CorruptPrimaryRecoversBackupAndKeepsDamagedFileWhenContinuing()
        {
            byte[] damaged;
            using (var store = new WorldSaveStore(directory))
            {
                store.Load(); store.Commit(Snapshot(1)); store.Commit(Snapshot(2));
                damaged = File.ReadAllBytes(store.PrimaryPath);
                damaged[damaged.Length - 7] ^= 32;
                File.WriteAllBytes(store.PrimaryPath, damaged);
            }
            using var recovery = new WorldSaveStore(directory);
            var loaded = recovery.Load();
            Assert.That(loaded.Recovered, Is.True);
            Assert.That(loaded.Snapshot.Sequence, Is.EqualTo(1));
            recovery.Commit(Snapshot(3));
            Assert.That(File.ReadAllBytes(Directory.GetFiles(directory, "world.damaged-*.sav").Single()), Is.EqualTo(damaged));
            Assert.That(WorldSaveStore.Read(recovery.BackupPath).Sequence, Is.EqualTo(1));
            Assert.That(WorldSaveStore.Read(recovery.PrimaryPath).Sequence, Is.EqualTo(3));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(999)]
        public void IncompatiblePrimaryCannotSilentlyFallBackOrBeOverwritten(int version)
        {
            using (var store = new WorldSaveStore(directory))
            {
                store.Load(); store.Commit(Snapshot(1)); store.Commit(Snapshot(2));
                using var stream = new FileStream(store.PrimaryPath, FileMode.Open, FileAccess.Write);
                stream.Position = 8;
                stream.Write(BitConverter.GetBytes(version), 0, 4);
            }
            var before = Directory.GetFiles(directory, "*.sav").Select(File.ReadAllBytes).ToArray();
            using var reader = new WorldSaveStore(directory);
            Assert.Throws<UnsupportedSaveException>(() => reader.Load());
            Assert.Throws<InvalidOperationException>(() => reader.Commit(Snapshot(3)));
            Assert.That(Directory.GetFiles(directory, "*.sav").Select(File.ReadAllBytes).ToArray(), Is.EqualTo(before));
        }

        [Test]
        public void DamagedOnlyFileBlocksFreshWorldAndIncompleteCaptureKeepsPriorCheckpoint()
        {
            using (var store = new WorldSaveStore(directory))
            {
                store.Load(); store.Commit(Snapshot(1));
                var invalid = Snapshot(2);
                invalid.Inventory[0].Value++;
                Assert.Throws<InvalidDataException>(() => store.Commit(invalid));
                Assert.That(WorldSaveStore.Read(store.PrimaryPath).Sequence, Is.EqualTo(1));
                File.WriteAllBytes(store.PrimaryPath, new byte[] { 1, 2, 3 });
            }
            using var reader = new WorldSaveStore(directory);
            Assert.Throws<InvalidDataException>(() => reader.Load());
            Assert.Throws<InvalidOperationException>(() => reader.Commit(Snapshot(2)));
        }

        [Test]
        public void ProfileLockPreventsTwoGameInstancesFromOverwritingEachOther()
        {
            using (var first = new WorldSaveStore(directory))
            using (var second = new WorldSaveStore(directory))
            {
                first.Load(); first.Commit(Snapshot(1));
                Assert.Throws<SaveProfileInUseException>(() => second.Load());
            }
            using var reopened = new WorldSaveStore(directory);
            Assert.That(reopened.Load().Snapshot.Sequence, Is.EqualTo(1));
        }

        [TestCase(96)] [TestCase(192)] [TestCase(312)] [TestCase(336)] [TestCase(552)] [TestCase(DiscoveryField.MaximumPopulation)]
        public void SparseAndDensePopulationsRoundTripWithoutAddingOrRerollingFinds(int count)
        {
            var saved = Snapshot(1);
            saved.Inventory = Array.Empty<ItemSnapshot>();
            saved.Finds = Enumerable.Range(0, count).Select(i => new FindSnapshot {
                ContentId = "common_rock_a", Item = new ItemSnapshot { Id = "saved-" + i, Name = "Rock", Value = 2 },
                Position = new Vector3(i * .001f, .5f, .5f), Rotation = Quaternion.Euler(0, i % 360, 0),
                Scale = Vector3.one, State = i % 7 == 0 ? FindState.Collected : FindState.World, PhysicsReleased = i % 3 == 0
            }).ToArray();
            using var stream = new MemoryStream();
            WorldSaveCodec.Write(stream, saved); stream.Position = 0;
            AssertSame(saved, WorldSaveCodec.Read(stream));
            saved.Finds = new FindSnapshot[DiscoveryField.MaximumPopulation + 1];
            Assert.Throws<InvalidDataException>(() => saved.Validate());
        }

        [Test]
        public void CapturedDensityIsIndependentOfFurtherDiggingAndRejectsInvalidSamples()
        {
            var grid = new ExcavationGrid(new Vector3Int(16, 16, 16), 0.125f);
            var snapshot = grid.Capture();
            var before = snapshot.Density.ToArray();
            grid.RemoveSphere(new Vector3(1, 1.9f, 1), 0.5f, out _);
            Assert.That(snapshot.Density.ToArray(), Is.EqualTo(before));
            Assert.That(grid.Capture().Density.ToArray(), Is.Not.EqualTo(before));
            before[0] = float.NaN; snapshot.Density = DensitySnapshot.CopyFrom(before);
            Assert.Throws<InvalidDataException>(snapshot.Validate);
        }

        [Test]
        public void MainGridCaptureCopiesOnlyAPageTableAndSmallCutsShareUntouchedPages()
        {
            var grid = new ExcavationGrid(new Vector3Int(192, 96, 192), 0.125f);
            var first = grid.Capture();
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            var unchanged = grid.Capture();
            bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            // Native Mono can report zero through this API; the native fixture uses
            // ProfilerRecorder instead. Shared page identity is checked on every host.
            if (bytes > 0) Assert.That(bytes, Is.LessThan(16384), "Capturing may copy references, never the 14 MB density field.");
            Assert.That(grid.SnapshotCopiedBytes, Is.Zero);
            Assert.That(first.Density.SharedPageCount(unchanged.Density), Is.EqualTo(first.Density.PageCount));
            Assert.That(grid.RemoveScoop(new Vector3(12, 11.95f, 12), 0.41f, Vector3.up, 42, 0.12f, out _), Is.True);
            var cut = grid.Capture();
            int changed = first.Density.PageCount - first.Density.SharedPageCount(cut.Density);
            Assert.That(changed, Is.InRange(1, 32));
            Assert.That(grid.SnapshotCopiedBytes, Is.LessThan(512 * 1024));
            Assert.That(first.Density.ToArray(), Is.EqualTo(unchanged.Density.ToArray()));
            Assert.That(cut.Density.ToArray(), Is.Not.EqualTo(first.Density.ToArray()));
        }

        [Test]
        public void ResetRestoreAndExternalArraysCannotMutateAnOlderCheckpoint()
        {
            var grid = new ExcavationGrid(new Vector3Int(24, 20, 24), 0.125f);
            grid.RemoveSphere(new Vector3(1.5f, 2.4f, 1.5f), 0.65f, out _);
            var saved = grid.Capture();
            var expected = saved.Density.ToArray();
            var imported = DensitySnapshot.CopyFrom(expected);
            expected[0] = float.NaN;
            Assert.That(float.IsNaN(imported[0]), Is.False);
            grid.Reset();
            Assert.That(grid.Capture().Density.ToArray(), Is.Not.EqualTo(saved.Density.ToArray()));
            grid.Restore(saved);
            Assert.That(grid.Capture().Density.SharedPageCount(saved.Density), Is.EqualTo(saved.Density.PageCount));
            grid.RemoveSphere(new Vector3(1.5f, 2.0f, 1.5f), 0.65f, out _);
            Assert.That(saved.Density.ToArray(), Is.EqualTo(imported.ToArray()));
        }

        [Test]
        public void RetainedWriterSnapshotKeepsOneRevisionAcrossFurtherLiveEdits()
        {
            var grid = new ExcavationGrid(new Vector3Int(64, 48, 64), 0.125f);
            var state = Snapshot(1); state.Terrain = grid.Capture();
            using var started = new System.Threading.ManualResetEventSlim();
            using var proceed = new System.Threading.ManualResetEventSlim();
            var encoding = System.Threading.Tasks.Task.Run(() =>
            {
                started.Set(); proceed.Wait();
                using var stream = new MemoryStream();
                WorldSaveCodec.Write(stream, state); stream.Position = 0;
                return WorldSaveCodec.Read(stream);
            });
            Assert.That(started.Wait(5000), Is.True);
            try
            {
                for (int i = 0; i < 24; i++)
                {
                    grid.RemoveSphere(new Vector3(1 + i % 6, 5.9f - i / 12 * 0.3f, 1 + i / 6), 0.6f, out _);
                    grid.Capture();
                }
            }
            finally { proceed.Set(); }
            Assert.That(encoding.Wait(5000), Is.True);
            AssertSame(state, encoding.Result);
            Assert.That(encoding.Result.Terrain.Revision, Is.Zero);
            Assert.That(grid.Revision, Is.GreaterThan(0));
        }

        [TestCase(SaveWriteStage.BeforeWrite, 1)]
        [TestCase(SaveWriteStage.TemporaryFlushed, 1)]
        [TestCase(SaveWriteStage.BeforeReplace, 1)]
        [TestCase(SaveWriteStage.Replaced, 7)]
        public void InterruptedNewGameKeepsACompleteWorldAndArchivesPreviousFiles(SaveWriteStage interruption, int expected)
        {
            var old = Snapshot(1);
            var fresh = Snapshot(7);
            fresh.Credits = 0;
            fresh.ShovelLevel = 1;
            using (var prior = new WorldSaveStore(directory)) { prior.Load(); prior.Commit(old); }
            var oldBytes = File.ReadAllBytes(Path.Combine(directory, "world.sav"));
            using (var replacement = new WorldSaveStore(directory, stage => { if (stage == interruption) throw new IOException("Interrupted new game"); }))
                Assert.Throws<IOException>(() => replacement.ReplaceWithNewGame(fresh, true));
            using var reopened = new WorldSaveStore(directory);
            AssertSame(expected == 1 ? old : fresh, reopened.Load().Snapshot);
            if (interruption == SaveWriteStage.BeforeReplace || interruption == SaveWriteStage.Replaced)
            {
                string archive = Directory.GetFiles(Path.Combine(directory, "PreviousGames"), "world.sav", SearchOption.AllDirectories).Single();
                Assert.That(File.ReadAllBytes(archive), Is.EqualTo(oldBytes));
            }
        }

        [Test]
        public void NewGameRequiresReplacementConsentAndAnExclusiveProfileLock()
        {
            using (var prior = new WorldSaveStore(directory)) { prior.Load(); prior.Commit(Snapshot(1)); }
            using (var declined = new WorldSaveStore(directory))
                Assert.Throws<IOException>(() => declined.ReplaceWithNewGame(Snapshot(2), false));
            using (var owner = new WorldSaveStore(directory))
            using (var second = new WorldSaveStore(directory))
            {
                owner.Load();
                Assert.Throws<SaveProfileInUseException>(() => second.ReplaceWithNewGame(Snapshot(2), true));
            }
            Assert.That(WorldSaveStore.Read(Path.Combine(directory, "world.sav")).Sequence, Is.EqualTo(1));
        }

        [Test]
        public void NewGameArchivesUnreadableSlotAndRecoveryBelongsToTheFreshWorld()
        {
            Directory.CreateDirectory(directory);
            byte[] damaged = { 1, 2, 3 };
            File.WriteAllBytes(Path.Combine(directory, "world.sav"), damaged);
            File.WriteAllBytes(Path.Combine(directory, "world.pending"), new byte[] { 5, 6 });
            var fresh = Snapshot(3);
            fresh.Credits = 0;
            using (var store = new WorldSaveStore(directory)) store.ReplaceWithNewGame(fresh, true);
            Assert.That(File.ReadAllBytes(Directory.GetFiles(Path.Combine(directory, "PreviousGames"), "world.sav", SearchOption.AllDirectories).Single()), Is.EqualTo(damaged));
            Assert.That(File.Exists(Path.Combine(directory, "world.pending")), Is.False);
            File.WriteAllBytes(Path.Combine(directory, "world.sav"), damaged);
            using var recovery = new WorldSaveStore(directory);
            var result = recovery.Load();
            Assert.That(result.Recovered, Is.True);
            AssertSame(fresh, result.Snapshot);
        }

        private static WorldSnapshot Snapshot(long sequence)
        {
            var item = new ItemSnapshot { Id = "stable-find-42", Name = "Blue marble", Value = 5 };
            return new WorldSnapshot { Sequence = sequence, UtcTicks = DateTime.UtcNow.Ticks,
                Terrain = new ExcavationGrid(new Vector3Int(8, 8, 8), 0.125f).Capture(), TerrainRotation = Quaternion.identity,
                ExcavationSeed = 2718, DiscoverySeed = 90127,
                Finds = new[] { new FindSnapshot { ContentId = "blue-marble", Item = item, Position = Vector3.one * 0.5f,
                    Rotation = Quaternion.identity, Scale = Vector3.one, State = FindState.Collected } },
                Inventory = new[] { new ItemSnapshot { Id = item.Id, Name = item.Name, Value = item.Value } },
                InventoryCapacity = 10, Credits = 17, ShovelLevel = 2, BatteryCapacity = 100, BatteryCharge = 37.25f,
                PlayerPosition = new Vector3(0.1f, 1, 0.4f), PlayerRotation = Quaternion.Euler(0, 76, 0), Pitch = 42, VerticalSpeed = -2 };
        }

        [Test]
        public void DifferentTerrainLayoutIsRejectedWithoutChangingTheSnapshot()
        {
            var saved = Snapshot(1);
            saved.ValidateTerrain(saved.Terrain.Size, saved.Terrain.CellSize, saved.TerrainPosition, saved.TerrainRotation);
            Assert.Throws<InvalidDataException>(() => saved.ValidateTerrain(saved.Terrain.Size + Vector3Int.up, saved.Terrain.CellSize, saved.TerrainPosition, saved.TerrainRotation));
            Assert.Throws<InvalidDataException>(() => saved.ValidateTerrain(saved.Terrain.Size, saved.Terrain.CellSize, saved.TerrainPosition + Vector3.right, saved.TerrainRotation));
        }

        private static void AssertSame(WorldSnapshot expected, WorldSnapshot actual)
        {
            Assert.That(actual.Sequence, Is.EqualTo(expected.Sequence));
            Assert.That(actual.Terrain.Density.ToArray(), Is.EqualTo(expected.Terrain.Density.ToArray()));
            Assert.That(actual.Terrain.Materials.ToArray(), Is.EqualTo(expected.Terrain.Materials.ToArray()));
            Assert.That(actual.Terrain.Revision, Is.EqualTo(expected.Terrain.Revision));
            Assert.That(actual.Terrain.LowestCarvedY, Is.EqualTo(expected.Terrain.LowestCarvedY));
            Assert.That(actual.Terrain.RemovedVolume, Is.EqualTo(expected.Terrain.RemovedVolume));
            Assert.That(actual.Credits, Is.EqualTo(expected.Credits));
            Assert.That(actual.ShovelLevel, Is.EqualTo(expected.ShovelLevel));
            Assert.That(actual.BatteryCharge, Is.EqualTo(expected.BatteryCharge));
            Assert.That(actual.InventoryLevel, Is.EqualTo(expected.InventoryLevel));
            Assert.That(actual.InventoryCapacity, Is.EqualTo(expected.InventoryCapacity));
            Assert.That(actual.FuelLevel, Is.EqualTo(expected.FuelLevel));
            Assert.That(actual.BatteryCapacity, Is.EqualTo(expected.BatteryCapacity));
            Assert.That(actual.PlayerPosition, Is.EqualTo(expected.PlayerPosition));
            Assert.That(actual.PlayerRotation, Is.EqualTo(expected.PlayerRotation));
            Assert.That(actual.CrouchAmount, Is.EqualTo(expected.CrouchAmount));
            Assert.That(actual.Inventory.Select(i => (i.Id, i.Name, i.Value)), Is.EqualTo(expected.Inventory.Select(i => (i.Id, i.Name, i.Value))));
            Assert.That(actual.Finds.Select(f => (f.ContentId, f.Item.Id, f.Collected, f.Position, f.Rotation, f.Scale, f.PhysicsReleased)),
                Is.EqualTo(expected.Finds.Select(f => (f.ContentId, f.Item.Id, f.Collected, f.Position, f.Rotation, f.Scale, f.PhysicsReleased))));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        public void InvalidStanceCannotReplaceAValidCheckpoint(float stance)
        {
            using var store = new WorldSaveStore(directory);
            store.Load();
            store.Commit(Snapshot(1));
            var invalid = Snapshot(2);
            invalid.CrouchAmount = stance;
            Assert.Throws<InvalidDataException>(() => store.Commit(invalid));
            Assert.That(WorldSaveStore.Read(store.PrimaryPath).Sequence, Is.EqualTo(1));
        }

    }
}
