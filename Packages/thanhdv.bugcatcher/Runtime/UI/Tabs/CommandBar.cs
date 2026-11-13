using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>
    /// Command input with suggestions: matches (tap to fill), recent commands, quick commands (one tap runs).
    /// Portrait shows suggestions above the input; landscape uses a column and desktop uses a popup.
    /// </summary>
    internal class CommandBar
    {
        private const int MobileMatches = 3;
        private const int DesktopMatches = 6;
        private const int LandscapeMatches = 20;
        private const int MobileRecent = 2;
        private const int MaxQuick = 8;
        private const int LandscapeMaxQuick = 32;

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

        public VisualElement Build(LayoutMode layoutMode, ScrollView suggestionsColumn)
        {
            mode = layoutMode;
            bool desktop = mode == LayoutMode.Desktop;
            var area = Div("cmd-area");

            suggestions = Div(desktop ? "sugg-pop" : "sugg");
            suggestions.pickingMode = PickingMode.Position;
            Show(suggestions, false);
            (suggestionsColumn?.contentContainer ?? area).Add(suggestions);

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
                if (mode != LayoutMode.Landscape)
                {
                    // Let a tap on a suggestion land before the list disappears.
                    field.schedule.Execute(Render).StartingIn(180);
                }
            });
            field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            VisualElement suggestionsSurface = suggestionsColumn != null ? (VisualElement)suggestionsColumn : suggestions;
            suggestionsSurface.RegisterCallback<PointerDownEvent>(KeepCommandFocus, TrickleDown.TrickleDown);

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

            area.Add(footer);
            focused = false;
            renderedFor = null;
            shown = false;
            if (suggestionsColumn != null) Render();
            return area;
        }

        private void KeepCommandFocus(PointerDownEvent evt)
        {
            if (!focused) return;

#if UNITY_6000_0_OR_NEWER
            field.focusController.IgnoreEvent(evt);
#else
            evt.PreventDefault();
#endif
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
            if (mode == LayoutMode.Landscape) field.Blur();
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
            string text = command + " ";
            field.value = text;
            if (focused)
            {
                TouchScreenKeyboard keyboard = field.touchScreenKeyboard;
                if (keyboard != null && keyboard.active)
                {
                    if (keyboard.text != text) keyboard.text = text;
                    if (keyboard.canSetSelection) keyboard.selection = new RangeInt(text.Length, 0);
                }
                field.SelectRange(text.Length, text.Length);
                return;
            }

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
            bool show = mode == LayoutMode.Landscape || historyOnly || focused;
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
                int matchLimit = desktop ? DesktopMatches : MobileMatches;
                if (mode == LayoutMode.Landscape) matchLimit = LandscapeMatches;
                BugCatcher.Commands.Suggest(text, matches, matchLimit);
                if (matches.Count > 0)
                {
                    if (!desktop) suggestions.Add(Div("sugg-head", Text("MATCHES", "lbl"), Text("tap to fill", "mono fs-caption c-muted")));
                    string typed = CommandService.FirstWord(text);
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

            BugCatcher.Commands.QuickCommands(quick, mode == LayoutMode.Landscape ? LandscapeMaxQuick : MaxQuick);
            if (quick.Count == 0) return;

            suggestions.Add(Div("sugg-head sugg-section", Text("QUICK COMMANDS", "lbl"), Text("no parameters · one tap runs", "mono fs-caption c-muted")));
            var chips = Div("quick-chips");
            foreach (string command in quick)
            {
                string c = command;
                chips.Add(Tap(Div("quick-chip", Text(c, "mono fs-chip")), () =>
                {
                    BugCatcher.Commands.Execute(c);
                    if (mode == LayoutMode.Landscape)
                    {
                        renderedFor = null;
                        Render();
                    }
                }));
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
            int match = CommandService.MatchIndex(info.Name, typed);
            var line = Div("cmd-name");
            if (match > 0) line.Add(Text(info.Name.Substring(0, match), "mono " + size));
            line.Add(Text(info.Name.Substring(match, typed.Length), "mono w600 c-accent " + size));
            line.Add(Text(info.Name.Substring(match + typed.Length), "mono " + size));
            if (info.Parameters.Length > 0)
            {
                // Literal "<name>": rich text would read it as a tag (and "<a>" throws inside Unity's text handle).
                Label parameters = Text(" " + info.Parameters, "mono c-muted " + size);
                parameters.enableRichText = false;
                line.Add(parameters);
            }
            return line;
        }

        private VisualElement RecentRow(string command) =>
            Tap(Div("sug sug-recent", Icon("history", 16f, "c-muted"), Text(command, "mono fs-body")), () => Fill(command.Trim()));

    }
}
