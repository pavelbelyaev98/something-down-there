using System;
using System.IO;
using System.Security;
using UnityEngine;

namespace SomethingDownThere
{
    public enum SettingsCategory { Display, Graphics, Audio, Controls, Accessibility }

    [Serializable]
    public sealed class GamePreferenceValues
    {
        public int Version = 1;
        public int Width, Height, WindowMode;
        public bool VSync, ShowFps;
        public int FrameLimit = GamePreferences.DefaultFrameLimit;
        public int Msaa = 2, TextureLimit, Filtering = 2;
        public int Shadows = 3;
        public int MasterVolume = 100;
        public int Sensitivity = 100;
        public bool InvertX, InvertY;
        public GamePreferenceValues Copy() => (GamePreferenceValues)MemberwiseClone();
    }

    public readonly struct DisplaySelection
    {
        public readonly int Width, Height, Mode;
        public DisplaySelection(int width, int height, int mode) { Width = width; Height = height; Mode = mode; }
        public bool Same(DisplaySelection other) => Width == other.Width && Height == other.Height && Mode == other.Mode;
    }

    public interface IGameSettingsPlatform : IDisposable
    {
        DisplaySelection CurrentDisplay { get; }
        Vector2Int[] Resolutions { get; }
        DisplaySelection NativeDisplay { get; }
        bool RenderingAvailable { get; }
        void Apply(GamePreferenceValues values, bool focused);
        void SetDisplay(DisplaySelection selection);
    }

    public sealed class GamePreferences : IDisposable
    {
        public const int DefaultFrameLimit = 144;
        public static readonly int[] FrameLimits = { 30, 45, 60, 90, 120, 144, 165, 240, -1 };
        private readonly IDevicePreferencesStore store;
        private readonly IGameSettingsPlatform platform;
        private bool focused = true;
        private DisplaySelection previousDisplay;
        private double deadline, earliestConfirmation;
        public GamePreferenceValues Values { get; private set; } = new GamePreferenceValues();
        public bool Dirty { get; private set; }
        public bool WriteFailed { get; private set; }
        public bool PreviewingDisplay { get; private set; }
        public int SecondsRemaining { get; private set; }
        public DisplaySelection CurrentDisplay => platform.CurrentDisplay;
        // The result of Keep/Revert is known before the native window finishes resizing.
        public DisplaySelection LastDisplayResult { get; private set; }
        public Vector2Int[] Resolutions => platform.Resolutions;
        public DisplaySelection NativeDisplay => platform.NativeDisplay;
        public bool RenderingAvailable => platform.RenderingAvailable;
        public event Action Changed;

        public GamePreferences(IDevicePreferencesStore store, IGameSettingsPlatform platform)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.platform = platform ?? throw new ArgumentNullException(nameof(platform));
            try
            {
                string text = store.Read();
                if (!string.IsNullOrEmpty(text) && text.Length <= 4096)
                {
                    var loaded = new GamePreferenceValues();
                    JsonUtility.FromJsonOverwrite(text, loaded);
                    if (loaded.Version == 1) { Sanitize(loaded); Values = loaded; }
                }
            }
            catch (Exception e) when (StorageFailure(e) || e is ArgumentException) { }
            platform.Apply(Values, focused);
            LastDisplayResult = Values.WindowMode != 0 && Values.Width > 0 && Supported(Values.Width, Values.Height)
                ? new DisplaySelection(Values.Width, Values.Height, Values.WindowMode) : platform.NativeDisplay;
            platform.SetDisplay(LastDisplayResult);
        }

        public void Edit(Action<GamePreferenceValues> edit)
        {
            var next = Values.Copy();
            edit(next); Sanitize(next);
            if (JsonUtility.ToJson(next) == JsonUtility.ToJson(Values)) return;
            Values = next; Dirty = true;
            platform.Apply(Values, focused);
            Changed?.Invoke();
        }

