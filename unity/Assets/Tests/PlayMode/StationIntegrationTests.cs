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

        // Trading uses inventory identities, never the buried population.
        private static void DeferFinds(Scene loaded, LoadSceneMode mode)
        {
            foreach (var root in loaded.GetRootGameObjects())
                foreach (var field in root.GetComponentsInChildren<DiscoveryField>(true)) field.DeferGeneration();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldTimeScale = Time.timeScale; oldCursor = Cursor.lockState; oldCursorVisible = Cursor.visible;
            devices = new InputTestFixture(); devices.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            Time.timeScale = 1;
            SceneManager.sceneLoaded += DeferFinds;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= DeferFinds;
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
        public IEnumerator RealStationInputSellsEverythingInOneActionAndRejectsOldButtons()
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
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Sell all"), "Selling is one action.");
            var rows = MenuTestUI.View(player).CurrentScreen.Query(className: "item-row").ToList();
            Assert.That(rows.Count, Is.EqualTo(1), "Finds of one kind share a row.");
            Assert.That(rows[0].Q<Label>("Find count").text, Is.EqualTo("\u00d72"));
            Assert.That(rows[0].Q<Label>("Sale value").text, Is.EqualTo("$22"));
            var oldClick = Button("Sell all");
            MenuTestUI.Click(oldClick);
            MenuTestUI.Click(oldClick); // A second submission of the same quote sells nothing more.
            yield return null;
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.Wallet.Balance, Is.EqualTo(22));
            StringAssert.Contains("$22", Text("Trade balance"));
            Assert.That(computer.Selling, Is.False);
            Assert.That(MenuTestUI.View(player).Root.Q<Button>("Sell all"), Is.Null);
            Assert.That(Button("Upgrade Tool").enabledSelf, Is.True);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Station));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.ExecuteStationCommand(ComputerStation.SellAllCommand), Is.False);
            MenuTestUI.Click(oldClick);
            Assert.That(player.Wallet.Balance, Is.EqualTo(22));
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
            MenuTestUI.Click(Button("Upgrade Tool"));
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
            StringAssert.Contains($"{player.Shovel.Current.Radius * 2:F2} m", Text("Tool effect"));
            StringAssert.Contains($"{player.Shovel.GetProfile(2).Radius * 2:F2} m", Text("Tool effect"));
            StringAssert.Contains("Reach", Button("Upgrade Tool").tooltip, "Secondary stats stay one hover away.");
            Assert.That(Button("Upgrade Tool").text, Is.EqualTo("$10"));
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
            MenuTestUI.Click(Button("Upgrade Tool"));
            yield return null;
            yield return null;
            Assert.That(player.Shovel.Level, Is.EqualTo(2), "A single activation buys exactly one level.");
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(Text("Trade balance"), Is.EqualTo("$0"));
            Assert.That(Button("Upgrade Tool").enabledSelf, Is.False, "An unaffordable button reads as disabled.");
            Assert.That(Button("Upgrade Tool").text, Is.EqualTo("$25"));
            Assert.That(Button("Upgrade Tool").ClassListContains("short"), Is.True, "Out of reach is shown on the chip.");
            MenuTestUI.Click(Button("Upgrade Tool"));
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
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Upgrade Tool"), "The rows are the focus route.");
            StringAssert.Contains("10 \u2192 15", Text("Backpack effect"));
            Assert.That(player.Wallet.Balance, Is.EqualTo(2 * EquipmentProgression.Price(1) + 1));
            // Pointing at another card must never re-target the purchase: the card
            // that is clicked is the card that buys.
            devices.Set(mouse.position, MenuTestUI.ScreenPoint(player, Button("Upgrade Battery")), queueEventOnly: true);
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
            MenuTestUI.Click(Button("Upgrade Battery"));
            yield return null; yield return null;
            Assert.That(player.Battery.Capacity, Is.EqualTo(150));
            Assert.That(player.Battery.Charge, Is.EqualTo(1));
            Assert.That(player.Wallet.Balance, Is.EqualTo(1));
            StringAssert.Contains("$1 per 100 charge", Button("Upgrade Recharge").tooltip);
            Assert.That(Button("Upgrade Recharge").text, Is.EqualTo("$1"));
            MenuTestUI.Click(Button("Upgrade Recharge"));
            yield return null; yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(101));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(Button("Upgrade Recharge").ClassListContains("short"), Is.True);
            MenuTestUI.Click(Button("Upgrade Recharge"));
            Assert.That(player.Wallet.Balance, Is.Zero);
            Assert.That(player.Battery.Charge, Is.EqualTo(101));
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
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
        public IEnumerator EmptyBagSkipsSellingAndFinalIndividualSaleAdvancesOnRevisit()
        {
            Face(computer);
            Assert.That(player.TryInteract(), Is.True);
            yield return null; yield return null;
            Assert.That(computer.Selling, Is.False);
            Assert.That(MenuTestUI.View(player).Root.Q<Button>("Upgrade Tool"), Is.Not.Null);
            Assert.That(MenuTestUI.View(player).Root.Q<Button>("Sell all"), Is.Null);
            player.CloseMenu();
            yield return null;
            player.Inventory.TryAdd(new InventoryItem("last", "Rock", 13));
            Assert.That(player.TryInteract(), Is.True);
            yield return null; yield return null;
            Assert.That(computer.Selling, Is.True);
            Assert.That(MenuTestUI.View(player).Root.Q<Button>("Upgrade Tool"), Is.Null);
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
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Upgrade Tool"));
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EveryTrackBuysThroughTheLastLevelAndTheDrillMilestoneIsVisible()
        {
            int last = EquipmentProgression.LevelCount;
            player.Wallet.TryCredit(3 * Enumerable.Range(1, last - 1).Sum(EquipmentProgression.Price));
            Face(computer); Assert.That(player.TryInteract(), Is.True);
            yield return null; yield return null;
            foreach (string track in new[] { "Tool", "Backpack", "Battery" })
            {
                for (int level = 1; level < last; level++)
                {
                    Assert.That(Progress(track), Is.EqualTo(100f * level / last).Within(0.01f));
                    if (track == "Tool" && level == 6)
                    {
                        StringAssert.Contains("Shovel → Drill", Text("Tool effect"));
                        Assert.That(player.ShavingEnabled, Is.False);
                    }
                    float charge = player.Battery.Charge;
                    MenuTestUI.Click(Button("Upgrade " + track));
                    yield return null; yield return null;
                    Assert.That(player.Battery.Charge, Is.EqualTo(charge));
                    if (track == "Tool" && level >= 6) Assert.That(player.ShavingEnabled, Is.True);
                }
                var max = Button("Upgrade " + track);
                Assert.That(max.text, Is.EqualTo("MAX")); Assert.That(max.enabledSelf, Is.False);
                Assert.That(Progress(track), Is.EqualTo(100f).Within(0.01f));
                decimal balance = player.Wallet.Balance; MenuTestUI.Click(max);
                Assert.That(player.Wallet.Balance, Is.EqualTo(balance));
            }
            Assert.That(player.Wallet.Balance, Is.Zero);
        }

        private float Progress(string track) =>
            MenuTestUI.View(player).Root.Q(track + " progress fill").style.width.value.value;

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
