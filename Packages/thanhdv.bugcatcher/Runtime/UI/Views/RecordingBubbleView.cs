using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>
    /// Shown while screenshots are being captured (auto screenshot or a Start/Stop session).
    /// Sits beside the main bubble on the screen side and moves with it; dragging either moves both.
    /// Hold 0.8 s to stop; a plain tap only explains that.
    /// </summary>
    internal class RecordingBubbleView
    {
        public const float Height = 36f;
        private const float HoldSeconds = 0.8f;
        private const float DragThreshold = 6f;
        private const float HintSeconds = 1.5f;
        private const float BlinkSeconds = 0.5f;
        private const float TimeInterval = 0.25f;

        private readonly BugCatcherUI ui;
        private readonly VisualElement camera;
        private readonly VisualElement dot;
        private readonly Label time;
        private readonly VisualElement warning;
        private readonly OutlineProgress outline;
        private readonly Label hint;
        private BubbleView main;
        private RecorderState state = (RecorderState)(-1);
        private float blinkTimer;
        private float timeTimer;
        private float hintTimer;

        private bool pressed;
        private bool dragging;
        private bool held;
        private Vector2 pressPosition;
        private double pressTime;

        public RecordingBubbleView(BugCatcherUI ui)
        {
            this.ui = ui;
            Root = Div("rec-bubble");
            Root.pickingMode = PickingMode.Position;
            camera = Icon("screenshot", 18f, "c-secondary");
            dot = Div("rec-dot");
            time = Text("00:00", "mono w600 fs-caption rec-time");
            warning = Icon("warning", 18f, "c-caution");
            outline = new OutlineProgress();
            outline.AddToClassList("rec-outline");
            hint = Text("Hold to stop", "fs-caption rec-hint");
            foreach (VisualElement child in new[] { camera, dot, time, warning, outline, hint })
            {
                child.pickingMode = PickingMode.Ignore;
                Root.Add(child);
            }
            Show(outline, false);
            Show(hint, false);
            Show(Root, false);

            Root.RegisterCallback<PointerDownEvent>(OnPointerDown);
            Root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            Root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            Root.RegisterCallback<PointerCaptureOutEvent>(_ => Cancel());
            ApplyState();
        }

        public VisualElement Root { get; }

        public void Pair(BubbleView bubble) => main = bubble;

        /// <summary>True while anything is being captured (or an error waits to be dismissed).</summary>
        public bool WantsToShow => state != RecorderState.Off;

        /// <summary>Current width: 36 px, wider while a session shows its timer.</summary>
        public float Width => Root.layout.width > 0f ? Root.layout.width : Height;

        public void SetShown(bool shown) => Show(Root, shown);

        /// <summary>Side of the screen the pair is docked to; the hint opens toward the centre.</summary>
        public void SetSide(bool right)
        {
            Root.EnableInClassList("on-right", right);
            Root.EnableInClassList("on-left", !right);
        }

        public void ApplyState()
        {
            RecorderState next = BugCatcher.Recorder != null ? BugCatcher.Recorder.State : RecorderState.Off;
            if (next == state) return;

            state = next;
            Show(camera, state == RecorderState.Waiting);
            Show(dot, state == RecorderState.Incident || state == RecorderState.Recording);
            Show(time, state == RecorderState.Recording);
            Show(warning, state == RecorderState.Error);
            SetClass(Root, "state-error", state == RecorderState.Error);
            SetClass(Root, "state-recording", state == RecorderState.Recording);
            Root.RemoveFromClassList("blink-off");
            timeTimer = TimeInterval;
        }

        public void Tick(float deltaTime)
        {
            ApplyState();
            if (Root.style.display == DisplayStyle.None) return;

            if (state == RecorderState.Incident || state == RecorderState.Recording)
            {
                blinkTimer += deltaTime;
                if (blinkTimer >= BlinkSeconds)
                {
                    blinkTimer = 0f;
                    Root.ToggleInClassList("blink-off");
                }
            }

            if (state == RecorderState.Recording)
            {
                timeTimer += deltaTime;
                if (timeTimer >= TimeInterval)
                {
                    timeTimer = 0f;
                    var elapsed = BugCatcher.Recorder.Elapsed;
                    time.text = ((int)elapsed.TotalMinutes).ToString("00") + ":" + elapsed.Seconds.ToString("00");
                }
            }

            if (hintTimer > 0f)
            {
                hintTimer -= deltaTime;
                if (hintTimer <= 0f) Show(hint, false);
            }

            if (pressed && !dragging && !held)
            {
                float progress = (float)((Time.realtimeSinceStartupAsDouble - pressTime) / HoldSeconds);
                Show(outline, true);
                outline.Progress = progress;
                if (progress >= 1f)
                {
                    held = true;
                    Show(outline, false);
                    BugCatcher.Recorder.StopFromBubble();
                }
            }
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            pressed = true;
            dragging = false;
            held = false;
            pressPosition = e.position;
            pressTime = Time.realtimeSinceStartupAsDouble;
            outline.Progress = 0f;
            Show(hint, false);
            Root.CapturePointer(e.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!pressed || held || !Root.HasPointerCapture(e.pointerId)) return;

            Vector2 delta = (Vector2)e.position - pressPosition;
            if (!dragging && delta.magnitude > DragThreshold)
            {
                // Moving cancels the hold: it is a drag of the pair.
                dragging = true;
                Show(outline, false);
                main.BeginGroupDrag(Root);
            }
            if (dragging) main.MoveGroup(delta);
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!pressed) return;

            bool wasDragging = dragging;
            bool wasHeld = held;
            pressed = false;
            dragging = false;
            Show(outline, false);
            Root.ReleasePointer(e.pointerId);

            if (wasDragging)
            {
                main.FinishGroupDrag();
            }
            else if (!wasHeld)
            {
                Show(hint, true);
                hintTimer = HintSeconds;
            }
        }

        private void Cancel()
        {
            if (!pressed) return;
            bool wasDragging = dragging;
            pressed = false;
            dragging = false;
            Show(outline, false);
            if (wasDragging) main.FinishGroupDrag();
        }
    }
}
