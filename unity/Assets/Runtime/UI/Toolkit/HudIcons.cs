using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    // The HUD's small flat glyphs, drawn as vector shapes into their element's box so they stay
    // sharp at any UI scale: a bolt on the battery bar, a stack of banknotes, the bag and a lamp.
    internal static class HudIcons
    {
        private static readonly Color Ink = new Color(0.12f, 0.09f, 0.06f, 0.85f);

        public static void Bolt(VisualElement element) => Draw(element, (painter, box) =>
        {
            painter.fillColor = new Color(0.96f, 1f, 0.98f);
            Polygon(painter, box, false, (0.62f, 0f), (0.1f, 0.58f), (0.47f, 0.58f), (0.36f, 1f), (0.9f, 0.4f), (0.53f, 0.4f));
        });

        // Three bills fanned up and to the right, the front one held by a paper band.
        public static void Money(VisualElement element) => Draw(element, (painter, box) =>
        {
            var colors = new[] { new Color(0.25f, 0.45f, 0.22f), new Color(0.33f, 0.56f, 0.28f), new Color(0.44f, 0.68f, 0.35f) };
            for (int i = 0; i < 3; i++)
            {
                float offset = (2 - i) * 0.07f;
                var bill = Rect(box, 0.02f + offset, 0.30f - offset * 2.2f, 0.84f, 0.62f);
                painter.fillColor = colors[i]; painter.strokeColor = Ink; painter.lineWidth = 1.2f;
                RoundRect(painter, bill, bill.height * 0.14f, true);
            }
            var front = Rect(box, 0.02f, 0.30f, 0.84f, 0.62f);
            painter.strokeColor = new Color(0.78f, 0.9f, 0.68f, 0.7f); painter.lineWidth = 1f;
            var inner = new Rect(front.x + front.width * 0.08f, front.y + front.height * 0.18f, front.width * 0.84f, front.height * 0.64f);
            painter.BeginPath(); RoundRectPath(painter, inner, inner.height * 0.12f); painter.Stroke();
            painter.fillColor = new Color(0.95f, 0.79f, 0.3f); painter.strokeColor = Ink; painter.lineWidth = 1.2f;
            var band = new Rect(front.x + front.width * 0.4f, front.y - 1f, front.width * 0.17f, front.height + 2f);
            painter.BeginPath(); RoundRectPath(painter, band, 1.5f); painter.Fill(); painter.Stroke();
        });

        // A canvas sack tied at the neck.
        public static void Bag(VisualElement element) => Draw(element, (painter, box) =>
        {
            painter.fillColor = new Color(0.9f, 0.76f, 0.53f); painter.strokeColor = Ink; painter.lineWidth = 1.3f;
            painter.BeginPath();
            painter.MoveTo(P(box, 0.36f, 0.3f));
            painter.BezierCurveTo(P(box, 0.06f, 0.48f), P(box, 0.02f, 1f), P(box, 0.5f, 1f));
            painter.BezierCurveTo(P(box, 0.98f, 1f), P(box, 0.94f, 0.48f), P(box, 0.64f, 0.3f));
            painter.ClosePath(); painter.Fill(); painter.Stroke();
            painter.fillColor = new Color(0.8f, 0.64f, 0.41f);
            painter.BeginPath();
            painter.MoveTo(P(box, 0.34f, 0.04f)); painter.LineTo(P(box, 0.66f, 0.04f));
            painter.LineTo(P(box, 0.58f, 0.3f)); painter.LineTo(P(box, 0.42f, 0.3f));
            painter.ClosePath(); painter.Fill(); painter.Stroke();
            painter.strokeColor = new Color(0.36f, 0.24f, 0.13f); painter.lineWidth = 2f;
            painter.BeginPath(); painter.MoveTo(P(box, 0.33f, 0.31f)); painter.LineTo(P(box, 0.67f, 0.31f)); painter.Stroke();
        });

        // A work lamp: bail, cap, lit glass and base.
        public static void Lamp(VisualElement element) => Draw(element, (painter, box) =>
        {
            painter.strokeColor = Ink; painter.lineWidth = 1.3f;
            painter.BeginPath(); painter.Arc(P(box, 0.5f, 0.22f), box.width * 0.2f, 180f, 360f); painter.Stroke();
            painter.fillColor = new Color(1f, 0.83f, 0.42f);
            RoundRect(painter, Rect(box, 0.27f, 0.32f, 0.46f, 0.48f), box.width * 0.07f, true);
            painter.strokeColor = new Color(0.62f, 0.45f, 0.18f, 0.8f); painter.lineWidth = 1f;
            painter.BeginPath(); painter.MoveTo(P(box, 0.5f, 0.36f)); painter.LineTo(P(box, 0.5f, 0.76f)); painter.Stroke();
            painter.fillColor = new Color(0.3f, 0.32f, 0.33f); painter.strokeColor = Ink; painter.lineWidth = 1.3f;
            RoundRect(painter, Rect(box, 0.2f, 0.22f, 0.6f, 0.12f), 1.5f, true);
            RoundRect(painter, Rect(box, 0.2f, 0.8f, 0.6f, 0.14f), 1.5f, true);
        });

        private static void Draw(VisualElement element, Action<Painter2D, Rect> draw)
        {
            element.generateVisualContent += context =>
            {
                var box = element.contentRect;
                if (box.width > 2 && box.height > 2) draw(context.painter2D, box);
            };
        }

        private static Vector2 P(Rect box, float x, float y) => new Vector2(box.x + box.width * x, box.y + box.height * y);
        private static Rect Rect(Rect box, float x, float y, float width, float height) =>
            new Rect(box.x + box.width * x, box.y + box.height * y, box.width * width, box.height * height);

        private static void Polygon(Painter2D painter, Rect box, bool stroke, params (float x, float y)[] points)
        {
            painter.BeginPath();
            painter.MoveTo(P(box, points[0].x, points[0].y));
            for (int i = 1; i < points.Length; i++) painter.LineTo(P(box, points[i].x, points[i].y));
            painter.ClosePath();
            painter.Fill();
            if (stroke) painter.Stroke();
        }

        private static void RoundRect(Painter2D painter, Rect rect, float radius, bool stroke)
        {
            painter.BeginPath(); RoundRectPath(painter, rect, radius); painter.Fill();
            if (stroke) painter.Stroke();
        }

        private static void RoundRectPath(Painter2D painter, Rect rect, float radius)
        {
            painter.MoveTo(new Vector2(rect.xMin + radius, rect.yMin));
            painter.ArcTo(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), radius);
            painter.ArcTo(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), radius);
            painter.ArcTo(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), radius);
            painter.ArcTo(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), radius);
            painter.ClosePath();
        }
    }
}
