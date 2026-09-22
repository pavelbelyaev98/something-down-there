#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class SurfaceRechargeTests
    {
        private Scene scene;
        private FpsPlayer player;
        private SurfaceRecharge recharge;
        private InputTestFixture devices;
        private float previousTimeScale;
        private CursorLockMode previousCursor;
        private bool previousCursorVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousCursor = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            devices = new InputTestFixture();
            devices.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            Time.timeScale = 1;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            var root = scene.GetRootGameObjects()[0];
            player = root.GetComponentInChildren<FpsPlayer>();
            recharge = root.GetComponentInChildren<SurfaceRecharge>();
            root.GetComponentInChildren<DiscoveryField>().gameObject.SetActive(false);
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices.TearDown();
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursor;
            Cursor.visible = previousCursorVisible;
        }

        [UnityTest]
        public IEnumerator SurfaceVisitPauseAndReentryNeverRefillOrBill()
        {
            player.Battery.TrySpend(99);
            player.Wallet.TryCredit(7);
            Place(recharge.transform.position + Vector3.up * .1f);
            yield return null; yield return null;
            Assert.That(recharge.IsPlayerInZone, Is.True);
            Assert.That(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(player.GetComponent<FpsHud>().View.Root, "Return warning").text,
                Is.EqualTo("FUEL AT COMPUTER"));
            player.OpenMenu(PlayerMenu.Inventory);
            yield return null;
            player.CloseMenu();
            player.SetApplicationFocus(false);
            yield return null;
            player.SetApplicationFocus(true);
            player.CloseMenu();
            Place(new Vector3(0, .1f, -12.5f));
            yield return null;
            Place(recharge.transform.position + Vector3.up * .1f);
            yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(1));
            Assert.That(player.Wallet.Balance, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator RealDigAndFlightShareFuelAndPaidServicePreservesTheTrip()
        {
            Place(new Vector3(0, .1f, -11.5f));
            player.ViewCamera.transform.localRotation = Quaternion.Euler(85, 0, 0);
            Physics.SyncTransforms();
            Assert.That(player.TryDig(), Is.True);
            float afterDig = player.Battery.Capacity - player.EffectiveDigEnergy;
            Assert.That(player.Battery.Charge, Is.EqualTo(afterDig).Within(.001f));
            float removed = recharge.Terrain.RemovedVolume;
            player.Inventory.TryAdd(new InventoryItem("kept-find", "Rock", 2));
            player.Shovel.TryUpgradeTo(2);
            player.Tick(new FpsInputFrame { JetpackHeld = true }, .3f);
            Assert.That(player.IsJetpackActive, Is.True);
            Assert.That(player.Battery.Charge, Is.LessThan(afterDig));
            float charge = player.Battery.Charge;
            player.Tick(new FpsInputFrame { Move = Vector2.right }, .02f);
            player.Tick(default, .02f);
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            player.Wallet.TryCredit(2);
            var station = scene.GetRootGameObjects()[0].GetComponentInChildren<ComputerStation>();
            Place(station.transform.position + new Vector3(0, .1f, 2.4f));
            player.transform.rotation = Quaternion.Euler(0, 180, 0);
            player.Tick(new FpsInputFrame { Look = new Vector2(0, (player.Pitch - 12) / player.Tuning.LookSensitivity) }, .016f);
            Physics.SyncTransforms();
            Assert.That(player.TryInteract(), Is.True);
            yield return null;
            Assert.That(station.Selling, Is.True);
            Assert.That(player.ExecuteStationCommand(ComputerStation.RefillCommand), Is.False, "Sell the haul before refilling.");
            Assert.That(player.ExecuteStationCommand(ComputerStation.SellAllCommand), Is.True);
            Assert.That(station.Selling, Is.False);
            decimal cost = station.Refill.Cost;
            Assert.That(player.ExecuteStationCommand(ComputerStation.RefillCommand), Is.True);
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            Assert.That(player.Wallet.Balance, Is.EqualTo(4 - cost));
            Assert.That(recharge.Terrain.RemovedVolume, Is.EqualTo(removed));
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.Shovel.Level, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator EmptyWalletBagAndFuelStillUseExistingEmergencyRescue()
        {
            player.Battery.TrySpend(100);
            yield return null; yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(player.Inventory.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BottomCenterFuelWarningSurvivesComputerProximityAndUsesOwnedCapacity()
        {
            Place(recharge.transform.position + Vector3.up * .1f);
            var hud = player.GetComponent<FpsHud>().View.Root;
            var warning = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(hud, "Fuel warning");
            player.Battery.RestoreCharge(35);
            yield return null; yield return null;
            Assert.That(warning.text, Is.EqualTo("LOW FUEL"));
            Assert.That(ColorUtility.ToHtmlStringRGB(warning.resolvedStyle.color), Is.EqualTo("FFD45C"));
            Assert.That(warning.worldBound.center.x, Is.EqualTo(hud.worldBound.center.x).Within(1));
            Assert.That(warning.worldBound.yMin, Is.GreaterThan(hud.worldBound.yMax * .8f));
            Assert.That(warning.worldBound.yMax, Is.LessThan(hud.worldBound.yMax));
            var feedback = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(hud, "Feedback");
            player.ShowFeedback("Rock collected");
            yield return null;
            Assert.That(feedback.worldBound.yMax, Is.LessThan(warning.worldBound.yMin));
            player.Wallet.TryCredit(EquipmentProgression.Price(player.Battery.Level));
            Assert.That(player.Trade.TryUpgrade(player.Trade.OfferUpgrade(EquipmentKind.Fuel)), Is.True);
            player.Battery.RestoreCharge(22.5f); // 15% of the upgraded tank.
            yield return null; yield return null;
            Assert.That(warning.text, Is.EqualTo("FUEL CRITICAL"));
            Assert.That(ColorUtility.ToHtmlStringRGB(warning.resolvedStyle.color), Is.EqualTo("FF625C"));
            player.Wallet.TryCredit(1);
            Assert.That(player.Trade.TryRefill(player.Trade.OfferRefill()), Is.True);
            yield return null; yield return null;
            Assert.That(warning.text, Is.Empty);
            Assert.That(warning.resolvedStyle.display, Is.EqualTo(UnityEngine.UIElements.DisplayStyle.None));
            player.Battery.RestoreCharge(15);
            player.ToggleAdminUnlimitedBattery();
            yield return null; yield return null;
            Assert.That(warning.resolvedStyle.display, Is.EqualTo(UnityEngine.UIElements.DisplayStyle.None));
        }

        [TestCase(1, 80)] [TestCase(2, 130)]
        public void MainGameDigBudgetLeavesTheSameFlightReserveAtStarterAndPaidCapacity(int level, int digEnergyBudget)
        {
            player.enabled = false;
            if (level == 2)
            {
                player.Wallet.TryCredit(EquipmentProgression.Price(player.Battery.Level));
                Assert.That(player.Trade.TryUpgrade(player.Trade.OfferUpgrade(EquipmentKind.Fuel)), Is.True);
            }
            player.Battery.Recharge();
            Assert.That(player.Tuning.DigEnergy, Is.EqualTo(1));
            int expectedStrokes = Mathf.RoundToInt(digEnergyBudget / player.EffectiveDigEnergy);
            int accepted = 0;
            for (int i = 0; i < expectedStrokes * 3 && accepted < expectedStrokes; i++)
            {
                int patch = i % 50;
                player.ViewCamera.transform.position = new Vector3(-8 + patch % 10 * 1.5f,
                    recharge.Terrain.SurfaceHeight + 1.6f, -8 + patch / 10 * 1.5f);
                player.ViewCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
                Physics.SyncTransforms();
                if (player.TryDig()) accepted++;
            }
            Assert.That(accepted, Is.EqualTo(expectedStrokes));
            Assert.That(player.Battery.Charge, Is.EqualTo(20).Within(.01f));
            Assert.That(player.Battery.Charge / player.Tuning.JetpackEnergyPerSecond, Is.EqualTo(2.5f).Within(.002f));
            Assert.That(recharge.Terrain.RemovedVolume, Is.GreaterThan(0));
        }

        private void Place(Vector3 position)
        {
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.position = position;
            motor.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
#endif
