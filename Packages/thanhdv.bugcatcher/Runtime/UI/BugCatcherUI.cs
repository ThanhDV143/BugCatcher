using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher.UI
{
    internal enum LayoutMode { Portrait, Landscape, Desktop }

    internal enum PanelTab { Console, Hud, Audio, System, Settings }

    /// <summary>The screen the UI lays itself out for. Normally the real screen; previews can substitute one.</summary>
    internal struct DisplayInfo
    {
        public int Width;
        public int Height;
        public Rect SafeArea;
        public float Dpi;
        public bool Mobile;

        public static DisplayInfo Current => new DisplayInfo
        {
            Width = Screen.width,
            Height = Screen.height,
            SafeArea = Screen.safeArea,
            Dpi = Screen.dpi,
            Mobile = Application.isMobilePlatform
        };

        public bool Same(DisplayInfo other) =>
            Width == other.Width && Height == other.Height && SafeArea == other.SafeArea && Mobile == other.Mobile;
    }

    /// <summary>Screen insets (safe area) in panel units.</summary>
    internal struct Insets
    {
        public float Left, Top, Right, Bottom;
    }

    /// <summary>
    /// Owns the UI Toolkit document and the layers drawn on top of the game:
    /// HUD → bubble → gesture rings → panel → quick menu → preview catcher.
    /// </summary>
    internal class BugCatcherUI
    {
        private const float DesignShortEdge = 375f;

        private readonly PanelSettings panelSettings;
        private readonly OpenTriggers triggers;
        private readonly HudView hud;
        private readonly BubbleView bubble;
        private readonly RecordingBubbleView recording;
        private readonly GestureOverlay gestures;
        private readonly PanelView panel;
        private readonly QuickMenu quickMenu;
        private readonly VisualElement previewCatcher;
        private readonly StyleSheet baseSheet;
        private DisplayInfo display;
        private string appliedTheme;
        private bool previewing;

        public BugCatcherUI(GameObject host, OpenTriggers triggers)
        {
            this.triggers = triggers;
            Fonts.Load();

            // A copy of the shipped asset (it carries the empty theme), so runtime scale changes never touch the asset.
            panelSettings = Object.Instantiate(Resources.Load<PanelSettings>("BugCatcher/BugCatcherPanelSettings"));
            panelSettings.name = "BugCatcher PanelSettings";

            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            VisualElement documentRoot = document.rootVisualElement;
            documentRoot.pickingMode = PickingMode.Ignore;
            // The empty theme does not size the document root, so stretch it here.
            documentRoot.style.position = Position.Absolute;
            documentRoot.style.left = documentRoot.style.top = documentRoot.style.right = documentRoot.style.bottom = 0f;

            baseSheet = Resources.Load<StyleSheet>("BugCatcher/BugCatcher");
            Root = UIB.Div("bc");
            Root.pickingMode = PickingMode.Ignore;
            Root.styleSheets.Add(baseSheet);
            Fonts.Apply(Root);
            documentRoot.Add(Root);

            Safe = UIB.Div("safe");
            Safe.pickingMode = PickingMode.Ignore;
            Root.Add(Safe);

            hud = new HudView(this);
            Safe.Add(hud.Root);
            bubble = new BubbleView(this);
            Safe.Add(bubble.Root);
            recording = new RecordingBubbleView(this);
            Safe.Add(recording.Root);
            bubble.Pair(recording);
            recording.Pair(bubble);
            gestures = new GestureOverlay(this);
            Root.Add(gestures.Root);
            panel = new PanelView(this);
            Root.Add(panel.Root);
            quickMenu = new QuickMenu(this);
            Root.Add(quickMenu.Root);

            previewCatcher = UIB.Tap(UIB.Div("preview-catcher"), EndPreview);
            UIB.Show(previewCatcher, false);
            Root.Add(previewCatcher);

            Settings.OnChanged += ApplySettings;
            Themes.OnRegistered += ApplySettings;
            BugCatcher.Logs.OnNewError += bubble.PulseError;
            BugCatcher.Recorder.StateChanged += () =>
            {
                recording.ApplyState();
                bubble.Refresh();
            };
            UpdateScreen(true);
            ApplySettings();
        }

        /// <summary>Renders the overlay into <paramref name="target"/> laid out for <paramref name="info"/>; null restores the screen.</summary>
        internal void SetPreviewTarget(RenderTexture target, DisplayInfo? info)
        {
            panelSettings.targetTexture = target;
            // Previews have no game behind them: clear to the design's game-view colour.
            panelSettings.clearColor = target != null;
            panelSettings.colorClearValue = new Color(0.133f, 0.2f, 0.227f);
            DisplayOverride = info;
            UpdateScreen(true);
        }

        public static BugCatcherSettings Settings => BugCatcher.Settings;

        public VisualElement Root { get; }
        /// <summary>Full-screen container inset by the device safe area.</summary>
        public VisualElement Safe { get; }
        /// <summary>HUD layer inside <see cref="Safe"/>; the bubble keeps clear of its modules.</summary>
        public VisualElement HudRoot => hud.Root;
        public LayoutMode Mode { get; private set; }
        public Insets SafeInsets { get; private set; }
        public float Scale { get; private set; } = 1f;
        public bool IsPanelOpen => panel.IsOpen;
        public ThemeInfo Theme => Themes.FindOrDefault(Settings.theme);

        public void OpenPanel() => OpenPanel(panel.CurrentTab);

        public void OpenPanel(PanelTab tab)
        {
            quickMenu.Close();
            EndPreview();
            panel.Open(tab);
            bubble.Refresh();
        }

        /// <summary>Lets the check-mark animation play (scale 1 → 1.12 → 1) before the panel covers it.</summary>
        public void OpenPanelAfterGesture() => Root.schedule.Execute(() => OpenPanel()).StartingIn(250);

        public void ClosePanel()
        {
            panel.Close();
            bubble.Refresh();
        }

        public void TogglePanel()
        {
            if (IsPanelOpen) ClosePanel();
            else OpenPanel();
        }

        public void ShowQuickMenu()
        {
            bubble.SetMenuOpen(true);
            quickMenu.Open(bubble.Root);
        }

        public void OnQuickMenuClosed() => bubble.SetMenuOpen(false);

        /// <summary>Hides the panel so the HUD can be seen; any tap brings the panel back.</summary>
        public void StartPreview()
        {
            panel.Close();
            previewing = true;
            UIB.Show(previewCatcher, true);
        }

        private void EndPreview()
        {
            if (!previewing) return;
            previewing = false;
            UIB.Show(previewCatcher, false);
            panel.Open(PanelTab.Hud);
        }

        public void SetHidden(bool hidden) => Root.visible = !hidden;

        /// <summary>True while any text field (ours or the game's uGUI one) has keyboard focus.</summary>
        public bool IsTyping()
        {
            if (Root.panel?.focusController?.focusedElement is VisualElement focused && focused.GetFirstOfType<TextField>() != null) return true;

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && (selected.GetComponent<UnityEngine.UI.InputField>() != null || selected.GetComponent("TMP_InputField") != null);
        }

        /// <summary>Screen position (origin bottom-left, pixels) to panel coordinates.</summary>
        public Vector2 ScreenToPanel(Vector2 screen) => new Vector2(screen.x / Scale, (display.Height - screen.y) / Scale);

        public Vector2 PanelSize => new Vector2(display.Width / Scale, display.Height / Scale);

        /// <summary>When set, the UI lays out for this display instead of the real screen (previews and screenshots).</summary>
        internal DisplayInfo? DisplayOverride { get; set; }

        /// <summary>bg/overlay with the HUD opacity setting applied.</summary>
        public Color OverlayColor
        {
            get
            {
                Color color = Theme.OverlayBase;
                color.a = Settings.hud.opacity;
                return color;
            }
        }

        public void Tick(float deltaTime)
        {
            UpdateScreen(false);
            hud.Tick(deltaTime);
            bubble.Tick(deltaTime);
            recording.Tick(deltaTime);
            gestures.Tick(triggers);
            panel.Tick(deltaTime);
        }

        private void ApplySettings()
        {
            ThemeInfo theme = Theme;
            if (appliedTheme != theme.UssClass)
            {
                if (appliedTheme != null) Root.RemoveFromClassList(appliedTheme);
                Root.AddToClassList(theme.UssClass);
                appliedTheme = theme.UssClass;

                Root.styleSheets.Clear();
                Root.styleSheets.Add(baseSheet);
                if (theme.StyleSheet != null) Root.styleSheets.Add(theme.StyleSheet);
            }

            hud.Apply();
            bubble.Refresh();
            panel.ApplySettings();
            quickMenu.Refresh();
        }

        private void UpdateScreen(bool force)
        {
            DisplayInfo current = DisplayOverride ?? DisplayInfo.Current;
            if (!force && current.Same(display)) return;

            display = current;
            Scale = ComputeScale(display);
            panelSettings.scale = Scale;

            Rect safe = display.SafeArea;
            SafeInsets = new Insets
            {
                Left = safe.xMin / Scale,
                Top = (display.Height - safe.yMax) / Scale,
                Right = (display.Width - safe.xMax) / Scale,
                Bottom = safe.yMin / Scale
            };
            Safe.style.left = SafeInsets.Left;
            Safe.style.top = SafeInsets.Top;
            Safe.style.right = SafeInsets.Right;
            Safe.style.bottom = SafeInsets.Bottom;

            LayoutMode mode = !display.Mobile ? LayoutMode.Desktop : display.Width > display.Height ? LayoutMode.Landscape : LayoutMode.Portrait;
            Root.EnableInClassList("layout-desktop", mode == LayoutMode.Desktop);
            Root.EnableInClassList("layout-mobile", mode != LayoutMode.Desktop);
            bool modeChanged = mode != Mode || force;
            Mode = mode;

            hud.OnScreenChanged();
            bubble.Refresh();
            panel.OnScreenChanged(modeChanged);
            quickMenu.Close();
        }

        private static float ComputeScale(DisplayInfo info)
        {
            float shortEdge = Mathf.Min(info.Width, info.Height);
            if (!info.Mobile)
            {
                return info.Dpi > 0f ? Mathf.Clamp(info.Dpi / 96f, 1f, 2f) : 1f;
            }

            // Phones: the design is drawn for a 375 pt short edge. Tablets: keep physical size close to a phone's.
            float byEdge = shortEdge / DesignShortEdge;
            float byDpi = info.Dpi > 0f ? info.Dpi / 150f : byEdge;
            return Mathf.Clamp(Mathf.Min(byEdge, Mathf.Max(byDpi, 1f)), 1f, 4f);
        }
    }
}
