using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    public sealed class GameHudView
    {
        private readonly FpsPlayer player;
        private readonly Label reticle, status, walletStatus, prompt, feedback, shovelStatus, adminHint, batteryStatus, returnWarning, fuelWarning;
        private readonly VisualElement batteryGroup, batteryFill, xrayRoot;
        private readonly List<Label> xrayMarkers = new List<Label>();
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
            status = Root.Q<Label>("Status");
            walletStatus = Root.Q<Label>("Wallet");
            prompt = Root.Q<Label>("Target");
            feedback = Root.Q<Label>("Feedback");
            shovelStatus = Root.Q<Label>("Shovel status");
            adminHint = Root.Q<Label>("Developer controls");
            batteryStatus = Root.Q<Label>("Battery status");
            returnWarning = Root.Q<Label>("Return warning");
            fuelWarning = Root.Q<Label>("Fuel warning");
            batteryGroup = Root.Q("batteryGroup");
            batteryFill = Root.Q("Charge");
            xrayRoot = Root.Q("Admin X-ray");
            Root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }

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
            prompt.text = gameplay ? player.TargetPrompt : "";
            feedback.text = player.Feedback;
            status.text = "FINDS  " + player.Inventory.Count + " / " + player.Inventory.Capacity;
            walletStatus.text = "$" + player.Wallet.Balance;
            if (player.GameplayActive || displayedBattery != player.Battery)
            {
                UpdateBattery();
                displayedBattery = player.Battery;
            }
            shovelStatus.EnableInClassList("hidden", !player.ExcavationAvailable);
            shovelStatus.text = $"SHOVEL {player.EffectiveShovelLevel} / {player.Shovel.LevelCount}    |    {player.EffectiveShovel.Radius * 2:F2} m scoop"
                + $"\nREACH {player.EffectiveDigReach:F1} m    |    DEPTH {player.Depth:F1} m";

            adminHint.text = !player.AdminAvailable || !gameplay ? ""
                : "DEVELOPER ADMIN"
                    + (player.HasAdminOverrides ? "  |  Overrides active" : "")
                    + (player.UnlimitedBattery ? "  |  Unlimited battery" : "");
            float pulse = player.CameraSettings.SteadyCrosshair ? 0 : player.DigPulse;
            reticle.style.scale = new Scale(Vector3.one * (1 + pulse * 0.3f));
            reticle.style.color = Color.Lerp(Color.white, new Color(1, 0.82f, 0.35f), pulse);
            UpdateXray(gameplay && player.AdminXray);
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
            batteryStatus.text = "BATTERY  " + Mathf.CeilToInt(100f * player.Battery.Charge / player.Battery.Capacity) + "%  |  " + band;
            batteryFill.style.width = Length.Percent((unlimited ? 1f : fraction) * 100);
            bool low = !unlimited && risk != ReturnRisk.Safe;
            fuelWarning.text = !low ? "" : fraction <= 0 ? "FUEL EMPTY"
                : risk == ReturnRisk.Critical ? "FUEL CRITICAL" : "LOW FUEL";
            fuelWarning.EnableInClassList("critical", low && risk == ReturnRisk.Critical);
            GameMenuView.Show(fuelWarning, low);
            var recharge = player.SurfaceRecharge;
            returnWarning.text = !unlimited && recharge != null
                && (recharge.IsPlayerInZone || (fraction < 1f && recharge.IsNearby)) ? "FUEL AT WORKSHOP" : "";
        }

        private void UpdateXray(bool visible)
        {
            xrayRoot.EnableInClassList("hidden", !visible);
            if (!visible) return;
            var finds = player.Discoveries.Finds;
            while (xrayMarkers.Count < finds.Count)
            {
                var marker = new Label("o") { name = "Buried find marker", pickingMode = PickingMode.Ignore };
                marker.AddToClassList("hud-marker");
                xrayRoot.Add(marker);
                xrayMarkers.Add(marker);
            }
            for (int i = 0; i < xrayMarkers.Count; i++)
            {
                var find = i < finds.Count ? finds[i] : null;
                Vector3 view = find == null ? Vector3.zero : player.ViewCamera.WorldToViewportPoint(find.transform.position);
                bool show = find != null && find.isActiveAndEnabled && !find.Collected && view.z > 0
                    && Vector3.Distance(player.ViewCamera.transform.position, find.transform.position) <= 18
                    && view.x > 0.02f && view.x < 0.98f && view.y > 0.12f && view.y < 0.85f;
                var marker = xrayMarkers[i];
                marker.EnableInClassList("hidden", !show);
                if (!show) continue;
                marker.style.left = Length.Percent(view.x * 100);
                marker.style.top = Length.Percent((1 - view.y) * 100);
                marker.EnableInClassList("collectible", find.Exposure > 0 && find.Collectible);
            }
        }
    }
}
