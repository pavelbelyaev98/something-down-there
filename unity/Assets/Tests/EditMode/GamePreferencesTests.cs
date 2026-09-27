using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class GamePreferencesTests
    {
        private sealed class Store : IDevicePreferencesStore
        {
            public string Text; public int Writes; public bool Fail;
            public string Read() => Text;
            public void Write(string text) { Writes++; if (Fail) throw new IOException("Test failure"); Text = text; }
        }
        private sealed class Platform : IGameSettingsPlatform
        {
            public DisplaySelection CurrentDisplay { get; private set; } = new DisplaySelection(1920, 1080, 0);
            public Vector2Int[] Resolutions => new[] { new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1080) };
            public DisplaySelection NativeDisplay => new DisplaySelection(1920, 1080, 0);
            public bool RenderingAvailable => true;
            public int RefreshRate => 144;
            public GamePreferenceValues Applied; public bool Focused; public int DisplayChanges; public bool Disposed, DeferDisplay;
            public void Apply(GamePreferenceValues values, bool focused) { Applied = values.Copy(); Focused = focused; }
            public void SetDisplay(DisplaySelection display) { if (!DeferDisplay) CurrentDisplay = display; DisplayChanges++; }
            public void Dispose() => Disposed = true;
        }

        [TestCase(null)]
        [TestCase("broken")]
        [TestCase("{\"Version\":2,\"FrameLimit\":999,\"Muted\":true}")]
        public void BadOrUnknownFormatsUseDefaultsWithoutReplacingSource(string text)
        {
            var store = new Store { Text = text };
            using var preferences = new GamePreferences(store, new Platform());
            Assert.That(preferences.Values.FrameLimit, Is.EqualTo(GamePreferences.DisplayFrameLimit));
            preferences.Flush();
            Assert.That(store.Writes, Is.Zero); Assert.That(store.Text, Is.EqualTo(text));
        }

        [Test]
        public void NewPreferencesUseNativeBorderlessAndHighGraphicsWithoutWriting()
        {
            var platform = new Platform(); var store = new Store();
            using var preferences = new GamePreferences(store, platform);
            Assert.That(platform.CurrentDisplay.Same(platform.NativeDisplay), Is.True);
            Assert.That(preferences.Values.VSync, Is.False);
            Assert.That(GraphicsQuality.Match(preferences.Values), Is.EqualTo(GraphicsQuality.High), "High is the accepted look.");
            Assert.That(preferences.Values.RenderScale, Is.EqualTo(100));
            Assert.That(preferences.Values.FrameLimit, Is.EqualTo(GamePreferences.DisplayFrameLimit), "The limit follows the monitor.");
            Assert.That(preferences.Values.GraphicsTuned, Is.False);
            Assert.That(store.Writes, Is.Zero);
        }

        [Test]
        public void SavedWindowChoiceOverridesRecommendedStartupWithoutRewritingIt()
        {
            var platform = new Platform();
            var store = new Store { Text = "{\"Version\":1,\"Width\":1280,\"Height\":720,\"WindowMode\":2,\"FrameLimit\":60}" };
            using var preferences = new GamePreferences(store, platform);
            Assert.That(platform.CurrentDisplay.Same(new DisplaySelection(1280, 720, 2)), Is.True);
            Assert.That(preferences.Values.FrameLimit, Is.EqualTo(60));
            Assert.That(preferences.Values.Shadows, Is.EqualTo(3), "Absent values use the current defaults.");
            Assert.That(store.Writes, Is.Zero);
        }

        [Test]
        public void BorderlessStartupAndPreviewUseTheMonitorInsteadOfSavedWindowAspect()
        {
            var platform = new Platform();
            var store = new Store { Text = "{\"Version\":1,\"Width\":2560,\"Height\":1080,\"WindowMode\":0}" };
            using var preferences = new GamePreferences(store, platform);
            Assert.That(platform.CurrentDisplay.Same(platform.NativeDisplay), Is.True);
            preferences.PreviewDisplay(new DisplaySelection(1280, 720, 2), 1);
            Assert.That(preferences.KeepDisplay(2), Is.True);
            preferences.PreviewDisplay(new DisplaySelection(1280, 720, 0), 3);
            Assert.That(platform.CurrentDisplay.Same(platform.NativeDisplay), Is.True);
            preferences.RevertDisplay();
            Assert.That(platform.CurrentDisplay.Same(new DisplaySelection(1280, 720, 2)), Is.True);
        }

        [Test]
        public void LoadedValuesAreBoundedAndUnrelatedFieldsKeepDefaults()
        {
            var store = new Store { Text = "{\"Version\":1,\"Sensitivity\":999,\"MasterVolume\":200,\"AntiAliasing\":9,\"FrameLimit\":7,\"Width\":-1,\"Height\":900,\"TextureLimit\":99,\"Shadows\":99,\"RenderScale\":37,\"ViewDistance\":9,\"RecommendedPreset\":9}" };
            var platform = new Platform();
            using var preferences = new GamePreferences(store, platform);
            Assert.That(preferences.Values.Sensitivity, Is.EqualTo(300));
            Assert.That(platform.Applied.MasterVolume, Is.EqualTo(100));
            Assert.That(platform.Applied.AntiAliasing, Is.EqualTo(GraphicsQuality.Msaa2));
            Assert.That(platform.Applied.TextureLimit, Is.EqualTo(2));
            Assert.That(platform.Applied.Shadows, Is.EqualTo(3));
            Assert.That(platform.Applied.FrameLimit, Is.EqualTo(GamePreferences.DisplayFrameLimit));
            Assert.That(platform.Applied.RenderScale, Is.EqualTo(GraphicsQuality.MinimumRenderScale));
            Assert.That(platform.Applied.ViewDistance, Is.EqualTo(GraphicsQuality.ViewHigh));
            Assert.That(platform.Applied.RecommendedPreset, Is.EqualTo(GraphicsQuality.Ultra));
            Assert.That(platform.DisplayChanges, Is.EqualTo(1)); Assert.That(store.Writes, Is.Zero);
        }

        [Test]
        public void ChangesApplyImmediatelyWriteAtBoundaryAndRetryWithoutLosingValues()
        {
            var store = new Store(); var platform = new Platform();
            using var preferences = new GamePreferences(store, platform);
            for (int i = 1; i <= 70; i++) preferences.Edit(v => v.MasterVolume = i);
            Assert.That(store.Writes, Is.Zero); Assert.That(platform.Applied.MasterVolume, Is.EqualTo(70));
            preferences.Edit(v => { v.Shadows = -1; v.AntiAliasing = GraphicsQuality.Msaa2; v.TextureLimit = 1; v.Filtering = 0; });
            Assert.That(platform.Applied.Shadows, Is.Zero);
            store.Fail = true; Assert.That(preferences.Flush(), Is.False);
            Assert.That(preferences.WriteFailed && preferences.Dirty, Is.True);
            store.Fail = false; Assert.That(preferences.Flush(), Is.True);
            using var reloaded = new GamePreferences(store, new Platform());
            Assert.That(reloaded.Values.MasterVolume, Is.EqualTo(70));
            Assert.That(reloaded.Values.Shadows, Is.Zero);
            Assert.That(reloaded.Values.AntiAliasing, Is.EqualTo(GraphicsQuality.Msaa2));
            Assert.That(reloaded.Values.TextureLimit, Is.EqualTo(1));
            Assert.That(reloaded.Values.Filtering, Is.Zero);
            int writes = store.Writes;
            preferences.Edit(v => v.MasterVolume = 70); preferences.Flush();
            Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [TestCase(SettingsCategory.Display)]
        [TestCase(SettingsCategory.Graphics)]
        [TestCase(SettingsCategory.Audio)]
        [TestCase(SettingsCategory.Controls)]
        public void CategoryResetPreservesOtherSettingsAndConfirmedDisplay(SettingsCategory category)
        {
            using var preferences = new GamePreferences(new Store(), new Platform());
            preferences.Edit(v => { v.Width = 1280; v.Height = 720; v.WindowMode = 2; v.FrameLimit = 144; v.VSync = false;
                v.Shadows = 1; v.AntiAliasing = GraphicsQuality.Msaa8; v.TextureLimit = 2; v.Filtering = 0; v.MasterVolume = 20; v.Sensitivity = 230; });
            preferences.Reset(category);
            Assert.That(preferences.Values.Width, Is.EqualTo(1280)); Assert.That(preferences.Values.WindowMode, Is.EqualTo(2));
            Assert.That(preferences.Values.FrameLimit, Is.EqualTo(category == SettingsCategory.Display ? GamePreferences.DisplayFrameLimit : 144));
            Assert.That(preferences.Values.Shadows, Is.EqualTo(category == SettingsCategory.Graphics ? 3 : 1));
            Assert.That(preferences.Values.AntiAliasing, Is.EqualTo(category == SettingsCategory.Graphics ? GraphicsQuality.Msaa2 : GraphicsQuality.Msaa8));
            Assert.That(preferences.Values.TextureLimit, Is.EqualTo(category == SettingsCategory.Graphics ? 0 : 2));
            Assert.That(preferences.Values.Filtering, Is.EqualTo(category == SettingsCategory.Graphics ? 2 : 0));
            Assert.That(preferences.Values.MasterVolume, Is.EqualTo(category == SettingsCategory.Audio ? 100 : 20));
            Assert.That(preferences.Values.Sensitivity, Is.EqualTo(category == SettingsCategory.Controls ? 100 : 230));
        }

        [Test]
        public void PresetsSetTheirRowsAndAnyIndividualChangeReadsAsCustom()
        {
            var values = new GamePreferenceValues();
            foreach (int preset in new[] { GraphicsQuality.Low, GraphicsQuality.Medium, GraphicsQuality.High, GraphicsQuality.Ultra })
            {
                GraphicsQuality.Apply(values, preset);
                Assert.That(GraphicsQuality.Match(values), Is.EqualTo(preset));
            }
            values.RenderScale = 75;
            Assert.That(GraphicsQuality.Match(values), Is.EqualTo(GraphicsQuality.Ultra), "Render resolution is its own choice.");
            values.AmbientOcclusion = false;
            Assert.That(GraphicsQuality.Match(values), Is.EqualTo(GraphicsQuality.Custom));
        }

        [Test]
        public void GraphicsPreviewNeverStoresAndResetReturnsToThisDevicesRecommendation()
        {
            var store = new Store(); var platform = new Platform();
            using var preferences = new GamePreferences(store, platform);
            var candidate = preferences.Values.Copy(); GraphicsQuality.Apply(candidate, GraphicsQuality.Low); candidate.FrameLimit = -1;
            preferences.PreviewGraphics(candidate);
            Assert.That(platform.Applied.FrameLimit, Is.EqualTo(-1));
            Assert.That(GraphicsQuality.Match(preferences.Values), Is.EqualTo(GraphicsQuality.High));
            preferences.EndGraphicsPreview();
            Assert.That(platform.Applied.FrameLimit, Is.EqualTo(GamePreferences.DisplayFrameLimit));
            Assert.That(store.Writes, Is.Zero);
            preferences.ApplyRecommendation(GraphicsQuality.Medium, 85);
            Assert.That(store.Writes, Is.EqualTo(1), "The recommendation persists immediately.");
            preferences.Edit(v => { GraphicsQuality.Apply(v, GraphicsQuality.Ultra); v.RenderScale = 100; });
            preferences.Reset(SettingsCategory.Graphics);
            Assert.That(GraphicsQuality.Match(preferences.Values), Is.EqualTo(GraphicsQuality.Medium));
            Assert.That(preferences.Values.RenderScale, Is.EqualTo(85));
            using var reloaded = new GamePreferences(store, new Platform());
            Assert.That(reloaded.Values.GraphicsTuned, Is.True);
            Assert.That(reloaded.Values.RecommendedPreset, Is.EqualTo(GraphicsQuality.Medium));
        }

        // Frame costs in candidate order: High, Medium, Low, then Low at 85/75/65/50%.
        [TestCase(144, new[] { 8f }, GraphicsQuality.High, 100, TestName = "Fast PC keeps High")]
        [TestCase(144, new[] { 13f, 9.5f }, GraphicsQuality.Medium, 100, TestName = "High-refresh display trades one tier for 90 FPS")]
        [TestCase(144, new[] { 14f, 12.5f, 11f }, GraphicsQuality.High, 100, TestName = "Best preset holding 60 when 90 needs more than Low")]
        [TestCase(60, new[] { 14f }, GraphicsQuality.High, 100, TestName = "60 Hz display only needs 60")]
        [TestCase(144, new[] { 30f, 25f, 20f, 17f, 14f }, GraphicsQuality.Low, 75, TestName = "Slow GPU lowers resolution until 60")]
        [TestCase(144, new[] { 40f, 40f, 40f, 40f, 40f, 40f, 40f }, GraphicsQuality.Low, 50, TestName = "Hopeless GPU ends at the lowest step")]
        public void AutoConfigureChoosesTheBestPresetThatStaysSmooth(int refresh, float[] costs, int preset, int scale)
        {
            var plan = new GraphicsRecommendationPlan(refresh);
            foreach (float cost in costs) plan.Record(cost);
            Assert.That(plan.Decided, Is.True);
            Assert.That(plan.Choice.Preset, Is.EqualTo(preset));
            Assert.That(plan.Choice.RenderScale, Is.EqualTo(scale));
        }

        [Test]
        public void DisplayPreviewCannotBePersistedBeforeExplicitKeep()
        {
            var store = new Store(); var platform = new Platform();
            using var preferences = new GamePreferences(store, platform);
            preferences.PreviewDisplay(new DisplaySelection(1280, 720, 2), 10);
            Assert.That(preferences.PreviewingDisplay, Is.True); Assert.That(preferences.KeepDisplay(10.1), Is.False);
            preferences.Edit(v => v.ShowFps = true); preferences.Flush();
            Assert.That(JsonUtility.FromJson<GamePreferenceValues>(store.Text).Width, Is.Zero);
            Assert.That(preferences.KeepDisplay(11), Is.True);
            using var reload = new GamePreferences(store, new Platform());
            Assert.That(reload.Values.Width, Is.EqualTo(1280)); Assert.That(reload.Values.WindowMode, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TimeoutAndLateKeepRevertToActualOriginalWithoutWriting(bool lateKeep)
        {
            var store = new Store(); var platform = new Platform();
            using var preferences = new GamePreferences(store, platform);
            preferences.PreviewDisplay(new DisplaySelection(960, 540, 2), 100);
            preferences.Tick(108.2); Assert.That(preferences.SecondsRemaining, Is.EqualTo(7));
            if (lateKeep) Assert.That(preferences.KeepDisplay(115), Is.False); else preferences.Tick(115);
            Assert.That(preferences.PreviewingDisplay, Is.False);
            Assert.That(platform.CurrentDisplay.Same(new DisplaySelection(1920, 1080, 0)), Is.True);
            Assert.That(store.Writes, Is.Zero);
        }

        [Test]
        public void FocusLossRevertsAndFlushesOnlyOrdinaryChanges()
        {
            var store = new Store(); var platform = new Platform();
            using var preferences = new GamePreferences(store, platform);
            preferences.Edit(v => v.Sensitivity = 160);
            preferences.PreviewDisplay(new DisplaySelection(1280, 720, 1), 1);
            preferences.SetFocus(false);
            Assert.That(platform.Focused, Is.False); Assert.That(preferences.PreviewingDisplay, Is.False);
            Assert.That(platform.CurrentDisplay.Mode, Is.Zero);
            var written = JsonUtility.FromJson<GamePreferenceValues>(store.Text);
            Assert.That(written.Width, Is.Zero); Assert.That(written.Sensitivity, Is.EqualTo(160));
        }

        [Test]
        public void RevertPublishesOriginalSelectionBeforeNativeWindowFinishesChanging()
        {
            var platform = new Platform(); var store = new Store();
            using var preferences = new GamePreferences(store, platform);
            var original = platform.CurrentDisplay;
            preferences.PreviewDisplay(new DisplaySelection(1280, 720, 2), 1);
            platform.DeferDisplay = true;
            preferences.RevertDisplay();
            Assert.That(platform.CurrentDisplay.Width, Is.EqualTo(1280), "Native window is still reporting the preview.");
            Assert.That(preferences.LastDisplayResult.Same(original), Is.True, "UI must show the restored selection immediately.");
            Assert.That(store.Writes, Is.Zero);
        }

        [Test]
        public void UnsupportedModeCannotStartPreviewAndDisposalRevertsPendingDisplay()
        {
            var platform = new Platform(); var preferences = new GamePreferences(new Store(), platform);
            preferences.PreviewDisplay(new DisplaySelection(640, 480, 2), 1);
            Assert.That(preferences.PreviewingDisplay, Is.False);
            preferences.PreviewDisplay(new DisplaySelection(1280, 720, 2), 1);
            preferences.Dispose();
            Assert.That(platform.CurrentDisplay.Width, Is.EqualTo(1920)); Assert.That(platform.Disposed, Is.True);
        }
    }
}
