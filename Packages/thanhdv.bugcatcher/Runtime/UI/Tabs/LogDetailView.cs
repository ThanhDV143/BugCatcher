using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>Message, stack trace, copy and share. A full page on phones, the right-hand aside on desktop.</summary>
    internal static class LogDetailView
    {
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
                root.Add(Div("sheet-handle detail-handle", Div("handle-bar")));
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
            root.Add(Div("detail-footer footer",
                Tap(Div("btn btn-raised grow", Icon("copy", desktop ? 18f : 20f), Text("Copy", "fs-body w500")), () =>
                {
                    GUIUtility.systemCopyBuffer = copyText;
                }),
                Tap(Div("btn btn-accent grow", Icon("share", desktop ? 18f : 20f), Text("Share", "fs-body w600")), () =>
                {
                    BugCatcher.Share(copyText, "BugCatcher_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
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
