using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    internal sealed class ToolkitDeviceSettings : IDisposable
    {
        private readonly GamePreferences settings;
        private readonly FpsPlayer player;
        private readonly VisualElement root, error;
        private readonly ScrollView scroll;
        private readonly Button reset, retry;
        private ToolkitSettingsRows rows;
        private SettingsCategory category;
        private DisplaySelection display;
        public VisualElement First => rows?.First ?? (reset.enabledSelf ? reset : root.parent.Q<Button>("settingsBack"));
        public ToolkitDeviceSettings(VisualElement root, FpsPlayer player)
        {
            this.root = root; this.player = player; settings = player.GameSettings;
            scroll = root.Q<ScrollView>("deviceScroll"); scroll.mouseWheelScrollSize = 42;
            reset = root.parent.Q<Button>("deviceReset"); error = root.Q("deviceError"); retry = root.Q<Button>("deviceRetry");
            reset.clicked += () => {
                settings.Reset(category);
                if (category == SettingsCategory.Display) settings.PreviewDisplay(settings.NativeDisplay, Time.realtimeSinceStartupAsDouble);
            };
            retry.clicked += () => settings.Flush(); settings.Changed += Refresh; player.GraphicsTuner.Changed += Refresh;
        }
        public void Show(SettingsCategory category, bool returningFromPreview = false)
        {
            this.category = category;
            display = returningFromPreview ? settings.LastDisplayResult : settings.CurrentDisplay;
            scroll.Clear(); scroll.scrollOffset = Vector2.zero; rows = new ToolkitSettingsRows(scroll, scroll);
            if (category == SettingsCategory.Display) BuildDisplay();
            else if (category == SettingsCategory.Graphics) BuildGraphics();
            else BuildAudio();
            Refresh();
            reset.SetEnabled(true);
        }
        private void BuildDisplay()
        {
            string[] modes = { "Borderless", "Fullscreen", "Windowed" };
            rows.Choice("windowMode", "Window mode", modes, () => display.Mode, index =>
                settings.PreviewDisplay(new DisplaySelection(display.Width, display.Height, index), Time.realtimeSinceStartupAsDouble));
            var options = settings.Resolutions;
            rows.Choice("resolution", "Resolution", options.Select(r => r.x + " × " + r.y).ToArray(),
                () => Math.Max(0, Array.FindIndex(options, r => r.x == display.Width && r.y == display.Height)), index => {
                var resolution = options[index];
                settings.PreviewDisplay(new DisplaySelection(resolution.x, resolution.y, display.Mode), Time.realtimeSinceStartupAsDouble);
            }, () => display.Mode != 0, "Desktop");
            rows.Toggle("vSync", "VSync", () => settings.Values.VSync, value => settings.Edit(v => v.VSync = value));
            rows.Choice("fpsLimit", "FPS limit", GamePreferences.FrameLimits.Select(v => v == GamePreferences.DisplayFrameLimit
                ? "Display (" + settings.RefreshRate + ")" : v < 0 ? "Unlimited" : v.ToString()).ToArray(),
                () => Array.IndexOf(GamePreferences.FrameLimits, settings.Values.FrameLimit), index =>
                settings.Edit(v => v.FrameLimit = GamePreferences.FrameLimits[index]), () => !settings.Values.VSync, "Automatic");
            rows.Toggle("showFps", "Show FPS", () => settings.Values.ShowFps, value => settings.Edit(v => v.ShowFps = value));
        }

        private void BuildGraphics()
        {
            // Rows wait while auto-configure measures candidates; its result replaces them.
            bool Idle() => !player.GraphicsTuner.Running;
            bool Rendering() => settings.RenderingAvailable && Idle();
            rows.Choice("qualityPreset", "Quality preset", GraphicsQuality.PresetNames, () => GraphicsQuality.Match(settings.Values),
                index => settings.Edit(v => GraphicsQuality.Apply(v, index)), Rendering, "Testing…", "Custom");
            rows.Binding("autoConfigure", "Auto-configure", () => player.GraphicsTuner.Running ? "Testing this PC…" : "Test this PC",
                player.AutoConfigureGraphics);
            rows.Slider("renderScale", "Render resolution", GraphicsQuality.MinimumRenderScale / GraphicsQuality.RenderScaleStep,
                100 / GraphicsQuality.RenderScaleStep, () => settings.Values.RenderScale / GraphicsQuality.RenderScaleStep,
                value => settings.Edit(v => v.RenderScale = value * GraphicsQuality.RenderScaleStep),
                value => value * GraphicsQuality.RenderScaleStep + "%", Rendering);
            rows.Choice("viewDistance", "View distance", GraphicsQuality.ViewDistanceNames, () => settings.Values.ViewDistance,
                index => settings.Edit(v => v.ViewDistance = index), Idle);
            rows.Choice("shadows", "Sun shadows", new[] { "Off", "Low", "Medium", "High" }, () => settings.Values.Shadows,
                index => settings.Edit(v => v.Shadows = index), Rendering);
            rows.Choice("antiAliasing", "Anti-aliasing", GraphicsQuality.AntiAliasingNames,
                () => settings.Values.AntiAliasing, index => settings.Edit(v => v.AntiAliasing = index), Rendering);
            rows.Toggle("ambientOcclusion", "Ambient occlusion", () => settings.Values.AmbientOcclusion,
                value => settings.Edit(v => v.AmbientOcclusion = value));
            rows.Choice("textureQuality", "Texture quality", new[] { "Low", "Medium", "High" },
                () => 2 - settings.Values.TextureLimit, index => settings.Edit(v => v.TextureLimit = 2 - index), Idle);
            rows.Choice("textureFiltering", "Texture filtering", new[] { "Off", "Standard", "High" },
                () => settings.Values.Filtering, index => settings.Edit(v => v.Filtering = index), Idle);
            var help = new Label("Test this PC picks the best preset that keeps play smooth. Render resolution below 100% upscales with FSR: the biggest boost on slower graphics cards. View distance sets how far grass and ground detail reach. Reset returns to this PC's recommendation.")
                { name = "graphicsHelp", pickingMode = PickingMode.Ignore };
            help.AddToClassList("settings-help");
            scroll.Add(help);
        }

        private void BuildAudio() => rows.Slider("masterVolume", "Master volume", 0, 100, () => settings.Values.MasterVolume, value => settings.Edit(v => v.MasterVolume = value), value => value + "%");
        private void Refresh()
        {
            rows?.Refresh(); GameMenuView.Show(error, settings.WriteFailed);
            if (!settings.WriteFailed && root.focusController?.focusedElement == retry) reset.Focus();
        }
        public void AddNavigation(List<VisualElement> controls) { rows?.AddNavigation(controls); if (reset.enabledSelf) controls.Add(reset); if (settings.WriteFailed) controls.Add(retry); }
        public bool Adjust(VisualElement focused, NavigationMoveEvent.Direction direction) => !settings.PreviewingDisplay && rows != null && rows.Adjust(focused, direction);
        public void Dispose() { settings.Changed -= Refresh; player.GraphicsTuner.Changed -= Refresh; }
    }
}
