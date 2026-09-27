using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    internal sealed class ToolkitSettingsRows
    {
        private readonly List<VisualElement> controls = new List<VisualElement>();
        private readonly Dictionary<VisualElement, Action<int>> adjustments = new Dictionary<VisualElement, Action<int>>();
        private readonly List<Action> refresh = new List<Action>();
        private readonly VisualElement parent;
        private readonly ScrollView scroll;
        public VisualElement First => controls.Count == 0 ? null : controls[0];
        public ToolkitSettingsRows(VisualElement parent, ScrollView scroll) { this.parent = parent; this.scroll = scroll; }

        // A negative value shows `unmatched` (e.g. Custom) while still cycling through the choices.
        public DropdownField Choice(string name, string label, string[] choices, Func<int> value, Action<int> change, Func<bool> enabled = null, string disabledValue = "Unavailable", string unmatched = null)
        {
            var row = Row(label);
            var options = new List<string>(choices);
            var control = new DropdownField(options, Math.Max(0, value())) { name = name };
            control.AddToClassList("setting-choice");
            control.RegisterValueChangedCallback(e => { int index = options.IndexOf(e.newValue); if (index >= 0) change(index); });
            row.Add(control); Register(control, row, delta => change(Cycle(value(), options.Count, delta)));
            refresh.Add(() => {
                bool available = enabled == null || enabled();
                control.SetEnabled(available); row.EnableInClassList("disabled-row", !available);
                int current = value();
                control.SetValueWithoutNotify(!available ? disabledValue
                    : current < 0 && unmatched != null ? unmatched : options[Mathf.Clamp(current, 0, options.Count - 1)]);
            });
            return control;
        }

        public Toggle Toggle(string name, string label, Func<bool> value, Action<bool> change)
        {
            var row = Row(label);
            var control = new Toggle { name = name }; control.AddToClassList("setting-toggle"); row.Add(control);
            control.RegisterValueChangedCallback(e => change(e.newValue));
            Register(control, row, delta => change(delta > 0));
            refresh.Add(() => { control.SetValueWithoutNotify(value()); control.EnableInClassList("is-on", value()); });
            return control;
        }

        public Button Binding(string name, string label, Func<string> value, Action select)
        {
            var row = Row(label);
            var button = ToolkitMenuComponents.Button(row, name, "", select, "setting-binding");
            Register(button, row, null); refresh.Add(() => button.text = value()); return button;
        }

        public SliderInt Slider(string name, string label, int minimum, int maximum, Func<int> value, Action<int> change, Func<int, string> format, Func<bool> enabled = null, string valueName = null)
        {
            var row = Row(label);
            var wrap = new VisualElement(); wrap.AddToClassList("setting-range");
            var text = new Label { name = valueName ?? name + "Value" }; text.AddToClassList("range-value");
            var slider = new SliderInt(minimum, maximum) { name = name }; slider.AddToClassList("setting-slider");
            slider.RegisterValueChangedCallback(e => change(e.newValue));
            wrap.Add(text); wrap.Add(slider); row.Add(wrap);
            Register(slider, row, delta => change(Mathf.Clamp(value() + delta, minimum, maximum)));
            refresh.Add(() => { bool available = enabled == null || enabled(); slider.SetEnabled(available); slider.SetValueWithoutNotify(value()); text.text = available ? format(value()) : "Unavailable"; });
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
        private void Register(VisualElement control, VisualElement row, Action<int> adjust)
        {
            controls.Add(control); if (adjust != null) adjustments[control] = adjust;
            control.RegisterCallback<FocusInEvent>(_ => { row.AddToClassList("focused-row"); scroll?.ScrollTo(row); });
            control.RegisterCallback<FocusOutEvent>(_ => row.RemoveFromClassList("focused-row"));
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
        public void Refresh() { foreach (var action in refresh) action(); }
        public static int Cycle(int value, int count, int delta) => (value + delta + count) % count;
        public static string OnOff(bool value) => value ? "On" : "Off";
    }
}
