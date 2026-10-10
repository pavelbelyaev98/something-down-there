using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace SomethingDownThere
{
    internal static class ToolkitMenuComponents
    {
        public static Button Button(VisualElement parent, string name, string caption, Action action, string variant = "", bool enabled = true)
        {
            var button = new Button(action) { name = name, text = caption };
            button.AddToClassList("menu-button");
            foreach (string token in variant.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) button.AddToClassList(token);
            button.SetEnabled(enabled); parent.Add(button); return button;
        }
    }

    // A settings value stepped in place: ◀ value ▶, with one pip per choice beneath (a
    // single track when there are too many to count). There is no popup list: the arrows
    // step it (Step(-1/+1), as Left/Right do), Enter or a click on the value advances it
    // (Step(0)), and `value` is the choice index.
    public sealed class SettingSelector : VisualElement, INotifyValueChanged<int>
    {
        private const int PipLimit = 8;
        private readonly IReadOnlyList<string> choices;
        private readonly Label caption;
        private readonly VisualElement marker;
        private readonly List<VisualElement> pips = new List<VisualElement>();
        private int index;
        private string shown;
        internal Action<int> Step;

        public SettingSelector(string name, IReadOnlyList<string> choices)
        {
            this.name = name; this.choices = choices; focusable = true;
            AddToClassList("setting-selector");
            var middle = new VisualElement(); middle.AddToClassList("selector-middle");
            caption = new Label { pickingMode = PickingMode.Ignore }; caption.AddToClassList("selector-value");
            var track = new VisualElement { pickingMode = PickingMode.Ignore }; track.AddToClassList("selector-pips");
            if (choices.Count <= PipLimit)
                for (int i = 0; i < choices.Count; i++)
                {
                    var pip = new VisualElement { pickingMode = PickingMode.Ignore }; pip.AddToClassList("selector-pip");
                    track.Add(pip); pips.Add(pip);
                }
            else
            {
                track.AddToClassList("selector-track");
                marker = new VisualElement { pickingMode = PickingMode.Ignore }; marker.AddToClassList("selector-marker");
                marker.style.width = Length.Percent(100f / choices.Count); track.Add(marker);
            }
            middle.Add(caption); middle.Add(track);
            middle.AddManipulator(new Clickable(() => Step?.Invoke(0)));
            Add(Arrow(-1)); Add(middle); Add(Arrow(1));
            RegisterCallback<NavigationSubmitEvent>(e => { Step?.Invoke(0); e.StopPropagation(); });
            Paint();
        }

        public IReadOnlyList<string> Choices => choices;
        public string Caption => caption.text;

        public int value
        {
            get => index;
            set
            {
                if (value == index) return;
                int previous = index;
                SetValueWithoutNotify(value);
                using (var change = ChangeEvent<int>.GetPooled(previous, index)) { change.target = this; SendEvent(change); }
            }
        }

        public void SetValueWithoutNotify(int newValue) { index = newValue; shown = null; Paint(); }

        // Text in place of the choice (Unavailable, Custom) while the pips keep the index.
        internal void ShowCaption(string text) { shown = text; Paint(); }

        private void Paint()
        {
            caption.text = shown ?? (index >= 0 && index < choices.Count ? choices[index] : "");
            for (int i = 0; i < pips.Count; i++) pips[i].EnableInClassList("selector-pip-on", i == index);
            if (marker == null) return;
            marker.style.left = Length.Percent(Mathf.Max(0, index) * 100f / choices.Count);
            marker.visible = index >= 0;
        }

        private VisualElement Arrow(int direction)
        {
            var arrow = new VisualElement(); arrow.AddToClassList("selector-arrow");
            arrow.AddManipulator(new Clickable(() => Step?.Invoke(direction)));
            arrow.generateVisualContent += context =>
            {
                var centre = arrow.contentRect.center;
                float half = 5.5f * direction, height = 7.5f;
                var painter = context.painter2D;
                painter.fillColor = arrow.resolvedStyle.color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(centre.x - half, centre.y - height));
                painter.LineTo(new Vector2(centre.x + half, centre.y));
                painter.LineTo(new Vector2(centre.x - half, centre.y + height));
                painter.ClosePath();
                painter.Fill();
            };
            return arrow;
        }
    }

    // The settings screen's right-hand panel: what the row under focus or the pointer
    // does, and every choice it offers with the current one marked.
    internal sealed class ToolkitSettingsHelp
    {
        private readonly Label title, text;
        private readonly VisualElement options;
        private Action shown;

        public ToolkitSettingsHelp(VisualElement root)
        {
            title = root.Q<Label>("settingsHelpTitle"); text = root.Q<Label>("settingsHelpText"); options = root.Q("settingsHelpOptions");
        }

        public void Describe(Action describe) { shown = describe; describe?.Invoke(); }
        public void Refresh() => shown?.Invoke();

        public void Show(string heading, string description, IReadOnlyList<string> choices = null, int selected = -1)
        {
            title.text = heading.ToUpperInvariant();
            text.text = description ?? "";
            GameMenuView.Show(text, !string.IsNullOrEmpty(text.text));
            options.Clear();
            if (choices == null) return;
            for (int i = 0; i < choices.Count; i++)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore }; line.AddToClassList("help-option");
                line.EnableInClassList("help-option-current", i == selected);
                var bullet = new VisualElement { pickingMode = PickingMode.Ignore }; bullet.AddToClassList("help-bullet");
                var label = new Label(choices[i]) { pickingMode = PickingMode.Ignore }; label.AddToClassList("help-option-text");
                line.Add(bullet); line.Add(label); options.Add(line);
            }
        }
    }

    internal sealed class ToolkitTabs
    {
        private readonly List<Button> buttons;
        public ToolkitTabs(VisualElement root, Action<int> selected)
        {
            buttons = root.Query<Button>().ToList();
            for (int i = 0; i < buttons.Count; i++)
            {
                int index = i;
                buttons[i].clicked += () => selected(index);
            }
        }
        public void Select(int index)
        {
            for (int i = 0; i < buttons.Count; i++) buttons[i].EnableInClassList("selected-tab", i == index);
        }
        public void AddNavigation(List<VisualElement> controls) => controls.AddRange(buttons);
    }

    // The shared menu frame becomes a compact dialog; all dialogs use its single heading,
    // message area and standard action components rather than introducing another layout.
    internal sealed class ToolkitDialog
    {
        private readonly Label title, subtitle;
        private readonly VisualElement body;
        public ToolkitDialog(Label title, Label subtitle, VisualElement body) { this.title = title; this.subtitle = subtitle; this.body = body; }
        public Label Set(string heading, string message)
        {
            title.text = heading; subtitle.text = ""; body.Clear();
            var label = new Label(message) { name = "Body", enableRichText = false, pickingMode = PickingMode.Ignore };
            label.AddToClassList("body"); body.Add(label); return label;
        }
    }
}
