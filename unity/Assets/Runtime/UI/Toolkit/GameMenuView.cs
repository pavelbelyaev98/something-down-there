using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    public sealed class GameMenuView : IDisposable
    {
        private readonly FpsPlayer player;
        private readonly Label title, subtitle, pauseError;
        private readonly VisualElement pausePage, cameraPage, contentPage, pauseActions, actions, startupPage, startupActions;
        private readonly Label startupNote;
        private readonly ScrollView scroll;
        private readonly VisualElement tradeSummary;
        private readonly List<VisualElement> navigation = new List<VisualElement>();
        private readonly ToolkitCameraSettings camera;
        private readonly ToolkitInputSettings input;
        private readonly VisualElement inputPage;
        private readonly VisualElement devicePage, settingsNavigation;
        private readonly ToolkitDeviceSettings device;
        private readonly Button settingsBack;
        private readonly ToolkitDialog dialog;
        private Label displayCountdown;
        private bool displayingPreview;
        private VisualElement openDropdown;
        private readonly ToolkitTabs settingsTabs;
        private readonly Label controlMove, controlCrouch, controlSprint, controlDig, controlDigDescription, controlGrab, controlJump, controlInteract, controlPause;
        private PlayerMenu displayed;
        private bool pending = true;
        private WorldSaveController persistence;
        private int generation;
        private string purchasedCard;
        public VisualElement Root { get; }
        public VisualElement CurrentScreen { get; private set; }
        public Focusable Focused => Root.focusController?.focusedElement;

        public GameMenuView(VisualElement document, FpsPlayer player)
        {
            this.player = player;
            Root = document.Q("menuRoot");
            title = Root.Q<Label>("menuTitle");
            subtitle = Root.Q<Label>("menuSubtitle");
            pausePage = Root.Q("pausePage");
            cameraPage = Root.Q("cameraPage");
            contentPage = Root.Q("contentPage");
            startupPage = Root.Q("startupPage");
            startupActions = Root.Q("startupActions");
            startupNote = Root.Q<Label>("startupNote");
            pauseActions = Root.Q("pauseActions");
            pauseError = Root.Q<Label>("pauseSettingsError");
            scroll = Root.Q<ScrollView>("menuScroll");
            tradeSummary = Root.Q("tradeSummary");
            scroll.mouseWheelScrollSize = 42;
            actions = Root.Q("menuActions");
            dialog = new ToolkitDialog(title, subtitle, scroll);
            settingsBack = Root.Q<Button>("settingsBack");
            settingsBack.clicked += player.BackFromSettings;
            camera = new ToolkitCameraSettings(cameraPage, player);
            inputPage = Root.Q("inputPage");
            input = new ToolkitInputSettings(inputPage, player);
            devicePage = Root.Q("devicePage");
            device = new ToolkitDeviceSettings(devicePage, player);
            settingsNavigation = Root.Q("settingsNavigation");
            settingsTabs = new ToolkitTabs(settingsNavigation, index => player.ShowSettingsCategory((SettingsCategory)index));
            controlMove = Root.Q<Label>("controlMove"); controlCrouch = Root.Q<Label>("controlCrouch");
            controlSprint = Root.Q<Label>("controlSprint");
            controlDig = Root.Q<Label>("controlDig"); controlDigDescription = Root.Q<Label>("controlDigDescription");
            controlGrab = Root.Q<Label>("controlGrab");
            controlJump = Root.Q<Label>("controlJump"); controlInteract = Root.Q<Label>("controlInteract"); controlPause = Root.Q<Label>("controlPause");
            Root.RegisterCallback<NavigationSubmitEvent>(e => { if (CapturingInput) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerUpEvent>(e => { if (CapturingInput) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationMoveEvent>(Navigate, TrickleDown.TrickleDown);
            Root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (CapturingInput || IsOwnedKey(e.keyCode)) e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            Root.RegisterCallback<KeyUpEvent>(e =>
            {
                if (CapturingInput || IsOwnedKey(e.keyCode)) e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerDownEvent>(e =>
            {
                Root.RemoveFromClassList("keyboard-navigation");
                if (CapturingInput) { e.StopImmediatePropagation(); return; }
                // Background clicks do not discard the current keyboard control.
                var target = e.target as VisualElement;
                if (target != null && FindNavigable(target) < 0) Root.focusController.IgnoreEvent(e);
            }, TrickleDown.TrickleDown);
            player.MenuChanged += QueueRefresh;
            player.CameraSettings.Changed += CameraChanged;
            player.InputSettings.Changed += ControlsChanged;
            player.GameSettings.Changed += DeviceChanged;
            ControlsChanged();
        }

        private bool CapturingInput => player.BindingCapture.BlocksInput;

        private void ControlsChanged()
        {
            var settings = player.InputSettings;
            controlMove.text = settings.Display(PlayerBinding.Forward) + " / " + settings.Display(PlayerBinding.Backward)
                + " / " + settings.Display(PlayerBinding.Left) + " / " + settings.Display(PlayerBinding.Right);
            controlCrouch.text = settings.Display(PlayerBinding.Crouch);
            controlSprint.text = settings.Display(PlayerBinding.Sprint);
            controlDig.text = settings.Display(PlayerBinding.Dig);
            controlDigDescription.text = settings.ToggleDig ? "Toggle dig / collect / throw" : "Dig / collect / throw";
            controlGrab.text = settings.Display(PlayerBinding.Grab);
            controlJump.text = settings.Display(PlayerBinding.Jump);
            controlInteract.text = settings.Display(PlayerBinding.Interact) + " / " + settings.Display(PlayerBinding.Inventory);
            controlPause.text = settings.Display(PlayerBinding.Pause);
            Show(Root.Q("pauseInputError"), settings.WriteFailed);
        }

        public void Tick()
        {
            openDropdown = FindDropdown();
            if (openDropdown != null) openDropdown.name = "menuDropdown";
            player.GameSettings.Tick(Time.realtimeSinceStartupAsDouble);
            settingsNavigation.SetEnabled(player.BindingCapture.State == BindingCaptureState.Idle && !player.GameSettings.PreviewingDisplay);
            settingsBack.SetEnabled(player.BindingCapture.State == BindingCaptureState.Idle);
            if (displayingPreview != player.GameSettings.PreviewingDisplay) pending = true;
            if (displayingPreview && displayCountdown != null) displayCountdown.text = "Reverting in " + player.GameSettings.SecondsRemaining + "s";
            if (persistence != player.Persistence)
            {
                if (persistence != null) persistence.Changed -= SaveChanged;
                persistence = player.Persistence;
                if (persistence != null) persistence.Changed += SaveChanged;
                pending = true;
            }
            if (pending)
            {
                Rebuild();
                purchasedCard = null;
            }
            // Losing window focus (alt-tab, a screenshot tool) still pauses the game,
            // but the dim overlay and the pause card are not drawn while focus is gone,
            // so a screenshot taken from another window captures the game itself. The
            // menu is there again the moment the window comes back.
            Root.EnableInClassList("focus-pause", !player.HasGameplayFocus && player.Menu == PlayerMenu.Pause);
            Show(title, !string.IsNullOrEmpty(title.text));
            Show(subtitle, !string.IsNullOrEmpty(subtitle.text));
        }

        private void QueueRefresh() => pending = true;
        private VisualElement FindDropdown() => Root.panel?.visualTree.Q(className: GenericDropdownMenu.ussClassName);
        public bool DismissDropdown()
        {
            // Retain this frame's open state even if Toolkit already consumed the
            // raw Escape event; the same key must not also leave Settings.
            var popup = FindDropdown() ?? openDropdown;
            openDropdown = null;
            if (popup == null) return false;
            if (popup.panel != null)
            {
                var content = popup.Q<ScrollView>()?.contentContainer;
                content?.Focus();
                using (var cancel = NavigationCancelEvent.GetPooled()) content?.SendEvent(cancel);
            }
            return true;
        }
        // Native players also send raw key events to sliders. Arrows are handled by
        // Input System navigation once; Escape/Space belong to the player barrier.
        private static bool IsOwnedKey(KeyCode key) => key == KeyCode.Escape || key == KeyCode.Space
            || key == KeyCode.LeftArrow || key == KeyCode.RightArrow || key == KeyCode.UpArrow || key == KeyCode.DownArrow;
        private void SaveChanged() { if (player.Menu == PlayerMenu.Persistence || player.Menu == PlayerMenu.MainMenu) pending = true; }
        private void CameraChanged() => Show(pauseError, player.CameraSettings.WriteFailed);
        private void DeviceChanged() => Show(Root.Q("pauseDeviceError"), player.GameSettings.WriteFailed);

        private void Rebuild()
        {
            pending = false;
            generation++;
            bool returningFromPreview = displayingPreview && !player.GameSettings.PreviewingDisplay;
            displayingPreview = player.GameSettings.PreviewingDisplay;
            bool cameraBack = displayed == PlayerMenu.CameraComfort;
            bool inputBack = displayed == PlayerMenu.InputSettings;
            bool deviceBack = displayed == PlayerMenu.DeviceSettings;
            displayed = player.Menu;
            navigation.Clear();
            Show(Root, player.IsMenuOpen);
            bool settingsVisible = player.IsSettingsOpen && !displayingPreview;
            bool isDialog = displayingPreview || displayed == PlayerMenu.ConfirmNewGame || displayed == PlayerMenu.ConfirmTerrainReset || displayed == PlayerMenu.Persistence;
            Root.EnableInClassList("dialog-menu", isDialog);
            Show(settingsBack, settingsVisible);
            Show(Root.Q("settingsFooter"), settingsVisible);
            Show(Root.Q("cameraReset"), settingsVisible && displayed == PlayerMenu.CameraComfort);
            Show(Root.Q("inputReset"), settingsVisible && displayed == PlayerMenu.InputSettings);
            Show(Root.Q("deviceReset"), settingsVisible && displayed == PlayerMenu.DeviceSettings);
            Show(pausePage, displayed == PlayerMenu.Pause);
            Show(cameraPage, displayed == PlayerMenu.CameraComfort && !displayingPreview);
            Show(inputPage, displayed == PlayerMenu.InputSettings && !displayingPreview);
            Show(devicePage, displayed == PlayerMenu.DeviceSettings && !displayingPreview);
            Show(settingsNavigation, settingsVisible);
            settingsTabs.Select((int)player.SettingsCategory);
            Show(startupPage, displayed == PlayerMenu.MainMenu);
            Show(contentPage, displayingPreview || (displayed != PlayerMenu.None && displayed != PlayerMenu.Pause && displayed != PlayerMenu.MainMenu && !player.IsSettingsOpen));
            Root.EnableInClassList("title-screen", displayed == PlayerMenu.MainMenu);
            bool stationShop = displayed == PlayerMenu.Station && player.Station is ComputerStation;
            Root.EnableInClassList("shop-menu", stationShop);
            Root.EnableInClassList("workshop-menu", displayed == PlayerMenu.Station && player.Station is ComputerStation { Selling: false });
            Root.EnableInClassList("station-menu", stationShop);
            Root.EnableInClassList("settings-menu", settingsVisible);
            Root.EnableInClassList("startup-menu", player.Persistence != null && (player.Persistence.AwaitingGameChoice
                || player.Persistence.State == WorldSaveState.Creating || player.Persistence.State == WorldSaveState.NewGameFailed));
            if (!player.IsMenuOpen) { CurrentScreen = null; return; }
            if (displayingPreview)
            {
                CurrentScreen = contentPage; actions.Clear(); tradeSummary.Clear(); Show(tradeSummary, false);
                displayCountdown = dialog.Set("Keep display changes?", "Reverting in " + player.GameSettings.SecondsRemaining + "s");
                displayCountdown.name = "displayCountdown";
                Button(actions, "displayRevert", player.GameSettings.RevertDisplay, true, "first-action", "Revert");
                Button(actions, "displayKeep", () => player.GameSettings.KeepDisplay(Time.realtimeSinceStartupAsDouble), true, "primary", "Keep changes");
                Show(actions, true); FocusAfterLayout(navigation[0]); return;
            }
            if (displayed == PlayerMenu.MainMenu)
            {
                CurrentScreen = startupPage;
                title.text = "SOMETHING\nDOWN THERE";
                subtitle.text = "";
                startupActions.Clear();
                var save = player.Persistence;
                Button(startupActions, "Continue", save.LoadGame, save.HasSavedGame);
                Button(startupActions, "New Game", save.RequestNewGame);
                var settings = Button(startupActions, "Settings", player.ShowSettings);
                Button(startupActions, "Quit", save.RequestExit, true, "quiet");
                startupNote.text = save.HasSavedGame ? "" : "No saved game";
                Show(startupNote, !save.HasSavedGame);
                FocusAfterLayout(cameraBack || inputBack || deviceBack ? settings : navigation[0]);
                return;
            }
            if (displayed == PlayerMenu.DeviceSettings)
            {
                CurrentScreen = devicePage;
                title.text = "Settings"; subtitle.text = "";
                device.Show(player.SettingsCategory, returningFromPreview);
                AddSettingsNavigation(); device.AddNavigation(navigation); navigation.Add(settingsBack);
                FocusAfterLayout(device.First);
                return;
            }
            if (displayed == PlayerMenu.CameraComfort)
            {
                CurrentScreen = cameraPage;
                title.text = "Settings";
                subtitle.text = "";
                AddSettingsNavigation();
                camera.AddNavigation(navigation);
                navigation.Add(settingsBack);
                FocusAfterLayout(camera.Slider);
                return;
            }
            if (displayed == PlayerMenu.InputSettings)
            {
                CurrentScreen = inputPage;
                title.text = "Settings";
                subtitle.text = "";
                AddSettingsNavigation();
                input.AddNavigation(navigation);
                navigation.Add(settingsBack);
                FocusAfterLayout(input.First);
                return;
            }
            if (displayed == PlayerMenu.Pause)
            {
                CurrentScreen = pausePage;
                title.text = "Paused";
                subtitle.text = "";
                pauseActions.Clear();
                Button(pauseActions, "Resume", player.CloseMenu);
                var comfort = Button(pauseActions, "Settings", player.ShowSettings);
                if (player.Persistence != null) Button(pauseActions, "Save and quit", player.Persistence.RequestExit, true, "quiet");
                if (player.AdminAvailable) Button(pauseActions, "Developer admin", player.ShowAdminMenu);
                CameraChanged();
                DeviceChanged();
                FocusAfterLayout(inputBack || cameraBack || deviceBack ? comfort : navigation[0]);
                return;
            }
            CurrentScreen = contentPage;
            scroll.Clear();
            tradeSummary.Clear();
            Show(tradeSummary, false);
            scroll.scrollOffset = Vector2.zero;
            actions.Clear();
            subtitle.text = "";
            if (displayed == PlayerMenu.Persistence) BuildSave();
            else if (displayed == PlayerMenu.ConfirmNewGame) BuildNewGameConfirmation();
            else if (displayed == PlayerMenu.DeveloperAdmin) BuildAdmin();
            else if (displayed == PlayerMenu.ConfirmTerrainReset) BuildTerrainReset();
            else if (displayed == PlayerMenu.Station && player.Station is ComputerStation { Selling: true } sell) BuildSale(sell);
            else if (displayed == PlayerMenu.Station && player.Station is ComputerStation upgrade) BuildUpgrade(upgrade);
            else BuildInventory();
            if (actions.childCount > 0) actions[0].AddToClassList("first-action");
            Show(actions, actions.childCount > 0);
            if (navigation.Count > 0)
            {
                // A purchase keeps focus on the card the player just used so
                // consecutive upgrades stay fluid; every other entry lands on the
                // safe action instead of on a spending target.
                VisualElement selected = null;
                if (displayed == PlayerMenu.Station && player.Station is ComputerStation)
                {
                    if (!string.IsNullOrEmpty(purchasedCard))
                    {
                        var bought = Root.Q<Button>(purchasedCard);
                        // A price that just became unaffordable is disabled, and disabled
                        // buttons cannot take focus, so fall back to the first live action.
                        if (bought != null && bought.enabledSelf) selected = bought;
                    }
                }
                if (selected == null) selected = navigation[0];
                FocusAfterLayout(selected);
            }
        }

        private void BuildInventory()
        {
            title.text = displayed == PlayerMenu.Inventory ? "Inventory" : player.Station?.Title ?? "Station unavailable";
            subtitle.text = $"Carried finds: {player.Inventory.Count} / {player.Inventory.Capacity}";
            if (displayed == PlayerMenu.Station && player.Station != null) Text(scroll, "Body", player.Station.Description(player), "body");
            if (player.Inventory.Count == 0) Text(scroll, "Empty bag", "No carried finds", "empty");
            foreach (var item in player.Inventory.Items)
            {
                var row = Element(scroll, "item-row");
                Text(row, "Find name", item.DisplayName, "item-name");
                Text(row, "Sale value", "$" + item.SaleValue, "item-value");
            }
            if (displayed == PlayerMenu.Station && player.Station != null)
                for (int i = 0; i < player.Station.CommandCount; i++)
                {
                    int command = i;
                    long revision = player.StationRevision;
                    Button(actions, player.Station.CommandLabel(i, player), () => player.ExecuteStationCommand(command, revision), player.Station.CanExecute(i, player));
                }
            Button(actions, "Close", player.CloseMenu, true, "primary");
        }

        private void BuildNewGameConfirmation()
        {
            dialog.Set("Start a new game?", "Your excavation, finds and upgrades will be reset.");
            Button(actions, "Cancel", player.Persistence.CancelNewGame);
            Button(actions, "Start New Game", player.Persistence.ConfirmNewGame, true, "primary");
        }

        private void BuildSale(ComputerStation station)
        {
            title.text = "";
            subtitle.text = "";
            Show(tradeSummary, true);
            BuildStationHead();
            long revision = player.StationRevision;
            var table = Element(scroll, "station-table");
            if (station.Items.Count == 0) Text(table, "Empty bag", "Your bag is empty", "station-empty");
            for (int i = 0; i < station.Items.Count; i++)
            {
                int command = ComputerStation.SellAllCommand + i + 1;
                var item = station.Items[i];
                var row = ToolkitStationRows.Block(table, "Sell " + item.InstanceId + " row", "station-sell-row");
                ToolkitStationRows.Text(row, "Find name", item.DisplayName, "station-sell-name");
                StationButton(row, "Sell " + item.InstanceId, $"Sell  +${item.SaleValue}",
                    () => player.ExecuteStationCommand(command, revision), "station-sell-value", station.CanExecute(command, player));
            }
            var sellAll = Button(actions, "Sell all", () => player.ExecuteStationCommand(ComputerStation.SellAllCommand, revision),
                station.CanExecute(ComputerStation.SellAllCommand, player), "sell-all", station.CommandLabel(ComputerStation.SellAllCommand, player));
            // Styled inline: the shared menu-button rules outrank class selectors here.
            sellAll.style.backgroundImage = StyleKeyword.None;
            sellAll.style.backgroundColor = new Color(0.561f, 0.682f, 0.290f, 1f);
            sellAll.style.color = Color.white;
        }

        private void BuildUpgrade(ComputerStation station)
        {
            title.text = "";
            subtitle.text = "";
            Show(tradeSummary, true);
            BuildStationHead();
            long revision = player.StationRevision;
            // Categories live in their own column: equipment tracks left, services and
            // consumables right, each with its own heading.
            var columns = Element(scroll, "station-columns");
            var upgrades = Element(columns, "station-column");
            Text(upgrades, "Upgrades heading", "UPGRADES", "station-column-heading");
            for (int i = 0; i < ComputerStation.RefillCommand; i++) BuildUpgradeRow(upgrades, station, i, revision);
            var services = Element(columns, "station-column station-column-divided");
            Text(services, "Services heading", "SERVICES", "station-column-heading");
            for (int i = ComputerStation.RefillCommand; i < station.CommandCount; i++) BuildUpgradeRow(services, station, i, revision);
        }

        // Header plate: money only. Close is ESC/B, and the machine itself says what
        // the menu is, so no title is printed.
        private void BuildStationHead()
        {
            var bar = Element(tradeSummary, "station-bar-head");
            var wallet = Element(bar, "station-money");
            ToolkitStationRows.Text(wallet, "Money caption", "Money:", "station-money-caption");
            Text(wallet, "Trade balance", $"${player.Wallet.Balance}", "trade-balance");
        }

        private void BuildUpgradeRow(VisualElement parent, ComputerStation station, int index, long revision)
        {
            bool refill = index == ComputerStation.RefillCommand;
            var offer = station.OfferAt(index);
            string track = refill ? "Refill fuel" : EquipmentProgression.Name(offer.Kind);
            // The row is decoration; only the price button is interactive.
            var row = ToolkitStationRows.Block(parent, "Upgrade " + track + " row", refill ? "station-row service" : "station-row");
            var main = ToolkitStationRows.Block(row, track + " main", "station-row-main");
            string caption = refill ? track : $"{track}  {offer.OwnedLevel}/{offer.LevelCount}";
            ToolkitStationRows.Text(main, track + " name", caption, "station-cell-name");
            bool shortfall = false;
            bool maxed = false;
            string price;
            if (refill)
            {
                var service = station.Refill;
                if (service.Full) { price = "FULL"; maxed = true; }
                else if (service.Cost == 0) { price = "$1"; shortfall = true; }
                else { price = $"${service.Cost:0}"; }
            }
            else if (offer.Complete) { price = "MAX"; maxed = true; }
            else { price = $"${offer.Cost}"; shortfall = player.Wallet.Balance < offer.Cost; }
            // One activation of this button buys. Out of reach or finished means the
            // button is disabled outright, so it never looks buyable when it is not.
            bool canBuy = !maxed && !shortfall;
            Button buy = null;
            buy = StationButton(row, "Upgrade " + track, price,
                () => ActivateUpgradeRow(station, index, revision, buy), "station-price", canBuy);
            buy.EnableInClassList("short", shortfall);
            buy.EnableInClassList("maxed", maxed);
            // Under the name: progress, then the number this purchase changes. Services
            // leave the progress cell empty so both columns still line up.
            var bottom = ToolkitStationRows.Block(main, track + " bottom", "station-cell-bottom");
            var progress = ToolkitStationRows.Block(bottom, track + " bar slot", "station-bar-slot");
            if (!refill) ToolkitStationRows.Segments(progress, track + " pips", offer.OwnedLevel, offer.LevelCount);
            ToolkitStationRows.Text(bottom, track + " effect", refill ? RefillHeadline(station) : UpgradeHeadline(offer),
                "station-cell-effect");
            // Everything else the purchase changes stays one hover away instead of
            // adding another column or sentence to the table.
            buy.tooltip = refill ? RefillDetail(station) : UpgradeDetail(offer);
            if (buy.name == purchasedCard)
            {
                buy.schedule.Execute(() => buy.AddToClassList("just-bought"));
                buy.schedule.Execute(() => buy.RemoveFromClassList("just-bought")).StartingIn(900);
            }
        }

        // The headline carries the number the purchase changes; the detail line keeps
        // every other stat visible without another interaction.
        private string UpgradeHeadline(StationTrade.UpgradeOffer offer)
        {
            if (offer.Kind == EquipmentKind.Shovel)
            {
                var current = player.Shovel.Current;
                var next = player.Shovel.GetProfile(offer.Complete ? offer.OwnedLevel : offer.NextLevel);
                string motion = Compared(EquipmentProgression.ToolName(offer.OwnedLevel),
                    EquipmentProgression.ToolName(offer.Complete ? offer.OwnedLevel : offer.NextLevel), offer.Complete);
                return motion + "  |  " + Compared($"{current.Radius * 2:F2} m", $"{next.Radius * 2:F2} m", offer.Complete);
            }
            if (offer.Kind == EquipmentKind.Inventory)
                return Compared($"{player.Inventory.Capacity}",
                    $"{player.Inventory.Capacity + (offer.Complete ? 0 : EquipmentProgression.InventoryIncrease(offer.OwnedLevel))}", offer.Complete);
            return Compared($"{player.Battery.Capacity:0.#}",
                $"{player.Battery.Capacity + (offer.Complete ? 0 : EquipmentProgression.FuelIncrease(offer.OwnedLevel)):0.#}", offer.Complete);
        }

        private string UpgradeDetail(StationTrade.UpgradeOffer offer)
        {
            if (offer.Kind == EquipmentKind.Shovel)
            {
                int level = offer.Complete ? offer.OwnedLevel : offer.NextLevel;
                string detail = "Reach " + Compared($"{player.DigReachAtLevel(offer.OwnedLevel):F1}",
                    $"{player.DigReachAtLevel(level):F1}", offer.Complete) + " m  |  Stroke "
                    + Compared($"{player.DigIntervalAtLevel(offer.OwnedLevel):F2}",
                        $"{player.DigIntervalAtLevel(level):F2}", offer.Complete) + " s";
                detail += offer.OwnedLevel < EquipmentProgression.DrillLevel
                    ? $"  |  Drill motion at level {EquipmentProgression.DrillLevel}" : "  |  Continuous drill cutting";
                return player.HasAdminOverrides ? detail + "  |  DEVELOPER OVERRIDES ACTIVE" : detail;
            }
            return offer.Kind == EquipmentKind.Fuel ? "Refill sold separately" : "";
        }

        private static string RefillDetail(ComputerStation station) =>
            $"$1 per {EquipmentProgression.FuelPerCredit:0.#} fuel, rounded up";

        private string RefillHeadline(ComputerStation station)
        {
            var refill = station.Refill;
            return refill.Full || refill.Amount <= 0
                ? $"{player.Battery.Charge:0.#} / {player.Battery.Capacity:0.#}"
                : $"+{refill.Amount:0.#} \u2192 {refill.ChargeAfter:0.#}";
        }

        private void ActivateUpgradeRow(ComputerStation station, int index, long revision, Button row)
        {
            if (row == null || player.Station != station) return;
            if (!station.CanExecute(index, player))
            {
                RefuseUpgradeRow(row);
                return;
            }
            purchasedCard = row.name;
            if (!player.ExecuteStationCommand(index, revision)) purchasedCard = null;
        }

        // An activation that cannot spend never reaches the trade path; the button
        // pulses so the refusal is visible without adding any text.
        private static void RefuseUpgradeRow(Button button)
        {
            button.AddToClassList("denied");
            button.schedule.Execute(() => button.RemoveFromClassList("denied")).StartingIn(320);
        }

        private static string Compared(string current, string next, bool complete) =>
            complete || current == next ? current : $"{current} \u2192 {next}";

        private Button StationButton(VisualElement parent, string name, string caption, Action action, string className, bool enabled)
        {
            var button = ToolkitStationRows.Button(parent, name, caption, action, className, enabled);
            if (enabled) navigation.Add(button);
            button.RegisterCallback<FocusInEvent>(_ => scroll.ScrollTo(button));
            return button;
        }

        private void BuildTerrainReset()
        {
            dialog.Set("Reset the excavation?", "All dug ground is lost. Inventory and upgrades stay.");
            Button(actions, "Keep excavation", player.CancelTerrainReset);
            Button(actions, "Reset ground", () => player.ConfirmTerrainReset(), true, "primary");
        }

        private void BuildAdmin()
        {
            title.text = "Developer admin";
            subtitle.text = "Session overrides";
            Text(scroll, "Body", $"Tool {player.EffectiveShovelLevel}/{player.Shovel.LevelCount}  |  {player.EffectiveDigReach:F1} m reach  |  {player.EffectiveShovel.Radius * 2:F2} m cut width\n"
                + $"This site: {player.SuccessfulStrokes} strokes, {player.ExcavatedVolume:F1} m³ removed."
                + DensityLine(), "body");
            var grid = Element(scroll, "admin-actions");
            Button(grid, "Motion: " + player.AdminMotionLabel, player.ToggleAdminShaving);
            for (int i = 1; i <= player.Shovel.LevelCount; i++)
            {
                int level = i;
                Button(grid, $"{(i == player.EffectiveShovelLevel ? "Selected: " : "")}{EquipmentProgression.ToolName(i)} {i} / {player.DigReachAtLevel(i):F1} m reach", () => player.SelectAdminLevel(level));
            }
            var tuningNote = Text(scroll, "Tool tuning", TuningNote(), "body");
            var tuningRows = new ToolkitSettingsRows(scroll, scroll);
            int tunedLevel = player.EffectiveShovelLevel;
            Label tuningTable = null;
            Action dialled = () =>
            {
                tuningNote.text = TuningNote();
                if (tuningTable != null) tuningTable.text = player.AdminTuningSummary();
            };
            var bite = tuningRows.Slider("adminBite", "Bite radius", 125, 2000,
                () => Mathf.RoundToInt(player.AdminTuningValue(tunedLevel, FpsPlayer.TuningDial.Bite) * 1000f),
                value => { player.SetAdminTuning(FpsPlayer.TuningDial.Bite, value / 1000f); tuningRows.Refresh(); dialled(); },
                value => (value / 1000f).ToString("0.000") + " m", valueName: "adminBiteValue");
            var cadence = tuningRows.Slider("adminCadence", "Bite speed (lower is faster)", 10, 400,
                () => Mathf.RoundToInt(player.AdminTuningValue(tunedLevel, FpsPlayer.TuningDial.Cadence) * 100f),
                value => { player.SetAdminTuning(FpsPlayer.TuningDial.Cadence, value / 100f); tuningRows.Refresh(); dialled(); },
                value => (value / 100f).ToString("0.00") + "x", valueName: "adminCadenceValue");
            var reach = tuningRows.Slider("adminReach", "Dig reach bonus", 0, 800,
                () => Mathf.RoundToInt(player.AdminTuningValue(tunedLevel, FpsPlayer.TuningDial.Reach) * 100f),
                value => { player.SetAdminTuning(FpsPlayer.TuningDial.Reach, value / 100f); tuningRows.Refresh(); dialled(); },
                value => (value / 100f).ToString("0.00") + " m  (max " + FpsPlayer.MaximumDigReach + " m)", valueName: "adminReachValue");
            // The settings rows only paint their handles and value labels from Refresh();
            // without it every slider opens at zero instead of the selected shovel.
            tuningRows.Refresh();
            navigation.Add(bite); navigation.Add(cadence); navigation.Add(reach);
            var tuningActions = Element(scroll, "admin-actions");
            Button(tuningActions, "Print tool tuning", player.PrintAdminTuning);
            Button(tuningActions, "Reset tool tuning", player.ResetAdminTuning, player.HasAdminTuning);
            tuningTable = Text(scroll, "Tool tuning table", player.AdminTuningSummary(), "body");
            Button(grid, "Refill battery", player.RefillAdminBattery);
            Button(grid, "Return to surface", player.AdminReturnToSurface);
            Button(grid, "Reset ground...", player.RequestTerrainReset);
            Button(grid, "Unlimited battery: " + (player.UnlimitedBattery ? "ON" : "OFF"), player.ToggleAdminUnlimitedBattery);
            Button(grid, "X-ray: " + (player.AdminXray ? "ON" : "OFF"), player.ToggleAdminXray, player.Discoveries != null);
            Button(grid, "Add $500", player.GrantAdminMoney);
            Button(grid, "Restore normal rules", player.RestoreAdminOverrides, player.HasAdminOverrides);
            Button(actions, "Resume digging", player.CloseMenu, true, "primary");
        }

        // Every shovel keeps its own calibrated values; switching levels shows that
        // level's numbers again. Printing sends them to Logs/tuning.txt for the source.
        private string TuningNote() => (player.HasAdminTuning
            ? "Tool tuning - session override active. " : "Tool tuning - authored ladder. ")
            + "Drag for the selected level; each shovel keeps its own values. "
            + "Print writes tuning.txt beside your saves to paste into EquipmentProgression.ToolProfiles().";

        // Tuning instrument for buried-find density: reads the live population, saves nothing.
        private string DensityLine()
        {
            var discoveries = player.Discoveries;
            if (discoveries == null || discoveries.Finds.Count == 0) return "";
            int exposed = 0, collected = 0;
            foreach (var find in discoveries.Finds)
            {
                if (find.Collected) collected++;
                else if (find.Exposure > 0f) exposed++;
            }
            float volume = player.ExcavatedVolume;
            string rate = volume >= .5f ? $"  |  {(exposed + collected) / volume:F3} per m³ dug" : "";
            return $"\nFinds: {exposed} exposed, {collected} collected of {discoveries.Finds.Count}{rate}";
        }

        private void BuildSave()
        {
            var save = player.Persistence;
            if (save == null) return;
            if (save.ProfileInUse && (save.State == WorldSaveState.NewGameFailed || save.State == WorldSaveState.LoadFailed))
            {
                title.text = "Saved game already open";
                Text(scroll, "Body", save.ErrorDetail, "body");
                Button(actions, "Retry", save.Retry, true, "primary");
                Button(actions, "Back to menu", save.RefreshStartup);
                Button(actions, "Quit", save.RequestExit, true, "quiet");
                return;
            }
            if (save.State == WorldSaveState.Creating)
            {
                title.text = "Starting a new excavation";
                Text(scroll, "Body", "Preparing your world...", "body");
                return;
            }
            if (save.State == WorldSaveState.NewGameFailed)
            {
                title.text = "Cannot start a new game";
                Text(scroll, "Body", "Your previous save files have been kept.\n\n" + save.ErrorDetail, "body");
                Button(actions, "Retry", save.Retry, true, "primary");
                Button(actions, "Back to menu", save.RefreshStartup);
                Button(actions, "Quit", save.RequestExit, true, "quiet");
                return;
            }
            title.text = save.ExitRequested ? "Saving your excavation" : save.State == WorldSaveState.Loading ? "Loading your excavation"
                : save.State == WorldSaveState.Recovery ? "Excavation recovered" : save.State == WorldSaveState.ConfirmQuit ? "Leave without saving?"
                : save.State == WorldSaveState.LoadFailed ? "Cannot load this excavation" : "Progress could not be saved";
            if (save.ExitRequested || save.State == WorldSaveState.Loading)
            {
                Text(scroll, "Body", save.ExitRequested ? "Saving before closing..." : "Restoring your excavation...", "body");
                return;
            }
            if (save.State == WorldSaveState.Recovery)
            {
                Text(scroll, "Body", "Restored the last complete checkpoint.\n" + save.LastSavedLabel
                    + "\nLater changes may be missing. Damaged file kept.", "body");
                Button(actions, "Continue recovered excavation", save.AcceptRecovery, true, "primary");
            }
            else if (save.State == WorldSaveState.ConfirmQuit)
            {
                Text(scroll, "Body", "Your last complete checkpoint will be kept. Changes since then will be lost.\n\n" + save.LastSavedLabel, "body");
                Button(actions, "Back", save.CancelUnsavedExit);
                Button(actions, "Quit without saving", save.ConfirmUnsavedExit, true, "primary");
                return;
            }
            else
            {
                Text(scroll, "Body", (save.State == WorldSaveState.LoadFailed ? "Your save files have been kept. No new excavation has been started."
                    : "Your excavation is still here. Keep this game open to retry saving.")
                    + "\n\n" + save.LastSavedLabel + "\n\n" + save.ErrorDetail, "body");
                Button(actions, save.State == WorldSaveState.LoadFailed ? "Retry loading" : "Retry saving", save.Retry, true, "primary");
            }
            Button(actions, save.State == WorldSaveState.WriteFailed ? "Quit..." : "Quit", save.RequestExit, true, "quiet");
        }

        private Button Button(VisualElement parent, string name, Action action, bool enabled = true, string style = "", string caption = null)
        {
            var button = ToolkitMenuComponents.Button(parent, name, caption ?? name, action, style, enabled);
            if (enabled) navigation.Add(button);
            button.RegisterCallback<FocusInEvent>(_ => { if (scroll.Contains(button)) scroll.ScrollTo(button); });
            return button;
        }

        private static void Stat(VisualElement parent, string name, string current, string next, bool complete)
        {
            var row = Element(parent, "stat-row");
            Text(row, name, name, "stat-name");
            Text(row, name + " value", current, "stat-value");
            if (!complete) Text(row, name + " next", next, "stat-value next-value");
        }

        private static VisualElement Element(VisualElement parent, string style)
        {
            var element = new VisualElement();
            foreach (string className in style.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) element.AddToClassList(className);
            parent.Add(element);
            return element;
        }

        private static Label Text(VisualElement parent, string name, string text, string style)
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore, enableRichText = false };
            foreach (string className in style.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) label.AddToClassList(className);
            Show(label, !string.IsNullOrEmpty(text));
            parent.Add(label);
            return label;
        }

        internal static void Show(VisualElement element, bool visible) => element.EnableInClassList("hidden", !visible);

        private void FocusAfterLayout(VisualElement element)
        {
            int expected = generation;
            element.Focus();
            Root.schedule.Execute(() => { if (generation == expected && player.IsMenuOpen) element.Focus(); });
        }

        private int FindNavigable(VisualElement element)
        {
            for (int i = 0; i < navigation.Count; i++)
                if (navigation[i] == element || navigation[i].Contains(element)) return i;
            return -1;
        }

        private void Navigate(NavigationMoveEvent e)
        {
            if (!player.IsMenuOpen) return;
            Root.AddToClassList("keyboard-navigation");
            // NavigationMoveEvent changes focus in PostDispatch even when propagation
            // was stopped. Our explicit order is the sole focus movement for this event.
            Root.focusController.IgnoreEvent(e);
            if (CapturingInput) { e.StopImmediatePropagation(); return; }
            if (e.direction == NavigationMoveEvent.Direction.None) { e.StopImmediatePropagation(); return; }
            if (displayed == PlayerMenu.CameraComfort)
            {
                navigation.Clear();
                AddSettingsNavigation();
                camera.AddNavigation(navigation);
                navigation.Add(settingsBack);
                if (camera.Adjust(Focused as VisualElement, e.direction)) { e.StopImmediatePropagation(); return; }
            }
            if (displayed == PlayerMenu.InputSettings)
            {
                navigation.Clear(); AddSettingsNavigation(); input.AddNavigation(navigation); navigation.Add(settingsBack);
                if (input.Adjust(Focused as VisualElement, e.direction)) { e.StopImmediatePropagation(); return; }
            }
            if (displayed == PlayerMenu.DeviceSettings && !displayingPreview)
            {
                navigation.Clear(); AddSettingsNavigation(); device.AddNavigation(navigation); navigation.Add(settingsBack);
                if (device.Adjust(Focused as VisualElement, e.direction)) { e.StopImmediatePropagation(); return; }
            }
            int direction = e.direction == NavigationMoveEvent.Direction.Up || e.direction == NavigationMoveEvent.Direction.Left
                || e.direction == NavigationMoveEvent.Direction.Previous ? -1 : 1;
            if (navigation.Count == 0) return;
            int index = FindNavigable(Focused as VisualElement);
            index = Mathf.Clamp(index + direction, 0, navigation.Count - 1);
            navigation[index].Focus();
            e.StopImmediatePropagation();
        }

        public void Dispose()
        {
            generation++;
            player.MenuChanged -= QueueRefresh;
            if (persistence != null) persistence.Changed -= SaveChanged;
            player.CameraSettings.Changed -= CameraChanged;
            camera.Dispose();
            player.InputSettings.Changed -= ControlsChanged;
            input.Dispose();
            player.GameSettings.Changed -= DeviceChanged;
            device.Dispose();
        }

        private void AddSettingsNavigation()
        {
            if (player.BindingCapture.State != BindingCaptureState.Idle || player.GameSettings.PreviewingDisplay) return;
            settingsTabs.AddNavigation(navigation);
        }
    }
}
