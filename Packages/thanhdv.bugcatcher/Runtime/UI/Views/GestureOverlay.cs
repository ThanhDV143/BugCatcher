using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>
    /// Feedback for "hold both top corners": rings appear after 0.3 s, fill until 1.0 s,
    /// then turn into a check. Released early: the rings grey out and fade.
    /// </summary>
    internal class GestureOverlay
    {
        private const float RingSize = 44f;
        private const float TowardCentre = 40f;

        private readonly BugCatcherUI ui;
        private readonly Indicator[] indicators = new Indicator[2];
        private GesturePhase lastPhase;

        private class Indicator
        {
            public VisualElement Root;
            public Ring Ring;
            public VisualElement Done;
        }

        public GestureOverlay(BugCatcherUI ui)
        {
            this.ui = ui;
            Root = Div("gesture-layer");
            Root.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < indicators.Length; i++)
            {
                var indicator = new Indicator { Root = Div("gesture"), Ring = new Ring(), Done = Div("gesture-done", Icon("check", 26f, "c-on-accent")) };
                indicator.Root.Add(indicator.Ring);
                indicator.Root.Add(indicator.Done);
                indicator.Root.pickingMode = PickingMode.Ignore;
                Root.Add(indicator.Root);
                indicators[i] = indicator;
            }
            Show(Root, false);
        }

        public VisualElement Root { get; }

        public void Tick(OpenTriggers triggers)
        {
            GesturePhase phase = triggers.Phase;
            if (phase == GesturePhase.Idle)
            {
                lastPhase = phase;
                return;
            }

            if (phase == GesturePhase.Holding)
            {
                if (lastPhase != GesturePhase.Holding)
                {
                    Show(Root, true);
                    Root.RemoveFromClassList("cancelled");
                    Root.RemoveFromClassList("done");
                    Root.RemoveFromClassList("visible");
                    Root.schedule.Execute(() => Root.AddToClassList("visible"));
                }

                Place(indicators[0], triggers.Finger0);
                Place(indicators[1], triggers.Finger1);
                foreach (Indicator indicator in indicators) indicator.Ring.Progress = triggers.Progress;
            }
            else if (phase == GesturePhase.Done && lastPhase != GesturePhase.Done)
            {
                Root.AddToClassList("done");
                Root.schedule.Execute(() => Root.AddToClassList("done-settle")).StartingIn(100);
                Root.schedule.Execute(Hide).StartingIn(260);
            }
            else if (phase == GesturePhase.Cancelled && lastPhase != GesturePhase.Cancelled)
            {
                Root.AddToClassList("cancelled");
                Root.RemoveFromClassList("visible");
                Root.schedule.Execute(Hide).StartingIn(120);
            }

            lastPhase = phase;
        }

        private void Hide()
        {
            Show(Root, false);
            Root.RemoveFromClassList("visible");
            Root.RemoveFromClassList("done");
            Root.RemoveFromClassList("done-settle");
            Root.RemoveFromClassList("cancelled");
        }

        // Offset toward the screen centre so the thumb never hides the ring.
        private void Place(Indicator indicator, Vector2 screenPosition)
        {
            Vector2 p = ui.ScreenToPanel(screenPosition);
            Vector2 centre = ui.PanelSize * 0.5f;
            p += (centre - p).normalized * TowardCentre;
            indicator.Root.style.left = p.x - RingSize * 0.5f;
            indicator.Root.style.top = p.y - RingSize * 0.5f;
        }
    }
}
