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
    }

    /// <summary>One series line graph over a <see cref="SampleHistory"/>.</summary>
    internal class LineGraph : PaintedElement
    {
        private const float HalfStroke = 0.75f;
        private readonly float[] grid;
        private SampleHistory data;
        private Func<float> maxValue;
        private GraphMesh mesh;
        private int[] drawnPoints;
        private Vector2 drawnSize;
        private int drawnCount;
        private Color drawnGrid, drawnLine;

        public LineGraph(float[] gridFractions = null)
        {
            AddToClassList("line-graph");
            grid = gridFractions;
        }

        public void Bind(SampleHistory history, Func<float> max)
        {
            data = history;
            maxValue = max;
            mesh = new GraphMesh(grid?.Length ?? 0, history.Capacity);
            drawnPoints = new int[history.Capacity];
            drawnCount = -1;
            MarkDirtyRepaint();
        }

        public void RepaintIfChanged()
        {
            Rect rect = contentRect;
            if (data == null || !(rect.width > 0f && rect.height > 0f)) return;

            if (drawnSize.x != rect.width || drawnSize.y != rect.height || drawnCount != data.Count ||
                !drawnGrid.Equals(GridColor) || !drawnLine.Equals(MainColor))
            {
                MarkDirtyRepaint();
                return;
            }

            if (data.Count < 2) return;
            float max = Mathf.Max(1e-3f, maxValue());
            for (int i = 0; i < data.Count; i++)
            {
                if (drawnPoints[i] == HeightKey(SampleY(data[i], max, rect.height))) continue;
                MarkDirtyRepaint();
                return;
            }
        }

        private static float SampleY(float sample, float max, float height) =>
            Mathf.Clamp(height - sample / max * height, 1f, height - 1f);

        protected override void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (data == null || !(rect.width > 0f && rect.height > 0f)) return;

            drawnSize = rect.size;
            drawnCount = data.Count;
            drawnGrid = GridColor;
            drawnLine = MainColor;
            mesh.SetHorizontalGrid(rect, grid, drawnGrid);

            if (drawnCount >= 2)
            {
                float max = Mathf.Max(1e-3f, maxValue());
                int last = drawnCount - 1;
                float step = rect.width / (data.Capacity - 1);
                float x0 = rect.width - last * step;
                float y = SampleY(data[0], max, rect.height);
                float nextY = SampleY(data[1], max, rect.height);
                Vector2 normal = new Vector2(y - nextY, step).normalized;
                Color32 color = drawnLine;
                for (int i = 0; i <= last; i++)
                {
                    Vector2 outgoing = i < last ? new Vector2(y - nextY, step).normalized : normal;
                    // Adjacent segments share the same join. Clamp its length at sharp changes.
                    Vector2 offset = (normal + outgoing) * (HalfStroke / Mathf.Max(0.5f, 1f + Vector2.Dot(normal, outgoing)));
                    mesh.SetLinePoint(i, new Vector2(x0 + i * step, y), offset, color);
                    drawnPoints[i] = HeightKey(y);
                    if (i == last) break;
                    normal = outgoing;
                    y = nextY;
                    if (i + 1 < last) nextY = SampleY(data[i + 2], max, rect.height);
                }

                // Collapse unused segments at the final sample during warm-up.
                var end = new Vector2(x0 + last * step, y);
                for (int i = drawnCount; i < data.Capacity; i++)
                    mesh.SetLinePoint(i, end, Vector2.zero, color);
            }
            mesh.Draw(context);
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
        private readonly int lineStart;

        public GraphMesh(int count, int linePoints = 0)
        {
            lineStart = count * 4;
            vertices = new Vertex[lineStart + linePoints * 2];
            indices = new ushort[(count + Mathf.Max(0, linePoints - 1)) * 6];
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

            for (int i = 0; i < linePoints - 1; i++)
            {
                int v = lineStart + i * 2, n = (count + i) * 6;
                indices[n] = (ushort)v;
                indices[n + 1] = (ushort)(v + 2);
                indices[n + 2] = (ushort)(v + 3);
                indices[n + 3] = (ushort)(v + 3);
                indices[n + 4] = (ushort)(v + 1);
                indices[n + 5] = (ushort)v;
            }
        }

        public int SetHorizontalGrid(Rect rect, float[] fractions, Color32 color)
        {
            if (fractions == null) return 0;
            for (int i = 0; i < fractions.Length; i++)
            {
                float y = Mathf.Clamp(rect.height * fractions[i], 0.5f, rect.height - 0.5f);
                SetQuad(i, 0f, y - 0.5f, rect.width, y + 0.5f, color);
            }
            return fractions.Length;
        }

        public void SetLinePoint(int point, Vector2 center, Vector2 offset, Color32 color)
        {
            int v = lineStart + point * 2;
            vertices[v].position = new Vector3(center.x - offset.x, center.y - offset.y, Vertex.nearZ);
            vertices[v + 1].position = new Vector3(center.x + offset.x, center.y + offset.y, Vertex.nearZ);
            vertices[v].tint = color;
            vertices[v + 1].tint = color;
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
            if (indices.Length == 0) return;
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
        private readonly GraphMesh mesh;
        private readonly int[] drawnBars;
        private readonly int[] drawnCaps;
        private Vector2 drawnSize;
        private Color drawnGrid, drawnBarColor, drawnCapColor;

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
            mesh = new GraphMesh((grid?.Length ?? 0) + bands + (drawCaps ? bands : 0));
            drawnBars = new int[bands];
            drawnCaps = drawCaps ? new int[bands] : null;
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
            RepaintIfChanged();
        }

        private float BarTop(int i, float height) => height - Mathf.Max(2f, bars[i] * height);
        private float CapY(int i, float height) => Mathf.Max(1f, height - caps[i] * height - 4f);

        private void RepaintIfChanged()
        {
            Rect rect = contentRect;
            if (!(rect.width > 0f && rect.height > 0f)) return;

            if (drawnSize.x != rect.width || drawnSize.y != rect.height || !drawnGrid.Equals(GridColor) ||
                !drawnBarColor.Equals(MainColor) || (drawCaps && !drawnCapColor.Equals(SecondaryColor)))
            {
                MarkDirtyRepaint();
                return;
            }

            for (int i = 0; i < bars.Length; i++)
            {
                if (drawnBars[i] == HeightKey(BarTop(i, rect.height)) &&
                    (!drawCaps || drawnCaps[i] == HeightKey(CapY(i, rect.height)))) continue;
                MarkDirtyRepaint();
                return;
            }
        }

        protected override void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (!(rect.width > 0f && rect.height > 0f)) return;

            drawnSize = rect.size;
            drawnGrid = GridColor;
            drawnBarColor = MainColor;
            int quad = mesh.SetHorizontalGrid(rect, grid, drawnGrid);

            float width = rect.width / bars.Length;
            float barWidth = Mathf.Max(1f, width - gap);
            Color32 barColor = drawnBarColor;
            for (int i = 0; i < bars.Length; i++)
            {
                float top = BarTop(i, rect.height);
                float x = i * width;
                drawnBars[i] = HeightKey(top);
                mesh.SetQuad(quad++, x, top, x + barWidth, rect.height, barColor);
            }

            if (drawCaps)
            {
                drawnCapColor = SecondaryColor;
                Color32 capColor = drawnCapColor;
                for (int i = 0; i < caps.Length; i++)
                {
                    float y = CapY(i, rect.height);
                    float x = i * width;
                    drawnCaps[i] = HeightKey(y);
                    mesh.SetQuad(quad++, x, y - 0.75f, x + barWidth, y + 0.75f, capColor);
                }
            }
            mesh.Draw(context);
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
