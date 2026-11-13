using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>Message, stack trace and a copy button. A full page on phones, the right-hand aside on desktop.</summary>
    internal static class LogDetailView
    {
        /// <summary>How long the button shows "Copied" after a tap.</summary>
        private const long CopiedMs = 1500;

        private struct Frame
        {
            public string Method;
            public string File;
            public int Line;
        }

        public static VisualElement Build(LogEntry entry, LayoutMode mode, Action onBack, Action onClose)
        {
            bool desktop = mode == LayoutMode.Desktop;
            var root = Div(desktop ? "detail detail-aside" : "detail");

            if (!desktop)
            {
                root.Add(Div("detail-header",
                    Tap(Div("icon-btn", Icon("back")), onBack),
                    Text("Log detail", "fs-title w600 grow"),
                    Tap(Div("icon-btn", Icon("close")), onClose)));
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("detail-body");
            scroll.contentContainer.AddToClassList("detail-content");
            scroll.AddToClassList("grow");
            scroll.verticalScrollerVisibility = desktop ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(scroll);

            string kindColor = entry.Kind == LogKind.Error ? "c-error" : entry.Kind == LogKind.Warning ? "c-warning" : "c-info";
            var chips = Div("detail-chips");
            chips.Add(Div("pill " + kindColor, Icon(Icons.ForKind(entry.Kind), 16f), Text(LogStore.TypeLabel(entry.Type), "fs-body w600")));
            string time = entry.FirstTime.ToString("HH:mm:ss.fff");
            string countText = entry.Count > 1 ? Format.Repeat(entry.Count) + " · " : string.Empty;
            chips.Add(Div("pill c-secondary", Text(countText + (desktop ? time : "first " + time), "mono fs-caption")));
            if (!desktop) chips.Add(Text("frame " + entry.FirstFrame.ToString("#,0", new System.Globalization.NumberFormatInfo { NumberGroupSeparator = " " }), "mono fs-caption c-muted"));
            scroll.Add(chips);

            var message = Text(entry.Message, "fs-body detail-message");
            message.selection.isSelectable = true;
            message.pickingMode = PickingMode.Position;
            scroll.Add(message);

            List<Frame> frames = Parse(entry.StackTrace);
            if (frames.Count > 0)
            {
                scroll.Add(Div("detail-section", Text("STACK TRACE", "lbl"), desktop ? null : Text(frames.Count + " frames", "mono fs-caption c-muted")));
                var list = Div("frames");
                bool firstAppFrame = true;
                foreach (Frame frame in frames)
                {
                    bool internalFrame = frame.File == null;
                    var row = Div("frame-row");
                    row.Add(Text(frame.Method, "mono fs-caption" + (internalFrame ? " c-muted" : "")));
                    if (internalFrame)
                    {
                        row.Add(Text("Unity internal", "mono fs-caption c-muted"));
                    }
                    else
                    {
                        var path = Text(frame.File + ":" + frame.Line, "mono fs-caption c-accent");
#if UNITY_EDITOR
                        Frame f = frame;
                        Tap(path, () => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(f.File, f.Line));
#endif
                        row.Add(path);
                        if (firstAppFrame && entry.Kind != LogKind.Log)
                        {
                            row.AddToClassList(entry.Kind == LogKind.Error ? "frame-error" : "frame-warning");
                            firstAppFrame = false;
                        }
                    }
                    list.Add(row);
                }
                scroll.Add(list);
#if UNITY_EDITOR
                if (desktop) scroll.Add(Text("In the Editor, clicking a file path opens it at that line.", "fs-caption c-muted detail-note"));
#endif
            }

            string copyText = LogStore.TypeLabel(entry.Type) + ": " + entry.Message + "\n" + entry.StackTrace;
            IconElement copyIcon = Icon("copy", desktop ? 18f : 20f);
            Label copyLabel = Text("Copy", "fs-body w600");
            IVisualElementScheduledItem revert = null;
            root.Add(Div("detail-footer footer",
                Tap(Div("btn btn-accent grow", copyIcon, copyLabel), () =>
                {
                    GUIUtility.systemCopyBuffer = copyText;
                    // Confirm the copy for a moment; tapping again restarts the timer.
                    copyIcon.Icon = "check";
                    copyLabel.text = "Copied";
                    if (revert == null)
                    {
                        revert = copyLabel.schedule.Execute(() =>
                        {
                            copyIcon.Icon = "copy";
                            copyLabel.text = "Copy";
                        }).StartingIn(CopiedMs);
                    }
                    else
                    {
                        revert.ExecuteLater(CopiedMs);
                    }
                })));
            return root;
        }

        /// <summary>Unity format: "Game.Player:Update () (at Assets/Scripts/Player.cs:98)".</summary>
        private static List<Frame> Parse(string stackTrace)
        {
            var frames = new List<Frame>();
            if (string.IsNullOrEmpty(stackTrace)) return frames;

            foreach (string raw in stackTrace.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;

                var frame = new Frame();
                int at = line.LastIndexOf(" (at ", StringComparison.Ordinal);
                string method = at >= 0 ? line.Substring(0, at) : line;
                if (at >= 0)
                {
                    string location = line.Substring(at + 5).TrimEnd(')');
                    int colon = location.LastIndexOf(':');
                    if (colon > 0 && !location.StartsWith("<", StringComparison.Ordinal) && int.TryParse(location.Substring(colon + 1), out int number))
                    {
                        frame.File = location.Substring(0, colon);
                        frame.Line = number;
                    }
                }

                int separator = method.IndexOf(':');
                if (separator > 0 && separator < method.IndexOf('(') ) method = method.Substring(0, separator) + "." + method.Substring(separator + 1);
                frame.Method = method;
                frames.Add(frame);
            }
            return frames;
        }
    }
}
