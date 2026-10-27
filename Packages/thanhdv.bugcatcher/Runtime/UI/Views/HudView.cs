using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>The four HUD modules, stacked in the corners of the safe area.</summary>
    internal class HudView
    {
        private const float TextInterval = 0.25f;
        private const float CornerMargin = 8f;

        private readonly BugCatcherUI ui;
        private readonly VisualElement[] corners = new VisualElement[4];
        private readonly HudModuleView[] modules = new HudModuleView[HudLayout.ModuleCount];
        private float textTimer;
        private int historyVersion = -1;

        public HudView(BugCatcherUI ui)
        {
            this.ui = ui;
            Root = Div("hud");
            Root.pickingMode = PickingMode.Ignore;
            string[] cornerClasses = { "hud-tl", "hud-tr", "hud-bl", "hud-br" };
            for (int i = 0; i < 4; i++)
            {
                corners[i] = Div("hud-corner " + cornerClasses[i]);
                corners[i].pickingMode = PickingMode.Ignore;
                Root.Add(corners[i]);
            }

            for (int i = 0; i < HudLayout.ModuleCount; i++) modules[i] = new HudModuleView((HudModule)i);
        }

        public VisualElement Root { get; }

        private static BugCatcherSettings Settings => BugCatcher.Settings;

        public void OnScreenChanged() => Apply(true);

        public void Apply() => Apply(false);

        private void Apply(bool forceRebuild)
        {
            HudLayout layout = Settings.hud;
            Show(Root, Settings.hudVisible);
            bool desktop = ui.Mode == LayoutMode.Desktop;
            Color overlay = ui.OverlayColor;
            var scale = new Scale(new Vector3(layout.scale, layout.scale, 1f));
            // The wide System module must still fit a portrait phone: safe width minus both corner margins, before scaling.
            float maxWidth = (ui.PanelSize.x - ui.SafeInsets.Left - ui.SafeInsets.Right - 2f * CornerMargin) / layout.scale;
            foreach (VisualElement corner in corners) corner.style.scale = scale;

            for (int i = 0; i < HudLayout.ModuleCount; i++)
            {
                HudModuleView module = modules[i];
                module.Build(layout.modes[i], desktop, forceRebuild);
                module.Root.style.backgroundColor = overlay;
                module.Root.style.maxWidth = maxWidth;
                VisualElement corner = corners[(int)layout.corners[i]];
                if (module.Root.parent != corner || module.Root.parent.IndexOf(module.Root) != CountBefore(i, layout))
                {
                    module.Root.RemoveFromHierarchy();
                    corner.Insert(Mathf.Min(CountBefore(i, layout), corner.childCount), module.Root);
                }
            }

            BugCatcher.Audio.SetViewing(this, Settings.hudVisible && layout.modes[(int)HudModule.Audio] != HudMode.Off);
            textTimer = TextInterval;
        }

        // Modules keep a fixed order inside a corner: FPS, RAM, Audio, Logs, System.
        private static int CountBefore(int module, HudLayout layout)
        {
            int count = 0;
            for (int i = 0; i < module; i++)
            {
                if (layout.corners[i] == layout.corners[module]) count++;
            }
            return count;
        }

        public void Tick(float deltaTime)
        {
            if (!Settings.hudVisible) return;

            textTimer += deltaTime;
            if (textTimer >= TextInterval)
            {
                textTimer = 0f;
                foreach (HudModuleView module in modules) module.UpdateText();
            }

            int version = BugCatcher.Performance.HistoryVersion;
            if (version != historyVersion)
            {
                historyVersion = version;
                foreach (HudModuleView module in modules) module.RepaintGraphs();
            }

            modules[(int)HudModule.Audio].TickAudio(deltaTime);
        }
    }

    /// <summary>
    /// One HUD module. Display only: touches pass through to the game; it is configured from the HUD tab.
    /// </summary>
    internal class HudModuleView
    {
        private readonly HudModule module;
        private HudMode mode = (HudMode)(-1);
        private bool builtForDesktop;

        private Label fpsValue, avgValue, oneValue, pointOneValue;
        private LineGraph fpsGraph;
        private Label allocValue, reservedValue, monoValue, ramBasicValue, ramBasicTotal;
        private LineGraph[] ramGraphs;
        private Label audioHeader, rmsValue, peakValue;
        private SpectrumBars spectrum;
        private LevelMeter meter;
        private Label[] logCounts;
        private VisualElement[] logLines;
        private Label logEmpty;
        private int logLinesTotal;

        public HudModuleView(HudModule module)
        {
            this.module = module;
            Root = Div("mod");
            Root.pickingMode = PickingMode.Ignore;
        }

        public VisualElement Root { get; }

        private static PerformanceService Perf => BugCatcher.Performance;

        public void Build(HudMode newMode, bool desktop, bool force)
        {
            if (newMode == mode && desktop == builtForDesktop && !force) return;

            mode = newMode;
            builtForDesktop = desktop;
            Root.Clear();
            Root.ClearClassList();
            Root.AddToClassList("mod");
            fpsGraph = null;
            ramGraphs = null;
            spectrum = null;
            logLines = null;
            Show(Root, mode != HudMode.Off);
            if (mode == HudMode.Off) return;

            bool basic = mode == HudMode.Basic;
            Root.EnableInClassList("mod-row", basic);
            switch (module)
            {
                case HudModule.Fps: BuildFps(); break;
                case HudModule.Ram: BuildRam(); break;
                case HudModule.Audio: BuildAudio(); break;
                case HudModule.Logs: BuildLogs(); break;
                case HudModule.System: BuildSystem(); break;
            }
            ApplyGaps(Root);
            // Nothing in a module may catch a touch, or it would block the game underneath.
            Root.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
            UpdateText();
        }

        private void BuildFps()
        {
            if (mode == HudMode.Basic)
            {
                fpsValue = Text("0", "mono w600 fs-title");
                oneValue = Text("0", "mono fs-caption");
                Root.Add(fpsValue);
                Root.Add(Text("FPS", "mono fs-caption c-secondary"));
                Root.Add(Div("row-baseline", Text("1% ", "mono fs-caption c-secondary"), oneValue));
                return;
            }

            Root.AddToClassList("mod-w");
            fpsValue = Text("0", "mono w600 fs-display mod-big");
            Root.Add(Div("mod-head", Text("FPS", "mono fs-caption c-secondary"), fpsValue));

            avgValue = Text("0", "mono fs-caption");
            oneValue = Text("0", "mono fs-caption");
            pointOneValue = Text("0", "mono fs-caption");
            bool desktop = builtForDesktop;
            Root.Add(Div("mod-stats",
                Div("mod-stat", Text("avg", "mono fs-caption c-secondary"), avgValue),
                Div("mod-stat", Text(desktop ? "1% low" : "1%", "mono fs-caption c-secondary"), oneValue),
                Div("mod-stat", Text(desktop ? "0.1% low" : "0.1%", "mono fs-caption c-secondary"), pointOneValue)));

            if (mode != HudMode.Full) return;

            fpsGraph = new LineGraph(new[] { 0.2f, 0.6f });
            fpsGraph.AddToClassList("mod-graph");
            fpsGraph.AddToClassList("graph-fps");
            fpsGraph.Bind(Perf.FpsHistory, () => Mathf.Max(75f, Perf.FpsHistory.Max() * 1.15f));
            Root.Add(fpsGraph);
        }

        private void BuildRam()
        {
            if (mode == HudMode.Basic)
            {
                ramBasicValue = Text("0", "mono w600 fs-title c-ram-allocated");
                ramBasicTotal = Text("/ 0 MB", "mono fs-caption c-secondary");
                Root.Add(Text("RAM", "mono fs-caption c-secondary"));
                Root.Add(ramBasicValue);
                Root.Add(ramBasicTotal);
                return;
            }

            Root.AddToClassList("mod-w");
            Root.Add(Div("mod-head",
                Text("RAM · MB", "mono fs-caption c-secondary"),
                Text("device " + SystemInfoService.Ram, "mono fs-caption c-secondary")));

            allocValue = Text("0", "mono fs-caption");
            reservedValue = Text("0", "mono fs-caption");
            monoValue = Text("0", "mono fs-caption");
            Root.Add(Div("mod-rows",
                RamRow("allocated", "sw-ram-allocated", allocValue),
                RamRow("reserved", "sw-ram-reserved", reservedValue),
                RamRow("mono", "sw-ram-mono", monoValue)));

            if (mode != HudMode.Full) return;

            var stack = Div("mod-graph graph-stack");
            System.Func<float> max = () => Mathf.Max(1f, Perf.ReservedHistory.Max() * 1.15f);
            ramGraphs = new[]
            {
                new LineGraph(new[] { 1f / 3f, 2f / 3f }),
                new LineGraph(),
                new LineGraph()
            };
            ramGraphs[0].AddToClassList("graph-ram-reserved");
            ramGraphs[1].AddToClassList("graph-ram-allocated");
            ramGraphs[2].AddToClassList("graph-ram-mono");
            ramGraphs[0].Bind(Perf.ReservedHistory, max);
            ramGraphs[1].Bind(Perf.AllocatedHistory, max);
            ramGraphs[2].Bind(Perf.MonoHistory, max);
            foreach (LineGraph graph in ramGraphs)
            {
                graph.AddToClassList("fill");
                stack.Add(graph);
            }
            Root.Add(stack);
        }

        private static VisualElement RamRow(string name, string swatch, Label value) =>
            Div("mod-ram-row", Div("swatch " + swatch), Text(name, "mono fs-caption c-secondary grow"), value);

        private void BuildAudio()
        {
            rmsValue = Text("0", "mono w600 fs-title");
            if (mode == HudMode.Basic)
            {
                meter = new LevelMeter("meter meter-mini");
                Root.Add(Text("dB", "mono fs-caption c-secondary"));
                Root.Add(rmsValue);
                Root.Add(meter);
                return;
            }

            Root.AddToClassList("mod-w");
            audioHeader = Text("AUDIO", "mono fs-caption c-secondary");
            if (mode == HudMode.Text)
            {
                rmsValue = Text("0", "mono fs-caption");
                peakValue = Text("0", "mono fs-caption");
                Root.AddToClassList("mod-tight");
                Root.Add(Div("mod-head", audioHeader));
                Root.Add(Div("mod-head", Text("rms", "mono fs-caption c-secondary"), rmsValue));
                Root.Add(Div("mod-head", Text("peak", "mono fs-caption c-secondary"), peakValue));
                return;
            }

            Root.Add(Div("mod-head", audioHeader, Div("row-baseline", rmsValue, Text(" dB", "mono fs-caption c-secondary"))));
            spectrum = new SpectrumBars(32, -60f, 1.65f, false, new[] { 0.5f });
            spectrum.AddToClassList("mod-spectrum");
            Root.Add(spectrum);
            meter = new LevelMeter();
            meter.Marker.AddToClassList("bg-caution");
            Root.Add(meter);
        }

        private const int LogLineCount = 3;
        private static readonly string[] logIcons = { "info", "warning", "error" };
        private static readonly string[] logNames = { "log", "warning", "error" };
        private static readonly string[] logColors = { "c-info", "c-warning", "c-error" };

        /// <summary>
        /// Like In-game Debug Console's popup: a counter per log type. Text lists them, Full adds the newest lines.
        /// </summary>
        private void BuildLogs()
        {
            logCounts = new Label[3];
            if (mode == HudMode.Basic)
            {
                for (int k = 0; k < 3; k++)
                {
                    logCounts[k] = Text("0", "mono w600 fs-caption");
                    Root.Add(Div("mod-log-count", Icon(logIcons[k], 14f, logColors[k]), logCounts[k]));
                }
                return;
            }

            Root.AddToClassList("mod-w");
            Root.Add(Div("mod-head", Text("LOGS", "mono fs-caption c-secondary")));
            var rows = Div("mod-rows");
            for (int k = 0; k < 3; k++)
            {
                logCounts[k] = Text("0", "mono fs-caption");
                rows.Add(Div("mod-log-row", Icon(logIcons[k], 12f, logColors[k]), Text(logNames[k], "mono fs-caption c-secondary grow"), logCounts[k]));
            }
            Root.Add(rows);

            if (mode != HudMode.Full) return;

            var lines = Div("mod-rows mod-log-lines");
            logLines = new VisualElement[LogLineCount];
            for (int i = 0; i < LogLineCount; i++)
            {
                logLines[i] = Div("mod-log-row", Icon("info", 12f), Text("", "mono fs-caption grow one-line"));
                lines.Add(logLines[i]);
            }
            logEmpty = Text("no logs yet", "mono fs-caption c-muted");
            lines.Add(logEmpty);
            Root.Add(lines);
            logLinesTotal = -1;
        }

        private void UpdateLogs()
        {
            LogStore logs = BugCatcher.Logs;
            for (int k = 0; k < 3; k++)
            {
                int count = logs.CountOf((LogKind)k);
                logCounts[k].text = Format.Int(count);
                SetClass(logCounts[k], "c-muted", count == 0);
            }

            // Newest lines, oldest first; only rebuilt when something was logged or cleared.
            if (logLines == null || logs.TotalCount == logLinesTotal) return;
            logLinesTotal = logs.TotalCount;
            IReadOnlyList<LogRecord> records = logs.Records;
            int shown = Mathf.Min(LogLineCount, records.Count);
            for (int i = 0; i < LogLineCount; i++)
            {
                VisualElement line = logLines[i];
                Show(line, i < shown);
                if (i >= shown) continue;

                LogEntry entry = records[records.Count - shown + i].Entry;
                var icon = (IconElement)line[0];
                icon.Icon = logIcons[(int)entry.Kind];
                for (int k = 0; k < 3; k++) SetClass(icon, logColors[k], k == (int)entry.Kind);
                ((Label)line[1]).text = FirstLine(entry.Message);
            }
            Show(logEmpty, shown == 0);
        }

        private static string FirstLine(string message)
        {
            int newline = message.IndexOf('\n');
            return newline < 0 ? message : message.Substring(0, newline);
        }

        /// <summary>Only Full exists (see HudLayout.Sanitize).</summary>
        private void BuildSystem()
        {
            Root.AddToClassList("mod-sys");
            Root.AddToClassList("mod-tight");
            Root.Add(SystemRow("gpu", SystemInfoService.GpuShort));
            Root.Add(SystemRow("cpu", SystemInfoService.CpuShort));
            Root.Add(SystemRow("os", SystemInfoService.Os));
            Root.Add(SystemRow("ram", SystemInfoService.Ram));
            Root.Add(SystemRow("screen", SystemInfoService.Screen));
        }

        private static VisualElement SystemRow(string name, string value) =>
            Div("mod-sys-row", Text(name, "mono fs-caption c-secondary mod-sys-key"), Text(value, "mono fs-caption grow"));

        public void UpdateText()
        {
            if (mode == HudMode.Off) return;

            switch (module)
            {
                case HudModule.Fps:
                    SetStatusText(fpsValue, Perf.CurrentFps);
                    if (avgValue != null) avgValue.text = Format.Int(Perf.AverageFps);
                    SetStatusText(oneValue, Perf.OnePercentFps);
                    if (pointOneValue != null) SetStatusText(pointOneValue, Perf.PointOnePercentFps);
                    break;
                case HudModule.Ram:
                    if (mode == HudMode.Basic)
                    {
                        ramBasicValue.text = Format.Int(Mathf.RoundToInt(Perf.AllocatedMb));
                        ramBasicTotal.text = "/ " + Format.Int(Mathf.RoundToInt(Perf.ReservedMb)) + " MB";
                    }
                    else
                    {
                        allocValue.text = Format.Mb(Perf.AllocatedMb);
                        reservedValue.text = Format.Mb(Perf.ReservedMb);
                        monoValue.text = Format.Mb(Perf.MonoMb);
                    }
                    break;
                case HudModule.Audio:
                    AudioService audio = BugCatcher.Audio;
                    IAudioProvider provider = audio.Current;
                    if (audioHeader != null) audioHeader.text = "AUDIO · " + provider.ShortName;
                    rmsValue.text = Format.Db(provider.RmsDb);
                    if (peakValue != null)
                    {
                        peakValue.text = Format.Db(audio.HeldPeakDb) + " dB";
                        if (mode == HudMode.Text) rmsValue.text += " dB";
                        SetClass(peakValue, "c-caution", audio.HeldPeakDb >= -6f);
                    }
                    break;
                case HudModule.Logs:
                    UpdateLogs();
                    break;
            }
        }

        public void TickAudio(float deltaTime)
        {
            if (mode == HudMode.Off || mode == HudMode.Text || module != HudModule.Audio) return;

            AudioService audio = BugCatcher.Audio;
            spectrum?.Tick(audio, deltaTime);
            meter?.Set(AudioService.Normalize(audio.Current.RmsDb), AudioService.Normalize(audio.HeldPeakDb), spectrum != null);
        }

        public void RepaintGraphs()
        {
            fpsGraph?.MarkDirtyRepaint();
            if (ramGraphs == null) return;
            foreach (LineGraph graph in ramGraphs) graph.MarkDirtyRepaint();
        }

        private static void SetStatusText(Label label, int fps)
        {
            if (label == null) return;
            label.text = Format.Int(fps);
            int status = PerformanceService.Status(fps);
            SetClass(label, "c-good", status == 0);
            SetClass(label, "c-caution", status == 1);
            SetClass(label, "c-critical", status == 2);
        }
    }
}
