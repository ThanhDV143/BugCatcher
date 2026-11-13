using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>
    /// Keeps a row on one line: while its content is wider than the row, applies the next class in
    /// <c>steps</c> (each one makes the content narrower); once the row is as wide as the content was when
    /// the last step was taken, undoes it.
    /// </summary>
    internal class StepFit
    {
        private readonly VisualElement row;
        private readonly string[] steps;
        private readonly Func<float> needed;
        private readonly float[] neededAt;
        private int level;

        /// <param name="needed">Width the content needs at the current step; negative to skip fitting for now.</param>
        public StepFit(VisualElement row, string[] steps, Func<float> needed)
        {
            this.row = row;
            this.steps = steps;
            this.needed = needed;
            neededAt = new float[steps.Length];
            row.RegisterCallback<GeometryChangedEvent>(_ => Fit());
        }

        /// <summary>Re-fits when this element changes size too (the row's own size may not change).</summary>
        public void Watch(VisualElement element) => element.RegisterCallback<GeometryChangedEvent>(_ => Fit());

        /// <summary>Starts again from the widest layout, e.g. after the text size changed.</summary>
        public void Reset()
        {
            if (row.panel != null) SetLevel(0);
        }

        public void Fit()
        {
            if (row.panel == null) return;

            float available = row.layout.width - row.resolvedStyle.paddingLeft - row.resolvedStyle.paddingRight;
            if (float.IsNaN(available) || available <= 0f) return; // Not laid out (panel or tab hidden).

            float width = needed();
            if (width < 0f) return;

            if (width > available + 0.5f && level < steps.Length)
            {
                neededAt[level] = width;
                SetLevel(level + 1);
            }
            else if (level > 0 && available >= neededAt[level - 1])
            {
                SetLevel(level - 1);
            }
        }

        private void SetLevel(int value)
        {
            level = value;
            for (int i = 0; i < steps.Length; i++) row.EnableInClassList(steps[i], i < level);
            // The row's own size does not change, so no geometry event may follow: check again after the next layout.
            row.schedule.Execute(Fit);
        }

        /// <summary>Layout width plus horizontal margins of the displayed elements.</summary>
        public static float OuterWidth(IEnumerable<VisualElement> elements)
        {
            float width = 0f;
            foreach (VisualElement e in elements)
            {
                if (e.resolvedStyle.display == DisplayStyle.None) continue;
                width += e.layout.width + e.resolvedStyle.marginLeft + e.resolvedStyle.marginRight;
            }
            return width;
        }
    }
}
