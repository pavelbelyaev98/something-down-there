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
    public sealed class StationIntegrationTests
    {
        private Scene scene;
        private FpsPlayer player;
        private ComputerStation computer;
        private InputTestFixture devices;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldTimeScale;
        private CursorLockMode oldCursor;
        private bool oldCursorVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldTimeScale = Time.timeScale; oldCursor = Cursor.lockState; oldCursorVisible = Cursor.visible;
            devices = new InputTestFixture(); devices.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            Time.timeScale = 1;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            var root = scene.GetRootGameObjects().Single();
            player = root.GetComponentInChildren<FpsPlayer>();
            computer = root.GetComponentInChildren<ComputerStation>();
            player.Tuning.Gravity = 0;
            yield return null;
            yield return null;
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices.TearDown();
            Time.timeScale = oldTimeScale; Cursor.lockState = oldCursor; Cursor.visible = oldCursorVisible;
        }

        [UnityTest]
        public IEnumerator RealStationInputSellsSelectedIdentitiesAndRejectsOldButtons()
        {
            var first = new InventoryItem("first", "Coin", 5);
            var second = new InventoryItem("second", "Coin", 17);
            player.Inventory.TryAdd(first); player.Inventory.TryAdd(second);
            player.OpenMenu(PlayerMenu.Inventory);
            Assert.That(player.ExecuteStationCommand(ComputerStation.SellAllCommand), Is.False);
            Assert.That(player.Inventory.Count, Is.EqualTo(2));
            player.CloseMenu();
            yield return null;
            Face(computer);
            devices.Press(keyboard.eKey, queueEventOnly: true);
            devices.Press(mouse.leftButton, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Station, Is.SameAs(computer));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(player.Inventory.Count, Is.EqualTo(2));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Sell first"), "The list itself is the focus route.");
            var oldClick = Button("Sell second");
            MenuTestUI.Click(oldClick);
            MenuTestUI.Click(oldClick);
            yield return null;
            Assert.That(player.Inventory.Items, Is.EqualTo(new[] { first }));
            Assert.That(player.Wallet.Balance, Is.EqualTo(17));
            MenuTestUI.Click(oldClick); // The displayed row has since been replaced.
            Assert.That(player.Inventory.Items, Is.EqualTo(new[] { first }));
            MenuTestUI.Click(Button("Sell all"));
            yield return null;
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.Wallet.Balance, Is.EqualTo(22));
            StringAssert.Contains("$22", Text("Trade balance"));
            Assert.That(computer.Selling, Is.False);
            Assert.That(MenuTestUI.View(player).Root.Q<Button>("Sell all"), Is.Null);
            Assert.That(Button("Upgrade Shovel").enabledSelf, Is.True);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Station));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.ExecuteStationCommand(ComputerStation.SellAllCommand), Is.False);
            MenuTestUI.Click(oldClick);
            Assert.That(player.Wallet.Balance, Is.EqualTo(22));
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
            MenuTestUI.Click(Button("Upgrade Shovel"));
            yield return null;
            Assert.That(player.Shovel.Level, Is.EqualTo(2));
            Assert.That(player.Wallet.Balance, Is.EqualTo(12));
            devices.Release(keyboard.eKey, queueEventOnly: true);
            player.CloseMenu();
            yield return null;
            Assert.That(player.GameplayActive, Is.True);
            Assert.That(player.SuccessfulStrokes, Is.Zero, "Held LMB must not leak out of the station.");
        }

        [UnityTest]
        public IEnumerator UpgradeCardBuysWithOneActivationAndSurvivesSurfaceTrips()
        {
            player.Wallet.TryCredit(10);
            Face(computer);
            Assert.That(player.TryInteract(), Is.True);
            yield return null;
            yield return null;
            // The row advertises its own numbers, so nothing has to be selected first.
            StringAssert.Contains($"{player.Shovel.Current.Radius * 2:F2} m", Text("Shovel effect"));
            StringAssert.Contains($"{player.Shovel.GetProfile(2).Radius * 2:F2} m", Text("Shovel effect"));
            StringAssert.Contains("Reach", Button("Upgrade Shovel").tooltip, "Secondary stats stay one hover away.");
            Assert.That(Button("Upgrade Shovel").text, Is.EqualTo("$10"));
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
            MenuTestUI.Click(Button("Upgrade Shovel"));
            yield return null;
            yield return null;
            Assert.That(player.Shovel.Level, Is.EqualTo(2), "A single activation buys exactly one level.");
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(Text("Trade balance"), Is.EqualTo("$0"));
            Assert.That(Button("Upgrade Shovel").enabledSelf, Is.False, "An unaffordable button reads as disabled.");
            Assert.That(Button("Upgrade Shovel").text, Is.EqualTo("$25"));
            Assert.That(Button("Upgrade Shovel").ClassListContains("short"), Is.True, "Out of reach is shown on the chip.");
            MenuTestUI.Click(Button("Upgrade Shovel"));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(player.Shovel.Level, Is.EqualTo(2));
            player.CloseMenu();
            yield return null;
            Place(new Vector3(0, 0.1f, -11.5f));
            player.transform.rotation = Quaternion.identity;
            player.Tick(new FpsInputFrame { Look = new Vector2(0, (player.Pitch - 85) / player.Tuning.LookSensitivity) }, 0.016f);
            Assert.That(player.TryDig(), Is.True);
            float removed = player.ExcavatedVolume;
            Assert.That(removed, Is.GreaterThan(0));
            player.Battery.TrySpend(player.Battery.Charge - 1);
            Place(player.SurfaceRecharge.transform.position + Vector3.up * 0.1f);
            yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(1), "Surface return no longer refills automatically.");
            Assert.That(player.Shovel.Level, Is.EqualTo(2));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(player.ExcavatedVolume, Is.EqualTo(removed));
        }

        [UnityTest]
        public IEnumerator WorkshopCardsBuyOneStepAtATimeAndKeepPointerTargetsStable()
        {
            // Budget for two tier-1 upgrades plus the refill beside them.
            player.Wallet.TryCredit(2 * EquipmentProgression.Price(1) + 1);
            player.Battery.TrySpend(99);
            Face(computer);
            Assert.That(player.TryInteract(), Is.True);
            yield return null; yield return null;
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Upgrade Shovel"), "The rows are the focus route.");
            StringAssert.Contains("10 \u2192 15", Text("Backpack effect"));
            Assert.That(player.Wallet.Balance, Is.EqualTo(2 * EquipmentProgression.Price(1) + 1));
            // Pointing at another card must never re-target the purchase: the card
            // that is clicked is the card that buys.
            devices.Set(mouse.position, MenuTestUI.ScreenPoint(player, Button("Upgrade Fuel tank")), queueEventOnly: true);
            yield return new WaitForSecondsRealtime(.1f);
            var backpack = Button("Upgrade Backpack");
            devices.Set(mouse.position, MenuTestUI.ScreenPoint(player, backpack), queueEventOnly: true);
            yield return null;
            devices.Press(mouse.leftButton, queueEventOnly: true); yield return null;
            devices.Release(mouse.leftButton, queueEventOnly: true); yield return null; yield return null;
            Assert.That(player.Inventory.Capacity, Is.EqualTo(15), "A single pointer press installs.");
            Assert.That(player.Battery.Capacity, Is.EqualTo(100));
            Assert.That(player.Wallet.Balance, Is.EqualTo(EquipmentProgression.Price(1) + 1));
            StringAssert.Contains("15 \u2192 20", Text("Backpack effect"));
            MenuTestUI.Click(Button("Upgrade Fuel tank"));
            yield return null; yield return null;
            Assert.That(player.Battery.Capacity, Is.EqualTo(150));
            Assert.That(player.Battery.Charge, Is.EqualTo(1));
            Assert.That(player.Wallet.Balance, Is.EqualTo(1));
            StringAssert.Contains("$1 per 100 fuel", Button("Upgrade Refill fuel").tooltip);
            Assert.That(Button("Upgrade Refill fuel").text, Is.EqualTo("$1"));
            MenuTestUI.Click(Button("Upgrade Refill fuel"));
            yield return null; yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(101));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(Button("Upgrade Refill fuel").ClassListContains("short"), Is.True);
            MenuTestUI.Click(Button("Upgrade Refill fuel"));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(player.Battery.Charge, Is.EqualTo(101));
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CriticalFractionalFuelRefillsFromPointerPurchaseAndClearsWarning()
        {
            player.Wallet.TryCredit(10);
            player.Battery.RestoreCharge(13.00586f);
            Face(computer);
            yield return null;
            Assert.That(player.TryInteract(), Is.True);
            yield return null; yield return null;
            Assert.That(computer.Refill.Cost, Is.EqualTo(1));
            Assert.That(computer.Refill.ChargeAfter, Is.EqualTo(100));
            var purchase = Button("Upgrade Refill fuel");
            devices.Set(mouse.position, MenuTestUI.ScreenPoint(player, purchase), queueEventOnly: true);
            yield return null;
            devices.Press(mouse.leftButton, queueEventOnly: true); yield return null;
            devices.Release(mouse.leftButton, queueEventOnly: true); yield return null; yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            Assert.That(player.Wallet.Balance, Is.EqualTo(9));
            Assert.That(Text("Trade balance"), Is.EqualTo("$9"));
            Assert.That(Button("Upgrade Refill fuel").text, Is.EqualTo("FULL"));
            player.CloseMenu(); yield return null; yield return null;
            Assert.That(player.GetComponent<FpsHud>().View.Root.Q<Label>("Fuel warning").text, Is.Empty);
            Assert.That(player.GetComponent<FpsHud>().View.Root.Q<Label>("Wallet").text, Is.EqualTo("$9"));
        }

        [UnityTest]
        public IEnumerator AdminCanGrantTestMoneyForPlaytesting()
        {
            Assert.That(player.AdminAvailable, Is.True, "Editor and development builds expose the admin page.");
            player.OpenMenu(PlayerMenu.DeveloperAdmin);
            yield return null; yield return null;
            MenuTestUI.Click(Button("Add $500"));
            yield return null;
            Assert.That(player.Wallet.Balance, Is.EqualTo(500));
            MenuTestUI.Click(Button("Add $500"));
            yield return null;
            Assert.That(player.Wallet.Balance, Is.EqualTo(1000));
            player.CloseMenu();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PausedStationRevalidatesFocusRangeDisabledStateAndDisplayedContents()
        {
            player.Inventory.TryAdd(new InventoryItem("a", "Marble", 5));
            Face(computer);
            Assert.That(player.TryInteract(), Is.True);
            yield return null;
            long displayed = player.StationRevision;
            player.Inventory.TryAdd(new InventoryItem("b", "Token", 9));
            Assert.That(player.ExecuteStationCommand(ComputerStation.SellAllCommand, displayed), Is.False);
            Assert.That(player.Inventory.Count, Is.EqualTo(2));
            Assert.That(player.Wallet.Balance, Is.Zero);
            yield return null;
            player.SetApplicationFocus(false);
            Assert.That(player.ExecuteStationCommand(ComputerStation.SellAllCommand), Is.False);
            player.SetApplicationFocus(true);
            computer.enabled = false;
            Assert.That(player.ExecuteStationCommand(ComputerStation.SellAllCommand), Is.False);
            computer.enabled = true;
            Place(player.transform.position + Vector3.forward * 8);
            Assert.That(player.ExecuteStationCommand(ComputerStation.SellAllCommand), Is.False);
            Assert.That(computer.TryExecute(ComputerStation.SellAllCommand, player), Is.False);
            Assert.That(player.Inventory.Count, Is.EqualTo(2));
            Assert.That(player.Wallet.Balance, Is.Zero);
            player.CloseMenu();
            player.OpenStation(computer);
            Assert.That(player.IsMenuOpen, Is.False, "A remote station cannot be opened directly.");
        }

        [UnityTest]
        public IEnumerator FullBagRowsScrollWithKeyboardAndResizeWithoutHidingTheFooter()
        {
            for (int i = 0; i < player.Inventory.Capacity; i++) player.Inventory.TryAdd(new InventoryItem("row-" + i, "Coin " + i, i + 1));
            Face(computer);
            Assert.That(player.TryInteract(), Is.True);
            yield return null;
            yield return null;
            var scroll = MenuTestUI.View(player).Root.Q<ScrollView>("menuScroll");
            Assert.That(scroll.contentContainer.layout.height, Is.GreaterThan(scroll.contentViewport.layout.height));
            Vector3 wheelDelta = Vector3.zero;
            scroll.RegisterCallback<WheelEvent>(e => wheelDelta = e.delta, TrickleDown.TrickleDown);
            devices.Set(mouse.position, MenuTestUI.ScreenPoint(player, scroll.contentViewport), queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.1f);
            devices.Set(mouse.scroll, new Vector2(0, -120), queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(scroll.scrollOffset.y, Is.GreaterThan(0), "Mouse wheel must reach the active Toolkit list: " + wheelDelta);
            scroll.scrollOffset = Vector2.zero;
            // Focusing a row scrolls it into view.
            Button("Sell row-9").Focus();
            yield return null;
            yield return null;
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Sell row-9"));
            Assert.That(scroll.scrollOffset.y, Is.GreaterThan(0));
            var row = Button("Sell row-9").worldBound;
            Assert.That(row.yMax, Is.LessThanOrEqualTo(scroll.contentViewport.worldBound.yMax + 1));
            Assert.That(row.yMin, Is.GreaterThanOrEqualTo(scroll.contentViewport.worldBound.yMin - 1));
            devices.Press(keyboard.enterKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Wallet.Balance, Is.EqualTo(10));
            Assert.That(player.Inventory.Items.Any(i => i.InstanceId == "row-9"), Is.False);
            var sellAll = Button("Sell all");
            Assert.That(sellAll.enabledInHierarchy, Is.True, "The whole-bag action stays below the list.");
            Assert.That(sellAll.worldBound.yMin, Is.GreaterThanOrEqualTo(scroll.worldBound.yMax - 1));
            Assert.That(sellAll.worldBound.yMax, Is.LessThanOrEqualTo(MenuTestUI.View(player).Root.worldBound.yMax));
        }

        [UnityTest]
        public IEnumerator EmptyBagSkipsSellingAndFinalIndividualSaleAdvancesOnRevisit()
        {
            Face(computer);
            Assert.That(player.TryInteract(), Is.True);
            yield return null; yield return null;
            Assert.That(computer.Selling, Is.False);
            Assert.That(MenuTestUI.View(player).Root.Q<Button>("Upgrade Shovel"), Is.Not.Null);
            Assert.That(MenuTestUI.View(player).Root.Q<Button>("Sell all"), Is.Null);
            player.CloseMenu();
            yield return null;
            player.Inventory.TryAdd(new InventoryItem("last", "Rock", 13));
            Assert.That(player.TryInteract(), Is.True);
            yield return null; yield return null;
            Assert.That(computer.Selling, Is.True);
            Assert.That(MenuTestUI.View(player).Root.Q<Button>("Upgrade Shovel"), Is.Null);
            player.CloseMenu();
            yield return null;
            Assert.That(player.Inventory.Count, Is.EqualTo(1), "Closing the computer never sells.");
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(player.TryInteract(), Is.True);
            yield return null; yield return null;
            var last = Button("Sell last");
            MenuTestUI.Click(last);
            yield return null; yield return null;
            MenuTestUI.Click(last);
            Assert.That(computer.Selling, Is.False);
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.Wallet.Balance, Is.EqualTo(13));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Upgrade Shovel"));
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
        }

        private void Face(StationTarget station)
        {
            Place(station.transform.position + new Vector3(0, 0.1f, 2.4f));
            player.transform.rotation = Quaternion.Euler(0, 180, 0);
            player.Tick(new FpsInputFrame { Look = new Vector2(0, (player.Pitch - 12) / player.Tuning.LookSensitivity) }, 0.016f);
            Physics.SyncTransforms();
        }

        private void Place(Vector3 position)
        {
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false; player.transform.position = position; motor.enabled = true;
            Physics.SyncTransforms();
        }

        private Button Button(string name) => MenuTestUI.Button(player, name);
        private string Text(string name) => MenuTestUI.Text(player, name);
    }
}
#endif
