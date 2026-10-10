#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SomethingDownThere.Tests
{
    public sealed class StartupMenuTests
    {
        private Scene scene;
        private FpsPlayer player;
        private WorldSaveController save;
        private string directory;
        private InputTestFixture devices;
        private PreferencesStore preferences;
        private TestInputPreferences inputPreferences;

        private sealed class PreferencesStore : ICameraPreferencesStore
        {
            public string Contents;
            public string Read() => Contents;
            public void Write(string contents) => Contents = contents;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "SDT-startup-tests", Guid.NewGuid().ToString("N"));
            preferences = new PreferencesStore();
            inputPreferences = new TestInputPreferences();
            devices = new InputTestFixture(); devices.Setup();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            Time.timeScale = 1;
            yield return Open();
        }

        private IEnumerator Open()
        {
            // sceneLoaded runs before Start, keeping HUD controls and the player
            // on the same isolated preferences store and deferring find generation.
            SceneManager.sceneLoaded += Loaded;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= Loaded;
            yield return null; yield return null;
            player.SetApplicationFocus(true);
        }

        private void Loaded(Scene loaded, LoadSceneMode mode)
        {
            if (loaded.path != "Assets/Scenes/MainGame.unity") return;
            scene = loaded;
            player = scene.GetRootGameObjects()[0].GetComponentInChildren<FpsPlayer>();
            player.enabled = false;
            player.ConfigureCameraPreferences(preferences);
            player.ConfigureInputPreferences(inputPreferences);
            player.ConfigureGamePreferences(new TestInputPreferences());
            save = player.GetComponent<WorldSaveController>();
            save.PresentStartup(directory);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= Loaded;
            if (save != null && (save.State == WorldSaveState.Creating || save.State == WorldSaveState.Saving))
                yield return Until(() => save.State != WorldSaveState.Creating && save.State != WorldSaveState.Saving);
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices.TearDown();
            Time.timeScale = 1;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            else if (File.Exists(directory)) File.Delete(directory);
        }

        [UnityTest]
        public IEnumerator StartupRestoresKeyboardRouteWithoutAPointerClick()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            events.SetSelectedGameObject(null);
            yield return null; yield return null;
            var keyboard = Keyboard.current;
            devices.Press(keyboard.downArrowKey, queueEventOnly: true);
            yield return null; yield return null;
            devices.Release(keyboard.downArrowKey, queueEventOnly: true);
            yield return null;
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Settings"));
            Assert.That(MenuTestUI.View(player).Root.ClassListContains("keyboard-navigation"), Is.True);
            devices.Press(keyboard.enterKey, queueEventOnly: true);
            yield return null; yield return null;
            devices.Release(keyboard.enterKey, queueEventOnly: true);
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeviceSettings));
            Assert.That(save.State, Is.EqualTo(WorldSaveState.Startup));
        }

        [UnityTest]
        public IEnumerator EmptyStartupAndSettingsBlockGameplayWithoutCreatingASave()
        {
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.MainMenu));
            Assert.That(save.HasSavedGame, Is.False);
            Assert.That(MenuTestUI.Button(player, "Continue").enabledSelf, Is.False);
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("New Game"));
            // Development builds (and the Editor) add the Ground Lab after Settings.
            var expected = FpsPlayer.AdminBuild ? new[] { "Continue", "New Game", "Settings", "Ground Lab", "Quit" }
                : new[] { "Continue", "New Game", "Settings", "Quit" };
            Assert.That(MenuTestUI.View(player).CurrentScreen.Query<Button>().ToList().Select(b => b.name), Is.EqualTo(expected));
            Assert.That(player.GetComponent<FpsHud>().View.Root.ClassListContains("hidden"), Is.True);
            Assert.That(UnityEngine.Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            var position = player.transform.position;
            var rotation = player.ViewCamera.transform.rotation;
            player.Tick(new FpsInputFrame { Move = Vector2.up, SprintHeld = true, Look = Vector2.one * 200, DigHeld = true, JetpackHeld = true, JumpPressed = true, BackPressed = true, AdminMenuPressed = true }, 1);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.MainMenu));
            Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(player.ViewCamera.transform.rotation, Is.EqualTo(rotation));
            Assert.That(player.ExcavationTerrain.Revision, Is.Zero);
            Assert.That(player.Discoveries.Initialized, Is.False);
            MenuTestUI.Click(MenuTestUI.Button(player, "Settings"));
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeviceSettings));
            MenuTestUI.Click(MenuTestUI.Button(player, "settingsControls"));
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.InputSettings));
            Assert.That(MenuTestUI.View(player).CurrentScreen.Query<Button>().ToList().Count(b => b.name.StartsWith("bind") && b.name != "bindingCancel" && b.name != "bindingReplace"), Is.EqualTo(InputPreferences.BindingCount));
            MenuTestUI.View(player).Root.Q<SettingSelector>("digMode").value = 1;
            player.InputSettings.Bind(PlayerBinding.Dig, "<Mouse>/rightButton", true);
            MenuTestUI.Click(MenuTestUI.Button(player, "settingsBack"));
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.MainMenu));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Settings"));
            MenuTestUI.Click(MenuTestUI.Button(player, "Settings"));
            yield return null; yield return null;
            MenuTestUI.Click(MenuTestUI.Button(player, "settingsControls"));
            yield return null; yield return null;
            MenuTestUI.Click(MenuTestUI.Button(player, "settingsAccessibility"));
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.CameraComfort));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("fovSlider"));
            Assert.That(new InputPreferences(inputPreferences).ToggleDig, Is.True);
            MenuTestUI.View(player).Root.Q<SliderInt>("fovSlider").value = 81;
            MenuTestUI.View(player).Root.Q<SettingSelector>("steadyCrosshair").value = 0;
            yield return null;
            player.Tick(new FpsInputFrame { BackPressed = true }, 0);
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.MainMenu));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Settings"));
            Assert.That(new CameraPreferences(preferences).VerticalFov, Is.EqualTo(81));
            Assert.That(new CameraPreferences(preferences).SteadyCrosshair, Is.False);
            Assert.That(Directory.Exists(directory), Is.False, "Browsing startup/settings must not touch the world profile.");
        }

        [UnityTest]
        public IEnumerator FreshGameStartsOnceAndExistingGameCanCancelThenLoadExactProgress()
        {
            yield return CreateProgress();
            var expected = WorldSaveStore.Read(Path.Combine(directory, "world.sav"));
            var bytes = File.ReadAllBytes(Path.Combine(directory, "world.sav"));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return Open();
            Assert.That(save.HasSavedGame, Is.True);
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Continue"));
            MenuTestUI.Click(MenuTestUI.Button(player, "New Game"));
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.ConfirmNewGame));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Cancel"));
            player.Tick(new FpsInputFrame { BackPressed = true }, 0);
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.MainMenu));
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "world.sav")), Is.EqualTo(bytes));
            MenuTestUI.Click(MenuTestUI.Button(player, "Continue"));
            yield return Until(() => save.State == WorldSaveState.Ready);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            Assert.That(player.Wallet.Balance, Is.EqualTo(expected.Credits));
            Assert.That(player.Shovel.Level, Is.EqualTo(expected.ShovelLevel));
            SaveIntegrationTests.AssertSameDensity(player.ExcavationTerrain.Capture().Density, expected.Terrain.Density);
            Assert.That(player.Discoveries.Capture().Select(f => f.Item.Id), Is.EqualTo(expected.Finds.Select(f => f.Item.Id)));
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "world.sav")), Is.EqualTo(bytes), "Loading alone is read-only.");
        }

        [UnityTest]
        public IEnumerator PauseNewGameAsksBeforeReplacingAndCancelReturnsToPause()
        {
            yield return CreateProgress();
            player.OpenMenu(PlayerMenu.Pause);
            yield return null; yield return null;
            MenuTestUI.Click(MenuTestUI.Button(player, "New Game"));
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.ConfirmNewGame));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Cancel"));
            MenuTestUI.Click(MenuTestUI.Button(player, "Cancel"));
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
            Assert.That(save.State, Is.EqualTo(WorldSaveState.Ready).Or.EqualTo(WorldSaveState.Saving), "The running game is untouched.");
        }

        [UnityTest]
        public IEnumerator ConfirmedNewGameResetsWorldProgressAndKeepsSettingsAndArchive()
        {
            yield return CreateProgress();
            player.CameraSettings.SetVerticalFov(83);
            player.CameraSettings.Flush();
            player.InputSettings.SetToggleDig(true);
            player.InputSettings.Bind(PlayerBinding.Dig, "<Mouse>/rightButton", true);
            player.InputSettings.Flush();
            var bytes = File.ReadAllBytes(Path.Combine(directory, "world.sav"));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return Open();
            save.RequestNewGame();
            yield return null; yield return null;
            var menu = MenuTestUI.View(player);
            Assert.That(menu.Root.ClassListContains("dialog-menu"), Is.True);
            Assert.That(menu.Root.Q<Label>("menuSubtitle").text, Is.Empty);
            Assert.That(menu.CurrentScreen.Query<Button>().ToList().Count(b => b.ClassListContains("primary")), Is.EqualTo(1));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Cancel"));
            MenuTestUI.Click(MenuTestUI.Button(player, "Start New Game"));
            save.ConfirmNewGame(); // A duplicate submission cannot start another writer.
            yield return Until(() => save.State == WorldSaveState.Ready);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.Battery.Charge, Is.EqualTo(player.Battery.Capacity));
            Assert.That(player.ExcavationTerrain.Revision, Is.Zero);
            Assert.That(player.ExcavationTerrain.RemovedVolume, Is.Zero);
            Assert.That(player.CameraSettings.VerticalFov, Is.EqualTo(83));
            Assert.That(player.InputSettings.ToggleDig, Is.True);
            Assert.That(player.InputSettings.Path(PlayerBinding.Dig), Is.EqualTo("<Mouse>/rightButton"));
            Assert.That(player.Discoveries.Finds.Count, Is.EqualTo(player.Discoveries.Catalog.TotalCount));
            Assert.That(player.Discoveries.Finds.Count(f => f.SaveContentId.StartsWith("mineral_")),
                Is.EqualTo(player.Discoveries.Catalog.Entries.Where(e => e.ItemId.StartsWith("mineral_")).Sum(e => e.Count)));
            string archive = Directory.GetFiles(Path.Combine(directory, "PreviousGames"), "world.sav", SearchOption.AllDirectories).Single();
            Assert.That(File.ReadAllBytes(archive), Is.EqualTo(bytes));
            Assert.That(WorldSaveStore.Read(Path.Combine(directory, "world.previous.sav")).Credits, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CorruptSaveStaysBlockedAndCannotSilentlyGenerateANewWorld()
        {
            Directory.CreateDirectory(directory);
            byte[] bad = { 1, 2, 3 };
            File.WriteAllBytes(Path.Combine(directory, "world.sav"), bad);
            save.RefreshStartup();
            LogAssert.Expect(LogType.Warning, new Regex("World save:"));
            save.LoadGame();
            yield return Until(() => save.State == WorldSaveState.LoadFailed);
            Assert.That(save.BlocksPlay, Is.True);
            Assert.That(player.Discoveries.Initialized, Is.False);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Persistence));
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "world.sav")), Is.EqualTo(bad));
        }

        [UnityTest]
        public IEnumerator FailedNewGameRemainsAtErrorUntilRetryCanCreateItsCheckpoint()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(directory));
            File.WriteAllText(directory, "A file blocks this test's profile directory.");
            LogAssert.Expect(LogType.Warning, new Regex("New game:"));
            save.RequestNewGame();
            yield return Until(() => save.State == WorldSaveState.NewGameFailed);
            Assert.That(save.BlocksPlay, Is.True);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Persistence));
            Assert.That(File.Exists(directory), Is.True);
            Assert.That(save.ProfileInUse, Is.False, "Storage failures are not duplicate game launches.");
            Assert.That(MenuTestUI.Text(player, "Body"), Does.Not.Contain(directory));
            File.Delete(directory);
            save.Retry();
            yield return Until(() => save.State == WorldSaveState.Ready);
            Assert.That(WorldSaveStore.Read(Path.Combine(directory, "world.sav")).Credits, Is.Zero);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
        }

        [UnityTest]
        public IEnumerator LockedNewGameExplainsConflictAndRetriesAfterOwnerCloses()
        {
            using var owner = new WorldSaveStore(directory);
            owner.Load();
            LogAssert.Expect(LogType.Warning, new Regex("New game:"));
            save.RequestNewGame();
            yield return Until(() => save.State == WorldSaveState.NewGameFailed);
            yield return null;
            AssertProfileInUse();
            Assert.That(WorldSaveStore.HasCheckpoint(directory), Is.False);
            MenuTestUI.Click(MenuTestUI.Button(player, "Back to menu"));
            yield return null;
            Assert.That(save.State, Is.EqualTo(WorldSaveState.Startup));
            Assert.That(save.ProfileInUse, Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("New game:"));
            save.RequestNewGame();
            yield return Until(() => save.State == WorldSaveState.NewGameFailed);
            owner.Dispose();
            yield return null; // Let the retained UI replace the busy page before submitting.
            MenuTestUI.Click(MenuTestUI.Button(player, "Retry"));
            yield return Until(() => save.State == WorldSaveState.Ready);
            Assert.That(save.ProfileInUse, Is.False);
            Assert.That(WorldSaveStore.Read(Path.Combine(directory, "world.sav")).Credits, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LockedLoadKeepsProgressAndCanReturnToMenuThenRetry()
        {
            yield return CreateProgress();
            byte[] original = File.ReadAllBytes(Path.Combine(directory, "world.sav"));
            yield return SceneManager.UnloadSceneAsync(scene);
            using var owner = new WorldSaveStore(directory);
            var expected = owner.Load().Snapshot;
            yield return Open();
            LogAssert.Expect(LogType.Warning, new Regex("World save:"));
            save.LoadGame();
            yield return Until(() => save.State == WorldSaveState.LoadFailed);
            yield return null;
            AssertProfileInUse();
            Assert.That(player.Discoveries.Initialized, Is.False);
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "world.sav")), Is.EqualTo(original));
            MenuTestUI.Click(MenuTestUI.Button(player, "Back to menu"));
            yield return null;
            Assert.That(save.State, Is.EqualTo(WorldSaveState.Startup));
            LogAssert.Expect(LogType.Warning, new Regex("World save:"));
            save.LoadGame();
            yield return Until(() => save.State == WorldSaveState.LoadFailed);
            owner.Dispose();
            yield return null;
            MenuTestUI.Click(MenuTestUI.Button(player, "Retry"));
            yield return Until(() => save.State == WorldSaveState.Ready);
            Assert.That(save.ProfileInUse, Is.False);
            Assert.That(player.Wallet.Balance, Is.EqualTo(expected.Credits));
            SaveIntegrationTests.AssertSameDensity(player.ExcavationTerrain.Capture().Density, expected.Terrain.Density);
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "world.sav")), Is.EqualTo(original));
        }

        private void AssertProfileInUse()
        {
            Assert.That(save.ProfileInUse, Is.True);
            Assert.That(save.BlocksPlay, Is.True);
            Assert.That(MenuTestUI.Text(player, "menuTitle"), Is.EqualTo("Saved game already open"));
            string body = MenuTestUI.Text(player, "Body");
            Assert.That(body, Does.Contain("another game window"));
            Assert.That(body, Does.Not.Contain(directory).And.Not.Contain("session.lock").And.Not.Contain("Sharing violation").And.Not.Contain("disk space"));
        }

        private IEnumerator CreateProgress()
        {
            save.RequestNewGame();
            save.RequestNewGame();
            yield return Until(() => save.State == WorldSaveState.Ready);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            player.SetApplicationFocus(true);
            Assert.That(Physics.Raycast(new Vector3(0, 2, 0), Vector3.down, out var hit, 4), Is.True);
            Assert.That(player.ExcavationTerrain.TryDig(hit, 0.8f), Is.True);
            Assert.That(player.Wallet.TryCredit(37), Is.True);
            Assert.That(player.Trade.TryUpgrade(player.Trade.OfferUpgrade()), Is.True);
            long sequence = save.CompletedSequence;
            save.RequestCheckpoint();
            yield return Until(() => save.State == WorldSaveState.Ready && save.CompletedSequence > sequence);
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail("Startup operation timed out.");
                yield return null;
            }
        }
    }
}
#endif
