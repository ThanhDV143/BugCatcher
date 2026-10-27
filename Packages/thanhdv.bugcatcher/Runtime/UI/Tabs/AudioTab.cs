using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>Spectrum, RMS / held peak, and engine extras (FMOD: DSP CPU, instances, bus meters).</summary>
    internal class AudioTab : IPanelTab
    {
        private const float TextInterval = 0.125f;

        private VisualElement root;
        private VisualElement sourceRow;
        private Segmented sourceSwitch;
        private Label spectrumCaption;
        private SpectrumBars spectrum;
        private Label rms;
        private Label peak;
        private VisualElement extrasGroup;
        private Label extrasTitle;
        private Label extrasVersion;
        private Label cpu;
        private Label instances;
        private VisualElement busList;
        private readonly List<(Label name, LevelMeter meter, Label value)> busRows = new List<(Label, LevelMeter, Label)>();
        private bool visible;
        private float textTimer;
        private int providerCount;

        public VisualElement Build(LayoutMode mode)
        {
            root = Div("tab-page");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.verticalScrollerVisibility = mode == LayoutMode.Desktop ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            var content = Div("tab-scroll pad-safe" + (mode == LayoutMode.Desktop ? " tab-narrow" : ""));
            scroll.Add(content);
            root.Add(scroll);

            sourceRow = Div("source-row");
            content.Add(sourceRow);
            BuildSourceSwitch();

            spectrumCaption = Text("", "mono fs-caption c-muted");
            spectrum = new SpectrumBars(48, -48f, 1.5f, true, new[] { 0f, 0.25f, 0.5f, 0.75f, 1f });
            spectrum.AddToClassList("tab-spectrum");
            content.Add(Div("card spectrum-card",
                Div("group-head", Text("Spectrum", "fs-body w600"), spectrumCaption),
                Div("spectrum-body",
                    Div("y-axis", Text("0", "mono fs-caption c-muted"), Text(Format.Minus + "24", "mono fs-caption c-muted"), Text(Format.Minus + "48", "mono fs-caption c-muted")),
                    spectrum),
                Div("x-axis",
                    Text("50", "mono fs-caption c-muted"), Text("200", "mono fs-caption c-muted"), Text("1k", "mono fs-caption c-muted"),
                    Text("5k", "mono fs-caption c-muted"), Text("20k Hz", "mono fs-caption c-muted"))));

            rms = Text("", "mono fs-display w600");
            peak = Text("", "mono fs-display w600");
            content.Add(Div("stat-row",
                Div("card stat-card", Text("RMS", "lbl"), Div("row-baseline", rms, Text(" dB", "mono fs-body c-secondary"))),
                Div("card stat-card", Text("PEAK · HELD 2 S", "lbl"), Div("row-baseline", peak, Text(" dB", "mono fs-body c-secondary")))));

            extrasTitle = Text("", "lbl");
            extrasVersion = Text("", "mono fs-caption c-muted");
            cpu = Text("", "mono fs-body");
            instances = Text("", "mono fs-body");
            busList = Div("bus-list");
            extrasGroup = Div("group",
                Div("group-head", extrasTitle, extrasVersion),
                Div("stat-row",
                    Div("card kv-card", Text("DSP CPU", "mono fs-body c-secondary"), cpu),
                    Div("card kv-card", Text("Instances", "mono fs-body c-secondary"), instances)),
                busList);
            content.Add(extrasGroup);
            busRows.Clear();
            Refresh();
            return root;
        }

        private void BuildSourceSwitch()
        {
            sourceRow.Clear();
            IReadOnlyList<IAudioProvider> providers = BugCatcher.Audio.Providers;
            providerCount = providers.Count;
            Show(sourceRow, providers.Count > 1);
            if (providers.Count < 2) return;

            var names = new string[providers.Count];
            int selected = 0;
            IAudioProvider current = BugCatcher.Audio.Current;
            for (int i = 0; i < providers.Count; i++)
            {
                names[i] = providers[i].DisplayName;
                if (providers[i] == current) selected = i;
            }

            sourceSwitch = new Segmented(names, selected, index => BugCatcher.Audio.Select(BugCatcher.Audio.Providers[index].Id));
            sourceSwitch.AddToClassList("grow");
            for (int i = 0; i < providers.Count; i++) sourceSwitch.SetDisabled(i, !providers[i].IsAvailable);
            sourceRow.Add(sourceSwitch);
        }

        public void SetVisible(bool isVisible)
        {
            visible = isVisible;
            BugCatcher.Audio.SetViewing(this, isVisible);
        }

        public void ApplySettings()
        {
            if (root == null) return;
            if (providerCount != BugCatcher.Audio.Providers.Count) BuildSourceSwitch();
            Refresh();
        }

        public void Tick(float deltaTime)
        {
            if (!visible) return;

            spectrum.Tick(BugCatcher.Audio, deltaTime);
            textTimer += deltaTime;
            if (textTimer < TextInterval) return;
            textTimer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            AudioService audio = BugCatcher.Audio;
            IAudioProvider provider = audio.Current;
            spectrumCaption.text = provider.SpectrumCaption;
            rms.text = Format.Db(provider.RmsDb);
            peak.text = Format.Db(audio.HeldPeakDb);
            SetClass(peak, "c-caution", audio.HeldPeakDb >= -6f && audio.HeldPeakDb < -1f);
            SetClass(peak, "c-critical", audio.HeldPeakDb >= -1f);

            AudioExtras extras = provider.Extras;
            Show(extrasGroup, extras != null);
            if (extras == null) return;

            extrasTitle.text = extras.Title.ToUpperInvariant();
            extrasVersion.text = extras.Version;
            cpu.text = extras.DspCpuPercent.ToString("0.0") + " %";
            instances.text = Format.Int(extras.Instances);

            while (busRows.Count < extras.Buses.Count)
            {
                var name = Text("", "fs-body bus-name");
                var meter = new LevelMeter("meter meter-bus");
                var value = Text("", "mono fs-caption bus-value");
                busRows.Add((name, meter, value));
                busList.Add(Div("bus", name, meter, value));
            }

            for (int i = 0; i < busRows.Count; i++)
            {
                (Label name, LevelMeter meter, Label value) = busRows[i];
                Show(name.parent, i < extras.Buses.Count);
                if (i >= extras.Buses.Count) continue;

                AudioBusLevel bus = extras.Buses[i];
                bool silent = float.IsNegativeInfinity(bus.LevelDb) || bus.LevelDb < AudioService.MinDb;
                name.text = bus.Name;
                SetClass(name, "c-muted", silent);
                SetClass(value, "c-muted", silent);
                value.text = silent ? "silent" : Format.Db(bus.LevelDb) + " dB";
                meter.Set(silent ? 0f : AudioService.Normalize(bus.LevelDb), AudioService.Normalize(bus.PeakDb), !silent);
                // The master bus marks its peak in caution colour, other buses in secondary text colour.
                SetClass(meter.Marker, "bg-caution", i == 0);
            }
        }
    }
}
