using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    internal sealed class ToolkitCameraSettings : IDisposable
    {
        private readonly CameraPreferences settings;
        private readonly VisualElement root, error;
        private readonly Button reset, retry;
        private readonly ToolkitSettingsRows rows;
        public SliderInt Slider { get; }
        public ToolkitCameraSettings(VisualElement root, FpsPlayer player, ToolkitSettingsHelp help)
        {
            this.root = root; settings = player.CameraSettings;
            reset = root.parent.Q<Button>("cameraReset"); retry = root.Q<Button>("settingsRetry"); error = root.Q("settingsError");
            var scroll = root.Q<ScrollView>("cameraScroll"); rows = new ToolkitSettingsRows(scroll, scroll, help);
            rows.Heading("COMFORT");
            Slider = rows.Slider("fovSlider", "Field of view", 55, 90, () => settings.VerticalFov, value => settings.SetVerticalFov(value), value => value + "°",
                valueName: "fovValue", description: "How much of the world fits on screen, measured top to bottom. A wider view can ease motion sickness.");
            rows.Toggle("steadyCrosshair", "Steady crosshair", () => settings.SteadyCrosshair, settings.SetSteadyCrosshair,
                "Keeps the crosshair still while the tool digs. Off, it pulses with every stroke.");
            reset.clicked += settings.Reset; retry.clicked += () => settings.Flush();
            settings.Changed += Refresh; Refresh();
        }
        public void AddNavigation(List<VisualElement> controls) { rows.AddNavigation(controls); controls.Add(reset); if (settings.WriteFailed) controls.Add(retry); }
        public bool Adjust(VisualElement focused, NavigationMoveEvent.Direction direction) => rows.Adjust(focused, direction);
        private void Refresh()
        {
            rows.Refresh(); GameMenuView.Show(error, settings.WriteFailed);
            if (!settings.WriteFailed && root.focusController?.focusedElement == retry) reset.Focus();
        }
        public void Dispose() => settings.Changed -= Refresh;
    }
}
