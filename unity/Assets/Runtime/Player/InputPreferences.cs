using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using UnityEngine.InputSystem;

namespace SomethingDownThere
{
    public enum PlayerBinding { Forward, Backward, Left, Right, Dig, Jump, Crouch, Interact, Inventory, Pause, Sprint, Grab }

    public sealed class InputPreferences
    {
        public const int BindingCount = 12;
        private static readonly string[] ids = { "forward", "backward", "left", "right", "dig", "jump", "crouch", "interact", "inventory", "pause", "sprint", "grab" };
        private static readonly string[] labels = { "Move forward", "Move backward", "Move left", "Move right", "Dig / collect / throw", "Jump / jetpack", "Hold to crouch", "Interact", "Inventory", "Pause", "Hold to sprint", "Lift / drop find" };
        private static readonly string[] defaults = { "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d", "<Mouse>/leftButton", "<Keyboard>/space", "<Keyboard>/leftCtrl", "<Keyboard>/e", "<Keyboard>/tab", "<Keyboard>/escape", "<Keyboard>/leftShift", "<Mouse>/rightButton" };
        private static readonly Dictionary<string, string> supportedPaths = CreatePaths();
        private readonly IDevicePreferencesStore store;
        private string[] paths = (string[])defaults.Clone();
        private readonly string[] displays = new string[BindingCount];
        public bool ToggleDig { get; private set; }
        public int Revision { get; private set; }
        public bool HasUnsavedChanges { get; private set; }
        public bool WriteFailed { get; private set; }
        public event Action Changed;

        public InputPreferences(IDevicePreferencesStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            try { Decode(store.Read()); }
            catch (Exception e) when (StorageFailure(e)) { }
            RefreshDisplays();
        }

        public string Path(PlayerBinding binding) => paths[(int)binding];
        public static string Label(PlayerBinding binding) => labels[(int)binding];
        public static string DefaultPath(PlayerBinding binding) => defaults[(int)binding];
        public string Display(PlayerBinding binding) => displays[(int)binding];
        public static string DisplayPath(string path)
        {
            if (path == "<Mouse>/leftButton") return "LMB";
            if (path == "<Mouse>/rightButton") return "RMB";
            if (path == "<Mouse>/middleButton") return "Middle mouse";
            if (path == "<Mouse>/backButton") return "Mouse 4";
            if (path == "<Mouse>/forwardButton") return "Mouse 5";
            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        public static bool TryNormalize(string path, out string normalized)
        {
            normalized = null;
            return path != null && supportedPaths.TryGetValue(path, out normalized);
        }

        public bool CanBind(PlayerBinding binding, string path, out int conflict)
        {
            conflict = -1;
            if ((int)binding < 0 || (int)binding >= BindingCount || !TryNormalize(path, out path)) return false;
            for (int i = 0; i < paths.Length; i++)
                if (i != (int)binding && paths[i] == path) conflict = i;
            return true;
        }

        public bool Bind(PlayerBinding binding, string path, bool replace = false)
        {
            if (!TryNormalize(path, out path) || !CanBind(binding, path, out int conflict) || (conflict >= 0 && !replace)) return false;
            if (paths[(int)binding] == path) return true;
            if (conflict >= 0) paths[conflict] = paths[(int)binding];
            paths[(int)binding] = path;
            Dirty();
            return true;
        }

        public void SetToggleDig(bool value)
        {
            if (ToggleDig == value) return;
            ToggleDig = value;
            Dirty();
        }

        public void Reset()
        {
            paths = (string[])defaults.Clone();
            ToggleDig = false;
            Dirty();
            Flush();
        }

        private void Dirty() { RefreshDisplays(); Revision++; HasUnsavedChanges = true; Changed?.Invoke(); }
        private void RefreshDisplays()
        {
            for (int i = 0; i < paths.Length; i++) displays[i] = DisplayPath(paths[i]);
        }

        public bool Flush()
        {
            if (!HasUnsavedChanges) return true;
            try
            {
                var text = new StringBuilder("version=1\ntoggleDig=" + (ToggleDig ? "true" : "false") + "\n");
                for (int i = 0; i < paths.Length; i++) text.Append(ids[i]).Append('=').Append(paths[i]).Append('\n');
                store.Write(text.ToString());
                HasUnsavedChanges = WriteFailed = false;
            }
            catch (Exception e) when (StorageFailure(e)) { WriteFailed = true; }
            Changed?.Invoke();
            return !WriteFailed;
        }

        private void Decode(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 4096) return;
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in text.Split('\n'))
            {
                int split = line.IndexOf('=');
                if (split < 0) continue;
                string key = line.Substring(0, split).Trim();
                if (fields.ContainsKey(key)) return;
                fields[key] = line.Substring(split + 1).Trim();
            }
            if (!fields.TryGetValue("version", out string version) || version != "1") return;
            var candidate = new string[BindingCount];
            var used = new HashSet<string>();
            for (int i = 0; i < candidate.Length; i++)
            {
                // Sprint is an additive v1 field. Keep every binding in older maps;
                // prefer either Shift, then an unused ordinary key if both are taken.
                if (i == (int)PlayerBinding.Sprint && !fields.ContainsKey(ids[i]))
                {
                    foreach (string key in new[] { "leftShift", "rightShift", "r", "f", "g", "q", "c", "v", "b", "n", "m" })
                    {
                        string available = "<Keyboard>/" + key;
                        if (used.Add(available)) { candidate[i] = available; break; }
                    }
                    continue;
                }
                if (i == (int)PlayerBinding.Grab && !fields.ContainsKey(ids[i]))
                {
                    // Preserve old maps that already used RMB, choosing an unused
                    // control for the additive action without rewriting the file.
                    foreach (string available in new[] { "<Mouse>/rightButton", "<Keyboard>/f", "<Keyboard>/g", "<Keyboard>/q", "<Mouse>/middleButton", "<Mouse>/backButton", "<Mouse>/forwardButton", "<Keyboard>/v", "<Keyboard>/b", "<Keyboard>/n", "<Keyboard>/m", "<Keyboard>/p" })
                        if (used.Add(available)) { candidate[i] = available; break; }
                    continue;
                }

                if (!fields.TryGetValue(ids[i], out string path) || !TryNormalize(path, out candidate[i])
                    || !used.Add(candidate[i])) return;
            }
            // Commit mode only with a valid map; rejected bindings also restore Hold.
            paths = candidate;
            if (fields.TryGetValue("toggleDig", out string toggle) && bool.TryParse(toggle, out bool value)) ToggleDig = value;
        }

        private static Dictionary<string, string> CreatePaths()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Key key in Enum.GetValues(typeof(Key)))
            {
                if (key == Key.None) continue;
                string name = key.ToString();
                if (name == "IMESelected") continue; // Deprecated dummy enum value, not a physical key.
                name = name.StartsWith("Digit", StringComparison.Ordinal) ? name.Substring(5) : char.ToLowerInvariant(name[0]) + name.Substring(1);
                string path = "<Keyboard>/" + name;
                result[path] = path;
            }
            foreach (string name in new[] { "leftButton", "rightButton", "middleButton", "backButton", "forwardButton" })
                result["<Mouse>/" + name] = "<Mouse>/" + name;
            return result;
        }

        private static bool StorageFailure(Exception e) => e is IOException || e is UnauthorizedAccessException || e is SecurityException;
    }
}
