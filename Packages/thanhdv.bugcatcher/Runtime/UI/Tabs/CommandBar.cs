using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>
    /// Command input with suggestions: matches (tap to fill), recent commands, quick commands (one tap runs).
    /// Phones show suggestions as a block above the input; desktop shows a popup with keyboard navigation.
    /// </summary>
    internal class CommandBar
    {
        private const int MobileMatches = 3;
        private const int DesktopMatches = 6;
        private const int MobileRecent = 2;
        private const int MaxQuick = 8;

        private readonly BugCatcherUI ui;
        private readonly Action<bool> onShown;
        private readonly List<CommandInfo> matches = new List<CommandInfo>();
        private readonly List<string> quick = new List<string>();
        private readonly List<VisualElement> matchRows = new List<VisualElement>();
        private LayoutMode mode;
        private TextField field;
        private VisualElement inputBox;
        private VisualElement suggestions;
        private VisualElement historyButton;
        private bool focused;
        private bool historyOnly;
        private bool shown;
        private int highlighted;
        private string renderedFor;

        public CommandBar(BugCatcherUI ui, Action<bool> onShown)
        {
            this.ui = ui;
            this.onShown = onShown;
        }

        /// <summary>Returns the suggestions block (phones) and input row; both go at the bottom of the console.</summary>
        public VisualElement Build(LayoutMode layoutMode)
        {
            mode = layoutMode;
            bool desktop = mode == LayoutMode.Desktop;
            var root = Div("cmd-area");

            suggestions = Div(desktop ? "sugg-pop" : "sugg");
            suggestions.pickingMode = PickingMode.Position;
            Show(suggestions, false);
            root.Add(suggestions);

            field = new TextField();
            field.AddToClassList("bare-field");
            field.AddToClassList("mono");
            Fonts.Apply(field);
            field.RegisterValueChangedCallback(_ => Render());
            field.RegisterCallback<FocusInEvent>(_ =>
            {
                focused = true;
                SetClass(inputBox, "focused", true);
                Render();
            });
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                focused = false;
                SetClass(inputBox, "focused", false);
                // Let a tap on a suggestion land before the list disappears.
                field.schedule.Execute(Render).StartingIn(180);
            });
            field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            var footer = Div("cmd-bar footer");
            inputBox = Div("field cmd-box grow", Text(">", "mono w600 c-accent cmd-prompt"), ConsoleTab.WithPlaceholder(field, "Enter command"));
            footer.Add(inputBox);

            // Phone portrait keeps the history button inside the input, as in the design; other layouts have none.
            historyButton = Tap(Div("icon-btn history-btn", Icon("history", 18f)), ToggleHistory);
            if (mode == LayoutMode.Portrait) inputBox.Add(historyButton);

            if (desktop)
            {
                footer.Add(Tap(Div("btn btn-accent run-text", Text("Run", "fs-body w600"), Icon("run", 18f)), Run));
            }
            else
            {
                footer.Add(Tap(Div("btn-square btn-accent", Icon("run")), Run));
            }

            root.Add(footer);
            renderedFor = null;
            shown = false;
            return root;
        }

        private void ToggleHistory()
        {
            historyOnly = !historyOnly;
            SetClass(historyButton, "on", historyOnly);
            renderedFor = null;
            Render();
        }

        private void Run()
        {
            string command = field.value;
            if (string.IsNullOrWhiteSpace(command)) return;

            BugCatcher.Commands.Execute(command);
            field.SetValueWithoutNotify(string.Empty);
            historyOnly = false;
            SetClass(historyButton, "on", false);
            renderedFor = null;
            UpdatePlaceholder();
            Render();
            if (mode == LayoutMode.Desktop) field.schedule.Execute(() => field.Focus());
        }

        private void UpdatePlaceholder()
        {
            Label placeholder = field.parent?.Q<Label>(className: "placeholder");
            if (placeholder != null) Show(placeholder, string.IsNullOrEmpty(field.value));
        }

        private void Fill(string command)
        {
            field.value = command + " ";
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectRange(field.value.Length, field.value.Length);
            });
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            switch (e.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Run();
                    e.StopPropagation();
                    break;
                case KeyCode.Tab:
                    if (matches.Count > 0)
                    {
                        Fill(matches[Mathf.Clamp(highlighted, 0, matches.Count - 1)].Name);
                        e.StopPropagation();
                    }
                    break;
                case KeyCode.UpArrow:
                    MoveHighlight(-1);
                    e.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    MoveHighlight(1);
                    e.StopPropagation();
                    break;
                case KeyCode.Escape:
                    field.Blur();
                    break;
            }

            // TextField would insert a tab / newline otherwise.
            if (e.character == '\t' || e.character == '\n') e.StopPropagation();
        }

        private void MoveHighlight(int delta)
        {
            if (matchRows.Count == 0) return;
            highlighted = (highlighted + delta + matchRows.Count) % matchRows.Count;
            for (int i = 0; i < matchRows.Count; i++) SetClass(matchRows[i], "on", i == highlighted);
        }

        private void Render()
        {
            string text = field.value ?? string.Empty;
            bool show = historyOnly || focused;
            if (shown != show)
            {
                shown = show;
                Show(suggestions, show);
                onShown?.Invoke(show);
            }

            string key = (historyOnly ? "H:" : "") + text;
            if (!show || key == renderedFor) return;
            renderedFor = key;

            suggestions.Clear();
            matchRows.Clear();
            highlighted = 0;
            bool desktop = mode == LayoutMode.Desktop;

            if (!historyOnly)
            {
                BugCatcher.Commands.Suggest(text, matches, desktop ? DesktopMatches : MobileMatches);
                if (matches.Count > 0)
                {
                    if (!desktop) suggestions.Add(Div("sugg-head", Text("MATCHES", "lbl"), Text("tap to fill", "mono fs-caption c-muted")));
                    string typed = FirstWord(text);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        CommandInfo info = matches[i];
                        VisualElement row = desktop ? DesktopMatch(info, typed) : MobileMatch(info, typed);
                        SetClass(row, "on", i == 0);
                        matchRows.Add(row);
                        suggestions.Add(row);
                    }
                }
            }
            else
            {
                matches.Clear();
            }

            if (desktop)
            {
                if (matches.Count == 0)
                {
                    foreach (string recent in BugCatcher.Commands.Recent) suggestions.Add(RecentRow(recent));
                }
                if (suggestions.childCount == 0)
                {
                    Show(suggestions, false);
                    shown = false;
                    return;
                }
                suggestions.Add(Div("sugg-keys", Text("↑ ↓ to move · Tab to fill · Enter to run", "fs-caption c-muted")));
                return;
            }

            IReadOnlyList<string> recentCommands = BugCatcher.Commands.Recent;
            if (recentCommands.Count > 0)
            {
                suggestions.Add(Div("sugg-head sugg-section", Text("RECENT", "lbl")));
                int count = historyOnly ? recentCommands.Count : Mathf.Min(MobileRecent, recentCommands.Count);
                for (int i = 0; i < count; i++) suggestions.Add(RecentRow(recentCommands[i]));
            }

            if (historyOnly) return;

            BugCatcher.Commands.QuickCommands(quick, MaxQuick);
            if (quick.Count == 0) return;

            suggestions.Add(Div("sugg-head sugg-section", Text("QUICK COMMANDS", "lbl"), Text("no parameters · one tap runs", "mono fs-caption c-muted")));
            var chips = Div("quick-chips");
            foreach (string command in quick)
            {
                string c = command;
                chips.Add(Tap(Div("quick-chip", Text(c, "mono fs-chip")), () => BugCatcher.Commands.Execute(c)));
            }
            suggestions.Add(chips);
        }

        private VisualElement MobileMatch(CommandInfo info, string typed)
        {
            var row = Tap(Div("sug"), () => Fill(info.Name));
            row.Add(CommandName(info, typed, "fs-body"));
            if (info.Description.Length > 0) row.Add(Text(info.Description, "fs-caption c-secondary"));
            return row;
        }

        private VisualElement DesktopMatch(CommandInfo info, string typed)
        {
            var row = Tap(Div("sug-desk"), () => Fill(info.Name));
            row.Add(CommandName(info, typed, "fs-body"));
            row.Add(Text(info.Description, "fs-caption c-secondary sug-desc"));
            return row;
        }

        private static VisualElement CommandName(CommandInfo info, string typed, string size)
        {
            int prefix = Mathf.Min(typed.Length, info.Name.Length);
            var line = Div("cmd-name");
            line.Add(Text(info.Name.Substring(0, prefix), "mono w600 c-accent " + size));
            line.Add(Text(info.Name.Substring(prefix), "mono " + size));
            if (info.Parameters.Length > 0) line.Add(Text(" " + info.Parameters, "mono c-muted " + size));
            return line;
        }

        private VisualElement RecentRow(string command) =>
            Tap(Div("sug sug-recent", Icon("history", 16f, "c-muted"), Text(command, "mono fs-body")), () => Fill(command.Trim()));

        private static string FirstWord(string input)
        {
            input = input.TrimStart();
            int space = input.IndexOf(' ');
            return space < 0 ? input : input.Substring(0, space);
        }
    }
}
