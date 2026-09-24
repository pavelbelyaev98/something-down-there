using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    internal sealed class ToolkitInputSettings : IDisposable
    {
        private readonly FpsPlayer player;
        private readonly InputPreferences settings;
        private readonly InputBindingCapture capture;
        private readonly VisualElement root, conflict, error;
        private readonly ScrollView scroll;
        private readonly Label notice;
        private readonly Button reset, retry, replace, cancel;
        private readonly ToolkitSettingsRows mouseRows;
        private readonly Button[] bindings = new Button[InputPreferences.BindingCount];
        private readonly PlayerBinding[] displayOrder = {
            PlayerBinding.Forward, PlayerBinding.Backward, PlayerBinding.Left, PlayerBinding.Right,
            PlayerBinding.Sprint, PlayerBinding.Crouch, PlayerBinding.Jump,
            PlayerBinding.Dig, PlayerBinding.Grab, PlayerBinding.Interact, PlayerBinding.Lamp, PlayerBinding.Mark,
            PlayerBinding.RotatePlacement, PlayerBinding.Inventory, PlayerBinding.Pause
        };
        private BindingCaptureState displayedState;
        public VisualElement First => mouseRows.First;

        public ToolkitInputSettings(VisualElement root, FpsPlayer player)
        {
            this.root = root; this.player = player;
            settings = player.InputSettings; capture = player.BindingCapture;
            notice = root.Q<Label>("bindingNotice");
            scroll = root.Q<ScrollView>("bindingScroll");
            scroll.mouseWheelScrollSize = 42;
            conflict = root.Q("bindingConflict");
            error = root.Q("inputSettingsError");
            reset = root.parent.Q<Button>("inputReset");
            retry = root.Q<Button>("inputRetry"); replace = root.Q<Button>("bindingReplace"); cancel = root.Q<Button>("bindingCancel");
            reset.clicked += () => { settings.Reset(); player.GameSettings.Reset(SettingsCategory.Controls); };
            retry.clicked += () => { settings.Flush(); player.GameSettings.Flush(); };
            replace.clicked += capture.Replace; cancel.clicked += capture.Cancel;
            mouseRows = new ToolkitSettingsRows(scroll, scroll);
            mouseRows.Slider("mouseSensitivity", "Mouse sensitivity", 10, 300, () => player.GameSettings.Values.Sensitivity,
                value => player.GameSettings.Edit(v => v.Sensitivity = value), value => (value / 100f).ToString("0.00") + "×");
            mouseRows.Toggle("invertX", "Invert horizontal look", () => player.GameSettings.Values.InvertX,
                value => player.GameSettings.Edit(v => v.InvertX = value));
            mouseRows.Toggle("invertY", "Invert vertical look", () => player.GameSettings.Values.InvertY,
                value => player.GameSettings.Edit(v => v.InvertY = value));
            mouseRows.Choice("digMode", "Digging mode", new[] { "Hold", "Toggle" }, () => settings.ToggleDig ? 1 : 0, index => settings.SetToggleDig(index == 1));
            for (int i = 0; i < displayOrder.Length; i++)
            {
                var binding = displayOrder[i];
                if (i == 0 || i == 7) mouseRows.Heading(i == 0 ? "Movement" : "Actions");
                bindings[(int)binding] = mouseRows.Binding("bind" + binding, InputPreferences.Label(binding), () => settings.Display(binding), () => capture.Begin(binding));
            }
            settings.Changed += Refresh;
            player.GameSettings.Changed += Refresh;
            capture.Changed += Refresh;
            player.MenuChanged += Refresh;
            Refresh();
        }

        public void AddNavigation(List<VisualElement> controls)
        {
            if (capture.BlocksInput) return;
            if (capture.State == BindingCaptureState.Conflict) { controls.Add(cancel); controls.Add(replace); return; }
            mouseRows.AddNavigation(controls);
            controls.Add(reset);
            if (settings.WriteFailed || player.GameSettings.WriteFailed) controls.Add(retry);
        }

        public bool Adjust(VisualElement focused, NavigationMoveEvent.Direction direction) => mouseRows.Adjust(focused, direction);

        private void Refresh()
        {
            mouseRows.Refresh();
            notice.text = capture.Notice == "Select an action, then press a key or mouse button." ? "" : capture.Notice;
            GameMenuView.Show(notice, !string.IsNullOrEmpty(notice.text));
            bool idle = capture.State == BindingCaptureState.Idle;
            scroll.SetEnabled(idle);
            reset.SetEnabled(idle);
            for (int i = 0; i < bindings.Length; i++)
            {
                bindings[i].text = settings.Display((PlayerBinding)i);
                bindings[i].SetEnabled(idle);
                bindings[i].EnableInClassList("binding-selected", !idle && i == (int)capture.Binding);
            }
            GameMenuView.Show(conflict, capture.State == BindingCaptureState.Conflict);
            if (!idle) root.schedule.Execute(() => { if (capture.State != BindingCaptureState.Idle) scroll.ScrollTo(bindings[(int)capture.Binding]); });
            bool failed = settings.WriteFailed || player.GameSettings.WriteFailed;
            GameMenuView.Show(error, failed);
            root.EnableInClassList("has-settings-error", failed);
            if (player.Menu == PlayerMenu.InputSettings && displayedState != capture.State)
            {
                if (capture.State == BindingCaptureState.Conflict) cancel.Focus();
                else if (idle) bindings[(int)capture.Binding].Focus();
            }
            if (!failed && root.focusController?.focusedElement == retry) reset.Focus();
            displayedState = capture.State;
        }

        public void Dispose() { settings.Changed -= Refresh; capture.Changed -= Refresh; player.GameSettings.Changed -= Refresh; player.MenuChanged -= Refresh; }
    }
}
