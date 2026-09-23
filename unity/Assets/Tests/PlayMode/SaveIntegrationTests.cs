#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SomethingDownThere.Tests
{
    public sealed class SaveIntegrationTests
    {
        private Scene scene;
        private FpsPlayer player;
        private TerrainVolume terrain;
        private DiscoveryField discoveries;
        private WorldSaveController save;
        private string directory;
        private InputTestFixture devices;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "SDT-save-integration", Guid.NewGuid().ToString("N"));
            devices = new InputTestFixture(); devices.Setup();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            Time.timeScale = 1;
            yield return Open();
        }

        private IEnumerator Open()
        {
            // The fixture deliberately disables its player; unloading it cannot run
            // OnDisable again to release a Pause opened during save inspection.
            Time.timeScale = 1f;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            player = scene.GetRootGameObjects()[0].GetComponentInChildren<FpsPlayer>();
            player.enabled = false;
            player.SetApplicationFocus(true);
            terrain = player.ExcavationTerrain;
            discoveries = player.Discoveries;
            save = player.GetComponent<WorldSaveController>();
            Assert.That(save, Is.Not.Null, "MainGame owns its save integration.");
            save.BeginSession(directory);
            Assert.That(save.BlocksPlay, Is.True);
            Assert.That(player.GameplayActive, Is.False);
            yield return Until(() => save.State != WorldSaveState.Loading);
            player.SetApplicationFocus(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (save != null && save.State == WorldSaveState.Saving) yield return Until(() => save.State != WorldSaveState.Saving);
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices.TearDown();
            Time.timeScale = 1;
            // OnDestroy may release the profile after its immutable writer finishes.
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (Directory.Exists(directory))
            {
                bool removed = false;
                try { Directory.Delete(directory, true); removed = true; }
                catch (IOException) { if (Time.realtimeSinceStartupAsDouble >= deadline) throw; }
                if (removed) break;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ExcavationAndOwnedProgressSurviveReload()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            player.CloseMenu(); yield return null;
            player.enabled = true;
            player.enabled = false;
            Assert.That(Physics.Raycast(new Vector3(-8, 2, -8), Vector3.down, out var hit, 5), Is.True);
            Assert.That(terrain.TryDig(hit, .6f), Is.True);
            var density = terrain.Capture().Density.ToArray();
            var population = discoveries.Capture().Select(f => f.Item.Id).ToArray();
            int owned = player.Shovel.Level; float charge = player.Battery.Charge;
            long sequence = save.CompletedSequence; save.RequestCheckpoint();
            yield return Until(() => save.CompletedSequence > sequence && save.State == WorldSaveState.Ready);
            yield return SceneManager.UnloadSceneAsync(scene); yield return Open();
            Assert.That(player.Shovel.Level, Is.EqualTo(owned)); Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            Assert.That(terrain.Capture().Density.ToArray(), Is.EqualTo(density));
            Assert.That(discoveries.Capture().Select(f => f.Item.Id), Is.EqualTo(population));
        }

        [UnityTest]
        public IEnumerator WholeRefillBalancesAndFullFuelSurviveRepeatedFileReloads()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            player.Wallet.TryCredit(10);
            player.Battery.RestoreCharge(13.00586f);
            var quote = player.Trade.OfferRefill();
            Assert.That(quote.Cost, Is.EqualTo(1));
            Assert.That(player.Trade.TryRefill(quote), Is.True);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                long sequence = save.CompletedSequence;
                save.RequestCheckpoint();
                yield return Until(() => save.CompletedSequence > sequence && save.State == WorldSaveState.Ready);
                yield return SceneManager.UnloadSceneAsync(scene); yield return Open();
                Assert.That(player.Wallet.Balance, Is.EqualTo(9));
                Assert.That(player.Battery.Charge, Is.EqualTo(100));
                Assert.That(player.Trade.TryRefill(player.Trade.OfferRefill()), Is.False);
                Assert.That(player.Trade.TryUpgrade(player.Trade.OfferUpgrade()), Is.False, "A refill leaves only $9, insufficient for the $10 shovel.");
            }
            player.Wallet.TryCredit(10);
            Assert.That(player.Trade.TryUpgrade(player.Trade.OfferUpgrade(EquipmentKind.Fuel)), Is.True);
            player.Battery.RestoreCharge(15.125f);
            var larger = player.Trade.OfferRefill();
            Assert.That(larger.Cost, Is.EqualTo(2));
            Assert.That(player.Trade.TryRefill(larger), Is.True);
            Assert.That(player.Battery.Charge, Is.EqualTo(150));
            Assert.That(player.Wallet.Balance, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator CollectionSavedDuringVisualTravelRestoresOneCarriedIdentity()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            player.CloseMenu(); yield return null;
            var find = discoveries.Finds[0]; Expose(find);
            player.ViewCamera.transform.position = find.transform.position + Vector3.up * 1.5f;
            player.ViewCamera.transform.LookAt(find.transform.position); Physics.SyncTransforms();
            Assert.That(find.TryCollect(player), Is.True);
            string identity = find.Item.InstanceId;
            Assert.That(player.transform.Find("Pickup visual").gameObject.activeSelf, Is.True);
            long sequence = save.CompletedSequence;
            save.RequestCheckpoint();
            yield return Until(() => save.CompletedSequence > sequence && save.State == WorldSaveState.Ready);
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return Open();
            Assert.That(discoveries.Finds.Single(f => f.Item.InstanceId == identity).Collected, Is.True);
            Assert.That(player.Inventory.Items.Count(i => i.InstanceId == identity), Is.EqualTo(1));
            Assert.That(player.transform.Find("Pickup visual"), Is.Null, "Cosmetic copies are not restored from saves.");
        }

        [UnityTest]
        public IEnumerator RealExcavationSaleUpgradeAndRescueSurviveRepeatedWholeWorldLoads()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            player.CloseMenu();
            yield return null;
            player.SetApplicationFocus(true);
            player.CloseMenu();
            var find = discoveries.Finds[0];
            Expose(find);
            player.ViewCamera.transform.position = find.transform.position + Vector3.up * 1.5f;
            player.ViewCamera.transform.LookAt(find.transform.position);
            Physics.SyncTransforms();
            Assert.That(find.TryCollect(player), Is.True);
            string collectedId = find.Item.InstanceId;
            int value = find.Item.SaleValue;
            Assert.That(player.Trade.TrySell(player.Trade.OfferSale()), Is.True);
            Assert.That(player.Wallet.Balance, Is.EqualTo(value));
            // Use actual low-value trial finds until this real sale funds the upgrade.
            int soldCount = 1;
            while (player.Wallet.Balance < 10)
            {
                var next = discoveries.Finds.First(f => !f.Collected);
                Expose(next);
                player.ViewCamera.transform.position = next.transform.position + Vector3.up * 1.5f;
                player.ViewCamera.transform.LookAt(next.transform.position);
                Physics.SyncTransforms();
                Assert.That(next.TryCollect(player), Is.True);
                Assert.That(player.Trade.TrySell(player.Trade.OfferSale()), Is.True);
                soldCount++;
            }
            var offer = player.Trade.OfferUpgrade();
            Assert.That(player.Trade.TryUpgrade(offer), Is.True);
            Assert.That(player.Trade.TryUpgrade(offer), Is.False);
            // Two tier upgrades plus the refill that follows them.
            player.Wallet.TryCredit(2 * EquipmentProgression.Price(1) + 1);
            Assert.That(player.Trade.TryUpgrade(player.Trade.OfferUpgrade(EquipmentKind.Inventory)), Is.True);
            Assert.That(player.Trade.TryUpgrade(player.Trade.OfferUpgrade(EquipmentKind.Fuel)), Is.True);
            Assert.That(player.Battery.Charge, Is.EqualTo(100), "Capacity purchase preserves charge.");
            Assert.That(player.Trade.TryRefill(player.Trade.OfferRefill()), Is.True);
            // Cut into a side face, not only the upward-facing entrance.
            var point = find.transform.position;
            // Low-value finds require more nearby excavation to fund the purchase.
            // Author a real shaft below that widened area before testing its wall.
            for (int i = 0; i < 16; i++)
            {
                Assert.That(Physics.Raycast(point + Vector3.up * 4, Vector3.down, out var floor, 12), Is.True);
                if (floor.point.y < point.y - 1.2f) break;
                Assert.That(terrain.TryDig(floor, .65f), Is.True);
            }
            bool sideways = false;
            foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (Physics.Raycast(point + Vector3.down * .6f, direction, out var side, 3)
                    && side.collider.GetComponentInParent<TerrainVolume>() == terrain)
                    sideways |= terrain.TryDig(side, 0.65f);
            Assert.That(sideways, Is.True);
            player.AdminReturnToSurface();
            player.Battery.TrySpend(37.5f);
            player.SelectAdminLevel(6);
            player.ToggleAdminUnlimitedBattery();
            player.OpenMenu(PlayerMenu.Pause);
            long previous = save.CompletedSequence;
            save.RequestCheckpoint();
            yield return Until(() => save.CompletedSequence > previous && save.State == WorldSaveState.Ready);
            var expected = WorldSaveStore.Read(Path.Combine(directory, "world.sav"));
            Assert.That(expected.ShovelLevel, Is.EqualTo(2), "Admin level 6 is not owned progression.");
            Assert.That(expected.BatteryCharge, Is.EqualTo(112.5f));
            Vector3 rayOrigin = find.transform.position + Vector3.up * 4;
            Assert.That(Physics.Raycast(rayOrigin, Vector3.down, out var ground, 12), Is.True);
            float groundY = ground.point.y;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
                yield return Open();
                Assert.That(save.State, Is.EqualTo(WorldSaveState.Ready));
                Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
                Assert.That(player.HasAdminOverrides, Is.False);
                Assert.That(player.Shovel.Level, Is.EqualTo(expected.ShovelLevel));
                Assert.That(player.Wallet.Balance, Is.EqualTo(expected.Credits));
                Assert.That(player.Inventory.Count, Is.Zero);
                Assert.That(player.Battery.Charge, Is.EqualTo(expected.BatteryCharge));
                Assert.That(player.Inventory.Level, Is.EqualTo(2));
                Assert.That(player.Inventory.Capacity, Is.EqualTo(15));
                Assert.That(player.Battery.Level, Is.EqualTo(2));
                Assert.That(player.Battery.Capacity, Is.EqualTo(150));
                Assert.That(player.Trade.OfferUpgrade(EquipmentKind.Fuel).Cost, Is.EqualTo(EquipmentProgression.Price(2)));
                Assert.That(player.transform.position, Is.EqualTo(expected.PlayerPosition));
                Assert.That(terrain.Capture().Density.ToArray(), Is.EqualTo(expected.Terrain.Density.ToArray()));
                Assert.That(discoveries.Finds.Single(f => f.Item.InstanceId == collectedId).Collected, Is.True);
                Assert.That(discoveries.Finds.Count, Is.EqualTo(discoveries.Catalog.TotalCount));
                Assert.That(discoveries.Finds.Count(f => f.Collected), Is.EqualTo(soldCount));
                Assert.That(Physics.Raycast(rayOrigin, Vector3.down, out ground, 12), Is.True);
                Assert.That(ground.point.y, Is.EqualTo(groundY).Within(0.001f), "Collision must be restored before Resume is available.");
            }
            // Rescue uses the same boundary and must not respawn its lost discovery.
            player.CloseMenu();
            var carried = discoveries.Finds.First(f => !f.Collected);
            Expose(carried);
            player.ViewCamera.transform.position = carried.transform.position + Vector3.up * 1.5f;
            player.ViewCamera.transform.LookAt(carried.transform.position);
            Physics.SyncTransforms();
            Assert.That(carried.TryCollect(player), Is.True);
            player.enabled = true;
            player.SetApplicationFocus(true);
            player.Battery.TrySpend(player.Battery.Charge);
            previous = save.CompletedSequence;
            yield return null;
            yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(player.Battery.Capacity));
            player.OpenMenu(PlayerMenu.Pause);
            yield return Until(() => save.CompletedSequence > previous && save.State == WorldSaveState.Ready);
            player.enabled = false;
            var rescued = WorldSaveStore.Read(Path.Combine(directory, "world.sav"));
            Assert.That(rescued.Inventory, Is.Empty);
            Assert.That(rescued.Finds.Count(f => f.Collected), Is.EqualTo(soldCount + 1));
            Assert.That(rescued.Credits, Is.EqualTo(Math.Max(0, expected.Credits - 10)));
            Assert.That(rescued.BatteryCharge, Is.EqualTo(150));
            Assert.That(rescued.Terrain.RemovedVolume, Is.GreaterThanOrEqualTo(expected.Terrain.RemovedVolume));
        }

        [UnityTest]
        public IEnumerator DirtyAutosaveAndOverlappingTransactionRequestsKeepLatestStateWithoutIdleGridCopies()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            long sequence = save.CompletedSequence;
            long copies = save.CapturedTerrainCopies;
            player.Battery.TrySpend(1);
            double started = Time.realtimeSinceStartupAsDouble;
            yield return Until(() => save.CompletedSequence > sequence);
            double elapsed = Time.realtimeSinceStartupAsDouble - started;
            Assert.That(elapsed, Is.InRange(WorldSaveController.AutosaveSeconds, WorldSaveController.AutosaveSeconds + 1));
            Assert.That(save.CapturedTerrainCopies, Is.EqualTo(copies), "Battery-only checkpoints reuse immutable density.");
            sequence = save.CompletedSequence;
            player.Wallet.TryCredit(10);
            save.RequestCheckpoint();
            yield return null;
            yield return null;
            Assert.That(player.Trade.TryUpgrade(player.Trade.OfferUpgrade()), Is.True);
            player.Battery.TrySpend(3);
            save.RequestCheckpoint();
            yield return Until(() => save.CompletedSequence > sequence && save.State == WorldSaveState.Ready);
            var latest = WorldSaveStore.Read(Path.Combine(directory, "world.sav"));
            Assert.That(latest.Credits, Is.Zero);
            Assert.That(latest.ShovelLevel, Is.EqualTo(2));
            Assert.That(latest.BatteryCharge, Is.EqualTo(96));
            Assert.That(save.CapturedTerrainCopies, Is.EqualTo(copies));
            // The 100 m site is 3.1x the old density, so the background encode (validate +
            // gzip + atomic replace) grows with it. Frame impact is the save-performance
            // fixture's job; this only guards against runaway checkpoint work.
            Assert.That(save.LastCheckpointLatencyMilliseconds, Is.LessThanOrEqualTo(2000));
            UnityEngine.Debug.Log($"Save timing: dirty={elapsed:F3}s capture={save.LastCaptureMilliseconds:F3}ms write={save.LastWriteMilliseconds:F3}ms checkpoint={save.LastCheckpointLatencyMilliseconds:F3}ms");
        }

        [UnityTest]
        public IEnumerator DamagedPrimaryUsesReadableRecoveryAndUnknownVersionCannotResume()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            player.Wallet.TryCredit(7);
            long previous = save.CompletedSequence;
            yield return Until(() => save.CompletedSequence > previous && save.State == WorldSaveState.Ready);
            yield return SceneManager.UnloadSceneAsync(scene);
            string path = Path.Combine(directory, "world.sav");
            var bytes = File.ReadAllBytes(path);
            bytes[bytes.Length - 1] ^= 1;
            File.WriteAllBytes(path, bytes);
            yield return Open();
            Assert.That(save.State, Is.EqualTo(WorldSaveState.Recovery));
            Assert.That(player.GameplayActive, Is.False);
            player.CloseMenu();
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Persistence));
            save.AcceptRecovery();
            yield return Until(() => save.State == WorldSaveState.Ready && save.CompletedSequence > 1);
            Assert.That(Directory.GetFiles(directory, "world.damaged-*.sav").Length, Is.EqualTo(1));
            yield return SceneManager.UnloadSceneAsync(scene);
            bytes = File.ReadAllBytes(path); bytes[8] = 99; File.WriteAllBytes(path, bytes);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("World save: SomethingDownThere.UnsupportedSaveException"));
            yield return Open();
            Assert.That(save.State, Is.EqualTo(WorldSaveState.LoadFailed));
            Assert.That(save.BlocksPlay, Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        }

        [UnityTest]
        public IEnumerator UnwritableCheckpointPausesWithRetryAndKeepsLatestWorldInMemory()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            long previous = save.CompletedSequence;
            var accepted = File.ReadAllBytes(Path.Combine(directory, "world.sav"));
            string blocked = Path.Combine(directory, "world.pending");
            Directory.CreateDirectory(blocked);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("World save: System.(UnauthorizedAccessException|IO.IOException)"));
            player.Wallet.TryCredit(23);
            yield return Until(() => save.State == WorldSaveState.WriteFailed);
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Persistence));
            Assert.That(save.BlocksPlay, Is.True);
            Assert.That(MenuTestUI.Text(player, "menuTitle"), Is.EqualTo("Progress could not be saved"));
            Assert.That(MenuTestUI.View(player).Root.Query<UnityEngine.UIElements.Button>().ToList().Any(b => b.text == "Open save folder"), Is.False);
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "world.sav")), Is.EqualTo(accepted));
            player.CloseMenu();
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Persistence));
            save.RequestExit();
            Assert.That(save.State, Is.EqualTo(WorldSaveState.ConfirmQuit));
            save.CancelUnsavedExit();
            Directory.Delete(blocked);
            save.Retry();
            yield return Until(() => save.CompletedSequence > previous && save.State == WorldSaveState.Ready);
            Assert.That(WorldSaveStore.Read(Path.Combine(directory, "world.sav")).Credits, Is.EqualTo(23));
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
        }

        [UnityTest]
        public IEnumerator ReleaseWriteFailureQuitsWithoutReplacingTheCurrentMenuOrCheckpoint()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            var accepted = File.ReadAllBytes(Path.Combine(directory, "world.sav"));
            var menu = player.Menu;
            bool quit = false;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                typeof(WorldSaveController).GetMethod("PresentFailure", flags).Invoke(save,
                    new object[] { false, false, (System.Action)(() => quit = true) });
                Assert.That(quit, Is.True);
                Assert.That(save.ExitRequested, Is.True);
                Assert.That(save.BlocksPlay, Is.True);
                Assert.That(save.State, Is.EqualTo(WorldSaveState.Ready), "Release never enters the write-error dialog state.");
                Assert.That(player.Menu, Is.EqualTo(menu));
                Assert.That(File.ReadAllBytes(Path.Combine(directory, "world.sav")), Is.EqualTo(accepted));
            }
            finally { typeof(WorldSaveController).GetField("exitRequested", flags).SetValue(save, false); }
        }

        [UnityTest]
        public IEnumerator CrouchOnlyChangesAutosaveAndLowRoofStanceSurvivesRelaunch()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            player.CloseMenu();
            yield return null;
            long previous = save.CompletedSequence;
            // No position, camera angle or battery change: stance alone is dirty.
            player.Tuning.Gravity = 0;
            player.Tick(new FpsInputFrame { CrouchHeld = true }, 0.1f);
            float partial = player.CrouchAmount;
            Assert.That(partial, Is.InRange(0.1f, 0.9f));
            yield return Until(() => save.CompletedSequence > previous && save.State == WorldSaveState.Ready);
            Assert.That(WorldSaveStore.Read(Path.Combine(directory, "world.sav")).CrouchAmount, Is.EqualTo(partial));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return Open();
            Assert.That(player.CrouchAmount, Is.EqualTo(partial));
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
            player.CloseMenu();
            yield return null;
            yield return CrouchTerrainFixture.Prepare(terrain);
            // Long terrain restores can overlap a native Editor focus transition. This
            // fixture tests saved stance; dedicated input tests own focus/pause barriers.
            player.SetApplicationFocus(true);
            if (player.Menu == PlayerMenu.Pause) player.CloseMenu();
            yield return null;
            Assert.That(save.BlocksPlay, Is.False);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            // Keep discovery state real, but place the fixture away from its finds.
            CrouchTerrainFixture.Place(player, new Vector3(6, -2.9f, 0), 1);
            player.Tick(default, 1f / 60);
            Assert.That(player.StandBlocked, Is.True, $"Stance={player.CrouchAmount}, position={player.transform.position}, menu={player.Menu}, save={save.State}");
            previous = save.CompletedSequence;
            save.RequestCheckpoint();
            yield return Until(() => save.CompletedSequence > previous && save.State == WorldSaveState.Ready);
            var expected = save.Capture(previous + 1);
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return Open();
            Assert.That(save.State, Is.EqualTo(WorldSaveState.Ready));
            Assert.That(player.CrouchAmount, Is.EqualTo(1));
            Assert.That(player.transform.position, Is.EqualTo(expected.PlayerPosition));
            Assert.That(player.ViewCamera.transform.localPosition.y, Is.EqualTo(0.95f).Within(0.001f));
            Assert.That(terrain.Capture().Density.ToArray(), Is.EqualTo(expected.Terrain.Density.ToArray()));
            player.CloseMenu();
            yield return null;
            player.Tick(default, 1f / 60);
            Assert.That(player.StandBlocked, Is.True, $"Stance={player.CrouchAmount}, position={player.transform.position}, menu={player.Menu}, save={save.State}");
            Assert.That(player.CrouchAmount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ImpossibleSavedStanceStaysBehindRecoveryWithoutEditingTheSave()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            var snapshot = save.Capture(save.CompletedSequence + 1);
            snapshot.PlayerPosition = new Vector3(0, -6, 0); // Entire capsule inside solid soil.
            snapshot.CrouchAmount = 1;
            yield return SceneManager.UnloadSceneAsync(scene);
            string path = Path.Combine(directory, "world.sav");
            using (var stream = File.Create(path)) WorldSaveCodec.Write(stream, snapshot);
            var bytes = File.ReadAllBytes(path);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("World save: System.IO.InvalidDataException: The saved player stance"));
            yield return Open();
            Assert.That(save.State, Is.EqualTo(WorldSaveState.LoadFailed));
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Persistence));
            Assert.That(save.BlocksPlay, Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        }

        [UnityTest]
        public IEnumerator FindMotionAloneAutosavesAndReleasedPoseSurvivesReload()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            player.SetApplicationFocus(true);
            player.CloseMenu();
            yield return null;
            long previous = save.CompletedSequence, terrainRevision = terrain.StateRevision;
            Vector3 playerPosition = player.transform.position;
            var find = discoveries.Finds[0]; var physical = find.GetComponent<FindPhysics>();
            find.transform.SetPositionAndRotation(terrain.transform.TransformPoint(new Vector3(12, terrain.Dimensions.y * terrain.CellSize + .8f, 12)), Quaternion.Euler(0, 0, 90));
            physical.Restore(false); Physics.SyncTransforms(); find.RefreshExposure();
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "The physics fixture must remain resumed while the find settles.");
            Assert.That(physical.Released, Is.True);
            Assert.That(terrain.StateRevision, Is.EqualTo(terrainRevision));
            Assert.That(player.transform.position, Is.EqualTo(playerPosition));
            yield return Until(() => save.CompletedSequence > previous && save.State == WorldSaveState.Ready);
            var expected = WorldSaveStore.Read(Path.Combine(directory, "world.sav")).Finds.First(f => f.Item.Id == find.Item.InstanceId);
            Assert.That(expected.PhysicsReleased, Is.True);
            Assert.That(Vector3.Distance(expected.Position, find.Capture().Position), Is.LessThan(.005f));
            yield return SceneManager.UnloadSceneAsync(scene); yield return Open();
            var restored = discoveries.Finds.First(f => f.Item.InstanceId == expected.Item.Id);
            Assert.That(Vector3.Distance(restored.Capture().Position, expected.Position), Is.LessThan(.00001f));
            Assert.That(restored.GetComponent<FindPhysics>().Released, Is.True);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
            player.SetApplicationFocus(true);
            player.CloseMenu(); yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Restored physics must resume before checking exposure.");
            Assert.That(restored.Collectible, Is.True);
        }

        [UnityTest]
        public IEnumerator FindAppearancePoseHistoricalValueAndCollectedAbsenceSurviveFileReload()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            var snapshot = save.Capture(save.CompletedSequence + 1);
            // One representative per shipped find type: coal chips plus two ore variants.
            var rocks = snapshot.Finds.GroupBy(f => f.ContentId).Select(g => g.First()).Take(3).ToArray();
            Assert.That(rocks.Length, Is.EqualTo(3));
            for (int i = 0; i < rocks.Length; i++)
            {
                rocks[i].Position = new Vector3(12 + i * 1.5f, terrain.Dimensions.y * terrain.CellSize + .5f, 12);
                rocks[i].Rotation = Quaternion.Euler(19 + i * 27, 33 + i * 53, 71 + i * 31);
                rocks[i].PhysicsReleased = true;
                rocks[i].Item.Value = 7 + i;
                rocks[i].State = i == 0 ? FindState.Collected : FindState.World;
            }
            snapshot.Inventory = new[] { rocks[0].Item };
            yield return SceneManager.UnloadSceneAsync(scene);
            using (var stream = File.Create(Path.Combine(directory, "world.sav"))) WorldSaveCodec.Write(stream, snapshot);
            yield return Open();
            foreach (var expected in rocks)
            {
                var restored = discoveries.Finds.Single(f => f.Item.InstanceId == expected.Item.Id);
                var actual = restored.Capture();
                Assert.That(actual.ContentId, Is.EqualTo(expected.ContentId));
                Assert.That(actual.Position, Is.EqualTo(expected.Position));
                Assert.That(Quaternion.Angle(actual.Rotation, expected.Rotation), Is.LessThan(.01f));
                Assert.That(actual.PhysicsReleased, Is.True);
                Assert.That(actual.Item.Value, Is.EqualTo(expected.Item.Value));
                Assert.That(actual.Collected, Is.EqualTo(expected.Collected));
                Assert.That(restored.gameObject.activeSelf, Is.EqualTo(!expected.Collected));
                Assert.That(restored.GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(discoveries.Catalog.Resolve(expected.ContentId).GetComponent<MeshFilter>().sharedMesh));
            }
            Assert.That(player.Inventory.Items.Single().InstanceId, Is.EqualTo(rocks[0].Item.Id));
        }

        [UnityTest]
        public IEnumerator CheckpointWhileLiftingRestoresOneReleasedWorldFindWithoutInventoryDuplication()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            player.CloseMenu(); yield return null;
            var find = discoveries.Finds.First(f => f.Size == FindSize.Large);
            find.transform.position = terrain.transform.TransformPoint(new Vector3(12, terrain.Dimensions.y * terrain.CellSize + .7f, 12));
            find.GetComponent<FindPhysics>().Restore(false); Physics.SyncTransforms(); find.RefreshExposure();
            player.ViewCamera.transform.position = find.transform.position + new Vector3(0, 1, -1);
            player.ViewCamera.transform.LookAt(find.transform.position);
            Assert.That(player.TryGrabOrDrop(), Is.True);
            var snapshot = save.Capture(save.CompletedSequence + 1);
            var expected = snapshot.Finds.Single(f => f.Item.Id == find.Item.InstanceId);
            Assert.That(expected.PhysicsReleased, Is.True); Assert.That(expected.Collected, Is.False);
            Assert.That(snapshot.Inventory.Any(i => i.Id == expected.Item.Id), Is.False);
            yield return SceneManager.UnloadSceneAsync(scene);
            using (var stream = File.Create(Path.Combine(directory, "world.sav"))) WorldSaveCodec.Write(stream, snapshot);
            yield return Open();
            var restored = discoveries.Finds.Single(f => f.Item.InstanceId == expected.Item.Id);
            Assert.That(player.HeldFind, Is.Null); Assert.That(restored.GetComponent<FindPhysics>().Released, Is.True);
            Assert.That(restored.Capture().Position, Is.EqualTo(expected.Position));
            Assert.That(restored.SaveContentId, Is.EqualTo(expected.ContentId)); Assert.That(restored.Collected, Is.False);
            Assert.That(player.Inventory.Items.Any(i => i.InstanceId == expected.Item.Id), Is.False);
        }

        [UnityTest]
        public IEnumerator EveryDepthMineralCanBeUncoveredCollectedSoldAndCheckpointed()
        {
            yield return Until(() => save.CompletedSequence > 0 && save.State == WorldSaveState.Ready);
            player.CloseMenu();
            string[] names = { "Coal", "Copper", "Iron", "Silver", "Gold", "Emerald", "Ruby", "Diamond" };
            int[] prices = { 4, 5, 6, 9, 13, 20, 30, 45 };
            var collected = new System.Collections.Generic.List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                var entry = discoveries.Catalog.Entries.Single(e => e.Prefab.DisplayName == names[i]);
                // Exercise each progression band; additional lower-reservoir allocations
                // may appear first in the deterministic population order.
                var find = discoveries.Finds.First(f => f.Item.DisplayName == names[i]
                    && terrain.SurfaceHeight - f.transform.position.y >= entry.CoreMinDepth
                    && terrain.SurfaceHeight - f.transform.position.y <= entry.CoreMaxDepth);
                Vector3 target = find.transform.position;
                // Excavate a real corridor down to this mineral's generated depth.
                // The terrain filter is the existing aimed-find dig-through rule;
                // collection below still requires ordinary exposure and a clear view.
                for (int stroke = 0; stroke < 100; stroke++)
                {
                    find.RefreshExposure();
                    if (find.Collectible) break;
                    bool cut = false;
                    // Change aim around a scoop edge when its interpolated surface
                    // cannot remove more soil. A real player can make the same move.
                    foreach (var offset in new[] { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                    {
                        var origin = new Vector3(target.x, 2, target.z) + offset * .3f;
                        var hits = Physics.RaycastAll(origin, Vector3.down, 38)
                            .Where(h => h.collider.GetComponentInParent<TerrainVolume>() == terrain).OrderBy(h => h.distance).ToArray();
                        if (hits.Length == 0 || hits[0].point.y < target.y - .6f) continue;
                        if (terrain.TryDig(hits[0], 1)) { cut = true; break; }
                    }
                    if (!cut) break;
                }
                find.RefreshExposure();
                Assert.That(find.Collectible, Is.True, names[i]);
                bool pickedUp = false;
                // Dense neighbours can cover the straight-down view. Approach the exposed
                // object from a clear angle, retaining the normal occlusion/pickup contract.
                foreach (float distance in new[] { 1.5f, 1f, .65f })
                {
                    for (int angle = 0; angle < 8 && !pickedUp; angle++)
                    {
                        var side = Quaternion.Euler(0, angle * 45, 0) * Vector3.forward;
                        player.ViewCamera.transform.position = find.transform.position + (Vector3.up + side * .65f).normalized * distance;
                        player.ViewCamera.transform.LookAt(find.transform.position); Physics.SyncTransforms();
                        pickedUp = find.TryCollect(player);
                    }
                    if (pickedUp) break;
                }
                Assert.That(pickedUp, Is.True, names[i]);
                Assert.That(find.TryCollect(player), Is.False, "Never duplicate a mineral.");
                Assert.That(player.Inventory.Items.Last().SaleValue, Is.EqualTo(prices[i]));
                collected.Add(find.Item.InstanceId);
                if (i == 0) Assert.That(player.Trade.TrySell(player.Trade.OfferSale(find.Item.InstanceId)), Is.True);
            }
            Assert.That(player.Inventory.Count, Is.EqualTo(7));
            Assert.That(player.Wallet.Balance, Is.EqualTo(prices[0]));
            // Return the review camera without changing the saved player's safe surface pose.
            player.ViewCamera.transform.localPosition = Vector3.up * 1.6f;
            var sale = player.Trade.OfferSale(); Assert.That(player.Trade.TrySell(sale), Is.True);
            Assert.That(player.Trade.TrySell(sale), Is.False);
            Assert.That(player.Wallet.Balance, Is.EqualTo(prices.Sum()));
            long sequence = save.CompletedSequence; save.RequestCheckpoint();
            yield return Until(() => save.CompletedSequence > sequence && save.State == WorldSaveState.Ready);
            yield return SceneManager.UnloadSceneAsync(scene); yield return Open();
            Assert.That(player.Wallet.Balance, Is.EqualTo(prices.Sum()));
            Assert.That(player.Inventory.Count, Is.Zero);
            foreach (string id in collected) Assert.That(discoveries.Finds.Single(f => f.Item.InstanceId == id).Collected, Is.True);
            Assert.That(discoveries.Finds.Count, Is.EqualTo(discoveries.Catalog.TotalCount));
        }

        private void Expose(BuriedFind find)
        {
            float ring = Mathf.Max(find.WorldBounds.extents.x, find.WorldBounds.extents.z) + 0.12f;
            for (int pass = 0; pass < 12 && !find.Collectible; pass++)
                foreach (var offset in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    if (find.Collectible) break;
                    Vector3 origin = find.transform.position + offset * ring;
                    origin.y = terrain.SurfaceHeight + 2;
                    // A neighbouring mineral may now be the first collider. Use the
                    // same terrain selection as aimed-find digging, then verify the
                    // target's real exposure and collection visibility below.
                    var hits = Physics.RaycastAll(origin, Vector3.down, 38)
                        .Where(h => h.collider.GetComponentInParent<TerrainVolume>() == terrain).OrderBy(h => h.distance).ToArray();
                    Assert.That(hits, Is.Not.Empty);
                    // Some perimeter rays land on an already-cleared scoop edge.
                    // Keep the bounded search; final exposure is the requirement.
                    terrain.TryDig(hits[0], 0.65f);
                }
            Assert.That(find.Collectible, Is.True, $"Expose {find.DisplayName} at {find.transform.position}, exposure {find.Exposure}.");
            // Exposure can be on one side while soil still covers the vertical camera ray.
            // The save/transaction fixture needs a real clear view, in addition to eligibility.
            for (int i = 0; i < 12; i++)
            {
                Assert.That(Physics.Raycast(find.transform.position + Vector3.up * 3, Vector3.down, out var hit, 6), Is.True);
                if (hit.collider == find.GetComponent<MeshCollider>()) return;
                Assert.That(terrain.TryDig(hit, .41f), Is.True);
            }
            Assert.Fail("Could not open an actual overhead view of the eligible bottle.");
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Save operation timed out.");
                yield return null;
            }
        }
    }
}
#endif
