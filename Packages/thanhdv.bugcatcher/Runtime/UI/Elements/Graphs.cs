using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>
    /// Base for custom visuals. Colours come from USS so themes swap them:
    /// "color" = main mark, "border-top-color" = grid, "border-left-color" = secondary mark.
    /// </summary>
    internal abstract class PaintedElement : VisualElement
    {
        protected PaintedElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        protected abstract void Draw(MeshGenerationContext context);

        protected Color MainColor => resolvedStyle.color;
        protected Color GridColor => resolvedStyle.borderTopColor;
        protected Color SecondaryColor => resolvedStyle.borderLeftColor;

        // Compare against the last draw at quarter-point resolution. The mesh keeps full precision.
        protected static int HeightKey(float y) => (int)(y * 4f + 0.5f);

        protected static void HorizontalLines(Painter2D painter, Rect rect, float[] fractions, Color color)
        {
            if (fractions == null || fractions.Length == 0) return;

            painter.strokeColor = color;
            painter.lineWidth = 1f;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            foreach (float f in fractions)
            {
                float y = Mathf.Clamp(rect.height * f, 0.5f, rect.height - 0.5f);
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(rect.width, y));
            }
            painter.Stroke();
        }
    }

    /// <summary>One series line graph over a <see cref="SampleHistory"/>.</summary>
    internal class LineGraph : PaintedElement
    {
        private SampleHistory data;
        private Func<float> maxValue;
        private readonly float[] grid;

        public LineGraph(float[] gridFractions = null)
        {
            AddToClassList("line-graph");
            grid = gridFractions;
        }

        public void Bind(SampleHistory history, Func<float> max)
        {
            data = history;
            maxValue = max;
            MarkDirtyRepaint();
        }

        protected override void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            Painter2D painter = context.painter2D;
            HorizontalLines(painter, rect, grid, GridColor);
            if (data == null || data.Count < 2) return;

            float max = Mathf.Max(1e-3f, maxValue());
            float step = rect.width / (data.Capacity - 1);
            float x0 = rect.width - (data.Count - 1) * step;
            painter.strokeColor = MainColor;
            painter.lineWidth = 1.5f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            for (int i = 0; i < data.Count; i++)
            {
                float y = rect.height - data[i] / max * rect.height;
                var point = new Vector2(x0 + i * step, Mathf.Clamp(y, 1f, rect.height - 1f));
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }
            painter.Stroke();
        }
    }

    /// <summary>
    /// One bar per frame, height = frame time on a fixed scale, coloured by the FPS status thresholds.
    /// Grid lines mark 60 and 30 FPS; frames slower than <see cref="MaxMs"/> reach the top.
    /// USS: "color" = good, "border-left-color" = caution, "border-right-color" = critical, "border-top-color" = grid.
    /// </summary>
    internal class FrameTimeGraph : PaintedElement
    {
        private const float MaxMs = 50f;
        private static readonly float[] grid = { 1f - 1000f / 60f / MaxMs, 1f - 1000f / 30f / MaxMs };

        private SampleHistory data;
        private GraphMesh mesh;
        private int[] drawnBars;
        private Color drawnGrid, drawnGood, drawnCaution, drawnCritical;
        private Vector2 layoutSize;
        private int layoutCount;

        public FrameTimeGraph()
        {
            AddToClassList("frame-graph");
        }

        public void Bind(SampleHistory history)
        {
            data = history;
            mesh = new GraphMesh(grid.Length + history.Capacity);
            drawnBars = new int[history.Capacity];
            layoutCount = -1;
            MarkDirtyRepaint();
        }

        public void RepaintIfChanged()
        {
            Rect rect = contentRect;
            if (data == null || !(rect.width > 0f && rect.height > 0f)) return;

            int count = data.Count;
            if (layoutSize.x != rect.width || layoutSize.y != rect.height || layoutCount != count ||
                !drawnGrid.Equals(GridColor) || !drawnGood.Equals(MainColor) ||
                !drawnCaution.Equals(SecondaryColor) || !drawnCritical.Equals(resolvedStyle.borderRightColor))
            {
                MarkDirtyRepaint();
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float ms = data[i];
                if (drawnBars[i] == BarKey(BarTop(ms, rect.height), BarStatus(ms))) continue;
                MarkDirtyRepaint();
                return;
            }
        }

        private static int BarKey(float top, int status) => HeightKey(top) * 3 + status;

        private static int BarStatus(float ms)
        {
            float fps = 1000f / Mathf.Max(ms, 0.01f);
            // RoundToInt rounds half to even, so 49.5 and 29.5 enter the higher band.
            if (fps >= PerformanceService.GoodFps - 0.5f) return 0;
            return fps >= PerformanceService.CautionFps - 0.5f ? 1 : 2;
        }

        private static float BarTop(float ms, float height) =>
            height - Mathf.Clamp(ms / MaxMs * height, 1f, height);

        protected override void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f || data == null) return;

            drawnGrid = GridColor;
            Color32 gridColor = drawnGrid;
            for (int i = 0; i < grid.Length; i++)
            {
                float y = Mathf.Round(rect.height * grid[i]);
                mesh.SetQuad(i, 0f, y, rect.width, y + 1f, gridColor);
            }

            if (layoutSize.x != rect.width || layoutSize.y != rect.height || layoutCount != data.Count)
            {
                float step = rect.width / data.Capacity;
                float x0 = rect.width - data.Count * step;
                float barWidth = Mathf.Max(1f, step - 0.5f);
                for (int i = 0; i < data.Count; i++)
                {
                    float left = x0 + i * step;
                    mesh.SetQuad(grid.Length + i, left, rect.height, left + barWidth, rect.height, default);
                }

                layoutSize = rect.size;
                layoutCount = data.Count;
            }

            drawnGood = MainColor;
            drawnCaution = SecondaryColor;
            drawnCritical = resolvedStyle.borderRightColor;
            Color32 good = drawnGood, caution = drawnCaution, critical = drawnCritical;
            for (int i = 0; i < data.Count; i++)
            {
                float ms = data[i];
                int status = BarStatus(ms);
                float top = BarTop(ms, rect.height);
                drawnBars[i] = BarKey(top, status);
                mesh.SetBar(grid.Length + i, top, status == 0 ? good : status == 1 ? caution : critical);
            }
            mesh.Draw(context);
        }
    }

    internal sealed class GraphMesh
    {
        private readonly Vertex[] vertices;
        private readonly ushort[] indices;

        public GraphMesh(int count)
        {
            vertices = new Vertex[count * 4];
            indices = new ushort[count * 6];
            for (int i = 0; i < count; i++)
            {
                int v = i * 4, n = i * 6;
                indices[n] = (ushort)v;
                indices[n + 1] = (ushort)(v + 1);
                indices[n + 2] = (ushort)(v + 2);
                indices[n + 3] = (ushort)(v + 2);
                indices[n + 4] = (ushort)(v + 3);
                indices[n + 5] = (ushort)v;
            }

        }

        public void SetQuad(int quad, float left, float top, float right, float bottom, Color32 color)
        {
            int v = quad * 4;
            vertices[v].position = new Vector3(left, top, Vertex.nearZ);
            vertices[v + 1].position = new Vector3(right, top, Vertex.nearZ);
            vertices[v + 2].position = new Vector3(right, bottom, Vertex.nearZ);
            vertices[v + 3].position = new Vector3(left, bottom, Vertex.nearZ);
            SetTint(v, color);
        }

        public void SetBar(int quad, float top, Color32 color)
        {
            int v = quad * 4;
            vertices[v].position.y = top;
            vertices[v + 1].position.y = top;
            SetTint(v, color);
        }

        private void SetTint(int vertex, Color32 color)
        {
            vertices[vertex].tint = color;
            vertices[vertex + 1].tint = color;
            vertices[vertex + 2].tint = color;
            vertices[vertex + 3].tint = color;
        }

        public void Draw(MeshGenerationContext context)
        {
            MeshWriteData mesh = context.Allocate(vertices.Length, indices.Length);
            mesh.SetAllVertices(vertices);
            mesh.SetAllIndices(indices);
        }
    }

    /// <summary>Log-frequency spectrum bars with optional falling peak caps.</summary>
    internal class SpectrumBars : PaintedElement
    {
        private const float CapFallPerSecond = 0.35f;
        private const float BarFallPerSecond = 2.5f;

        private readonly float[] bandsDb;
        private readonly float[] bars;
        private readonly float[] caps;
        private readonly float minDb;
        private readonly float gap;
        private readonly bool drawCaps;
        private readonly float[] grid;

        public SpectrumBars(int bands, float minDb, float gap, bool drawCaps, float[] gridFractions)
        {
            AddToClassList("spectrum");
            bandsDb = new float[bands];
            bars = new float[bands];
            caps = new float[bands];
            this.minDb = minDb;
            this.gap = gap;
            this.drawCaps = drawCaps;
            grid = gridFractions;
        }

        public void Tick(AudioService audio, float deltaTime)
        {
            audio.FillBands(bandsDb, 50f, 20000f);
            for (int i = 0; i < bars.Length; i++)
            {
                float target = AudioService.Normalize(bandsDb[i], minDb);
                // Rise instantly, fall smoothly, so bars read as levels rather than noise.
                bars[i] = target > bars[i] ? target : Mathf.Max(target, bars[i] - BarFallPerSecond * deltaTime);
                caps[i] = Mathf.Max(bars[i], caps[i] - CapFallPerSecond * deltaTime);
            }
            MarkDirtyRepaint();
        }

        protected override void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            Painter2D painter = context.painter2D;
            HorizontalLines(painter, rect, grid, GridColor);

            float width = rect.width / bars.Length;
            float barWidth = Mathf.Max(1f, width - gap);
            painter.fillColor = MainColor;
            painter.BeginPath();
            for (int i = 0; i < bars.Length; i++)
            {
                float h = Mathf.Max(2f, bars[i] * rect.height);
                float x = i * width;
                painter.MoveTo(new Vector2(x, rect.height));
                painter.LineTo(new Vector2(x, rect.height - h));
                painter.LineTo(new Vector2(x + barWidth, rect.height - h));
                painter.LineTo(new Vector2(x + barWidth, rect.height));
                painter.ClosePath();
            }
            painter.Fill();

            if (!drawCaps) return;

            painter.strokeColor = SecondaryColor;
            painter.lineWidth = 1.5f;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            for (int i = 0; i < caps.Length; i++)
            {
                float y = Mathf.Max(1f, rect.height - caps[i] * rect.height - 4f);
                painter.MoveTo(new Vector2(i * width, y));
                painter.LineTo(new Vector2(i * width + barWidth, y));
            }
            painter.Stroke();
        }
    }

    /// <summary>
    /// Progress drawn along a rounded rectangle, clockwise from the top centre, so it follows the outline of the
    /// element it surrounds (square or pill). Track = border-top-color, progress = color, corner radius = border-radius.
    /// </summary>
    internal class OutlineProgress : PaintedElement
    {
        private const float Stroke = 3f;
        private const int ArcSegments = 8;

        private readonly List<Vector2> points = new List<Vector2>();
        private float progress;

        public float Progress
        {
            get => progress;
            set
            {
                progress = Mathf.Clamp01(value);
                MarkDirtyRepaint();
            }
        }

        protected override void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            float half = Stroke * 0.5f;
            rect = Rect.MinMaxRect(rect.xMin + half, rect.yMin + half, rect.xMax - half, rect.yMax - half);
            if (rect.width <= 0f || rect.height <= 0f) return;

            float radius = Mathf.Min(resolvedStyle.borderTopLeftRadius - half, rect.width * 0.5f, rect.height * 0.5f);
            BuildOutline(rect, Mathf.Max(0f, radius));

            Painter2D painter = context.painter2D;
            painter.lineWidth = Stroke;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            painter.strokeColor = GridColor;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Count; i++) painter.LineTo(points[i]);
            painter.Stroke();

            if (progress <= 0f) return;

            float total = 0f;
            for (int i = 1; i < points.Count; i++) total += Vector2.Distance(points[i - 1], points[i]);
            float remaining = total * progress;

            painter.strokeColor = MainColor;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Count && remaining > 0f; i++)
            {
                float length = Vector2.Distance(points[i - 1], points[i]);
                if (length >= remaining)
                {
                    painter.LineTo(Vector2.Lerp(points[i - 1], points[i], remaining / length));
                    break;
                }
                painter.LineTo(points[i]);
                remaining -= length;
            }
            painter.Stroke();
        }

        // Clockwise from the top centre: top edge, corners as short arcs, back to the start.
        private void BuildOutline(Rect r, float radius)
        {
            points.Clear();
            points.Add(new Vector2(r.center.x, r.yMin));
            AddCorner(new Vector2(r.xMax - radius, r.yMin + radius), radius, -90f);
            AddCorner(new Vector2(r.xMax - radius, r.yMax - radius), radius, 0f);
            AddCorner(new Vector2(r.xMin + radius, r.yMax - radius), radius, 90f);
            AddCorner(new Vector2(r.xMin + radius, r.yMin + radius), radius, 180f);
            points.Add(new Vector2(r.center.x, r.yMin));
        }

        private void AddCorner(Vector2 centre, float radius, float startDegrees)
        {
            for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = (startDegrees + 90f * i / ArcSegments) * Mathf.Deg2Rad;
                points.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
    }

    /// <summary>Progress ring for the corner-hold gesture: track = border-top-color, arc = color.</summary>
    internal class Ring : PaintedElement
    {
        private float progress;

        public Ring()
        {
            AddToClassList("ring");
        }

        public float Progress
        {
            get => progress;
            set
            {
                progress = Mathf.Clamp01(value);
                MarkDirtyRepaint();
            }
        }

        protected override void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            const float stroke = 4f;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f - stroke * 0.5f;
            if (radius <= 0f) return;

            var center = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            Painter2D painter = context.painter2D;
            painter.lineWidth = stroke;
            painter.lineCap = LineCap.Round;

            painter.strokeColor = GridColor;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Stroke();

            if (progress <= 0f) return;

            painter.strokeColor = MainColor;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * progress));
            painter.Stroke();
        }
    }
}
