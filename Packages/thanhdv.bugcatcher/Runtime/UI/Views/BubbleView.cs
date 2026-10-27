using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>
    /// 56 × 56 bubble: FPS in status colour + error badge. While the HUD already shows FPS it shrinks to a
    /// 36 × 36 bug icon + badge. Tap opens the panel, long-press (400 ms) opens the quick menu, drag snaps
    /// to the nearest edge. It never rests on top of a HUD module.
    /// </summary>
    internal class BubbleView
    {
        private const float FullSize = 56f;
        private const float CompactSize = 36f;
        private const float EdgeGap = 4f;
        private const float HudGap = 8f;
        private const float DragThreshold = 6f;
        private const float IdleSeconds = 3f;
        private const float TextInterval = 0.25f;
        private const long LongPressMs = 400;

        private readonly BugCatcherUI ui;
        private readonly Label fps;
        private readonly Label unit;
        private readonly VisualElement bug;
        private readonly Label badge;
        private readonly List<Vector2> blocked = new List<Vector2>();
        private readonly VisualElement edgeGlow;
        private float idleTimer;
        private float textTimer;
        private int lastErrors = -1;

        private bool pressed;
        private bool dragging;
        private bool longPressed;
        private Vector2 pressPosition;
        private Vector2 pressOrigin;
        private IVisualElementScheduledItem longPressTimer;

        public BubbleView(BugCatcherUI ui)
        {
            this.ui = ui;
            Root = Div("bubble");
            Root.pickingMode = PickingMode.Position;
            fps = Text("0", "mono w600 bubble-fps");
            unit = Text("FPS", "mono fs-caption c-secondary bubble-unit");
            bug = Icon("bug", 20f, "c-secondary");
            badge = Text("0", "mono w600 badge bubble-badge");
            Root.Add(fps);
            Root.Add(unit);
            Root.Add(bug);
            Root.Add(badge);

            edgeGlow = Div("edge-glow");
            edgeGlow.pickingMode = PickingMode.Ignore;
            Show(edgeGlow, false);
            ui.Safe.Add(edgeGlow);

            Root.RegisterCallback<PointerDownEvent>(OnPointerDown);
            Root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            Root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            Root.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());

            // Module size or corner changes move the space the bubble has to avoid.
            ui.HudRoot.Query(className: "hud-corner").ForEach(corner => corner.RegisterCallback<GeometryChangedEvent>(_ => Refresh()));
        }

        public VisualElement Root { get; }

        private static BugCatcherSettings Settings => BugCatcher.Settings;

        /// <summary>Re-reads position, size and visibility from the settings.</summary>
        public void Refresh()
        {
            Show(Root, Settings.showBubble && !ui.IsPanelOpen);
            if (dragging) return;

            bool compact = HudShowsFps;
            if (compact != Root.ClassListContains("compact"))
            {
                SetClass(Root, "compact", compact);
                Show(fps, !compact);
                Show(unit, !compact);
                Show(bug, compact);
            }

            ShowBadge();
            Root.style.left = Settings.bubbleOnRight ? StyleKeyword.Auto : EdgeGap;
            Root.style.right = Settings.bubbleOnRight ? EdgeGap : StyleKeyword.Auto;
            Root.style.top = AvoidHud(EdgeGap + Settings.bubbleY * (MaxTop - EdgeGap));
            Root.EnableInClassList("on-left", !Settings.bubbleOnRight);
        }

        private static bool HudShowsFps => Settings.hudVisible && Settings.hud.modes[(int)HudModule.Fps] != HudMode.Off;

        private static bool HudShowsLogs => Settings.hudVisible && Settings.hud.modes[(int)HudModule.Logs] != HudMode.Off;

        // No errors: no badge at all, never "0". The Logs module already counts them when it is on.
        private void ShowBadge() => Show(badge, lastErrors > 0 && !HudShowsLogs);

        private float Size => Root.ClassListContains("compact") ? CompactSize : FullSize;

        /// <summary>Lowest resting top: the same <see cref="EdgeGap"/> from the bottom as from the sides (and the top).</summary>
        private float MaxTop => Mathf.Max(EdgeGap, SafeHeight - Size - EdgeGap);

        /// <summary>
        /// The top closest to <paramref name="desired"/> where the bubble, on its edge, overlaps no visible module.
        /// The saved position stays as the viewer left it; only the shown one moves.
        /// </summary>
        private float AvoidHud(float desired)
        {
            float size = Size;
            float maxTop = MaxTop;
            if (!Settings.hudVisible) return desired;

            Rect safe = ui.Safe.worldBound;
            float left = Settings.bubbleOnRight ? SafeWidth - EdgeGap - size : EdgeGap;
            float right = left + size;

            // Tops that would overlap a module (plus the gap), as open intervals.
            blocked.Clear();
            ui.HudRoot.Query(className: "mod").ForEach(module =>
            {
                if (module.resolvedStyle.display == DisplayStyle.None) return;
                Rect r = module.worldBound;
                if (r.width <= 0f) return;
                float x = r.x - safe.x;
                if (x + r.width + HudGap <= left || x - HudGap >= right) return;
                float y = r.y - safe.y;
                blocked.Add(new Vector2(y - HudGap - size, y + r.height + HudGap));
            });
            if (blocked.Count == 0) return desired;

            // Candidates: the wish itself, both ends of the edge, and just outside each blocked span.
            float best = desired;
            float bestDistance = float.MaxValue;
            TryCandidate(desired, desired, maxTop, ref best, ref bestDistance);
            TryCandidate(EdgeGap, desired, maxTop, ref best, ref bestDistance);
            TryCandidate(maxTop, desired, maxTop, ref best, ref bestDistance);
            foreach (Vector2 span in blocked)
            {
                TryCandidate(span.x, desired, maxTop, ref best, ref bestDistance);
                TryCandidate(span.y, desired, maxTop, ref best, ref bestDistance);
            }
            return best;
        }

        private void TryCandidate(float top, float desired, float maxTop, ref float best, ref float bestDistance)
        {
            if (top < EdgeGap || top > maxTop) return;
            foreach (Vector2 span in blocked)
            {
                if (top > span.x + 0.01f && top < span.y - 0.01f) return;
            }
            float distance = Mathf.Abs(top - desired);
            if (distance >= bestDistance) return;
            best = top;
            bestDistance = distance;
        }

        private float SafeHeight => ui.PanelSize.y - ui.SafeInsets.Top - ui.SafeInsets.Bottom;
        private float SafeWidth => ui.PanelSize.x - ui.SafeInsets.Left - ui.SafeInsets.Right;

        public void Tick(float deltaTime)
        {
            if (Root.style.display == DisplayStyle.None) return;

            if (!pressed && !Root.ClassListContains("menu-open"))
            {
                idleTimer += deltaTime;
                SetClass(Root, "idle", idleTimer >= IdleSeconds);
            }

            textTimer += deltaTime;
            if (textTimer < TextInterval) return;
            textTimer = 0f;

            int value = BugCatcher.Performance.CurrentFps;
            fps.text = Format.Int(value);
            int status = PerformanceService.Status(value);
            SetClass(fps, "c-good", status == 0);
            SetClass(fps, "c-caution", status == 1);
            SetClass(fps, "c-critical", status == 2);

            int errors = BugCatcher.Logs.CountOf(LogKind.Error);
            if (errors != lastErrors)
            {
                lastErrors = errors;
                badge.text = Format.Count(errors);
                ShowBadge();
            }
        }

        /// <summary>One pulse when a new error arrives: ring + badge scale 1.15 → 1. Never loops.</summary>
        public void PulseError()
        {
            WakeUp();
            Root.AddToClassList("pulse");
            Root.schedule.Execute(() => Root.RemoveFromClassList("pulse")).StartingIn(200);
        }

        private void WakeUp()
        {
            idleTimer = 0f;
            Root.RemoveFromClassList("idle");
        }

        /// <summary>While the quick menu is open the bubble stays awake with an accent ring.</summary>
        public void SetMenuOpen(bool open)
        {
            WakeUp();
            SetClass(Root, "menu-open", open);
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            WakeUp();
            pressed = true;
            dragging = false;
            longPressed = false;
            pressPosition = e.position;
            pressOrigin = new Vector2(Root.layout.x, Root.layout.y);
            Root.CapturePointer(e.pointerId);
            longPressTimer = Root.schedule.Execute(() =>
            {
                if (!pressed || dragging) return;
                longPressed = true;
                ui.ShowQuickMenu();
            }).StartingIn(LongPressMs);
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!pressed || !Root.HasPointerCapture(e.pointerId)) return;

            Vector2 delta = (Vector2)e.position - pressPosition;
            if (!dragging && !longPressed && delta.magnitude > DragThreshold)
            {
                dragging = true;
                longPressTimer?.Pause();
                Root.AddToClassList("dragging");
                Show(edgeGlow, true);
            }

            if (!dragging) return;

            Vector2 position = pressOrigin + delta;
            position.x = Mathf.Clamp(position.x, 0f, SafeWidth - Size);
            position.y = Mathf.Clamp(position.y, 0f, SafeHeight - Size);
            Root.style.left = position.x;
            Root.style.right = StyleKeyword.Auto;
            Root.style.top = position.y;

            bool right = position.x + Size * 0.5f > SafeWidth * 0.5f;
            edgeGlow.EnableInClassList("edge-right", right);
            edgeGlow.EnableInClassList("edge-left", !right);
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!pressed) return;

            bool wasDragging = dragging;
            bool wasLongPress = longPressed;
            float x = Root.style.left.value.value;
            float y = Root.style.top.value.value;
            if (wasDragging)
            {
                // Stop dragging before Refresh() runs from the capture release.
                dragging = false;
                Settings.bubbleOnRight = x + Size * 0.5f > SafeWidth * 0.5f;
                Settings.bubbleY = Mathf.Clamp01((y - EdgeGap) / Mathf.Max(1f, MaxTop - EdgeGap));
            }
            Root.ReleasePointer(e.pointerId);
            EndDrag();

            if (wasDragging)
            {
                Settings.Changed();
            }
            else if (!wasLongPress)
            {
                ui.OpenPanel();
            }
        }

        private void EndDrag()
        {
            pressed = false;
            dragging = false;
            idleTimer = 0f;
            longPressTimer?.Pause();
            Root.RemoveFromClassList("dragging");
            Show(edgeGlow, false);
            Refresh();
        }
    }
}
