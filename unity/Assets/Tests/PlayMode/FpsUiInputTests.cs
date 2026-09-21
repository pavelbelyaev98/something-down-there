using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class FpsUiInputTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private GameObject root, target, floor;
        private FpsPlayer player;
        private InputTestFixture devices;
        private PreferencesStore preferences;
        private PreferencesStore inputPreferences;

        private sealed class PreferencesStore : ICameraPreferencesStore
        {
            public string Contents;
            public int Writes;
            public bool Fail;
            public string Read() => Contents;
            public void Write(string contents)
            {
                Writes++;
                if (Fail) throw new System.IO.IOException("Test settings write failure");
                Contents = contents;
            }
        }

        [UnitySetUp]
        public IEnumerator CreatePlayer()
        {
            // Own the isolated input lifetime inside the coroutine setup: a later NUnit
            // SetUp reset would otherwise invalidate the player's already enabled actions.
            devices = new InputTestFixture();
            devices.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            root = new GameObject("UI test player");
            root.SetActive(false);
            root.layer = 2;
            var motor = root.AddComponent<CharacterController>();
            motor.center = new Vector3(0, 0.9f, 0);
            motor.height = 1.8f;
            motor.radius = 0.3f;
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(root.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.6f, 0);
            player = root.AddComponent<FpsPlayer>();
            preferences = new PreferencesStore();
            player.ConfigureCameraPreferences(preferences);
            inputPreferences = new PreferencesStore();
            player.ConfigureInputPreferences(inputPreferences);
            player.ConfigureGamePreferences(new PreferencesStore());
            root.AddComponent<FpsHud>();
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.transform.localScale = new Vector3(20, 1, 20);
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0, 1.6f, 2);
            root.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            // Establish the fixture's focus after Editor activation callbacks; individual
            // tests drive focus changes explicitly through the production input barrier.
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RemovePlayer()
        {
            Object.Destroy(root);
            Object.Destroy(target);
            Object.Destroy(floor);
            yield return null;
            Time.timeScale = 1f;
            devices.TearDown();
        }

        [UnityTest]
        public IEnumerator CameraKeyboardNavigationPreservesPauseFocusAndHeldActionBarriers()
        {
            var dig = target.AddComponent<ValidationDigTarget>();
            player.OpenMenu(PlayerMenu.Pause);
            yield return null; yield return null;
            yield return Key(keyboard.downArrowKey);
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Settings"));
            yield return Key(keyboard.enterKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeviceSettings));
            MenuTestUI.Click(MenuTestUI.Button(player, "settingsAccessibility"));
            yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.CameraComfort));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("fovSlider"));
            // A native keyboard supplies raw Toolkit keys as well as Input System
            // navigation. Only the latter may change the one-degree camera value.
            using (var raw = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.RightArrow }))
                MenuTestUI.View(player).Root.SendEvent(raw);
            Assert.That(player.ViewCamera.fieldOfView, Is.EqualTo(75));
            yield return Key(keyboard.rightArrowKey);
            Assert.That(player.ViewCamera.fieldOfView, Is.EqualTo(76));
            yield return Key(keyboard.downArrowKey);
            yield return Key(keyboard.leftArrowKey);
            Assert.That(player.CameraSettings.SteadyCrosshair, Is.False);
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("steadyCrosshair"));
            yield return Key(keyboard.tabKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.CameraComfort));
            float charge = player.Battery.Charge;
            Vector3 position = player.transform.position;
            Quaternion rotation = player.ViewCamera.transform.rotation;
            devices.Press(keyboard.wKey, queueEventOnly: true);
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            devices.Press(keyboard.eKey, queueEventOnly: true);
            devices.Press(mouse.leftButton, queueEventOnly: true);
            devices.Set(mouse.delta, new Vector2(100, 100), queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(player.ViewCamera.transform.rotation, Is.EqualTo(rotation));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            Assert.That(dig.HitsRemaining, Is.EqualTo(3));
            player.SetApplicationFocus(false);
            yield return null;
            player.SetApplicationFocus(true);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.CameraComfort));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("steadyCrosshair"));
            Assert.That(preferences.Writes, Is.EqualTo(1));
            yield return Key(keyboard.escapeKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Settings"));
            devices.Release(keyboard.wKey, queueEventOnly: true);
            yield return Key(keyboard.upArrowKey);
            yield return Key(keyboard.enterKey);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            Assert.That(player.IsJetpackActive, Is.False);
            Assert.That(dig.HitsRemaining, Is.EqualTo(3));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            devices.Release(mouse.leftButton, queueEventOnly: true);
            yield return null; yield return null;
            devices.Press(mouse.leftButton, queueEventOnly: true);
            yield return null; yield return null;
            Assert.That(dig.HitsRemaining, Is.EqualTo(2), "A new held press resumes digging.");
        }

        [UnityTest]
        public IEnumerator CameraResetAndRetryKeepControlsSelectionAndWorldState()
        {
            player.Inventory.TryAdd(new InventoryItem("kept", "Find", 7));
            player.Wallet.TryCredit(12);
            player.OpenMenu(PlayerMenu.Pause);
            yield return null;
            player.ShowCameraComfort();
            yield return null; yield return null;
            var page = MenuTestUI.View(player).CurrentScreen;
            var controls = page.Query().ToList().ToArray();
            var slider = page.Q<SliderInt>("fovSlider");
            slider.value = 55;
            player.CameraSettings.SetSteadyCrosshair(false);
            Assert.That(preferences.Writes, Is.Zero);
            var reset = MenuTestUI.Button(player, "cameraReset");
            reset.Focus();
            preferences.Fail = true;
            yield return Key(keyboard.enterKey);
            Assert.That(player.ViewCamera.fieldOfView, Is.EqualTo(75));
            Assert.That(player.CameraSettings.SteadyCrosshair, Is.True);
            Assert.That(player.CameraSettings.WriteFailed, Is.True);
            Assert.That(MenuTestUI.View(player).Focused, Is.SameAs(reset));
            Assert.That(page.Query().ToList(), Is.EquivalentTo(controls));
            Assert.That(page.Q("settingsError").ClassListContains("hidden"), Is.False);
            Assert.That(player.Inventory.Count, Is.EqualTo(1));
            Assert.That(player.Wallet.Balance, Is.EqualTo(12));
            preferences.Fail = false;
            var retry = page.Q<UnityEngine.UIElements.Button>("settingsRetry");
            retry.Focus();
            yield return Key(keyboard.enterKey);
            Assert.That(player.CameraSettings.WriteFailed, Is.False);
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("cameraReset"));
            int writes = preferences.Writes, redraws = 0;
            var label = page.Q<Label>("fovValue");
            yield return null; yield return null;
            EventCallback<GeometryChangedEvent> dirty = e => redraws++;
            label.RegisterCallback(dirty);
            for (int i = 0; i < 10; i++) yield return null;
            label.UnregisterCallback(dirty);
            Assert.That(preferences.Writes, Is.EqualTo(writes));
            Assert.That(redraws, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CameraMouseSliderAndSteadyCrosshairPreserveActualCenterActions()
        {
            target.AddComponent<ValidationDigTarget>();
            yield return null; yield return null;
            var reticle = root.GetComponent<FpsHud>().View.Root.Q<Label>("Reticle");
            Vector2 center = reticle.worldBound.center;
            foreach (int fov in new[] { 55, 75, 90 })
            {
                player.CameraSettings.SetVerticalFov(fov);
                Assert.That(player.TryDig(), Is.True);
                yield return null;
                Assert.That(reticle.worldBound.center, Is.EqualTo(center));
                Assert.That(reticle.resolvedStyle.scale.value, Is.EqualTo(Vector3.one));
                Assert.That(reticle.resolvedStyle.color, Is.EqualTo(Color.white));
            }
            player.CameraSettings.SetSteadyCrosshair(false);
            yield return null;
            Assert.That(reticle.resolvedStyle.scale.value.x, Is.GreaterThan(1));
            Assert.That(reticle.resolvedStyle.color, Is.Not.EqualTo(Color.white));
            player.CameraSettings.SetSteadyCrosshair(true);
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(player.IsJetpackActive, Is.True);
            Assert.That(reticle.resolvedStyle.scale.value, Is.EqualTo(Vector3.one));
            player.OpenMenu(PlayerMenu.Pause);
            yield return null;
            player.ShowCameraComfort();
            yield return null; yield return null;
            var slider = MenuTestUI.View(player).Root.Q<SliderInt>("fovSlider");
            devices.Set(mouse.position, MenuTestUI.ScreenPoint(player, slider, 0.25f), queueEventOnly: true);
            yield return null;
            devices.Press(mouse.leftButton, queueEventOnly: true);
            yield return null; yield return null;
            Assert.That(player.ViewCamera.fieldOfView, Is.InRange(62, 65));
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.CameraComfort));
        }

        private IEnumerator Key(UnityEngine.InputSystem.Controls.ButtonControl key)
        {
            devices.Press(key, queueEventOnly: true);
            yield return null; yield return null;
            devices.Release(key, queueEventOnly: true);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator HudRetainsControlsAndCenteredLayoutAcrossScaleAndMenuChanges()
        {
            yield return null; yield return null;
            var hud = root.GetComponent<FpsHud>().View.Root;
            var status = hud.Q<Label>("Status");
            var reticle = hud.Q<Label>("Reticle");
            var panel = root.GetComponentInChildren<UIDocument>().panelSettings;
            var content = status.text;
            try
            {
                foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1920, 1080), new Vector2Int(1280, 800) })
                {
                    panel.referenceResolution = size;
                    yield return null; yield return null;
                    Assert.That(hud.worldBound.Contains(status.worldBound.min), Is.True);
                    Assert.That(hud.worldBound.Contains(status.worldBound.max), Is.True);
                    Assert.That(Vector2.Distance(reticle.worldBound.center, hud.worldBound.center) * Screen.width / hud.worldBound.width, Is.LessThanOrEqualTo(1f), "Pixel-rounded UI must keep the reticle within one screen pixel of the aiming center.");
                    Assert.That(status.text, Is.EqualTo(content));
                    Assert.That(hud.Q<Label>("Status"), Is.SameAs(status));
                    Assert.That(hud.Query<VisualElement>().ToList().All(e => e.pickingMode == PickingMode.Ignore), Is.True);
                }
                player.OpenMenu(PlayerMenu.Pause);
                yield return null; yield return null;
                Assert.That(hud.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                player.CloseMenu();
                yield return null; yield return null;
                Assert.That(hud.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                int layouts = 0;
                EventCallback<GeometryChangedEvent> changed = e => layouts++;
                status.RegisterCallback(changed);
                for (int i = 0; i < 5; i++) yield return null;
                status.UnregisterCallback(changed);
                Assert.That(layouts, Is.Zero, "Unchanged HUD content should retain its layout.");
            }
            finally { panel.referenceResolution = new Vector2Int(1280, 720); }
        }

        [UnityTest]
        public IEnumerator ReturnWarningsShowReserveBandsAndFreezeAcrossInventoryInspection()
        {
            var batteryLabel = root.GetComponent<FpsHud>().View.Root.Q<Label>("Battery status");
            var warning = root.GetComponent<FpsHud>().View.Root.Q<Label>("Fuel warning");
            StringAssert.Contains("SAFE", batteryLabel.text);
            player.Battery.TrySpend(65);
            yield return null;
            StringAssert.Contains("RISKY", batteryLabel.text);
            Assert.That(warning.text, Is.EqualTo("LOW FUEL"));
            Assert.That(ColorUtility.ToHtmlStringRGB(warning.resolvedStyle.color), Is.EqualTo("FFD45C"));
            devices.Press(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(root.GetComponent<FpsHud>().View.Root.ClassListContains("hidden"), Is.True);
            player.Battery.TrySpend(20); // Simulate an external state change while inspection is open.
            yield return null;
            StringAssert.Contains("RISKY", batteryLabel.text, "A paused HUD does not cross warning bands.");
            devices.Release(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            devices.Press(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            StringAssert.Contains("CRITICAL", batteryLabel.text);
            StringAssert.Contains("15%", batteryLabel.text, "The displayed percentage must agree with the critical threshold.");
            Assert.That(warning.text, Is.EqualTo("FUEL CRITICAL"));
            Assert.That(ColorUtility.ToHtmlStringRGB(warning.resolvedStyle.color), Is.EqualTo("FF625C"));
            Assert.That(root.GetComponent<FpsHud>().View.Root.ClassListContains("hidden"), Is.False);
            player.Battery.TrySpend(15);
            yield return null;
            StringAssert.Contains("EMPTY", batteryLabel.text);
            Assert.That(warning.text, Is.EqualTo("FUEL EMPTY"));
            player.Battery.Recharge();
            yield return null;
            StringAssert.Contains("SAFE", batteryLabel.text);
            Assert.That(warning.text, Is.Empty);
            Assert.That(warning.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator HeldDigDoesNotLeakThroughInventoryClose()
        {
            var dig = target.AddComponent<ValidationDigTarget>();
            devices.Press(mouse.leftButton, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(dig.HitsRemaining, Is.EqualTo(2));
            devices.Press(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Inventory));
            devices.Release(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            devices.Press(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            yield return WaitForDigReady();
            Assert.That(dig.HitsRemaining, Is.EqualTo(2));
            devices.Release(mouse.leftButton, queueEventOnly: true);
            yield return null;
            yield return null;
            devices.Press(mouse.leftButton, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(dig.HitsRemaining, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TabInspectsNamesAndValuesWithoutOfferingOrExecutingTransactions()
        {
            target.AddComponent<ValidationStation>();
            var carried = Enumerable.Range(0, 10).Select(i => new InventoryItem("coin-" + i, "Coin", i + 1)).ToArray();
            foreach (var item in carried) player.Inventory.TryAdd(item);
            devices.Press(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Inventory));
            var view = MenuTestUI.View(player);
            StringAssert.Contains("Carried finds: 10 / 10", MenuTestUI.Text(player, "menuSubtitle"));
            var rows = view.CurrentScreen.Query(className: "item-row").ToList();
            Assert.That(rows.Count, Is.EqualTo(10));
            for (int i = 0; i < carried.Length; i++)
            {
                Assert.That(rows[i].Q<Label>("Find name").text, Is.EqualTo("Coin"));
                Assert.That(rows[i].Q<Label>("Sale value").text, Is.EqualTo("$" + carried[i].SaleValue));
            }
            Assert.That(view.CurrentScreen.Query<UnityEngine.UIElements.Button>().ToList().Select(b => b.name), Is.EqualTo(new[] { "Close" }));
            devices.Press(keyboard.eKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Inventory));
            Assert.That(player.ExecuteStationCommand(0), Is.False);
            devices.Release(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            devices.Press(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            Assert.That(player.Inventory.Items, Is.EqualTo(carried));
        }

        [UnityTest]
        public IEnumerator StationUsesEnterAndArrowNavigationWithoutSpaceSubmission()
        {
            target.AddComponent<ValidationStation>();
            var first = new InventoryItem("coin-01", "Coin", 5);
            var second = new InventoryItem("coin-02", "Coin", 17);
            player.Inventory.TryAdd(first);
            player.Inventory.TryAdd(second);
            // E opens the station while Space is held. Space must not submit Sell All.
            devices.Press(keyboard.eKey, queueEventOnly: true);
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Station));
            Assert.That(player.Inventory.Items, Is.EqualTo(new[] { first, second }));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Sell All"));
            devices.Press(keyboard.downArrowKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Sell first item"));
            devices.Press(keyboard.enterKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Inventory.Items, Is.EqualTo(new[] { second }));
            var rows = MenuTestUI.View(player).CurrentScreen.Query(className: "item-row").ToList();
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Q<Label>("Find name").text, Is.EqualTo("Coin"));
            Assert.That(rows[0].Q<Label>("Sale value").text, Is.EqualTo("$17"));
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Station));
        }

        [UnityTest]
        public IEnumerator HeldSpaceCannotRestartThrustAfterFocusPauseUntilReleased()
        {
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(player.IsJetpackActive, Is.True);
            player.SetApplicationFocus(false);
            float charge = player.Battery.Charge;
            Assert.That(player.IsJetpackActive, Is.False);
            yield return null;
            player.SetApplicationFocus(true);
            player.CloseMenu();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            Assert.That(player.IsJetpackActive, Is.False);
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            devices.Release(keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            yield return null;
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(player.IsJetpackActive, Is.True);
            Assert.That(player.Battery.Charge, Is.LessThan(charge));
        }

        [UnityTest]
        public IEnumerator HeldSpaceCannotRestartThrustAfterInventoryCloseUntilReleased()
        {
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(player.IsJetpackActive, Is.True);
            devices.Press(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Inventory));
            Assert.That(player.IsJetpackActive, Is.False);
            float charge = player.Battery.Charge;
            devices.Release(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            devices.Press(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(player.IsJetpackActive, Is.False);
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            devices.Release(keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            yield return null;
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.IsJetpackActive, Is.True, "A fresh press resumes this flight immediately.");
            Assert.That(player.Battery.Charge, Is.LessThan(charge));
        }

        [UnityTest]
        public IEnumerator HeldCrouchUsesLoweredTargetingAndSurvivesMenuAndDeviceRecovery()
        {
            target.transform.position = new Vector3(0, 1, 2);
            var dig = target.AddComponent<ValidationDigTarget>();
            devices.Press(keyboard.leftCtrlKey, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(player.CrouchAmount, Is.EqualTo(1));
            devices.Press(mouse.leftButton, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(dig.HitsRemaining, Is.EqualTo(2));
            player.OpenMenu(PlayerMenu.Pause);
            yield return null;
            Assert.That(MenuTestUI.View(player).Root.Query<Label>().ToList()
                .Any(label => label.text == "Crouch (hold)"), Is.True);
            Assert.That(MenuTestUI.View(player).Root.Query<Label>().ToList()
                .Any(label => label.text == player.InputSettings.Display(PlayerBinding.Crouch)), Is.True);
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            float charge = player.Battery.Charge;
            player.SetApplicationFocus(false);
            InputSystem.RemoveDevice(keyboard);
            keyboard = InputSystem.AddDevice<Keyboard>();
            devices.Press(keyboard.leftCtrlKey, queueEventOnly: true);
            devices.Press(keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            player.SetApplicationFocus(true);
            player.CloseMenu();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(player.CrouchAmount, Is.EqualTo(1));
            Assert.That(player.IsJetpackActive, Is.False);
            Assert.That(dig.HitsRemaining, Is.EqualTo(2));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            player.OpenMenu(PlayerMenu.Pause);
            devices.Release(keyboard.leftCtrlKey, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(player.CrouchAmount, Is.EqualTo(1), "Paused stance does not animate on key release.");
            player.CloseMenu();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(player.CrouchAmount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ControlsCaptureBlocksSubmitBackMovementAndDigThenUpdatesPauseLabels()
        {
            var dig = target.AddComponent<ValidationDigTarget>();
            player.OpenMenu(PlayerMenu.Pause); yield return null;
            MenuTestUI.Click(MenuTestUI.Button(player, "Settings")); yield return null; yield return null;
            MenuTestUI.Click(MenuTestUI.Button(player, "settingsControls")); yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.InputSettings));
            var page = MenuTestUI.View(player).CurrentScreen;
            var bind = page.Q<UnityEngine.UIElements.Button>("bindDig");
            bind.Focus(); yield return Key(keyboard.enterKey);
            Assert.That(player.BindingCapture.State, Is.EqualTo(BindingCaptureState.Listening));
            // Enter is now a binding candidate, never a second click on the focused row.
            yield return Key(keyboard.enterKey);
            Assert.That(player.InputSettings.Path(PlayerBinding.Dig), Is.EqualTo("<Keyboard>/enter"));
            Assert.That(player.BindingCapture.State, Is.EqualTo(BindingCaptureState.Idle));
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.InputSettings));
            float charge = player.Battery.Charge; var position = player.transform.position;
            page.Q<DropdownField>("digMode").value = "Toggle";
            Assert.That(player.InputSettings.ToggleDig, Is.True);
            player.InputSettings.Bind(PlayerBinding.Dig, "<Mouse>/rightButton", true);
            devices.Press(keyboard.wKey, queueEventOnly: true); devices.Press(mouse.rightButton, queueEventOnly: true);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(player.Battery.Charge, Is.EqualTo(charge)); Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(dig.HitsRemaining, Is.EqualTo(3));
            devices.Release(keyboard.wKey, queueEventOnly: true); devices.Release(mouse.rightButton, queueEventOnly: true);
            yield return null; yield return null;
            player.BackFromInputSettings(); yield return null; yield return null;
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Settings"));
            Assert.That(MenuTestUI.Text(player, "controlDig"), Is.EqualTo(player.InputSettings.Display(PlayerBinding.Dig)));
            Assert.That(MenuTestUI.Text(player, "controlDigDescription"), Is.EqualTo("Toggle dig / collect / throw"));
            Assert.That(MenuTestUI.Text(player, "controlGrab"), Is.EqualTo(player.InputSettings.Display(PlayerBinding.Grab)));
            Assert.That(new InputPreferences(inputPreferences).ToggleDig, Is.True);
            player.CloseMenu(); yield return null; yield return null;
            yield return new WaitForSecondsRealtime(0.4f); Assert.That(dig.HitsRemaining, Is.EqualTo(3));
            yield return Key(mouse.rightButton);
            yield return new WaitForSecondsRealtime(0.4f); Assert.That(dig.HitsRemaining, Is.LessThan(3));
        }

        [UnityTest]
        public IEnumerator ControlsConflictCancelReplaceResetAndWriteRetryPreserveWorldAndCamera()
        {
            player.Inventory.TryAdd(new InventoryItem("kept", "Find", 7)); player.Wallet.TryCredit(12);
            player.CameraSettings.SetVerticalFov(80);
            player.OpenMenu(PlayerMenu.Pause); yield return null; player.ShowInputSettings(); yield return null; yield return null;
            var page = MenuTestUI.View(player).CurrentScreen;
            MenuTestUI.Click(page.Q<UnityEngine.UIElements.Button>("bindDig")); yield return null;
            yield return Key(keyboard.spaceKey);
            Assert.That(player.BindingCapture.State, Is.EqualTo(BindingCaptureState.Conflict));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("bindingCancel"));
            yield return Key(keyboard.enterKey); Assert.That(player.InputSettings.Path(PlayerBinding.Dig), Is.EqualTo("<Mouse>/leftButton"));
            MenuTestUI.Click(page.Q<UnityEngine.UIElements.Button>("bindDig")); yield return null;
            yield return Key(keyboard.spaceKey);
            MenuTestUI.Click(page.Q<UnityEngine.UIElements.Button>("bindingReplace")); yield return null; yield return null;
            Assert.That(player.InputSettings.Path(PlayerBinding.Dig), Is.EqualTo("<Keyboard>/space"));
            Assert.That(player.InputSettings.Path(PlayerBinding.Jump), Is.EqualTo("<Mouse>/leftButton"));
            inputPreferences.Fail = true;
            MenuTestUI.Click(MenuTestUI.Button(player, "inputReset")); yield return null;
            Assert.That(player.InputSettings.WriteFailed, Is.True);
            Assert.That(page.Q("inputSettingsError").ClassListContains("hidden"), Is.False);
            Assert.That(player.InputSettings.Path(PlayerBinding.Dig), Is.EqualTo("<Mouse>/leftButton"));
            inputPreferences.Fail = false;
            MenuTestUI.Click(page.Q<UnityEngine.UIElements.Button>("inputRetry")); yield return null;
            Assert.That(player.InputSettings.WriteFailed, Is.False);
            Assert.That(player.CameraSettings.VerticalFov, Is.EqualTo(80)); Assert.That(player.Inventory.Count, Is.EqualTo(1)); Assert.That(player.Wallet.Balance, Is.EqualTo(12));
        }

        [UnityTest]
        public IEnumerator UnboundKeyDoesNotInterruptHeldDig()
        {
            var dig = target.AddComponent<ValidationDigTarget>();
            yield return null; yield return null;
            devices.Press(mouse.leftButton, queueEventOnly: true); yield return null; yield return null;
            Assert.That(dig.HitsRemaining, Is.EqualTo(2));
            yield return Key(keyboard.qKey);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(dig.HitsRemaining, Is.LessThan(2));
            devices.Release(mouse.leftButton, queueEventOnly: true); yield return null;
            devices.Press(mouse.leftButton, queueEventOnly: true); yield return null; yield return null;
            devices.Release(mouse.leftButton, queueEventOnly: true);
        }

        [UnityTest]
        public IEnumerator ToggleClearsOnFocusAndModeChangesWithoutAutonomousResume()
        {
            var dig = target.AddComponent<ValidationDigTarget>();
            player.InputSettings.Bind(PlayerBinding.Dig, "<Keyboard>/q", true); player.InputSettings.SetToggleDig(true);
            yield return null; yield return null;
            yield return Key(keyboard.qKey); Assert.That(dig.HitsRemaining, Is.EqualTo(2));
            player.SetApplicationFocus(false); yield return null; player.SetApplicationFocus(true); player.CloseMenu();
            yield return WaitForDigReady();
            Assert.That(dig.HitsRemaining, Is.EqualTo(2));
            yield return Key(keyboard.qKey); Assert.That(dig.HitsRemaining, Is.EqualTo(1));
            player.InputSettings.SetToggleDig(false);
            yield return WaitForDigReady(); Assert.That(dig.HitsRemaining, Is.EqualTo(1));
            player.InputSettings.SetToggleDig(true);
            yield return WaitForDigReady(); Assert.That(dig.HitsRemaining, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WorldRestoreRetainsPreferencesAndClearsActiveToggle()
        {
            var dig = target.AddComponent<ValidationDigTarget>();
            player.InputSettings.SetToggleDig(true); player.InputSettings.Bind(PlayerBinding.Dig, "<Keyboard>/q", true);
            yield return null; yield return null;
            yield return Key(keyboard.qKey); Assert.That(dig.HitsRemaining, Is.EqualTo(2));
            var snapshot = new WorldSnapshot(); player.Capture(snapshot); player.Restore(snapshot);
            yield return WaitForDigReady();
            Assert.That(dig.HitsRemaining, Is.EqualTo(2)); Assert.That(player.InputSettings.ToggleDig, Is.True);
            yield return Key(keyboard.qKey); Assert.That(dig.HitsRemaining, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CaptureEscapeAndFocusLossCancelWithoutClosingControls()
        {
            player.OpenMenu(PlayerMenu.Pause); yield return null; player.ShowInputSettings(); yield return null; yield return null;
            var page = MenuTestUI.View(player).CurrentScreen;
            MenuTestUI.Click(page.Q<UnityEngine.UIElements.Button>("bindPause")); yield return null;
            yield return Key(keyboard.escapeKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.InputSettings));
            Assert.That(player.InputSettings.Path(PlayerBinding.Pause), Is.EqualTo("<Keyboard>/escape"));
            MenuTestUI.Click(page.Q<UnityEngine.UIElements.Button>("bindPause")); yield return null;
            player.SetApplicationFocus(false); yield return null; player.SetApplicationFocus(true); yield return null;
            Assert.That(player.BindingCapture.State, Is.EqualTo(BindingCaptureState.Idle));
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.InputSettings));
            yield return Key(keyboard.escapeKey); Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
        }

        [UnityTest]
        public IEnumerator PauseReboundToMouseOrEnterStillAllowsMenuButtonsAndCannotImmediatelyResume()
        {
            player.OpenMenu(PlayerMenu.Pause); yield return null; yield return null;
            var point = MenuTestUI.ScreenPoint(player, MenuTestUI.Button(player, "Resume"));
            player.CloseMenu();
            player.InputSettings.Bind(PlayerBinding.Pause, "<Mouse>/leftButton", true);
            yield return null; yield return null;
            devices.Set(mouse.position, point, queueEventOnly: true); yield return null;
            yield return Key(mouse.leftButton);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause), "The press opening Pause cannot click Resume.");
            yield return Key(keyboard.enterKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            player.InputSettings.Bind(PlayerBinding.Pause, "<Keyboard>/enter"); yield return null; yield return null;
            yield return Key(keyboard.enterKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
            yield return Key(keyboard.downArrowKey); yield return Key(keyboard.downArrowKey); yield return Key(keyboard.enterKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeviceSettings), "Enter belongs to menu submit while a menu is open.");
        }

        [UnityTest]
        public IEnumerator CategorizedSettingsHaveShortCopyAndKeepInputBlocked()
        {
            player.OpenMenu(PlayerMenu.Pause); player.ShowSettings();
            yield return null; yield return null;
            var view = MenuTestUI.View(player);
            Assert.That(view.Root.Q("settingsNavigation").Query<UnityEngine.UIElements.Button>().ToList().Select(b => b.text),
                Is.EqualTo(new[] { "Display", "Graphics", "Audio", "Controls", "Accessibility" }));
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("windowMode"));
            Assert.That(view.Root.Q<Label>("menuSubtitle").text, Is.Empty);
            Assert.That(view.Root.Q("fpsLimit").enabledInHierarchy, Is.True);
            view.Root.Q<Toggle>("vSync").value = true;
            Assert.That(view.Root.Q("fpsLimit").enabledInHierarchy, Is.False);
            view.Root.Q("vSync").Focus(); yield return Key(keyboard.leftArrowKey);
            Assert.That(player.GameSettings.Values.VSync, Is.False);
            Assert.That(view.Root.Q("fpsLimit").enabledInHierarchy, Is.True);
            view.Root.Q("fpsLimit").Focus(); yield return Key(keyboard.rightArrowKey);
            Assert.That(player.GameSettings.Values.FrameLimit, Is.EqualTo(165));
            var position = player.transform.position;
            var rotation = player.ViewCamera.transform.rotation;
            player.Tick(new FpsInputFrame { Move = Vector2.one, Look = Vector2.one * 100, DigHeld = true, JumpPressed = true }, 1);
            Assert.That(player.transform.position, Is.EqualTo(position)); Assert.That(player.ViewCamera.transform.rotation, Is.EqualTo(rotation));
            MenuTestUI.Click(MenuTestUI.Button(player, "settingsAudio")); yield return null; yield return null;
            view.Root.Q<SliderInt>("masterVolume").value = 35;
            Assert.That(player.GameSettings.Values.MasterVolume, Is.EqualTo(35));
            MenuTestUI.Click(MenuTestUI.Button(player, "deviceReset"));
            Assert.That(player.GameSettings.Values.MasterVolume, Is.EqualTo(100));
            Assert.That(player.GameSettings.Values.FrameLimit, Is.EqualTo(165));
            yield return Key(keyboard.escapeKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause)); Assert.That(MenuTestUI.Focused(player), Is.EqualTo("Settings"));
        }

        [UnityTest]
        public IEnumerator SettingsRowsShareColumnsAndControlsScrollAsOneList()
        {
            player.OpenMenu(PlayerMenu.Pause); player.ShowSettings();
            float right = 0, height = 0;
            foreach (var category in new[] { SettingsCategory.Display, SettingsCategory.Graphics, SettingsCategory.Audio, SettingsCategory.Controls, SettingsCategory.Accessibility })
            {
                player.ShowSettingsCategory(category); yield return null; yield return null;
                var view = MenuTestUI.View(player);
                var row = view.CurrentScreen.Q(className: "preference-row");
                Assert.That(row, Is.Not.Null);
                if (right == 0) { right = row.worldBound.xMax; height = row.worldBound.height; }
                Assert.That(row.worldBound.xMax, Is.EqualTo(right).Within(1));
                Assert.That(row.worldBound.height, Is.EqualTo(height).Within(1));
                Assert.That(view.Root.Q("settingsBack").ClassListContains("hidden"), Is.False);
                Assert.That(view.Root.Q("settingsFooter").Contains(view.Root.Q("settingsBack")), Is.True);
                Assert.That(view.Root.Q("qualityPreset"), Is.Null);
                Assert.That(view.CurrentScreen.Query<UnityEngine.UIElements.Button>().ToList().Any(b => b.text == "Back" || b.text == "Apply display"), Is.False);
                if (category == SettingsCategory.Audio) Assert.That(view.CurrentScreen.Query(className: "preference-row").ToList().Count, Is.EqualTo(1));
                if (category == SettingsCategory.Controls) Assert.That(view.Root.Q<ScrollView>("bindingScroll").Contains(view.Root.Q("digMode")), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator SharedControlsKeepReadableStatesAndAlignedNativeSlider()
        {
            player.OpenMenu(PlayerMenu.Pause);
            yield return null; yield return null;
            var view = MenuTestUI.View(player);
            var states = typeof(VisualElement).GetProperty("pseudoStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(states, Is.Not.Null);
            foreach (var button in view.CurrentScreen.Query<UnityEngine.UIElements.Button>().ToList())
            {
                object original = states.GetValue(button);
                foreach (string state in new[] { "Hover", "Focus", "Hover, Focus", "Hover, Active" })
                {
                    states.SetValue(button, System.Enum.Parse(states.PropertyType, state));
                    yield return null;
                    AssertReadableNeutralText(button);
                    Assert.That(button.resolvedStyle.borderTopColor.a, Is.Zero, "Pointer-only secondary actions stay borderless.");
                }
                states.SetValue(button, original);
            }
            player.ShowSettings(); yield return null; yield return null;
            var tabs = view.Root.Q("settingsNavigation").Query<UnityEngine.UIElements.Button>().ToList();
            foreach (var tab in tabs)
            {
                object original = states.GetValue(tab);
                states.SetValue(tab, System.Enum.Parse(states.PropertyType, "Hover, Focus, Active"));
                yield return null; AssertReadableNeutralText(tab); states.SetValue(tab, original);
            }
            var dropdown = view.Root.Q<DropdownField>("windowMode");
            dropdown.Focus(); yield return Key(keyboard.enterKey); yield return null;
            var popup = view.Root.panel.visualTree.Q("menuDropdown");
            Assert.That(popup, Is.Not.Null);
            foreach (var item in popup.Query(className: "unity-base-dropdown__item").ToList())
            {
                object original = states.GetValue(item);
                states.SetValue(item, System.Enum.Parse(states.PropertyType, "Hover, Focus"));
                yield return null;
                AssertReadableNeutralText(item.Q<Label>());
                var checkmark = item.Q(className: "unity-base-dropdown__checkmark");
                if (checkmark != null) Assert.That(checkmark.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                states.SetValue(item, original);
            }
            yield return Key(keyboard.escapeKey);
            view.Root.Q<Toggle>("vSync").value = true; yield return null;
            Assert.That(view.Root.Q<DropdownField>("fpsLimit").Q<TextElement>().resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.MiddleRight));
            player.ShowSettingsCategory(SettingsCategory.Audio); yield return null; yield return null;
            var slider = view.Root.Q<SliderInt>("masterVolume");
            foreach (int value in new[] { 0, 50, 100 })
            {
                slider.value = value; yield return null; yield return null;
                var track = slider.Q(className: "unity-base-slider__tracker").worldBound;
                var thumb = slider.Q(className: "unity-base-slider__dragger").worldBound;
                Assert.That(thumb.center.y, Is.EqualTo(track.center.y).Within(1));
                Assert.That(view.Root.Q("masterVolumeValue").worldBound.center.y, Is.EqualTo(track.center.y).Within(1));
            }
            var back = view.Root.Q("settingsBack").worldBound;
            var reset = view.Root.Q("deviceReset").worldBound;
            Assert.That(reset.y, Is.EqualTo(back.y).Within(1));
            Assert.That(reset.width, Is.EqualTo(back.width).Within(1));
            Assert.That(reset.x - back.xMax, Is.InRange(10, 14));
        }

        private static void AssertReadableNeutralText(TextElement text)
        {
            Assert.That(text, Is.Not.Null);
            Color background = Color.white;
            var parents = new System.Collections.Generic.List<VisualElement>();
            for (var element = (VisualElement)text; element != null; element = element.parent) parents.Add(element);
            parents.Reverse();
            foreach (var element in parents)
            {
                Color color = element.resolvedStyle.backgroundColor;
                background = Color.Lerp(background, new Color(color.r, color.g, color.b, 1), color.a);
            }
            Color foreground = text.resolvedStyle.color;
            Assert.That(foreground.r, Is.EqualTo(foreground.g).Within(0.001));
            Assert.That(foreground.g, Is.EqualTo(foreground.b).Within(0.001));
            Assert.That(background.r, Is.EqualTo(background.g).Within(0.001));
            Assert.That(background.g, Is.EqualTo(background.b).Within(0.001));
            float a = ContrastLuminance(foreground), b = ContrastLuminance(background);
            Assert.That((Mathf.Max(a,b)+0.05f)/(Mathf.Min(a,b)+0.05f), Is.GreaterThanOrEqualTo(4.5f), text.text);
        }

        private static float ContrastLuminance(Color color)
        {
            float Linear(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);
        }

        [UnityTest]
        public IEnumerator DropdownEscapeClosesOnlyTheListAndSelectionAppliesImmediately()
        {
            player.OpenMenu(PlayerMenu.Pause); player.ShowSettings();
            yield return null; yield return null;
            var view = MenuTestUI.View(player);
            var dropdown = view.Root.Q<DropdownField>("windowMode");
            dropdown.Focus(); yield return Key(keyboard.enterKey);
            Assert.That(view.Root.panel.visualTree.Q(className: GenericDropdownMenu.ussClassName), Is.Not.Null);
            yield return Key(keyboard.escapeKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeviceSettings));
            Assert.That(view.Root.panel.visualTree.Q(className: GenericDropdownMenu.ussClassName), Is.Null);
            Assert.That(player.GameSettings.PreviewingDisplay, Is.False);
            dropdown.Focus(); yield return Key(keyboard.enterKey);
            yield return Key(keyboard.downArrowKey); yield return Key(keyboard.downArrowKey); yield return Key(keyboard.enterKey);
            Assert.That(player.GameSettings.PreviewingDisplay, Is.True);
            yield return Key(keyboard.escapeKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeviceSettings));
            yield return Key(keyboard.escapeKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
        }

        [UnityTest]
        public IEnumerator DisplayPreviewUsesSafeFocusBlocksTabsAndEscapeRevertsInPlace()
        {
            player.OpenMenu(PlayerMenu.Pause); player.ShowSettings();
            yield return null; yield return null;
            var view = MenuTestUI.View(player); var original = player.GameSettings.CurrentDisplay;
            view.Root.Q("windowMode").Focus(); yield return Key(keyboard.rightArrowKey);
            yield return null; yield return null;
            Assert.That(view.Root.ClassListContains("dialog-menu"), Is.True);
            Assert.That(view.Root.Q("settingsNavigation").ClassListContains("hidden"), Is.True);
            Assert.That(view.Root.Q("displayApply"), Is.Null);
            Assert.That(player.GameSettings.PreviewingDisplay, Is.True);
            Assert.That(MenuTestUI.Focused(player), Is.EqualTo("displayRevert"));
            Assert.That(view.Root.Q("settingsNavigation").enabledInHierarchy, Is.False);
            player.ShowInputSettings(); Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeviceSettings));
            yield return Key(keyboard.escapeKey);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeviceSettings));
            Assert.That(player.GameSettings.PreviewingDisplay, Is.False);
            Assert.That(player.GameSettings.CurrentDisplay.Same(original), Is.True);
            Assert.That(player.GameSettings.Values.Width, Is.Zero);
            yield return Key(keyboard.escapeKey); Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
        }

        [UnityTest]
        public IEnumerator MouseGainAndInversionChangeLookWithoutChangingFieldOfView()
        {
            player.GameSettings.Edit(v => { v.Sensitivity = 200; v.InvertX = v.InvertY = true; });
            yield return null;
            player.Tick(new FpsInputFrame { Look = new Vector2(10, 10) }, 0.01f);
            Assert.That(Mathf.DeltaAngle(0, player.transform.eulerAngles.y), Is.EqualTo(-2.4f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(0, player.ViewCamera.transform.localEulerAngles.x), Is.EqualTo(2.4f).Within(0.01f));
            Assert.That(player.ViewCamera.fieldOfView, Is.EqualTo(75));
            player.OpenMenu(PlayerMenu.Pause); player.ShowInputSettings(); yield return null; yield return null;
            MenuTestUI.Click(MenuTestUI.Button(player, "inputReset"));
            Assert.That(player.GameSettings.Values.Sensitivity, Is.EqualTo(100));
            Assert.That(player.GameSettings.Values.InvertY, Is.False);
        }

        [UnityTest]
        public IEnumerator RuntimeSettingsApplyToFramePacingAudioAndClonedRendererThenRestore()
        {
            int originalSync = QualitySettings.vSyncCount, originalLimit = Application.targetFrameRate, originalTextures = QualitySettings.globalTextureMipmapLimit;
            float originalVolume = AudioListener.volume;
            var original = QualitySettings.renderPipeline;
            var originalFiltering = QualitySettings.anisotropicFiltering;
            var source = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            float shadowDistance = source.shadowDistance;
            int shadowResolution = source.mainLightShadowmapResolution, shadowCascades = source.shadowCascadeCount;
            using (var preferences = new GamePreferences(new PreferencesStore { Contents = "{\"Version\":1,\"MasterVolume\":25,\"Muted\":true,\"MuteUnfocused\":true}" }, new UnityGameSettingsPlatform()))
            {
                Assert.That(preferences.Values.RenderScale, Is.EqualTo(100));
                Assert.That((float)QualitySettings.renderPipeline.GetType().GetProperty("renderScale").GetValue(QualitySettings.renderPipeline), Is.EqualTo(1f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.25f), "Removed legacy mute flags must have no hidden effect.");
                preferences.Edit(v => { v.VSync = false; v.FrameLimit = 30; v.MasterVolume = 25; v.RenderScale = 75; v.Msaa = 2; v.TextureLimit = 1; v.Filtering = 2; });
                Assert.That(Application.targetFrameRate, Is.EqualTo(30)); Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(AudioListener.volume, Is.EqualTo(0.25f));
                Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(1));
                Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(AnisotropicFiltering.ForceEnable));
                var pipeline = QualitySettings.renderPipeline;
                Assert.That(pipeline, Is.Not.SameAs(original));
                Assert.That((float)pipeline.GetType().GetProperty("renderScale").GetValue(pipeline), Is.EqualTo(0.75f));
                Assert.That((int)pipeline.GetType().GetProperty("msaaSampleCount").GetValue(pipeline), Is.EqualTo(2));
                var runtime = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)pipeline;
                foreach (int level in new[] { 0, 1, 2, 3, 0, 3 })
                {
                    preferences.Edit(v => v.Shadows = level);
                    yield return null;
                    Assert.That(runtime.shadowDistance, Is.EqualTo(level == 0 ? 0 : level == 3 ? shadowDistance : Mathf.Min(shadowDistance, level == 1 ? 25f : 35f)));
                    Assert.That(runtime.mainLightShadowmapResolution, Is.EqualTo(level == 3 ? shadowResolution : Mathf.Min(shadowResolution, level <= 1 ? 1024 : 2048)));
                    Assert.That(runtime.shadowCascadeCount, Is.EqualTo(level == 3 ? shadowCascades : Mathf.Min(shadowCascades, level <= 1 ? 1 : 2)));
                    Assert.That(source.shadowDistance, Is.EqualTo(shadowDistance), "Runtime settings must not mutate the source asset.");
                    Assert.That(source.mainLightShadowmapResolution, Is.EqualTo(shadowResolution));
                    Assert.That(source.shadowCascadeCount, Is.EqualTo(shadowCascades));
                }
                preferences.SetFocus(false); Assert.That(AudioListener.volume, Is.EqualTo(0.25f), "Focus changes no longer mute the listener.");
                preferences.SetFocus(true); Assert.That(AudioListener.volume, Is.EqualTo(0.25f));
                preferences.Edit(v => { v.MasterVolume = 0; v.VSync = true; });
                Assert.That(AudioListener.volume, Is.Zero); Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
            }
            yield return null;
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(original));
            Assert.That(AudioListener.volume, Is.EqualTo(originalVolume));
            Assert.That(Application.targetFrameRate, Is.EqualTo(originalLimit));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(originalSync));
            Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(originalTextures));
            Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(originalFiltering));
        }

        [UnityTest]
        public IEnumerator PointerCanResumePauseWithoutDiggingUnderneath()
        {
            var dig = target.AddComponent<ValidationDigTarget>();
            devices.Press(keyboard.escapeKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
            var resume = MenuTestUI.Button(player, "Resume");
            var screen = MenuTestUI.ScreenPoint(player, resume);
            devices.Set(mouse.position, screen, queueEventOnly: true);
            yield return null;
            devices.Press(mouse.leftButton, queueEventOnly: true);
            yield return null;
            devices.Release(mouse.leftButton, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
            Assert.That(dig.HitsRemaining, Is.EqualTo(3));
        }

        // A stroke only lands once the tool's cooldown has elapsed, so the wait tracks
        // the live cadence instead of a value tuned to the old starter bite.
        private IEnumerator WaitForDigReady() => new WaitForSecondsRealtime(player.EffectiveDigInterval + .1f);
    }
}
