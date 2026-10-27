using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>
    /// Long-press menu beside the bubble. Opens on the side with more room;
    /// a tap outside closes it and does not reach the game.
    /// </summary>
    internal class QuickMenu
    {
        private const float Width = 264f;
        private const float Gap = 12f;

        private readonly BugCatcherUI ui;
        private readonly VisualElement menu;
        private readonly Switch hudSwitch;
        private readonly Segmented hudMode;
        private readonly Label clearCount;

        public QuickMenu(BugCatcherUI ui)
        {
            this.ui = ui;
            Root = Tap(Div("scrim-clear"), Close);
            Show(Root, false);

            menu = Div("qmenu");
            menu.pickingMode = PickingMode.Position;
            // Taps inside the menu must not close it.
            menu.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            menu.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
            Root.Add(menu);

            hudSwitch = new Switch(true, on =>
            {
                BugCatcher.Settings.hudVisible = on;
                BugCatcher.Settings.Changed();
            });
            menu.Add(Div("qmenu-row", Icon("hud", 20f, "c-secondary"), Text("HUD", "fs-body grow"), hudSwitch));

            hudMode = new Segmented(new[] { "Basic", "Text", "Full" }, 2, SetAllModes);
            menu.Add(Div("qmenu-indent", hudMode));
            menu.Add(Div("qmenu-divider"));

            clearCount = Text("0", "mono fs-caption c-muted");
            menu.Add(Item("clear", Text("Clear logs", "fs-body grow"), clearCount, () =>
            {
                BugCatcher.Logs.Clear();
                Close();
            }));
            menu.Add(Item("screenshot", Text("Screenshot", "fs-body grow"), null, () =>
            {
                Close();
                BugCatcher.CaptureScreenshot();
            }));
            menu.Add(Div("qmenu-divider"));
            menu.Add(Item("hide", Div("col grow", Text("Hide bubble", "fs-body"), Text("Reopen: hold both top corners 1 s", "fs-caption c-muted")), null, () =>
            {
                BugCatcher.Settings.showBubble = false;
                BugCatcher.Settings.Changed();
                Close();
            }, "qmenu-item-tall"));
        }

        public VisualElement Root { get; }

        private static VisualElement Item(string icon, VisualElement label, VisualElement trailing, System.Action onTap, string extra = null)
        {
            var item = Tap(Div("qmenu-item " + extra, Icon(icon, 20f, "c-secondary"), label), onTap);
            if (trailing != null) item.Add(trailing);
            return item;
        }

        private static void SetAllModes(int index)
        {
            HudMode mode = index == 0 ? HudMode.Basic : index == 1 ? HudMode.Text : HudMode.Full;
            HudMode[] modes = BugCatcher.Settings.hud.modes;
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == HudMode.Off) continue;
                modes[i] = mode;
            }
            BugCatcher.Settings.hudVisible = true;
            BugCatcher.Settings.activePreset = -2;
            BugCatcher.Settings.Changed();
        }

        public void Refresh()
        {
            BugCatcherSettings settings = BugCatcher.Settings;
            hudSwitch.SetValue(settings.hudVisible);
            HudMode fpsMode = settings.hud.modes[(int)HudModule.Fps];
            hudMode.SetSelected(fpsMode == HudMode.Basic ? 0 : fpsMode == HudMode.Text ? 1 : fpsMode == HudMode.Full ? 2 : -1);
        }

        public void Open(VisualElement bubble)
        {
            Refresh();
            clearCount.text = Format.Int(BugCatcher.Logs.TotalCount);

            Rect b = bubble.worldBound;
            Vector2 size = ui.PanelSize;
            bool roomOnLeft = b.center.x > size.x * 0.5f;
            menu.style.left = roomOnLeft ? Mathf.Max(8f, b.xMin - Gap - Width) : b.xMax + Gap;
            menu.style.top = Mathf.Clamp(b.yMin, ui.SafeInsets.Top + 8f, Mathf.Max(ui.SafeInsets.Top + 8f, size.y - ui.SafeInsets.Bottom - 340f));

            Show(Root, true);
            menu.RemoveFromClassList("open");
            // Next frame so the transition (scale 0.96 → 1 + fade, 150 ms) runs.
            menu.schedule.Execute(() => menu.AddToClassList("open"));
        }

        public void Close()
        {
            menu.RemoveFromClassList("open");
            Show(Root, false);
            ui.OnQuickMenuClosed();
        }
    }
}
