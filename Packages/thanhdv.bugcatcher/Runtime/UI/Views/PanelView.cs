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
        /// <summary>Space kept between a focused field in a scroll view and the keyboard.</summary>
        private const float KeyboardGap = 10f;
        /// <summary>Height of a labelled rail tab (.rail-tab in the USS).</summary>
        private const float RailTabHeight = 52f;
        /// <summary>How long the keyboard layout outlives the keyboard, so the tap that closed it still lands.</summary>
        private const float KeyboardHoldTime = 0.3f;

        private static readonly string[] tabNames = { "Console", "HUD", "Audio", "System", "Settings" };
        private static readonly string[] tabIcons = { "console", "hud", "audio", "system", "settings" };

        private readonly BugCatcherUI ui;
        private readonly IPanelTab[] tabs;
        private readonly VisualElement[] tabContent = new VisualElement[5];
        private readonly VisualElement[] navItems = new VisualElement[5];
        private VisualElement container;
        /// <summary>Desktop window header; null in the phone layouts.</summary>
        private StepFit headerFit;
        private VisualElement body;
        private VisualElement page;
        private LayoutMode builtMode = (LayoutMode)(-1);
        private int animationToken;
        private float sheetDragStart;
        private float sheetDragOffset;
        private bool sheetDragging;
        private float keyboardLift;
        private float keyboardHoldUntil;
        /// <summary>System bars shown over the game at the sides (see ApplySystemBars), panel units.</summary>
        private float barLeft;
        private float barRight;

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
            if (builtMode == LayoutMode.Landscape) ApplySafeInsets();
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
                bool small = BugCatcher.Settings.textSize == TextSize.Small;
                bool large = BugCatcher.Settings.textSize == TextSize.Large;
                bool resized = container.ClassListContains("text-small") != small || container.ClassListContains("text-large") != large;
                container.EnableInClassList("text-small", small);
                container.EnableInClassList("text-large", large);
                // The header's step widths were measured at the old text size.
                if (resized) headerFit?.Reset();
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
            if (builtMode == LayoutMode.Desktop) return;

            ApplySystemBars();
            ApplyKeyboardInset();
        }

        // --- Building -------------------------------------------------------------------

        private void Build(bool force = false)
        {
            if (!force && builtMode == ui.Mode && container != null) return;

            bool wasOpen = IsOpen;
            builtMode = ui.Mode;
            Root.Clear();
            page = null;
            headerFit = null;

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
            // Edit in place: the native input strip Android/iOS add above the keyboard would cover the panel.
            container.Query<TextField>().ForEach(f => f.hideMobileInput = true);
            keyboardLift = 0f;

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
            rail.Add(Tap(Div("rail-close", Icon("close")), ui.ClosePanel));
            rail.Add(Div("rail-divider"));
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                navItems[i] = Tap(Div("rail-tab", Icon(tabIcons[i]), Text(tabNames[i], "fs-caption")), () => Select((PanelTab)index));
                rail.Add(navItems[i]);
            }
            container.Add(rail);
            rail.RegisterCallback<GeometryChangedEvent>(_ => FitRail(rail));

            body = Div("panel-body");
            container.Add(body);
            Root.Add(container);
        }

        /// <summary>Drops the tab labels when the rail is too short to show every tab at full size.</summary>
        private static void FitRail(VisualElement rail)
        {
            float available = rail.layout.height - rail.resolvedStyle.paddingTop - rail.resolvedStyle.paddingBottom;
            float needed = 0f;
            foreach (VisualElement child in rail.Children())
            {
                needed += child.ClassListContains("rail-tab") ? RailTabHeight : child.layout.height + child.resolvedStyle.marginTop + child.resolvedStyle.marginBottom;
            }
            rail.EnableInClassList("compact", available < needed);
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
            // Narrow window: the close hint goes first, then the brand name, then the tab labels.
            headerFit = new StepFit(header, new[] { "no-hint", "no-brand", "icon-tabs" }, () =>
            {
                float needed = 0f;
                foreach (VisualElement child in header.Children())
                {
                    if (child.resolvedStyle.display == DisplayStyle.None) continue;
                    // The tab strip stretches and clips; its tabs give its natural width.
                    float width = child == nav ? StepFit.OuterWidth(nav.Children()) : child.layout.width;
                    needed += width + child.resolvedStyle.marginLeft + child.resolvedStyle.marginRight;
                }
                return needed;
            });
            container.Add(header);

            body = Div("panel-body");
            container.Add(body);
            Root.Add(container);
        }

        public static VisualElement Keycap(string classes = "kc") => Div(classes, Text("`", "mono fs-chip"));

        // --- Safe area, keyboard, sheet height --------------------------------------------

        private void ApplySafeInsets()
        {
            // The layer is the safe area (it clips): the panel never draws under a notch or the home indicator.
            Insets safe = ui.SafeInsets;
            Root.style.left = safe.Left;
            Root.style.top = safe.Top;
            Root.style.right = safe.Right;
            Root.style.bottom = safe.Bottom;
            if (container == null) return;

            // System bars beyond the safe area: margins, so the panel animates clear of them (see USS).
            float marginLeft = Mathf.Max(0f, barLeft - safe.Left);
            float marginRight = Mathf.Max(0f, barRight - safe.Right);
            container.style.marginLeft = marginLeft;
            container.style.marginRight = marginRight;

            float width = ui.PanelSize.x - safe.Left - safe.Right - marginLeft - marginRight;
            switch (builtMode)
            {
                case LayoutMode.Portrait:
                    ApplySheetHeight();
                    break;
                case LayoutMode.Landscape:
                    container.style.width = CurrentTab == PanelTab.Console ? width : Mathf.Min(650f, width * 0.75f);
                    break;
                default:
                    container.style.top = 24f;
                    container.style.bottom = 24f;
                    container.style.right = 24f;
                    container.style.width = Mathf.Min(1040f, width - 48f);
                    break;
            }
        }

        private void ApplySheetHeight()
        {
            if (builtMode != LayoutMode.Portrait || container == null) return;

            float full = 5f;
            Insets margins = ui.ScreenMargins;
            float height = ui.PanelSize.y - margins.Top - margins.Bottom;
            float half = Mathf.Max(full, margins.Top + height * HalfSheet - ui.SafeInsets.Top);
            container.style.top = (BugCatcher.Settings.sheetHalf ? half : full) + sheetDragOffset;
        }

        private void ApplyKeyboardInset()
        {
            float lift = 0f;
            if (TouchScreenKeyboard.visible)
            {
                lift = Mathf.Max(0f, KeyboardHeight() / ui.Scale - ui.SafeInsets.Bottom);
            }

            // A tap that takes focus away closes the keyboard on pointer down; holding the layout a moment
            // keeps the tapped button (run, a suggestion) under the finger until pointer up.
            bool keyboardUp = lift > 0f;
            if (keyboardUp) keyboardHoldUntil = Time.unscaledTime + KeyboardHoldTime;
            else if (Time.unscaledTime < keyboardHoldUntil) lift = keyboardLift;

            if (Mathf.Approximately(lift, keyboardLift)) return;

            keyboardLift = lift;
            container.style.marginBottom = lift;
            if (lift > 0f) container.RegisterCallback<TransitionEndEvent>(OnKeyboardLayout);
        }

        /// <summary>Once the panel has finished shrinking (USS transition), brings a field inside a scroll view back into view.</summary>
        private void OnKeyboardLayout(TransitionEndEvent e)
        {
            if (e.target != e.currentTarget || !e.AffectsProperty("margin-bottom")) return;

            var target = (VisualElement)e.currentTarget;
            target.UnregisterCallback<TransitionEndEvent>(OnKeyboardLayout);
            // Next tick: the scroll view updates its scroll range in its own handler of this layout pass.
            target.schedule.Execute(() =>
            {
                if (!(target.panel?.focusController?.focusedElement is VisualElement focused)) return;
                ScrollView scroll = focused.GetFirstAncestorOfType<ScrollView>();
                if (scroll == null) return;

                Rect view = scroll.contentViewport.worldBound;
                // Measure the visible box (the bordered .field wrapper), not the bare TextField inside it.
                VisualElement box = focused;
                for (VisualElement el = focused; el != null && el != scroll; el = el.parent)
                {
                    if (el.ClassListContains("field")) { box = el; break; }
                }
                Rect field = box.worldBound;
                float delta = 0f;
                if (field.yMax + KeyboardGap > view.yMax) delta = field.yMax + KeyboardGap - view.yMax;
                else if (field.yMin < view.yMin) delta = field.yMin - view.yMin;
                scroll.scrollOffset += new Vector2(0f, delta);
            });
        }

        /// <summary>Keyboard height in screen pixels.</summary>
        private static float KeyboardHeight()
        {
            float height = TouchScreenKeyboard.area.height;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Unity reports an empty area on some Android devices; measure the window's visible frame instead.
            if (height <= 0f) height = AndroidCovered().Bottom;
#endif
            return height;
        }

        /// <summary>
        /// Android: the immersive game's navigation bar shows (over the game) while the keyboard is up,
        /// and the safe area never includes it; keeps the panel clear of it.
        /// </summary>
        private void ApplySystemBars()
        {
            float left = 0f;
            float right = 0f;
#if UNITY_ANDROID && !UNITY_EDITOR
            Insets covered = AndroidCovered();
            left = covered.Left / ui.Scale;
            right = covered.Right / ui.Scale;
#endif
            if (Mathf.Approximately(left, barLeft) && Mathf.Approximately(right, barRight)) return;

            barLeft = left;
            barRight = right;
            ApplySafeInsets();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static Insets androidCovered;
        private static float androidPolled = float.NegativeInfinity;

        /// <summary>Screen pixels each edge loses to the keyboard or system bars. Polled at 10 Hz: each poll is several JNI calls.</summary>
        private static Insets AndroidCovered()
        {
            if (Time.unscaledTime - androidPolled < 0.1f) return androidCovered;

            androidPolled = Time.unscaledTime;
            androidCovered = MeasureAndroidCovered();
            return androidCovered;
        }

        private static Insets MeasureAndroidCovered()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
            using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
            using (var visible = new AndroidJavaObject("android.graphics.Rect"))
            {
                decor.Call("getWindowVisibleDisplayFrame", visible);
                int width = decor.Call<int>("getWidth");
                int height = decor.Call<int>("getHeight");
                if (width <= 0 || height <= 0) return default;

                // Screen may render below native resolution.
                float sx = (float)Screen.width / width;
                float sy = (float)Screen.height / height;
                return new Insets
                {
                    Left = Mathf.Max(0, visible.Get<int>("left")) * sx,
                    Right = Mathf.Max(0, width - visible.Get<int>("right")) * sx,
                    Bottom = Mathf.Max(0, height - visible.Get<int>("bottom")) * sy
                };
            }
        }
#endif

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