        public void Reset(SettingsCategory category)
        {
            var defaults = new GamePreferenceValues();
            Edit(v => {
                switch (category)
                {
                    case SettingsCategory.Display: v.VSync = defaults.VSync; v.FrameLimit = defaults.FrameLimit; v.ShowFps = false; break;
                    case SettingsCategory.Graphics: v.Msaa = defaults.Msaa; v.TextureLimit = defaults.TextureLimit; v.Filtering = defaults.Filtering; v.Shadows = defaults.Shadows; break;
                    case SettingsCategory.Audio: v.MasterVolume = 100; break;
                    case SettingsCategory.Controls: v.Sensitivity = 100; v.InvertX = v.InvertY = false; break;
                }
            });
            Dirty = true;
            Flush();
        }

        public void PreviewDisplay(DisplaySelection selection, double now)
        {
            if (selection.Mode == 0) selection = platform.NativeDisplay;
            if (PreviewingDisplay || !focused || !Supported(selection.Width, selection.Height) || selection.Mode < 0 || selection.Mode > 2) return;
            previousDisplay = platform.CurrentDisplay;
            if (previousDisplay.Same(selection)) return;
            PreviewingDisplay = true;
            deadline = now + 15; earliestConfirmation = now + 0.5; SecondsRemaining = 15;
            platform.SetDisplay(selection);
            Changed?.Invoke();
        }

        public bool KeepDisplay(double now)
        {
            if (!PreviewingDisplay || !focused || now < earliestConfirmation) return false;
            if (now >= deadline) { RevertDisplay(); return false; }
            var actual = platform.CurrentDisplay;
            if (actual.Width < 960 || actual.Height < 540) { RevertDisplay(); return false; }
            LastDisplayResult = actual;
            PreviewingDisplay = false;
            Edit(v => { v.Width = actual.Width; v.Height = actual.Height; v.WindowMode = actual.Mode; });
            Flush(); Changed?.Invoke();
            return true;
        }

        public void RevertDisplay()
        {
            if (!PreviewingDisplay) return;
            LastDisplayResult = previousDisplay;
            PreviewingDisplay = false;
            platform.SetDisplay(previousDisplay);
            Changed?.Invoke();
        }

        public void Tick(double now)
        {
            if (!PreviewingDisplay) return;
            if (now >= deadline) { RevertDisplay(); return; }
            int seconds = (int)Math.Ceiling(deadline - now);
            if (seconds != SecondsRemaining) { SecondsRemaining = seconds; Changed?.Invoke(); }
        }

        public void SetFocus(bool value)
        {
            focused = value;
            if (!value) { RevertDisplay(); Flush(); }
            platform.Apply(Values, focused);
        }

        public bool Flush()
        {
            if (!Dirty) return !WriteFailed;
            try { store.Write(JsonUtility.ToJson(Values, true)); Dirty = WriteFailed = false; }
            catch (Exception e) when (StorageFailure(e)) { WriteFailed = true; }
            Changed?.Invoke();
            return !WriteFailed;
        }

        private bool Supported(int width, int height)
        {
            var current = platform.CurrentDisplay;
            if (width >= 960 && height >= 540 && current.Width == width && current.Height == height) return true;
            foreach (var resolution in platform.Resolutions) if (resolution.x == width && resolution.y == height) return true;
            return false;
        }

        private static void Sanitize(GamePreferenceValues v)
        {
            v.Version = 1; v.WindowMode = Mathf.Clamp(v.WindowMode, 0, 2);
            if (v.Width < 960 || v.Width > 16384 || v.Height < 540 || v.Height > 8640) v.Width = v.Height = 0;
            if (Array.IndexOf(FrameLimits, v.FrameLimit) < 0) v.FrameLimit = DefaultFrameLimit;
            if (v.Msaa != 1 && v.Msaa != 2 && v.Msaa != 4 && v.Msaa != 8) v.Msaa = 2;
            v.TextureLimit = Mathf.Clamp(v.TextureLimit, 0, 2); v.Filtering = Mathf.Clamp(v.Filtering, 0, 2);
            v.Shadows = Mathf.Clamp(v.Shadows, 0, 3);
            v.MasterVolume = Mathf.Clamp(v.MasterVolume, 0, 100); v.Sensitivity = Mathf.Clamp(v.Sensitivity, 10, 300);
        }

        private static bool StorageFailure(Exception e) => e is IOException || e is UnauthorizedAccessException || e is SecurityException;
        public void Dispose() { RevertDisplay(); platform.Dispose(); }
    }
}
