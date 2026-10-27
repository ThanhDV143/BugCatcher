using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>A row of the log list: one entry (collapsed) or one occurrence.</summary>
    internal struct LogItem
    {
        public LogEntry Entry;
        public DateTime Time;
        public int Frame;
        public int Count;
    }

    internal class ConsoleTab : IPanelTab
    {
        private const float RefreshInterval = 0.1f;
        private const int MaxMessageChars = 300;
        private static readonly string[] kindNames = { "Log", "Warning", "Error" };

        private readonly BugCatcherUI ui;
        private readonly PanelView panel;
        private readonly List<LogItem> items = new List<LogItem>();
        private readonly Label[] chipCounts = new Label[3];
        private readonly VisualElement[] chips = new VisualElement[3];
        private readonly CommandBar commandBar;
        private LayoutMode mode;
        private ListView list;
        private VisualElement collapseButton;
        private VisualElement searchButton;
        private VisualElement searchRow;
        private TextField searchField;
        private VisualElement moreMenu;
        private VisualElement aside;
        private string search = string.Empty;
        private LogEntry selected;
        private LogEntry asideEntry;
        private int asideCount;
        private bool dirty = true;
        private bool visible;
        private float refreshTimer;

        public ConsoleTab(BugCatcherUI ui, PanelView panel)
        {
            this.ui = ui;
            this.panel = panel;
            commandBar = new CommandBar(ui, OnSuggestionsShown);
            BugCatcher.Logs.OnChanged += () => dirty = true;
        }

        private static BugCatcherSettings Settings => BugCatcher.Settings;

        public VisualElement Build(LayoutMode layoutMode)
        {
            mode = layoutMode;
            bool desktop = mode == LayoutMode.Desktop;
            var root = Div("console" + (desktop ? " console-desktop" : ""));
            var main = Div("console-main");
            root.Add(main);

            main.Add(desktop ? BuildDesktopToolbar() : BuildMobileToolbar());
            if (!desktop) main.Add(BuildSearchRow());
            main.Add(BuildList());
            main.Add(commandBar.Build(mode));

            if (desktop)
            {
                aside = Div("console-aside");
                root.Add(aside);
                ShowDetailInAside();
            }
            else
            {
                aside = null;
            }

            moreMenu = BuildMoreMenu();
            root.Add(moreMenu);
            dirty = true;
            return root;
        }

        public void SetVisible(bool isVisible)
        {
            visible = isVisible;
            if (isVisible) dirty = true;
        }

        public void ApplySettings()
        {
            if (list == null) return;

            for (int i = 0; i < 3; i++) SetClass(chips[i], "on", FilterOn((LogKind)i));
            SetClass(collapseButton, "on", Settings.collapse);
            dirty = true;
        }

        public void Tick(float deltaTime)
        {
            refreshTimer += deltaTime;
            if (!dirty || refreshTimer < RefreshInterval) return;

            refreshTimer = 0f;
            dirty = false;
            Refresh();
        }

        // --- Toolbars ------------------------------------------------------------------

        private VisualElement BuildMobileToolbar()
        {
            var bar = Div("console-toolbar");
            for (int i = 0; i < 3; i++)
            {
                var kind = (LogKind)i;
                chipCounts[i] = Text("0", "mono fs-chip");
                chips[i] = Tap(Div("chip", Icon(Icons.ForKind(kind), 14f, KindColor(kind)), chipCounts[i]), () => ToggleFilter(kind));
                bar.Add(chips[i]);
            }
            bar.Add(Spacer());
            searchButton = Tap(Div("tb", Icon("search", 18f)), ToggleSearch);
            collapseButton = Tap(Div("tb", Icon("group", 18f)), ToggleCollapse);
            bar.Add(searchButton);
            bar.Add(collapseButton);
            bar.Add(Tap(Div("tb", Icon("more", 18f)), () => Show(moreMenu, moreMenu.resolvedStyle.display == DisplayStyle.None)));
            return bar;
        }

        private VisualElement BuildDesktopToolbar()
        {
            var bar = Div("console-toolbar desktop-toolbar");
            searchField = new TextField();
            searchField.AddToClassList("bare-field");
            searchField.AddToClassList("grow");
            Fonts.Apply(searchField);
            searchField.RegisterValueChangedCallback(e => SetSearch(e.newValue));
            bar.Add(Div("field search-box", Icon("search", 18f, "c-muted"), WithPlaceholder(searchField, "Search logs   Ctrl F")));

            for (int i = 0; i < 3; i++)
            {
                var kind = (LogKind)i;
                chipCounts[i] = Text("0", "mono fs-body c-secondary");
                chips[i] = Tap(Div("chip chip-lg", Icon(Icons.ForKind(kind), 16f, KindColor(kind)), Text(kindNames[i], "fs-body w500"), chipCounts[i]), () => ToggleFilter(kind));
                bar.Add(chips[i]);
            }
            bar.Add(Div("vdivider"));
            collapseButton = Tap(Div("tb", Icon("group")), ToggleCollapse);
            bar.Add(collapseButton);
            bar.Add(Tap(Div("tb", Icon("clear")), () => BugCatcher.Logs.Clear()));
            bar.Add(Tap(Div("tb", Icon("save")), SaveLog));
            return bar;
        }

        private VisualElement BuildSearchRow()
        {
            searchField = new TextField();
            searchField.AddToClassList("bare-field");
            searchField.AddToClassList("grow");
            Fonts.Apply(searchField);
            searchField.RegisterValueChangedCallback(e => SetSearch(e.newValue));
            searchRow = Div("search-row", Div("field search-box", Icon("search", 18f, "c-muted"), WithPlaceholder(searchField, "Search logs")));
            Show(searchRow, search.Length > 0);
            searchField.SetValueWithoutNotify(search);
            return searchRow;
        }

        private VisualElement BuildMoreMenu()
        {
            var menu = Div("popover more-menu");
            menu.Add(Tap(Div("menu-item", Icon("clear", 20f, "c-secondary"), Text("Clear logs", "fs-body")), () =>
            {
                Show(menu, false);
                BugCatcher.Logs.Clear();
            }));
            menu.Add(Tap(Div("menu-item", Icon("save", 20f, "c-secondary"), Text("Save log file", "fs-body")), () =>
            {
                Show(menu, false);
                SaveLog();
            }));
            menu.Add(Tap(Div("menu-item", Icon("share", 20f, "c-secondary"), Text("Share log", "fs-body")), () =>
            {
                Show(menu, false);
                BugCatcher.Share(BugCatcher.Logs.Export(), LogFileName());
            }));
            Show(menu, false);
            return menu;
        }

        /// <summary>TextField has no placeholder in 2022.3; overlay a muted label while it is empty.</summary>
        internal static VisualElement WithPlaceholder(TextField field, string placeholder)
        {
            var wrap = Div("placeholder-wrap grow");
            field.AddToClassList("fs-input");
            var hint = Text(placeholder, "fs-input c-muted placeholder");
            wrap.Add(field);
            wrap.Add(hint);
            void Update() => Show(hint, string.IsNullOrEmpty(field.value));
            field.RegisterValueChangedCallback(_ => Update());
            Update();
            return wrap;
        }

        private static string LogFileName() => "BugCatcher_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";

        private static void SaveLog()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, LogFileName());
            System.IO.File.WriteAllText(path, BugCatcher.Logs.Export());
            Debug.Log("[BugCatcher] Log saved: " + path);
        }

        private static string KindColor(LogKind kind) => kind == LogKind.Warning ? "c-warning" : kind == LogKind.Error ? "c-error" : "c-info";

        private static bool FilterOn(LogKind kind) =>
            kind == LogKind.Log ? Settings.showLog : kind == LogKind.Warning ? Settings.showWarning : Settings.showError;

        private static void ToggleFilter(LogKind kind)
        {
            switch (kind)
            {
                case LogKind.Log: Settings.showLog = !Settings.showLog; break;
                case LogKind.Warning: Settings.showWarning = !Settings.showWarning; break;
                default: Settings.showError = !Settings.showError; break;
            }
            Settings.Changed();
        }

        private static void ToggleCollapse()
        {
            Settings.collapse = !Settings.collapse;
            Settings.Changed();
        }

        private void ToggleSearch()
        {
            bool show = searchRow.resolvedStyle.display == DisplayStyle.None;
            Show(searchRow, show);
            SetClass(searchButton, "on", show);
            if (show) searchField.schedule.Execute(() => searchField.Focus());
            else SetSearch(string.Empty);
        }

        private void SetSearch(string text)
        {
            search = text ?? string.Empty;
            if (searchField != null && searchField.value != search) searchField.SetValueWithoutNotify(search);
            dirty = true;
        }

        // --- List ------------------------------------------------------------------------

        private VisualElement BuildList()
        {
            bool portrait = mode == LayoutMode.Portrait;
            list = new ListView(items, mode == LayoutMode.Desktop ? 38f : 34f, MakeRow, BindRow)
            {
                selectionType = SelectionType.None,
                virtualizationMethod = portrait ? CollectionVirtualizationMethod.DynamicHeight : CollectionVirtualizationMethod.FixedHeight
            };
            list.AddToClassList("log-list");
            list.AddToClassList("grow");
            ScrollView scroll = list.Q<ScrollView>();
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            if (mode != LayoutMode.Desktop) scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            return list;
        }

        private VisualElement MakeRow()
        {
            var row = Div("log-row");
            row.Add(Icon("info", mode == LayoutMode.Desktop ? 16f : 14f, "log-icon"));
            row.Add(Text("", "mono fs-caption c-muted log-time"));
            row.Add(Text("", "fs-body log-msg grow"));
            row.Add(Text("", "mono fs-caption count-badge"));
            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (row.userData is int index && index < items.Count) Select(items[index].Entry);
            });
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            LogItem item = items[index];
            row.userData = index;
            var icon = (IconElement)row[0];
            icon.Icon = Icons.ForKind(item.Entry.Kind);
            SetClass(icon, "c-info", item.Entry.Kind == LogKind.Log);
            SetClass(icon, "c-warning", item.Entry.Kind == LogKind.Warning);
            SetClass(icon, "c-error", item.Entry.Kind == LogKind.Error);

            ((Label)row[1]).text = item.Time.ToString(mode == LayoutMode.Desktop ? "HH:mm:ss.fff" : "mm:ss.fff");
            var message = (Label)row[2];
            message.text = FirstLine(item.Entry.Message);
            bool twoLines = mode == LayoutMode.Portrait && item.Entry.Kind == LogKind.Error;
            SetClass(row, "two-lines", twoLines);
            SetClass(message, "one-line", !twoLines);

            var count = (Label)row[3];
            count.text = item.Count > 1 ? Format.Repeat(item.Count) : string.Empty;
            Show(count, item.Count > 1);
            SetClass(row, "selected", item.Entry == selected);
        }

        private static string FirstLine(string message)
        {
            int newline = message.IndexOf('\n');
            if (newline >= 0) message = message.Substring(0, newline);
            return message.Length > MaxMessageChars ? message.Substring(0, MaxMessageChars) + "…" : message;
        }

        private void Refresh()
        {
            if (list == null) return;

            LogStore logs = BugCatcher.Logs;
            for (int i = 0; i < 3; i++) chipCounts[i].text = Format.Int(logs.CountOf((LogKind)i));
            if (!visible) return;

            ScrollView scroll = list.Q<ScrollView>();
            bool atBottom = scroll.verticalScroller.value >= scroll.verticalScroller.highValue - 4f;

            items.Clear();
            if (Settings.collapse)
            {
                foreach (LogEntry entry in logs.Entries)
                {
                    if (Passes(entry)) items.Add(new LogItem { Entry = entry, Time = entry.FirstTime, Frame = entry.FirstFrame, Count = entry.Count });
                }
            }
            else
            {
                foreach (LogRecord record in logs.Records)
                {
                    if (Passes(record.Entry)) items.Add(new LogItem { Entry = record.Entry, Time = record.Time, Frame = record.Frame, Count = 1 });
                }
            }

            list.RefreshItems();
            if (atBottom && items.Count > 0) list.ScrollToItem(items.Count - 1);
            if (aside != null && (asideEntry != selected || (selected != null && selected.Count != asideCount))) ShowDetailInAside();
        }

        private bool Passes(LogEntry entry)
        {
            if (!FilterOn(entry.Kind)) return false;
            return search.Length == 0 || entry.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Select(LogEntry entry)
        {
            selected = entry;
            list.RefreshItems();
            if (mode == LayoutMode.Desktop)
            {
                ShowDetailInAside();
                return;
            }

            panel.ShowPage(LogDetailView.Build(entry, mode, onBack: () =>
            {
                panel.ClosePage();
                selected = null;
                list.RefreshItems();
            }, onClose: ui.ClosePanel));
        }

        private void ShowDetailInAside()
        {
            asideEntry = selected;
            asideCount = selected?.Count ?? 0;
            aside.Clear();
            if (selected == null)
            {
                aside.Add(Div("aside-empty", Icon("console", 24f, "c-muted"), Text("Select a log to see its stack trace", "fs-body c-muted")));
                return;
            }
            aside.Add(LogDetailView.Build(selected, mode, null, null));
            ApplyGaps(aside);
        }

        private void OnSuggestionsShown(bool shown)
        {
            if (list != null && mode != LayoutMode.Desktop) SetClass(list, "dimmed", shown);
        }
    }
}
