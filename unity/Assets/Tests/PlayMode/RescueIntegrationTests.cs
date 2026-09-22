#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class RescueIntegrationTests
    {
        private Scene scene;
        private FpsPlayer player;
        private TerrainVolume terrain;
        private Transform landing;
        private InputTestFixture devices;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldTimeScale;
        private CursorLockMode oldCursor;
        private bool oldCursorVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldTimeScale = Time.timeScale;
            oldCursor = Cursor.lockState;
            oldCursorVisible = Cursor.visible;
            devices = new InputTestFixture();
            devices.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            Time.timeScale = 1;
            SceneManager.sceneLoaded += TestInputPreferences.Configure;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            var root = scene.GetRootGameObjects()[0];
            player = root.GetComponentInChildren<FpsPlayer>();
            terrain = root.GetComponentInChildren<TerrainVolume>();
            landing = root.transform.Find("Surface/ReturnAnchor");
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            player.Tuning.Gravity = 0;
            Place(new Vector3(-8, 3, 0));
            yield return null;
            yield return null;
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null;
            player.SetApplicationFocus(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices.TearDown();
            Time.timeScale = oldTimeScale;
            Cursor.lockState = oldCursor;
            Cursor.visible = oldCursorVisible;
        }

        [UnityTest]
        public IEnumerator DepletionPreservesTerrainUpgradesAndCollectedIdentitiesAndChargesOnce()
        {
            var find = CollectFirstFind();
            Place(new Vector3(-8, 3, 0));
            player.Wallet.TryCredit(25);
            player.Shovel.TryUpgradeTo(2);
            int revision = terrain.Revision;
            float volume = terrain.RemovedVolume;
            player.Battery.TrySpend(player.Battery.Charge);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            Assert.That(Vector3.Distance(player.transform.position, landing.position), Is.LessThan(0.01f));
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.Wallet.Balance, Is.EqualTo(15));
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            Assert.That(player.Shovel.Level, Is.EqualTo(2));
            Assert.That(terrain.Revision, Is.EqualTo(revision));
            Assert.That(terrain.RemovedVolume, Is.EqualTo(volume));
            StringAssert.Contains("Fuel empty", player.Feedback);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(player.Wallet.Balance, Is.EqualTo(15));
            Assert.That(find.Collected && !find.gameObject.activeSelf, Is.True);
            terrain.ResetExcavation();
            Assert.That(find.Collected && !find.gameObject.activeSelf, Is.True, "Lost loot never respawns.");
        }

        [UnityTest]
        public IEnumerator FinalJetpackFuelRescuesWithoutLeakingHeldDigOrThrust()
        {
            yield return ExerciseFinalFuel(false);
        }

        [UnityTest]
        public IEnumerator FinalFuelClearsRemappedToggleIntentAfterAutomaticRefill()
        {
            yield return ExerciseFinalFuel(true);
        }

        private IEnumerator ExerciseFinalFuel(bool toggle)
        {
            player.InputSettings.SetToggleDig(toggle);
            if (toggle) player.InputSettings.Bind(PlayerBinding.Dig, "<Mouse>/rightButton", true);
            yield return null; yield return null;
            player.Battery.RestoreCharge(0.08f);
            devices.Press(toggle ? mouse.rightButton : mouse.leftButton, queueEventOnly: true);
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(Vector3.Distance(player.transform.position, landing.position), Is.LessThan(0.01f));
            Assert.That(player.GameplayActive, Is.True);
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(player.SuccessfulStrokes, Is.Zero);
            Assert.That(player.IsJetpackActive, Is.False);
            Assert.That(player.VerticalSpeed, Is.Zero);
            devices.Release(toggle ? mouse.rightButton : mouse.leftButton, queueEventOnly: true);
            devices.Release(keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            devices.Press(keyboard.wKey, queueEventOnly: true);
            var start = player.transform.position;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Vector3.Distance(player.transform.position, start), Is.GreaterThan(0.1f));
        }

        [UnityTest]
        public IEnumerator FinalDigCompletesBeforeRescueAndHeldMouseCannotDigAtLanding()
        {
            player.ViewCamera.transform.position = new Vector3(0, 1.5f, 0);
            player.ViewCamera.transform.rotation = Quaternion.LookRotation(Vector3.down);
            player.Battery.RestoreCharge(player.EffectiveDigEnergy);
            int revision = terrain.Revision;
            Assert.That(player.TryDig(), Is.True);
            Assert.That(terrain.Revision, Is.GreaterThan(revision));
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(1));
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            Assert.That(Vector3.Distance(player.transform.position, landing.position), Is.LessThan(0.01f));
            yield return null;
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EmptyRestoredBatteryWaitsForFocusAndPauseResumeAndPauseHasNoRescue()
        {
            var snapshot = new WorldSnapshot();
            player.Capture(snapshot);
            snapshot.BatteryCharge = 0;
            player.OpenMenu(PlayerMenu.Pause);
            player.Restore(snapshot);
            yield return null;
            yield return null;
            Assert.That(MenuTestUI.View(player).CurrentScreen.Q<Button>("Call rescue..."), Is.Null);
            Assert.That(MenuTestUI.Button(player, "Settings"), Is.Not.Null);
            Assert.That(player.Battery.Charge, Is.Zero);
            player.SetApplicationFocus(false);
            yield return null;
            Assert.That(player.Battery.Charge, Is.Zero);
            player.SetApplicationFocus(true);
            player.CloseMenu();
            yield return null;
            yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            Assert.That(Vector3.Distance(player.transform.position, landing.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator BlockedLandingPreservesItemsAndCreditsThenRetriesAutomatically()
        {
            player.Inventory.TryAdd(new InventoryItem("review-a", "Marble", 5));
            player.Wallet.TryCredit(25);
            var original = landing.position;
            landing.position = new Vector3(0, -1, 0);
            player.Battery.TrySpend(player.Battery.Charge);
            yield return null;
            yield return null;
            StringAssert.Contains("clear landing area", player.Feedback);
            Assert.That(player.Inventory.Count, Is.EqualTo(1));
            Assert.That(player.Wallet.Balance, Is.EqualTo(25));
            Assert.That(player.Battery.Charge, Is.Zero);
            landing.position = original;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.Wallet.Balance, Is.EqualTo(15));
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator UnlimitedBatteryDoesNotRescueUntilNormalRulesResume()
        {
            player.ToggleAdminUnlimitedBattery();
            player.Battery.TrySpend(player.Battery.Charge);
            var before = player.transform.position;
            yield return null;
            yield return null;
            Assert.That(player.transform.position, Is.EqualTo(before));
            player.RestoreAdminOverrides();
            yield return null;
            yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
        }

        private void Place(Vector3 position)
        {
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.position = position;
            motor.enabled = true;
            Physics.SyncTransforms();
        }

        private BuriedFind CollectFirstFind()
        {
            var find = player.Discoveries.Finds[0];
            float ring = Mathf.Max(find.WorldBounds.extents.x, find.WorldBounds.extents.z) + 0.12f;
            for (int pass = 0; pass < 12 && !find.Collectible; pass++)
                foreach (var offset in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    if (find.Collectible) break;
                    Vector3 origin = find.transform.position + offset * ring;
                    origin.y = 2;
                    // Dense ground puts other finds in the ray, and repeated strokes
                    // leave ledges whose hit point is already void: dig the first soil
                    // hit that still removes ground.
                    var ground = Physics.RaycastAll(origin, Vector3.down, 30, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        .Where(h => h.collider.GetComponentInParent<TerrainVolume>() == terrain).OrderBy(h => h.distance).ToArray();
                    Assert.That(ground.Length, Is.GreaterThan(0), "The stroke must start on soil.");
                    foreach (var hit in ground) if (terrain.TryDig(hit, 0.65f)) break;
                }
            player.ViewCamera.transform.position = find.transform.position + Vector3.up * 1.5f;
            player.ViewCamera.transform.LookAt(find.transform.position);
            Physics.SyncTransforms();
            Assert.That(find.TryCollect(player), Is.True,
                $"Collection setup: menu={player.Menu}, active={player.GameplayActive}, exposure={find.Exposure}, required={find.RequiredExposure}, timeScale={Time.timeScale}");
            player.ViewCamera.transform.localPosition = Vector3.up * 1.6f;
            player.ViewCamera.transform.localRotation = Quaternion.Euler(player.Pitch, 0, 0);
            return find;
        }
    }
}
#endif
