using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    public sealed class GameHudView
    {
        private readonly FpsPlayer player;
        private readonly Label status, walletStatus, lampStatus, uniqueStatus, prompt, feedback, adminHint, fuelWarning, inventoryWarning;
        private readonly VisualElement reticle, batteryGroup, batteryFill, bagGroup, lampGroup, uniqueGroup, pickupNotes;
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
            reticle = Root.Q("Reticle");
            pickupNotes = Root.Q("PickupNotes");
            player.Collected += AddPickupNote;
            detectorPanel = Root.Q("DetectorPanel");
            detectorBars = new[] { Root.Q("DetectorBar1"), Root.Q("DetectorBar2"), Root.Q("DetectorBar3") };
            status = Root.Q<Label>("Status");
            walletStatus = Root.Q<Label>("Wallet");
            lampStatus = Root.Q<Label>("Lamps");
            bagGroup = Root.Q("BagGroup");
            lampGroup = Root.Q("LampGroup");
            uniqueGroup = Root.Q("UniqueGroup");
            uniqueStatus = Root.Q<Label>("Uniques");
            prompt = Root.Q<Label>("Target");
            feedback = Root.Q<Label>("Feedback");
            adminHint = Root.Q<Label>("Developer controls");
            fuelWarning = Root.Q<Label>("Fuel warning");
            inventoryWarning = Root.Q<Label>("Inventory warning");
            batteryGroup = Root.Q("batteryGroup");
            batteryFill = Root.Q("Charge");
            Root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }

        private (int, int, int) shownStatus = (-1, -1, -1);
        private (int, int, bool) shownLamps = (-1, -1, false);
        private (int, int) shownUniques = (-1, -1);
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
            // Uniques secured (taken, on the crane's rope or kept at camp) of the save's uniques (119).
            (int secured, int total) uniqueKey = player.Discoveries != null ? player.Discoveries.UniqueTally() : (0, 0);
            GameMenuView.Show(uniqueGroup, uniqueKey.total > 0);
            if (uniqueKey != shownUniques)
            {
                shownUniques = uniqueKey;
                uniqueStatus.text = uniqueKey.secured + "/" + uniqueKey.total;
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
            reticle.style.backgroundColor = Color.Lerp(Color.white, new Color(1, 0.82f, 0.35f), pulse);
            TickPickupNotes();
        }

        // "+Coal" for each find taken into the bag. The same find again while its note shows counts it up ("+Coal ×2")
        // and moves it to the bottom.
        private const float NoteSeconds = 2.5f, NoteFadeSeconds = .5f;
        private const int MaxNotes = 4;
        private sealed class PickupNote { public Label Label; public string Name; public int Count; public float Shown; }
        private readonly List<PickupNote> notes = new List<PickupNote>();

        private void AddPickupNote(InventoryItem item)
        {
            var note = notes.Find(n => n.Name == item.DisplayName);
            if (note != null) notes.Remove(note);
            else
            {
                if (notes.Count == MaxNotes) { notes[0].Label.RemoveFromHierarchy(); notes.RemoveAt(0); }
                note = new PickupNote { Label = new Label { pickingMode = PickingMode.Ignore }, Name = item.DisplayName };
                note.Label.AddToClassList("hud-number");
                note.Label.AddToClassList("hud-pickup");
            }
            notes.Add(note);
            pickupNotes.Add(note.Label);
            note.Count++;
            note.Shown = Time.unscaledTime;
            note.Label.text = "+" + note.Name + (note.Count > 1 ? " ×" + note.Count : "");
        }

        private void TickPickupNotes()
        {
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                float left = notes[i].Shown + NoteSeconds - Time.unscaledTime;
                if (left <= 0) { notes[i].Label.RemoveFromHierarchy(); notes.RemoveAt(i); continue; }
                notes[i].Label.style.opacity = Mathf.Clamp01(left / NoteFadeSeconds);
            }
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
            fuelWarning.text = !low ? "" : fraction <= 0 ? "BATTERY EMPTY"
                : risk == ReturnRisk.Critical ? "BATTERY CRITICAL" : "LOW BATTERY";
            fuelWarning.EnableInClassList("critical", low && risk == ReturnRisk.Critical);
            GameMenuView.Show(fuelWarning, low);
        }

    }
}
