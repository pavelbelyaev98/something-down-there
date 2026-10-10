using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    internal sealed class ToolkitSettingsRows
    {
        private static readonly string[] OffOn = { "Off", "On" };
        private readonly List<VisualElement> controls = new List<VisualElement>();
        private readonly Dictionary<VisualElement, Action<int>> adjustments = new Dictionary<VisualElement, Action<int>>();
        private readonly List<Action> refresh = new List<Action>();
        private readonly VisualElement parent;
        private readonly ScrollView scroll;
        private readonly ToolkitSettingsHelp help;
        public VisualElement First => controls.Count == 0 ? null : controls[0];
        public ToolkitSettingsRows(VisualElement parent, ScrollView scroll, ToolkitSettingsHelp help = null) { this.parent = parent; this.scroll = scroll; this.help = help; }

        // A negative value shows `unmatched` (e.g. Custom) while still cycling through the choices.
        public SettingSelector Choice(string name, string label, string[] choices, Func<int> value, Action<int> change, Func<bool> enabled = null,
            string disabledValue = "Unavailable", string unmatched = null, string description = null)
        {
            var row = Row(label);
            var control = new SettingSelector(name, choices);
            control.RegisterValueChangedCallback(e => { if (e.newValue >= 0 && e.newValue < choices.Length) change(e.newValue); });
            Action<int> adjust = delta => change(Cycle(value(), choices.Length, delta));
            control.Step = delta => adjust(delta == 0 ? 1 : delta);
            row.Add(control);
            Register(control, row, adjust, () => help.Show(label, description, choices.Length <= 8 ? choices : null, control.value));
            refresh.Add(() => {
                bool available = enabled == null || enabled();
                control.SetEnabled(available); row.EnableInClassList("disabled-row", !available);
                int current = value();
                control.SetValueWithoutNotify(Mathf.Clamp(current, -1, choices.Length - 1));
                if (!available) control.ShowCaption(disabledValue);
                else if (current < 0) control.ShowCaption(unmatched ?? choices[0]);
            });
            return control;
        }

        // Off/On on the same selector as every other choice: Left is Off, Right is On, and
        // Enter or a click on the value flips it.
        public SettingSelector Toggle(string name, string label, Func<bool> value, Action<bool> change, string description = null)
        {
            var row = Row(label);
            var control = new SettingSelector(name, OffOn);
            control.RegisterValueChangedCallback(e => change(e.newValue == 1));
            control.Step = delta => change(delta == 0 ? !value() : delta > 0);
            row.Add(control);
            Register(control, row, delta => change(delta > 0), () => help.Show(label, description, OffOn, control.value));
            refresh.Add(() => control.SetValueWithoutNotify(value() ? 1 : 0));
            return control;
        }

        public Button Binding(string name, string label, Func<string> value, Action select, string description = null)
        {
            var row = Row(label);
            var button = ToolkitMenuComponents.Button(row, name, "", select, "setting-binding");
            Register(button, row, null, () => help.Show(label, description, new[] { value() }, 0));
            refresh.Add(() => button.text = value()); return button;
        }

        public SliderInt Slider(string name, string label, int minimum, int maximum, Func<int> value, Action<int> change, Func<int, string> format,
            Func<bool> enabled = null, string valueName = null, string description = null, string disabledValue = "Unavailable")
        {
            var row = Row(label);
            var wrap = new VisualElement(); wrap.AddToClassList("setting-range");
            var text = new Label { name = valueName ?? name + "Value" }; text.AddToClassList("range-value");
            var slider = new SliderInt(minimum, maximum) { name = name }; slider.AddToClassList("setting-slider");
            slider.RegisterValueChangedCallback(e => change(e.newValue));
            wrap.Add(text); wrap.Add(slider); row.Add(wrap);
            Register(slider, row, delta => change(Mathf.Clamp(value() + delta, minimum, maximum)),
                () => help.Show(label, description, new[] { format(minimum) + "  to  " + format(maximum) }, -1));
            refresh.Add(() => { bool available = enabled == null || enabled(); slider.SetEnabled(available); slider.SetValueWithoutNotify(value()); text.text = available ? format(value()) : disabledValue; });
            return slider;
        }

        public void Heading(string text)
        {
            var heading = new Label(text) { pickingMode = PickingMode.Ignore }; heading.AddToClassList("setting-section"); parent.Add(heading);
        }
        private VisualElement Row(string label)
        {
            var row = new VisualElement(); row.AddToClassList("preference-row");
            var text = new Label(label) { pickingMode = PickingMode.Ignore }; text.AddToClassList("preference-label");
            row.Add(text); parent.Add(row); return row;
        }
        private void Register(VisualElement control, VisualElement row, Action<int> adjust, Action describe)
        {
            controls.Add(control); if (adjust != null) adjustments[control] = adjust;
            if (help == null) describe = null;
            control.RegisterCallback<FocusInEvent>(_ => { row.AddToClassList("focused-row"); scroll?.ScrollTo(row); help?.Focus(describe); });
            control.RegisterCallback<FocusOutEvent>(_ => row.RemoveFromClassList("focused-row"));
            row.RegisterCallback<PointerEnterEvent>(_ => help?.Hover(describe));
            row.RegisterCallback<PointerLeaveEvent>(_ => help?.Leave(describe));
        }
        public void AddNavigation(List<VisualElement> list) { foreach (var control in controls) if (control.enabledInHierarchy) list.Add(control); }
        public bool Adjust(VisualElement focused, NavigationMoveEvent.Direction direction)
        {
            if (direction != NavigationMoveEvent.Direction.Left && direction != NavigationMoveEvent.Direction.Right) return false;
            foreach (var entry in adjustments)
                if (entry.Key.enabledInHierarchy && (entry.Key == focused || entry.Key.Contains(focused)))
                { entry.Value(direction == NavigationMoveEvent.Direction.Left ? -1 : 1); return true; }
            return false;
        }
        public void Refresh() { foreach (var action in refresh) action(); help?.Refresh(); }
        public static int Cycle(int value, int count, int delta) => (value + delta + count) % count;
    }
}
