using UnityEngine;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    public sealed class GameHudView
    {
        private readonly FpsPlayer player;
        private readonly Label reticle, status, walletStatus, prompt, feedback, shovelStatus, adminHint, batteryStatus, returnWarning, fuelWarning, inventoryWarning;
        private readonly VisualElement batteryGroup, batteryFill;
        private readonly VisualElement detectorPanel;
        private readonly VisualElement[] detectorBars;
        private Battery displayedBattery;
        private readonly Label fps;
        private int frameSamples;
        private float sampleSeconds;
        public VisualElement Root { get; }

        public GameHudView(VisualElement document, FpsPlayer player)
        {
            this.player = player;
            fps = document.Q<Label>("FpsReadout");
            Root = document.Q("hudRoot");
            reticle = Root.Q<Label>("Reticle");
            detectorPanel = Root.Q("DetectorPanel");
            detectorBars = new[] { Root.Q("DetectorBar1"), Root.Q("DetectorBar2"), Root.Q("DetectorBar3") };
            status = Root.Q<Label>("Status");
            walletStatus = Root.Q<Label>("Wallet");
            prompt = Root.Q<Label>("Target");
            feedback = Root.Q<Label>("Feedback");
            shovelStatus = Root.Q<Label>("Shovel status");
            adminHint = Root.Q<Label>("Developer controls");
            batteryStatus = Root.Q<Label>("Battery status");
            returnWarning = Root.Q<Label>("Return warning");
            fuelWarning = Root.Q<Label>("Fuel warning");
            inventoryWarning = Root.Q<Label>("Inventory warning");
            batteryGroup = Root.Q("batteryGroup");
            batteryFill = Root.Q("Charge");
            Root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }

        private (int, int, int) shownStatus = (-1, -1, -1);
        private (int, int, int, int, int, int, int) shownTool;
        private (bool, bool, bool, bool, bool) shownAdmin = (true, true, true, true, true);
        private float toolRefresh;
        private int shownPercent = -1;
        private string shownBand;

        public void Tick()
        {
            GameMenuView.Show(fps, player.GameSettings.Values.ShowFps);
            if (player.GameSettings.Values.ShowFps)
            {
                sampleSeconds += Time.unscaledDeltaTime; frameSamples++;
                if (sampleSeconds >= 0.5f)
                {
                    fps.text = Mathf.RoundToInt(frameSamples / sampleSeconds) + " FPS";
                    sampleSeconds = 0; frameSamples = 0;
                }
            }
            else { sampleSeconds = 0; frameSamples = 0; fps.text = ""; }
            bool gameplay = !player.IsMenuOpen;
            Root.EnableInClassList("hidden", !gameplay);
            var detector = player.Detector;
            int signalLevel = player.GameplayActive && detector != null ? detector.SignalLevel : 0;
            GameMenuView.Show(detectorPanel, signalLevel > 0);
            for (int i = 0; i < detectorBars.Length; i++)
                detectorBars[i].EnableInClassList("active", i < signalLevel);
            prompt.text = gameplay ? player.TargetPrompt : "";
            feedback.text = player.Feedback;
            // Texts are rebuilt only when what they show changes, so the HUD allocates nothing on an ordinary frame.
            var statusKey = (player.Inventory.Count, player.Inventory.Capacity, player.Wallet.Balance);
            if (statusKey != shownStatus)
            {
                shownStatus = statusKey;
                status.text = "FINDS  " + statusKey.Item1 + " / " + statusKey.Item2;
                walletStatus.text = "$" + statusKey.Item3;
            }
            if (player.GameplayActive || displayedBattery != player.Battery)
            {
                UpdateBattery();
                displayedBattery = player.Battery;
            }
            GameMenuView.Show(inventoryWarning, player.Inventory.IsFull);
            Root.EnableInClassList("stacked-warnings", player.Inventory.IsFull && !fuelWarning.ClassListContains("hidden"));
            shovelStatus.EnableInClassList("hidden", !player.ExcavationAvailable);
            var toolKey = (player.EffectiveShovelLevel, player.Shovel.LevelCount, Mathf.RoundToInt(player.EffectiveShovel.Radius * 200),
                Mathf.RoundToInt(player.EffectiveDigReach * 10), Mathf.RoundToInt(player.DisplayDepth * 10),
                player.WorksiteTools != null ? player.WorksiteTools.AvailableLamps : -1, player.WorksiteTools != null ? player.LampKit.Owned : -1);
            // Key names change only in the settings; a slow refresh picks them up.
            if (toolKey != shownTool || (toolRefresh -= Time.unscaledDeltaTime) <= 0)
            {
                shownTool = toolKey; toolRefresh = 1;
                string text = $"SHOVEL {player.EffectiveShovelLevel} / {player.Shovel.LevelCount}    |    {player.EffectiveShovel.Radius * 2:F2} m cut"
                    + $"\nREACH {player.EffectiveDigReach:F1} m    |    DEPTH {player.DisplayDepth:F1} m";
                if (player.WorksiteTools != null)
                    text += $"\n{player.InputSettings.Display(PlayerBinding.Lamp)}  LAMPS {(player.LampKit.Unlimited ? "UNLIMITED" : $"{player.WorksiteTools.AvailableLamps}/{player.LampKit.Owned}")}"
                        + $"    |    {player.InputSettings.Display(PlayerBinding.Mark)}  MARK";
                shovelStatus.text = text;
            }

            var adminKey = (player.AdminAvailable && gameplay, player.HasAdminOverrides, player.UnlimitedBattery, player.AdminXray, player.AdminGroundXray);
            if (adminKey != shownAdmin)
            {
                shownAdmin = adminKey;
                adminHint.text = !adminKey.Item1 ? ""
                    : "DEVELOPER ADMIN"
                        + (player.HasAdminOverrides ? "  |  Overrides active" : "")
                        + (player.UnlimitedBattery ? "  |  Unlimited battery" : "")
                        + (player.AdminXray ? "  |  X-ray: transparent ground" : "")
                        + (player.AdminGroundXray ? "\nGround X-ray: " + TerrainVolume.XrayLegend : "");
            }
            float pulse = player.CameraSettings.SteadyCrosshair ? 0 : player.DigPulse;
            reticle.style.scale = new Scale(Vector3.one * (1 + pulse * 0.3f));
            reticle.style.color = Color.Lerp(Color.white, new Color(1, 0.82f, 0.35f), pulse);
        }

        private void UpdateBattery()
        {
            float fraction = player.Battery.Charge / player.Battery.Capacity;
            var risk = player.ReturnWarning.Evaluate(player.Battery);
            bool unlimited = player.UnlimitedBattery;
            batteryGroup.EnableInClassList("unlimited", unlimited);
            batteryGroup.EnableInClassList("risky", !unlimited && risk == ReturnRisk.Risky);
            batteryGroup.EnableInClassList("critical", !unlimited && risk == ReturnRisk.Critical);
            string band = unlimited ? "UNLIMITED" : fraction <= 0 ? "EMPTY"
                : risk == ReturnRisk.Critical ? "CRITICAL" : risk == ReturnRisk.Risky ? "RISKY" : "SAFE";
            int percent = Mathf.CeilToInt(100f * player.Battery.Charge / player.Battery.Capacity);
            if (percent != shownPercent || !ReferenceEquals(band, shownBand))
            {
                shownPercent = percent; shownBand = band;
                batteryStatus.text = "BATTERY  " + percent + "%  |  " + band;
            }
            batteryFill.style.width = Length.Percent((unlimited ? 1f : fraction) * 100);
            bool low = !unlimited && risk != ReturnRisk.Safe;
            fuelWarning.text = !low ? "" : fraction <= 0 ? "FUEL EMPTY"
                : risk == ReturnRisk.Critical ? "FUEL CRITICAL" : "LOW FUEL";
            fuelWarning.EnableInClassList("critical", low && risk == ReturnRisk.Critical);
            GameMenuView.Show(fuelWarning, low);
            var recharge = player.SurfaceRecharge;
            returnWarning.text = !unlimited && recharge != null
                && (recharge.IsPlayerInZone || (fraction < 1f && recharge.IsNearby)) ? "FUEL AT COMPUTER" : "";
        }

    }
}
