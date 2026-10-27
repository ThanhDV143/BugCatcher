using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>A panel tab. Content is rebuilt whenever the layout mode changes.</summary>
    internal interface IPanelTab
    {
        VisualElement Build(LayoutMode mode);
        void SetVisible(bool visible);
        void Tick(float deltaTime);
        void ApplySettings();
    }

    /// <summary>
    /// The panel in three layouts: bottom sheet with top tabs (phone portrait), right side panel
    /// with a tab rail (phone landscape), floating window with header tabs (desktop / Editor).
    /// </summary>
    internal class PanelView
    {
        private const float AnimationMs = 200f;
        private const float HalfSheet = 0.43f;
        private const float SheetDragSnap = 80f;

        private static readonly string[] tabNames = { "Console", "HUD", "Audio", "System", "Settings" };
        private static readonly string[] tabIcons = { "console", "hud", "audio", "system", "settings" };

        private readonly BugCatcherUI ui;
        private readonly IPanelTab[] tabs;
        private readonly VisualElement[] tabContent = new VisualElement[5];
        private readonly VisualElement[] navItems = new VisualElement[5];
        private VisualElement container;
        private VisualElement body;
        private VisualElement page;
        private LayoutMode builtMode = (LayoutMode)(-1);
        private int animationToken;
        private float sheetDragStart;
        private float sheetDragOffset;
        private bool sheetDragging;

        public PanelView(BugCatcherUI ui)
        {
            this.ui = ui;
            Root = Div("panel-layer");
            Root.pickingMode = PickingMode.Ignore;
            Show(Root, false);

            tabs = new IPanelTab[]
            {
                new ConsoleTab(ui, this),
                new HudTab(ui),
                new AudioTab(),
                new SystemTab(),
                new SettingsTab()
            };
        }

        public VisualElement Root { get; }
        public bool IsOpen { get; private set; }
        public PanelTab CurrentTab { get; private set; } = PanelTab.Console;
        public LayoutMode Mode => builtMode;

        public void Open(PanelTab tab)
        {
            Build();
            Select(tab);
            if (IsOpen) return;

            IsOpen = true;
            int token = ++animationToken;
            Show(Root, true);
            container.RemoveFromClassList("open");
            container.schedule.Execute(() =>
            {
                if (token == animationToken) container.AddToClassList("open");
            });
            tabs[(int)CurrentTab].SetVisible(true);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            ClosePage();
            tabs[(int)CurrentTab].SetVisible(false);
            int token = ++animationToken;
            container.RemoveFromClassList("open");
            container.schedule.Execute(() =>
            {
                if (token == animationToken) Show(Root, false);
            }).StartingIn((long)AnimationMs);
            // Hand keyboard focus back to the game.
            ui.Root.panel?.focusController?.focusedElement?.Blur();
        }

        public void Select(PanelTab tab)
        {
            if (IsOpen && tab != CurrentTab) tabs[(int)CurrentTab].SetVisible(false);
            ClosePage();
            CurrentTab = tab;
            for (int i = 0; i < tabContent.Length; i++)
            {
                Show(tabContent[i], i == (int)tab);
                SetClass(navItems[i], "active", i == (int)tab);
                foreach (Label label in navItems[i].Query<Label>().ToList()) SetClass(label, "w600", i == (int)tab);
            }
            if (IsOpen) tabs[(int)tab].SetVisible(true);
        }

        /// <summary>Phone layouts: shows a full-panel page (log detail) over the tabs.</summary>
        public void ShowPage(VisualElement content)
        {
            ClosePage();
            page = content;
            page.AddToClassList("panel-page");
            container.Add(page);
            ApplyGaps(page);
            ApplySafeInsets();
        }

        public void ClosePage()
        {
            page?.RemoveFromHierarchy();
            page = null;
        }

        public void ApplySettings()
        {
            if (container != null)
            {
                container.EnableInClassList("text-small", BugCatcher.Settings.textSize == TextSize.Small);
                container.EnableInClassList("text-large", BugCatcher.Settings.textSize == TextSize.Large);
                ApplySheetHeight();
            }
            foreach (IPanelTab tab in tabs) tab.ApplySettings();
        }

        public void OnScreenChanged(bool modeChanged)
        {
            if (modeChanged && container != null) Build(true);
            ApplySafeInsets();
        }

        public void Tick(float deltaTime)
        {
            if (!IsOpen) return;

            tabs[(int)CurrentTab].Tick(deltaTime);
            if (builtMode != LayoutMode.Desktop) ApplyKeyboardInset();
        }

        // --- Building -------------------------------------------------------------------

        private void Build(bool force = false)
        {
            if (!force && builtMode == ui.Mode && container != null) return;

            bool wasOpen = IsOpen;
            builtMode = ui.Mode;
            Root.Clear();
            page = null;

            switch (builtMode)
            {
                case LayoutMode.Portrait: BuildSheet(); break;
                case LayoutMode.Landscape: BuildSide(); break;
                default: BuildWindow(); break;
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                tabContent[i] = tabs[i].Build(builtMode);
                tabContent[i].AddToClassList("tab-content");
                body.Add(tabContent[i]);
            }

            ApplyGaps(container);
            if (wasOpen) container.AddToClassList("open");
            Select(CurrentTab);
            ApplySettings();
            ApplySafeInsets();
        }

        private void BuildSheet()
        {
            container = Div("panel sheet");
            var handle = Div("sheet-handle", Div("handle-bar"));
            handle.pickingMode = PickingMode.Position;
            handle.RegisterCallback<PointerDownEvent>(OnHandleDown);
            handle.RegisterCallback<PointerMoveEvent>(OnHandleMove);
            handle.RegisterCallback<PointerUpEvent>(OnHandleUp);
            container.Add(handle);

            var nav = Div("tabs");
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                navItems[i] = Tap(Div("tab", Icon(tabIcons[i], 18f), Text(tabNames[i], "fs-caption")), () => Select((PanelTab)index));
                nav.Add(navItems[i]);
            }
            nav.Add(Tap(Div("icon-btn close-btn", Icon("close")), ui.ClosePanel));
            container.Add(nav);

            body = Div("panel-body");
            container.Add(body);
            Root.Add(container);
        }

        private void BuildSide()
        {
            container = Div("panel side");
            var rail = Div("rail");
            rail.Add(Tap(Div("rail-tab", Icon("close")), ui.ClosePanel));
            rail.Add(Div("rail-divider"));
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                navItems[i] = Tap(Div("rail-tab", Icon(tabIcons[i]), Text(tabNames[i], "fs-caption")), () => Select((PanelTab)index));
                rail.Add(navItems[i]);
            }
            container.Add(rail);

            body = Div("panel-body");
            container.Add(body);
            Root.Add(container);
        }

        private void BuildWindow()
        {
            container = Div("panel window");
            var header = Div("window-header");
            header.Add(Div("brand", Icon("bug", 20f, "c-accent"), Text("BugCatcher", "fs-title w600")));
            var nav = Div("dtabs");
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                navItems[i] = Tap(Div("dtab", Icon(tabIcons[i], 18f), Text(tabNames[i], "fs-body")), () => Select((PanelTab)index));
                nav.Add(navItems[i]);
            }
            header.Add(nav);
            header.Add(Div("close-hint", Keycap(), Keycap(), Keycap(), Text("to close", "fs-caption c-muted hint-text")));
            header.Add(Tap(Div("icon-btn close-btn-sm", Icon("close")), ui.ClosePanel));
            container.Add(header);

            body = Div("panel-body");
            container.Add(body);
            Root.Add(container);
        }

        public static VisualElement Keycap(string classes = "kc") => Div(classes, Text("`", "mono fs-chip"));

        // --- Safe area, keyboard, sheet height --------------------------------------------

        private void ApplySafeInsets()
        {
            if (container == null) return;

            Insets safe = ui.SafeInsets;
            Vector2 size = ui.PanelSize;
            switch (builtMode)
            {
                case LayoutMode.Portrait:
                    ApplySheetHeight();
                    break;
                case LayoutMode.Landscape:
                    container.style.width = Mathf.Min(500f + safe.Right, size.x * 0.75f);
                    container.style.paddingRight = safe.Right;
                    container.Q(className: "rail").style.paddingBottom = safe.Bottom;
                    break;
                default:
                    container.style.top = 24f + safe.Top;
                    container.style.bottom = 24f + safe.Bottom;
                    container.style.right = 24f + safe.Right;
                    container.style.width = Mathf.Min(1040f, size.x - 48f - safe.Left - safe.Right);
                    break;
            }

            // Footers and scrolling content clear the home indicator.
            float bottom = builtMode == LayoutMode.Desktop ? 0f : safe.Bottom;
            container.Query(className: "footer").ForEach(f => f.style.paddingBottom = Mathf.Max(12f, bottom));
            container.Query(className: "pad-safe").ForEach(f => f.style.paddingBottom = Mathf.Max(16f, bottom));
        }

        private void ApplySheetHeight()
        {
            if (builtMode != LayoutMode.Portrait || container == null) return;

            float full = ui.SafeInsets.Top + 5f;
            float half = ui.PanelSize.y * HalfSheet;
            container.style.top = (BugCatcher.Settings.sheetHalf ? half : full) + sheetDragOffset;
        }

        private void ApplyKeyboardInset()
        {
            float lift = 0f;
            if (TouchScreenKeyboard.visible)
            {
                Rect area = TouchScreenKeyboard.area;
                lift = Mathf.Max(0f, area.height / ui.Scale - ui.SafeInsets.Bottom);
            }
            container.style.marginBottom = lift;
        }

        private void OnHandleDown(PointerDownEvent e)
        {
            sheetDragging = true;
            sheetDragStart = e.position.y;
            sheetDragOffset = 0f;
            container.AddToClassList("dragging");
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
        }

        private void OnHandleMove(PointerMoveEvent e)
        {
            if (!sheetDragging) return;
            sheetDragOffset = e.position.y - sheetDragStart;
            ApplySheetHeight();
        }

        private void OnHandleUp(PointerUpEvent e)
        {
            if (!sheetDragging) return;

            ((VisualElement)e.currentTarget).ReleasePointer(e.pointerId);
            sheetDragging = false;
            container.RemoveFromClassList("dragging");
            float offset = sheetDragOffset;
            sheetDragOffset = 0f;

            BugCatcherSettings settings = BugCatcher.Settings;
            if (offset > SheetDragSnap)
            {
                if (settings.sheetHalf)
                {
                    ApplySheetHeight();
                    ui.ClosePanel();
                    return;
                }
                settings.sheetHalf = true;
            }
            else if (offset < -SheetDragSnap)
            {
                settings.sheetHalf = false;
            }
            settings.Changed();
        }
    }
}
