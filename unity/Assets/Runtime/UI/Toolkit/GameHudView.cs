using UnityEngine;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    public sealed class GameHudView
    {
        private readonly FpsPlayer player;
        private readonly Label reticle, status, walletStatus, lampStatus, prompt, feedback, adminHint, returnWarning, fuelWarning, inventoryWarning;
        private readonly VisualElement batteryGroup, batteryFill, bagGroup, lampGroup;
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
            lampStatus = Root.Q<Label>("Lamps");
            bagGroup = Root.Q("BagGroup");
            lampGroup = Root.Q("LampGroup");
            prompt = Root.Q<Label>("Target");
            feedback = Root.Q<Label>("Feedback");
            adminHint = Root.Q<Label>("Developer controls");
            returnWarning = Root.Q<Label>("Return warning");
            fuelWarning = Root.Q<Label>("Fuel warning");
            inventoryWarning = Root.Q<Label>("Inventory warning");
            batteryGroup = Root.Q("batteryGroup");
            batteryFill = Root.Q("Charge");
            HudIcons.Money(Root.Q("MoneyIcon")); HudIcons.Bag(Root.Q("BagIcon"));
            HudIcons.Lamp(Root.Q("LampIcon")); HudIcons.Bolt(Root.Q("BatteryIcon"));
            Root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }

        private (int, int, int) shownStatus = (-1, -1, -1);
        private (int, int, bool) shownLamps = (-1, -1, false);
        private (bool, bool, bool, bool, bool) shownAdmin = (true, true, true, true, true);

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
                status.text = statusKey.Item1 + "/" + statusKey.Item2;
                walletStatus.text = statusKey.Item3.ToString();
                bagGroup.EnableInClassList("full", player.Inventory.IsFull);
            }
            if (player.GameplayActive || displayedBattery != player.Battery)
            {
                UpdateBattery();
                displayedBattery = player.Battery;
            }
            GameMenuView.Show(inventoryWarning, player.Inventory.IsFull);
            Root.EnableInClassList("stacked-warnings", player.Inventory.IsFull && !fuelWarning.ClassListContains("hidden"));
            GameMenuView.Show(lampGroup, player.WorksiteTools != null);
            var lampKey = player.WorksiteTools != null ? (player.WorksiteTools.AvailableLamps, player.LampKit.Owned, player.LampKit.Unlimited) : (-1, -1, false);
            if (lampKey != shownLamps)
            {
                shownLamps = lampKey;
                lampStatus.text = lampKey.Item3 ? "\u221e" : lampKey.Item1 + "/" + lampKey.Item2;
                lampStatus.EnableInClassList("unlimited", lampKey.Item3);
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
            batteryFill.style.height = Length.Percent((unlimited ? 1f : fraction) * 100);
            bool low = !unlimited && risk != ReturnRisk.Safe;
            fuelWarning.text = !low ? "" : fraction <= 0 ? "FUEL EMPTY"
                : risk == ReturnRisk.Critical ? "FUEL CRITICAL" : "LOW FUEL";
            fuelWarning.EnableInClassList("critical", low && risk == ReturnRisk.Critical);
            GameMenuView.Show(fuelWarning, low);
            var recharge = player.SurfaceRecharge;
            returnWarning.text = !unlimited && recharge != null
                && (recharge.IsPlayerInZone || (fraction < 1f && recharge.IsNearby)) ? "FUEL AT COMPUTER" : "";
            GameMenuView.Show(returnWarning, returnWarning.text.Length > 0);
        }

    }
}
