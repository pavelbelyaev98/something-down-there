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
        private readonly ToolkitSettingsHelp help;
        private ToolkitSettingsRows rows;
        private SettingsCategory category;
        private DisplaySelection display;
        public VisualElement First => rows?.First ?? (reset.enabledSelf ? reset : root.parent.Q<Button>("settingsBack"));
        public ToolkitDeviceSettings(VisualElement root, FpsPlayer player, ToolkitSettingsHelp help)
        {
            this.root = root; this.player = player; this.help = help; settings = player.GameSettings;
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
            scroll.Clear(); scroll.scrollOffset = Vector2.zero; rows = new ToolkitSettingsRows(scroll, scroll, help);
            if (category == SettingsCategory.Display) BuildDisplay();
            else if (category == SettingsCategory.Graphics) BuildGraphics();
            else BuildAudio();
            Refresh();
            reset.SetEnabled(true);
        }
        private void BuildDisplay()
        {
            rows.Heading("DISPLAY");
            string[] modes = { "Borderless", "Fullscreen", "Windowed" };
            rows.Choice("windowMode", "Window mode", modes, () => display.Mode, index =>
                settings.PreviewDisplay(new DisplaySelection(display.Width, display.Height, index), Time.realtimeSinceStartupAsDouble),
                description: "Borderless fills the monitor at its desktop resolution and switches to other windows instantly. "
                    + "Fullscreen takes the display over. Windowed runs in a frame you can resize.");
            var options = settings.Resolutions;
            rows.Choice("resolution", "Resolution", options.Select(r => r.x + " × " + r.y).ToArray(),
                () => Math.Max(0, Array.FindIndex(options, r => r.x == display.Width && r.y == display.Height)), index => {
                var resolution = options[index];
                settings.PreviewDisplay(new DisplaySelection(resolution.x, resolution.y, display.Mode), Time.realtimeSinceStartupAsDouble);
            }, () => display.Mode != 0, "Desktop",
                description: "The output resolution in Fullscreen and Windowed modes. Borderless always uses the desktop resolution.");
            rows.Toggle("vSync", "VSync", () => settings.Values.VSync, value => settings.Edit(v => v.VSync = value),
                "Matches each frame to the monitor's refresh so the picture never tears. While it is on, it sets the frame rate instead of the FPS limit.");
            // A slider over the limits in rising order; Display (the monitor's own rate) sits where that rate falls.
            int[] limits = GamePreferences.FrameLimitSteps(settings.RefreshRate);
            rows.Slider("fpsLimit", "FPS limit", 0, limits.Length - 1,
                () => Math.Max(0, Array.IndexOf(limits, settings.Values.FrameLimit)),
                step => settings.Edit(v => v.FrameLimit = limits[step]),
                step => limits[step] == GamePreferences.DisplayFrameLimit ? "Display" : limits[step] < 0 ? "Unlimited" : limits[step].ToString(),
                () => !settings.Values.VSync, description: "The highest frame rate the game draws. Display matches your monitor ("
                    + settings.RefreshRate + " Hz). A lower limit keeps the graphics card cooler and quieter. Unavailable while VSync is on.",
                disabledValue: "VSync");
            rows.Toggle("showFps", "Show FPS", () => settings.Values.ShowFps, value => settings.Edit(v => v.ShowFps = value),
                "Shows the frame rate in the top-right corner.");
        }

        private void BuildGraphics()
        {
            // Rows wait while auto-configure measures candidates; its result replaces them.
            bool Idle() => !player.GraphicsTuner.Running;
            bool Rendering() => settings.RenderingAvailable && Idle();
            rows.Heading("GRAPHICS QUALITY");
            rows.Choice("qualityPreset", "Quality preset", GraphicsQuality.PresetNames, () => GraphicsQuality.Match(settings.Values),
                index => settings.Edit(v => GraphicsQuality.Apply(v, index)), Rendering, "Testing…", "Custom",
                "Sets every graphics option at once; changing one of them shows Custom. High is the intended look. Reset category returns to this PC's recommendation.");
            rows.Binding("autoConfigure", "Auto-configure", () => player.GraphicsTuner.Running ? "Testing this PC…" : "Test this PC",
                player.AutoConfigureGraphics, "Measures this PC for a few seconds and picks the best preset that keeps play smooth.");
            rows.Slider("renderScale", "Render resolution", GraphicsQuality.MinimumRenderScale / GraphicsQuality.RenderScaleStep,
                100 / GraphicsQuality.RenderScaleStep, () => settings.Values.RenderScale / GraphicsQuality.RenderScaleStep,
                value => settings.Edit(v => v.RenderScale = value * GraphicsQuality.RenderScaleStep),
                value => value * GraphicsQuality.RenderScaleStep + "%", Rendering,
                description: "Below 100% the game draws fewer pixels and upscales them with FSR: the biggest boost on slower graphics cards.");
            rows.Choice("viewDistance", "View distance", GraphicsQuality.ViewDistanceNames, () => settings.Values.ViewDistance,
                index => settings.Edit(v => v.ViewDistance = index), Idle, description: "How far grass, ground and object detail reach.");
            rows.Choice("shadows", "Sun shadows", new[] { "Off", "Low", "Medium", "High" }, () => settings.Values.Shadows,
                index => settings.Edit(v => v.Shadows = index), Rendering,
                description: "Detail of the shadows the sun casts. Lamps never shine through solid ground, even with sun shadows off.");
            rows.Choice("antiAliasing", "Anti-aliasing", GraphicsQuality.AntiAliasingNames,
                () => settings.Values.AntiAliasing, index => settings.Edit(v => v.AntiAliasing = index), Rendering,
                description: "Smooths jagged edges. FXAA is the cheapest; the multisampled settings are sharper and cost more.");
            rows.Toggle("ambientOcclusion", "Ambient occlusion", () => settings.Values.AmbientOcclusion,
                value => settings.Edit(v => v.AmbientOcclusion = value), "Soft contact shading where ground, rocks and finds meet.");
            rows.Choice("textureQuality", "Texture quality", new[] { "Low", "Medium", "High" },
                () => 2 - settings.Values.TextureLimit, index => settings.Edit(v => v.TextureLimit = 2 - index), Idle,
                description: "Texture resolution. Lower it when the graphics card runs short of memory.");
            rows.Choice("textureFiltering", "Texture filtering", new[] { "Off", "Standard", "High" },
                () => settings.Values.Filtering, index => settings.Edit(v => v.Filtering = index), Idle,
                description: "Keeps textures sharp on ground seen at a slant.");
        }

        private void BuildAudio()
        {
            rows.Heading("AUDIO");
            rows.Slider("masterVolume", "Master volume", 0, 100, () => settings.Values.MasterVolume, value => settings.Edit(v => v.MasterVolume = value),
                value => value + "%", description: "The volume of every sound in the game.");
        }
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
