using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>Module modes, positions (drag a chip to a corner), scale, opacity and theme.</summary>
    internal class HudTab : IPanelTab
    {
        private static readonly string[] moduleNames = { "FPS", "RAM", "Audio", "Logs", "System" };
        private static readonly string[] modeNames = { "Off", "Basic", "Text", "Full" };

        private readonly BugCatcherUI ui;
        private readonly Segmented[] modeControls = new Segmented[HudLayout.ModuleCount];
        private readonly VisualElement[] quadrants = new VisualElement[4];
        private readonly List<VisualElement> themeButtons = new List<VisualElement>();
        private VisualElement root;
        private VisualElement positionArea;
        private VisualElement themeRow;
        private SliderBar scaleSlider;
        private SliderBar opacitySlider;
        private Label scaleValue;
        private Label opacityValue;
        private int customThemeIndex;

        public HudTab(BugCatcherUI ui)
        {
            this.ui = ui;
        }

        private static BugCatcherSettings Settings => BugCatcher.Settings;

        public VisualElement Build(LayoutMode mode)
        {
            root = Div("tab-page");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.verticalScrollerVisibility = mode == LayoutMode.Desktop ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            var content = Div("tab-scroll" + (mode == LayoutMode.Desktop ? " tab-narrow" : ""));
            scroll.Add(content);
            root.Add(scroll);

            var modules = Div("group", Text("MODULES", "lbl"));
            for (int i = 0; i < HudLayout.ModuleCount; i++)
            {
                int module = i;
                modeControls[i] = new Segmented(modeNames, (int)Settings.hud.modes[i], index =>
                {
                    Settings.hud.modes[module] = (HudMode)index;
                    Settings.hudVisible = true;
                    Settings.activePreset = -2;
                    Settings.Changed();
                });
                modeControls[i].AddToClassList("grow");
                modules.Add(Div("module-row", Text(moduleNames[i], "fs-body w500 module-name"), modeControls[i]));
            }
            // System has only Off and Full.
            modeControls[(int)HudModule.System].SetDisabled((int)HudMode.Basic, true);
            modeControls[(int)HudModule.System].SetDisabled((int)HudMode.Text, true);
            content.Add(modules);

            positionArea = Div("position-area");
            for (int i = 0; i < 4; i++)
            {
                quadrants[i] = Div("quadrant q" + i);
                positionArea.Add(quadrants[i]);
            }
            content.Add(Div("group",
                Div("group-head", Text("POSITION", "lbl"), Text("drag a chip to a corner", "fs-caption c-muted")),
                positionArea));

            scaleValue = Text("1.00×", "mono fs-body c-secondary");
            scaleSlider = new SliderBar(0.75f, 1.5f, Settings.hud.scale, v =>
            {
                Settings.hud.scale = Mathf.Round(v * 20f) / 20f;
                Settings.Changed();
            });
            opacityValue = Text("78 %", "mono fs-body c-secondary");
            opacitySlider = new SliderBar(0f, 1f, Settings.hud.opacity, v =>
            {
                Settings.hud.opacity = Mathf.Round(v * 100f) / 100f;
                Settings.Changed();
            });
            themeRow = Div("theme-row");
            content.Add(Div("group",
                Text("APPEARANCE", "lbl"),
                Div("slider-group", Div("slider-head", Text("Scale", "fs-body"), scaleValue), scaleSlider),
                Div("slider-group", Div("slider-head", Text("Background opacity", "fs-body"), opacityValue), opacitySlider),
                themeRow));

            root.Add(Div("footer tab-footer",
                Tap(Div("btn btn-raised grow", Icon("preview"), Text("Preview HUD — hides panel, tap anywhere to return", "fs-body w500")), ui.StartPreview)));

            ApplySettings();
            return root;
        }

        public void SetVisible(bool visible) { }

        public void Tick(float deltaTime) { }

        public void ApplySettings()
        {
            if (root == null) return;

            HudLayout layout = Settings.hud;
            for (int i = 0; i < HudLayout.ModuleCount; i++) modeControls[i].SetSelected((int)layout.modes[i]);
            scaleSlider.SetValue(layout.scale);
            scaleValue.text = layout.scale.ToString("0.00") + "×";
            opacitySlider.SetValue(layout.opacity);
            opacityValue.text = Mathf.RoundToInt(layout.opacity * 100f) + " %";
            BuildChips();
            BuildThemes();
        }

        private void BuildChips()
        {
            HudLayout layout = Settings.hud;
            for (int q = 0; q < 4; q++)
            {
                quadrants[q].Clear();
                int count = 0;
                for (int m = 0; m < HudLayout.ModuleCount; m++)
                {
                    if (layout.modes[m] == HudMode.Off || (int)layout.corners[m] != q) continue;
                    quadrants[q].Add(PositionChip((HudModule)m, layout.modes[m]));
                    count++;
                }
                if (count == 0) quadrants[q].Add(Text("empty", "mono fs-caption quadrant-empty"));
            }
        }

        /// <summary>A chip on the mini screen. Dragging it onto another quadrant moves the module there.</summary>
        private VisualElement PositionChip(HudModule module, HudMode mode)
        {
            var chip = Div("pos-chip", Text(moduleNames[(int)module] + " · " + modeNames[(int)mode], "mono fs-caption"));
            chip.pickingMode = PickingMode.Position;
            Vector2 start = default;
            bool dragging = false;

            chip.RegisterCallback<PointerDownEvent>(e =>
            {
                start = e.position;
                dragging = true;
                chip.CapturePointer(e.pointerId);
                chip.AddToClassList("dragging");
            });
            chip.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging) return;
                Vector2 delta = (Vector2)e.position - start;
                chip.style.translate = new Translate(delta.x, delta.y, 0f);
                int target = QuadrantAt(e.position);
                for (int q = 0; q < 4; q++) SetClass(quadrants[q], "target", q == target);
            });
            chip.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging) return;
                dragging = false;
                chip.ReleasePointer(e.pointerId);
                chip.RemoveFromClassList("dragging");
                chip.style.translate = StyleKeyword.Null;
                foreach (VisualElement quadrant in quadrants) quadrant.RemoveFromClassList("target");

                int target = QuadrantAt(e.position);
                if (target < 0 || target == (int)Settings.hud.corners[(int)module]) return;
                Settings.hud.corners[(int)module] = (HudCorner)target;
                Settings.activePreset = -2;
                Settings.Changed();
            });
            return chip;
        }

        private int QuadrantAt(Vector2 panelPosition)
        {
            Rect area = positionArea.worldBound;
            if (!area.Contains(panelPosition)) return -1;
            bool right = panelPosition.x > area.center.x;
            bool bottom = panelPosition.y > area.center.y;
            return (bottom ? 2 : 0) + (right ? 1 : 0);
        }

        private void BuildThemes()
        {
            themeRow.Clear();
            themeButtons.Clear();
            ThemeInfo current = ui.Theme;
            foreach (ThemeInfo theme in Themes.All)
            {
                if (!theme.IsBuiltIn && theme != current) continue;

                ThemeInfo t = theme;
                var swatches = Div("swatches");
                for (int i = 0; i < theme.Swatches.Length; i++)
                {
                    var swatch = Div("theme-swatch");
                    swatch.style.backgroundColor = theme.Swatches[i];
                    swatches.Add(swatch);
                }
                bool selected = theme == current;
                var button = Tap(Div("theme-btn" + (selected ? " on" : ""), swatches, Text(theme.DisplayName, "fs-body" + (selected ? " w600" : ""))), () =>
                {
                    Settings.theme = t.Id;
                    Settings.Changed();
                });
                themeButtons.Add(button);
                themeRow.Add(button);
            }

            themeRow.Add(Tap(Div("theme-btn theme-custom", Icon("plus", 16f), Text("Custom", "fs-body")), CycleCustomTheme));
        }

        /// <summary>Cycles through themes registered with <see cref="Themes.Register"/>.</summary>
        private void CycleCustomTheme()
        {
            var custom = new List<ThemeInfo>();
            foreach (ThemeInfo theme in Themes.All)
            {
                if (!theme.IsBuiltIn) custom.Add(theme);
            }

            if (custom.Count == 0)
            {
                Debug.Log("[BugCatcher] No custom theme registered. Call Themes.Register(...) with a StyleSheet that defines .theme-<id>.");
                return;
            }

            customThemeIndex = (customThemeIndex + 1) % custom.Count;
            Settings.theme = custom[customThemeIndex].Id;
            Settings.Changed();
        }
    }
}
