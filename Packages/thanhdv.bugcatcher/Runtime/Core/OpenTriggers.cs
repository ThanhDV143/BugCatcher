using UnityEngine;
#if ENABLE_INPUT_SYSTEM && BUGCATCHER_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ThanhDV.BugCatcher
{
    /// <summary>Reads touches and the backquote key from whichever input backend is enabled.</summary>
    internal static class InputCompat
    {
        public static int TouchCount
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.touchCount;
#elif ENABLE_INPUT_SYSTEM && BUGCATCHER_INPUT_SYSTEM
                Touchscreen screen = Touchscreen.current;
                if (screen == null) return 0;
                int count = 0;
                foreach (var touch in screen.touches)
                {
                    if (touch.isInProgress) count++;
                }
                return count;
#else
                return 0;
#endif
            }
        }

        /// <summary>Screen position (origin bottom-left) of the n-th active touch.</summary>
        public static Vector2 TouchPosition(int index)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetTouch(index).position;
#elif ENABLE_INPUT_SYSTEM && BUGCATCHER_INPUT_SYSTEM
            int n = 0;
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.isInProgress) continue;
                if (n++ == index) return touch.position.ReadValue();
            }
            return Vector2.zero;
#else
            return Vector2.zero;
#endif
        }

        public static bool BackquotePressed
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.BackQuote);
#elif ENABLE_INPUT_SYSTEM && BUGCATCHER_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame;
#else
                return false;
#endif
            }
        }
    }

    public enum GesturePhase { Idle, Holding, Done, Cancelled }

    /// <summary>
    /// Two ways to open the panel without the bubble:
    /// hold both top corners for 1 s (mobile), or press ` three times within 0.8 s (desktop).
    /// Cheap by design: one integer compare per frame while fewer than two fingers are down.
    /// </summary>
    public class OpenTriggers
    {
        public const float HoldSeconds = 1f;
        public const float ShowRingAfter = 0.3f;
        private const float ZoneFraction = 0.15f;
        private const float MoveToleranceMm = 3f;
        private const float KeyWindow = 0.8f;

        private readonly float[] keyTimes = { -10f, -10f, -10f };
        private int keyIndex;
        private bool holding;
        private float holdStart;
        private Vector2 start0;
        private Vector2 start1;

        public GesturePhase Phase { get; private set; }
        /// <summary>0..1 over the visible part of the hold (0.3 s – 1.0 s).</summary>
        public float Progress { get; private set; }
        /// <summary>Screen positions (origin bottom-left) of the two fingers, left one first.</summary>
        public Vector2 Finger0 { get; private set; }
        public Vector2 Finger1 { get; private set; }

        /// <summary>When true (panel open), the corner gesture is ignored.</summary>
        public bool GestureEnabled { get; set; } = true;

        /// <summary>Returns true when the key sequence fired this frame.</summary>
        public bool TickKeys(bool textFieldFocused)
        {
            if (!InputCompat.BackquotePressed || textFieldFocused) return false;

            float now = Time.unscaledTime;
            keyTimes[keyIndex] = now;
            keyIndex = (keyIndex + 1) % keyTimes.Length;
            // keyIndex now points at the oldest of the last three presses.
            if (now - keyTimes[keyIndex] > KeyWindow) return false;

            keyTimes[0] = keyTimes[1] = keyTimes[2] = -10f;
            return true;
        }

        /// <summary>Returns true when the hold completed this frame.</summary>
        public bool TickGesture()
        {
            if (Phase == GesturePhase.Done || Phase == GesturePhase.Cancelled) Phase = GesturePhase.Idle;

            if (InputCompat.TouchCount < 2 || !GestureEnabled)
            {
                Release();
                return false;
            }

            Vector2 a = InputCompat.TouchPosition(0);
            Vector2 b = InputCompat.TouchPosition(1);
            if (a.x > b.x) (a, b) = (b, a);

            if (!holding)
            {
                if (InTopCorners(a, b))
                {
                    holding = true;
                    holdStart = Time.unscaledTime;
                    start0 = a;
                    start1 = b;
                }
                return false;
            }

            float tolerance = MoveToleranceMm * (Screen.dpi > 0 ? Screen.dpi : 160f) / 25.4f;
            if ((a - start0).sqrMagnitude > tolerance * tolerance || (b - start1).sqrMagnitude > tolerance * tolerance)
            {
                Release();
                return false;
            }

            Finger0 = a;
            Finger1 = b;
            float held = Time.unscaledTime - holdStart;
            if (held < ShowRingAfter) return false;

            Phase = GesturePhase.Holding;
            Progress = Mathf.Clamp01((held - ShowRingAfter) / (HoldSeconds - ShowRingAfter));
            if (held < HoldSeconds) return false;

            holding = false;
            Phase = GesturePhase.Done;
            Progress = 1f;
            return true;
        }

        private void Release()
        {
            if (holding && Phase == GesturePhase.Holding) Phase = GesturePhase.Cancelled;
            holding = false;
        }

        private static bool InTopCorners(Vector2 left, Vector2 right)
        {
            Rect safe = Screen.safeArea;
            float zone = Mathf.Min(Screen.width, Screen.height) * ZoneFraction;
            bool leftOk = left.x <= safe.xMin + zone && left.y >= safe.yMax - zone;
            bool rightOk = right.x >= safe.xMax - zone && right.y >= safe.yMax - zone;
            return leftOk && rightOk;
        }
    }
}
