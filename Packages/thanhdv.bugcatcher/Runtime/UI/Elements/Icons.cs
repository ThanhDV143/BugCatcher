using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>The design's icon set: 24 px grid, 1.8 px stroke, round caps. Paths are SVG "d" strings.</summary>
    internal static class Icons
    {
        private const string Circle = "a9 9 0 1 0 18 0a9 9 0 1 0-18 0";

        private static readonly Dictionary<string, string> paths = new Dictionary<string, string>
        {
            { "info", "M3 12" + Circle + "M12 11v5M12 8h.01" },
            { "warning", "M12 3.5 2.5 20h19L12 3.5ZM12 10v4.5M12 17.5h.01" },
            { "error", "M3 12" + Circle + "M9 9l6 6M15 9l-6 6" },
            { "console", "M5 4h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2ZM7 9l3 3-3 3M12 15h5" },
            { "hud", "M4 18a8 8 0 1 1 16 0M12 18l4-6" },
            { "audio", "M3 12h1M7 8v8M11 5v14M15 9v6M19 11v2" },
            { "system", "M8 6h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2ZM10 10h4v4h-4zM9 3v3M15 3v3M9 18v3M15 18v3M3 9h3M3 15h3M18 9h3M18 15h3" },
            { "settings", "M4 7h10M18 7h2M4 17h4M12 17h8M14 7a2 2 0 1 0 4 0a2 2 0 1 0-4 0M8 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0" },
            { "search", "M4.5 11a6.5 6.5 0 1 0 13 0a6.5 6.5 0 1 0-13 0M20 20l-4.2-4.2" },
            { "close", "M6 6l12 12M18 6 6 18" },
            { "group", "M12 3 21 8 12 13 3 8 12 3ZM3 13l9 5 9-5" },
            { "clear", "M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3" },
            { "share", "M12 3v12M7 8l5-5 5 5M5 14v6h14v-6" },
            { "save", "M12 4v11M7 10l5 5 5-5M5 20h14" },
            { "copy", "M10 8h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2h-8a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2ZM16 8V5a1 1 0 0 0-1-1H5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3" },
            { "run", "M5 12h14M13 6l6 6-6 6" },
            { "history", "M4 12a8 8 0 1 0 2.4-5.7L4 8.5M4 4v4.5h4.5M12 8v4l3 2" },
            { "back", "M15 6l-6 6 6 6" },
            { "next", "M9 6l6 6-6 6" },
            { "screenshot", "M4 8h3l2-3h6l2 3h3v11H4ZM8.5 13a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0-7 0" },
            { "preview", "M3 12s3.5-6 9-6 9 6 9 6-3.5 6-9 6-9-6-9-6ZM9.5 12a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0-5 0" },
            { "hide", "M3 3l18 18M10.6 6.1A9.8 9.8 0 0 1 12 6c5 0 9 6 9 6a15 15 0 0 1-2.6 3.1M6.4 7.6C4.3 9.1 3 12 3 12s4 6 9 6a8.6 8.6 0 0 0 4-1" },
            { "reset", "M20 12a8 8 0 1 1-2.3-5.6L20 8.5M20 4v4.5h-4.5" },
            { "keyboard", "M5 6h14a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2ZM7 10h.01M11 10h.01M15 10h.01M7 14h10" },
            { "gesture", "M9 11V5a1.5 1.5 0 0 1 3 0v5M12 10V4a1.5 1.5 0 0 1 3 0v6M15 10V6a1.5 1.5 0 0 1 3 0v8a6 6 0 0 1-6 6h-1a6 6 0 0 1-5-2.7L3.5 13a1.5 1.5 0 0 1 2.5-1.7L9 14" },
            { "bug", "M9 7a3 3 0 0 1 6 0M7 10h10v5a5 5 0 0 1-10 0v-5ZM12 10v10M4 12h3M17 12h3M5 18l2.5-1.5M19 18l-2.5-1.5M5 7l2.5 2M19 7l-2.5 2" },
            { "layout", "M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z" },
            { "check", "M5 12.5l4.5 4.5L19 7" },
            { "expand", "M8 4H4v4M16 4h4v4M8 20H4v-4M16 20h4v-4" },
            { "more", "M5 12h.01M12 12h.01M19 12h.01" },
            { "plus", "M12 5v14M5 12h14" }
        };

        private static readonly Dictionary<string, List<SvgPath.SubPath>> parsed = new Dictionary<string, List<SvgPath.SubPath>>();

        public static List<SvgPath.SubPath> Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (parsed.TryGetValue(name, out var result)) return result;
            if (!paths.TryGetValue(name, out string d)) return null;

            result = SvgPath.Parse(d);
            parsed.Add(name, result);
            return result;
        }

        public static string ForKind(LogKind kind)
        {
            switch (kind)
            {
                case LogKind.Warning: return "warning";
                case LogKind.Error: return "error";
                default: return "info";
            }
        }
    }

    /// <summary>Stroked vector icon. Colour comes from the element's USS "color".</summary>
    internal class IconElement : VisualElement
    {
        private string icon;
        private float strokeWidth = 1.8f;

        public IconElement(string icon, float size = 20f)
        {
            AddToClassList("ic");
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            this.icon = icon;
            generateVisualContent += Draw;
            // Repaint when the resolved colour may have changed (theme or state classes).
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        public string Icon
        {
            get => icon;
            set
            {
                if (icon == value) return;
                icon = value;
                MarkDirtyRepaint();
            }
        }

        public float StrokeWidth
        {
            get => strokeWidth;
            set
            {
                strokeWidth = value;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            var subPaths = Icons.Get(icon);
            Rect rect = contentRect;
            if (subPaths == null || rect.width <= 0f) return;

            float scale = rect.width / 24f;
            Painter2D painter = context.painter2D;
            painter.strokeColor = resolvedStyle.color;
            painter.lineWidth = Mathf.Max(1f, strokeWidth * scale);
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            SvgPath.Stroke(painter, subPaths, scale, Vector2.zero);
        }
    }

    /// <summary>Minimal SVG path parser (M L H V C S A Z, absolute and relative) producing lines and cubics.</summary>
    internal static class SvgPath
    {
        public struct Segment
        {
            public bool IsCubic;
            public Vector2 C1;
            public Vector2 C2;
            public Vector2 End;
        }

        public class SubPath
        {
            public Vector2 Start;
            public bool Closed;
            public readonly List<Segment> Segments = new List<Segment>();
        }

        public static void Stroke(Painter2D painter, List<SvgPath.SubPath> subPaths, float scale, Vector2 offset)
        {
            foreach (SubPath sub in subPaths)
            {
                painter.BeginPath();
                painter.MoveTo(offset + sub.Start * scale);
                if (sub.Segments.Count == 1 && !sub.Segments[0].IsCubic && (sub.Segments[0].End - sub.Start).sqrMagnitude < 0.01f)
                {
                    // Dots ("h.01"): stretch slightly so the round caps render a visible dot.
                    painter.LineTo(offset + (sub.Start + new Vector2(0.05f, 0f)) * scale);
                }
                else
                {
                    foreach (Segment s in sub.Segments)
                    {
                        if (s.IsCubic) painter.BezierCurveTo(offset + s.C1 * scale, offset + s.C2 * scale, offset + s.End * scale);
                        else painter.LineTo(offset + s.End * scale);
                    }
                }
                if (sub.Closed) painter.ClosePath();
                painter.Stroke();
            }
        }

        public static List<SubPath> Parse(string d)
        {
            var result = new List<SubPath>();
            SubPath current = null;
            Vector2 pos = Vector2.zero;
            Vector2 lastControl = Vector2.zero;
            char command = 'M';
            char previous = ' ';
            int i = 0;

            while (true)
            {
                SkipSeparators(d, ref i);
                if (i >= d.Length) break;

                if (char.IsLetter(d[i]))
                {
                    command = d[i++];
                }
                else if (command == 'M')
                {
                    command = 'L'; // Implicit lineto after moveto.
                }
                else if (command == 'm')
                {
                    command = 'l';
                }

                bool relative = char.IsLower(command);
                Vector2 origin = relative ? pos : Vector2.zero;
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                    {
                        pos = origin + new Vector2(Number(d, ref i), Number(d, ref i));
                        current = new SubPath { Start = pos };
                        result.Add(current);
                        break;
                    }
                    case 'L':
                        pos = origin + new Vector2(Number(d, ref i), Number(d, ref i));
                        Line(current, pos);
                        break;
                    case 'H':
                        pos = new Vector2((relative ? pos.x : 0f) + Number(d, ref i), pos.y);
                        Line(current, pos);
                        break;
                    case 'V':
                        pos = new Vector2(pos.x, (relative ? pos.y : 0f) + Number(d, ref i));
                        Line(current, pos);
                        break;
                    case 'C':
                    {
                        Vector2 c1 = origin + new Vector2(Number(d, ref i), Number(d, ref i));
                        Vector2 c2 = origin + new Vector2(Number(d, ref i), Number(d, ref i));
                        Vector2 end = origin + new Vector2(Number(d, ref i), Number(d, ref i));
                        Cubic(current, c1, c2, end);
                        lastControl = c2;
                        pos = end;
                        break;
                    }
                    case 'S':
                    {
                        bool chained = "CcSs".IndexOf(previous) >= 0;
                        Vector2 c1 = chained ? 2f * pos - lastControl : pos;
                        Vector2 c2 = origin + new Vector2(Number(d, ref i), Number(d, ref i));
                        Vector2 end = origin + new Vector2(Number(d, ref i), Number(d, ref i));
                        Cubic(current, c1, c2, end);
                        lastControl = c2;
                        pos = end;
                        break;
                    }
                    case 'A':
                    {
                        float rx = Number(d, ref i);
                        float ry = Number(d, ref i);
                        float rotation = Number(d, ref i);
                        bool largeArc = Flag(d, ref i);
                        bool sweep = Flag(d, ref i);
                        Vector2 end = origin + new Vector2(Number(d, ref i), Number(d, ref i));
                        Arc(current, pos, rx, ry, rotation, largeArc, sweep, end);
                        pos = end;
                        break;
                    }
                    case 'Z':
                        if (current != null)
                        {
                            current.Closed = true;
                            pos = current.Start;
                        }
                        break;
                    default:
                        throw new FormatException("Unsupported SVG command " + command);
                }

                previous = command;
            }

            return result;
        }

        private static void Line(SubPath sub, Vector2 end) => sub?.Segments.Add(new Segment { End = end });

        private static void Cubic(SubPath sub, Vector2 c1, Vector2 c2, Vector2 end) =>
            sub?.Segments.Add(new Segment { IsCubic = true, C1 = c1, C2 = c2, End = end });

        // SVG endpoint arc -> centre parameterisation -> cubic segments of at most 90°.
        private static void Arc(SubPath sub, Vector2 from, float rx, float ry, float rotationDeg, bool largeArc, bool sweep, Vector2 to)
        {
            if (rx == 0f || ry == 0f || from == to)
            {
                Line(sub, to);
                return;
            }

            rx = Mathf.Abs(rx);
            ry = Mathf.Abs(ry);
            float phi = rotationDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
            Vector2 half = (from - to) * 0.5f;
            var p = new Vector2(cos * half.x + sin * half.y, -sin * half.x + cos * half.y);

            float lambda = p.x * p.x / (rx * rx) + p.y * p.y / (ry * ry);
            if (lambda > 1f)
            {
                float s = Mathf.Sqrt(lambda);
                rx *= s;
                ry *= s;
            }

            float num = rx * rx * ry * ry - rx * rx * p.y * p.y - ry * ry * p.x * p.x;
            float den = rx * rx * p.y * p.y + ry * ry * p.x * p.x;
            float coef = Mathf.Sqrt(Mathf.Max(0f, num / den)) * (largeArc == sweep ? -1f : 1f);
            var cp = new Vector2(coef * rx * p.y / ry, -coef * ry * p.x / rx);
            Vector2 mid = (from + to) * 0.5f;
            var center = new Vector2(cos * cp.x - sin * cp.y + mid.x, sin * cp.x + cos * cp.y + mid.y);

            float theta1 = Angle(new Vector2(1f, 0f), new Vector2((p.x - cp.x) / rx, (p.y - cp.y) / ry));
            float delta = Angle(new Vector2((p.x - cp.x) / rx, (p.y - cp.y) / ry), new Vector2((-p.x - cp.x) / rx, (-p.y - cp.y) / ry));
            if (!sweep && delta > 0f) delta -= 2f * Mathf.PI;
            else if (sweep && delta < 0f) delta += 2f * Mathf.PI;

            int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta) / (Mathf.PI * 0.5f) - 0.001f));
            float step = delta / segments;
            float k = 4f / 3f * Mathf.Tan(step / 4f);
            float t = theta1;
            for (int n = 0; n < segments; n++)
            {
                float t2 = t + step;
                Vector2 e1 = Ellipse(center, rx, ry, cos, sin, t);
                Vector2 e2 = Ellipse(center, rx, ry, cos, sin, t2);
                Vector2 d1 = Derivative(rx, ry, cos, sin, t);
                Vector2 d2 = Derivative(rx, ry, cos, sin, t2);
                Cubic(sub, e1 + k * d1, e2 - k * d2, n == segments - 1 ? to : e2);
                t = t2;
            }
        }

        private static Vector2 Ellipse(Vector2 c, float rx, float ry, float cos, float sin, float t) =>
            new Vector2(c.x + rx * Mathf.Cos(t) * cos - ry * Mathf.Sin(t) * sin, c.y + rx * Mathf.Cos(t) * sin + ry * Mathf.Sin(t) * cos);

        private static Vector2 Derivative(float rx, float ry, float cos, float sin, float t) =>
            new Vector2(-rx * Mathf.Sin(t) * cos - ry * Mathf.Cos(t) * sin, -rx * Mathf.Sin(t) * sin + ry * Mathf.Cos(t) * cos);

        private static float Angle(Vector2 u, Vector2 v)
        {
            float angle = Mathf.Acos(Mathf.Clamp(Vector2.Dot(u, v) / (u.magnitude * v.magnitude), -1f, 1f));
            return u.x * v.y - u.y * v.x < 0f ? -angle : angle;
        }

        private static void SkipSeparators(string d, ref int i)
        {
            while (i < d.Length && (d[i] == ' ' || d[i] == ',' || d[i] == '\n' || d[i] == '\t')) i++;
        }

        private static bool Flag(string d, ref int i)
        {
            SkipSeparators(d, ref i);
            return d[i++] == '1';
        }

        private static float Number(string d, ref int i)
        {
            SkipSeparators(d, ref i);
            int start = i;
            if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
            bool dot = false;
            while (i < d.Length)
            {
                char c = d[i];
                if (char.IsDigit(c))
                {
                    i++;
                }
                else if (c == '.' && !dot)
                {
                    dot = true;
                    i++;
                }
                else
                {
                    break;
                }
            }
            return float.Parse(d.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
