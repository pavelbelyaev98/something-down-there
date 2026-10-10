using System;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    // Station rows: one table row per upgrade track, refill service or carried
    // find. The row itself is the activation target, so a single click or submit
    // commits the trade the row advertises — no selection step, no confirm
    // control, and nothing changes size on hover.
    internal static class ToolkitStationRows
    {
        // The only interactive element in a station row: its own button.
        public static Button Button(VisualElement parent, string name, string caption, Action action, string className, bool enabled = true)
        {
            var button = new Button(action) { name = name, text = caption };
            foreach (string token in Tokens(className)) button.AddToClassList(token);
            button.SetEnabled(enabled);
            parent.Add(button);
            return button;
        }

        public static VisualElement Block(VisualElement parent, string name, string className)
        {
            var block = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            foreach (string token in Tokens(className)) block.AddToClassList(token);
            parent.Add(block);
            return block;
        }

        public static Label Text(VisualElement parent, string name, string text, string className)
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore, enableRichText = false };
            foreach (string token in Tokens(className)) label.AddToClassList(token);
            label.EnableInClassList("hidden", string.IsNullOrEmpty(text));
            parent.Add(label);
            return label;
        }

        // How far a track has come: one bar filled owned/total. The exact numbers stay in
        // the price button's tooltip.
        public static void Progress(VisualElement parent, string name, int owned, int total)
        {
            var track = Block(parent, name, "station-progress");
            var fill = Block(track, name + " fill", "station-progress-fill");
            fill.style.width = Length.Percent(100f * owned / total);
        }

        private static string[] Tokens(string className) =>
            className.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
