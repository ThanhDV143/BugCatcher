using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>Bubble visibility, the three ways to open, text size, and HUD layout presets.</summary>
    internal class SettingsTab : IPanelTab
    {
        private VisualElement root;
        private Switch bubbleSwitch;
        private Segmented textSize;
        private VisualElement presetList;
        private VisualElement nameRow;
        private TextField nameField;
        private LayoutMode mode;

        private static BugCatcherSettings Settings => BugCatcher.Settings;

        public VisualElement Build(LayoutMode layoutMode)
        {
            mode = layoutMode;
            root = Div("tab-page");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.verticalScrollerVisibility = mode == LayoutMode.Desktop ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            var content = Div("tab-scroll pad-safe" + (mode == LayoutMode.Desktop ? " tab-narrow" : ""));
            scroll.Add(content);
            root.Add(scroll);

            bubbleSwitch = new Switch(Settings.showBubble, on =>
            {
                Settings.showBubble = on;
                Settings.Changed();
            });
            content.Add(Div("card switch-card", Text("Show bubble", "fs-body w500 grow"), bubbleSwitch));

            bool mobile = mode != LayoutMode.Desktop;
            var ways = Div("card");
            ways.Add(Way(Div("way-icon", Icon("bug")), "Tap the bubble", "Long-press it for the quick menu", false));
            ways.Add(Way(PhoneIcon(), "Hold both top corners for 1 s", "Mobile. Still works when the bubble is hidden.", mobile));
            ways.Add(Way(Div("way-keys", PanelView.Keycap("kc kc-sm"), PanelView.Keycap("kc kc-sm"), PanelView.Keycap("kc kc-sm")),
                "Press ` 3× within 0.8 s", "Desktop & Editor. Same keys close it.", !mobile, last: true));
            content.Add(Div("group", Text("WAYS TO OPEN BUGCATCHER", "lbl"), ways));

            textSize = new Segmented(new[] { "Small", "Default", "Large" }, (int)Settings.textSize, index =>
            {
                Settings.textSize = (TextSize)index;
                Settings.Changed();
            });
            textSize.Option(0).AddToClassList("fs-chip");
            textSize.Option(2).AddToClassList("fs-large");
            content.Add(Div("group", Text("TEXT SIZE", "lbl"), textSize));

            presetList = Div("card");
            nameField = new TextField();
            nameField.AddToClassList("bare-field");
            Fonts.Apply(nameField);
            nameRow = Div("name-row",
                Div("field grow", ConsoleTab.WithPlaceholder(nameField, "Preset name")),
                Tap(Div("btn btn-accent", Text("Save", "fs-body w600")), SavePreset));
            Show(nameRow, false);
            content.Add(Div("group",
                Text("LAYOUT PRESETS", "lbl"),
                presetList,
                nameRow,
                Div("button-row",
                    Tap(Div("btn btn-raised grow", Icon("plus", 18f), Text("Save current", "fs-body w500")), () =>
                    {
                        Show(nameRow, true);
                        nameField.value = "Preset " + (Settings.presets.Count + 1);
                        nameField.schedule.Execute(() => nameField.Focus());
                    }),
                    Tap(Div("btn btn-text grow", Icon("reset", 18f, "c-accent"), Text("Reset layout", "fs-body w500 c-accent")), ResetLayout))));

            ApplySettings();
            return root;
        }

        private static VisualElement Way(VisualElement icon, string title, string caption, bool highlight, bool last = false) =>
            Div("way" + (highlight ? " way-on" : "") + (last ? " last" : ""), icon, Div("col", Text(title, "fs-body w500"), Text(caption, "fs-caption c-secondary")));

        private static VisualElement PhoneIcon() =>
            Div("way-icon way-phone", Div("phone-outline"), Div("phone-dot phone-dot-l"), Div("phone-dot phone-dot-r"));

        public void SetVisible(bool visible)
        {
            if (!visible && nameRow != null) Show(nameRow, false);
        }

        public void Tick(float deltaTime) { }

        public void ApplySettings()
        {
            if (root == null) return;

            bubbleSwitch.SetValue(Settings.showBubble);
            textSize.SetSelected((int)Settings.textSize);
            BuildPresets();
        }

        private void BuildPresets()
        {
            presetList.Clear();
            presetList.Add(PresetRow(-1, "Default", "built-in", Settings.presets.Count == 0));
            for (int i = 0; i < Settings.presets.Count; i++) presetList.Add(PresetRow(i, Settings.presets[i].name, "saved", i == Settings.presets.Count - 1));
        }

        /// <summary>Tap applies the preset. Long-press deletes a saved one.</summary>
        private VisualElement PresetRow(int index, string name, string tag, bool last)
        {
            bool selected = Settings.activePreset == index;
            var radio = Div("radio" + (selected ? " on" : ""), Div("radio-dot"));
            var row = Tap(Div("preset" + (last ? " last" : ""), radio, Text(name, "fs-body grow"), Text(tag, "mono fs-caption c-muted")), () => ApplyPreset(index));
            if (index >= 0)
            {
                IVisualElementScheduledItem timer = null;
                row.RegisterCallback<PointerDownEvent>(_ => timer = row.schedule.Execute(() => DeletePreset(index)).StartingIn(800), TrickleDown.TrickleDown);
                row.RegisterCallback<PointerUpEvent>(_ => timer?.Pause(), TrickleDown.TrickleDown);
                row.RegisterCallback<PointerLeaveEvent>(_ => timer?.Pause());
            }
            return row;
        }

        private static void ApplyPreset(int index)
        {
            Settings.hud = index < 0 ? new HudLayout() : Settings.presets[index].layout.Clone();
            Settings.activePreset = index;
            Settings.hudVisible = true;
            Settings.Changed();
        }

        private static void DeletePreset(int index)
        {
            if (index >= Settings.presets.Count) return;
            Debug.Log("[BugCatcher] Deleted layout preset \"" + Settings.presets[index].name + "\"");
            Settings.presets.RemoveAt(index);
            Settings.activePreset = -2;
            Settings.Changed();
        }

        private void SavePreset()
        {
            string name = string.IsNullOrWhiteSpace(nameField.value) ? "Preset " + (Settings.presets.Count + 1) : nameField.value.Trim();
            Settings.presets.Add(new LayoutPreset { name = name, layout = Settings.hud.Clone() });
            Settings.activePreset = Settings.presets.Count - 1;
            Show(nameRow, false);
            Settings.Changed();
        }

        private static void ResetLayout()
        {
            Settings.hud = new HudLayout();
            Settings.activePreset = -1;
            Settings.hudVisible = true;
            Settings.Changed();
        }
    }
}
