using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>Tiny element builders so views read close to the design's markup.</summary>
    internal static class UIB
    {
        public static VisualElement Div(string classes = null, params VisualElement[] children)
        {
            var element = new VisualElement();
            AddClasses(element, classes);
            foreach (VisualElement child in children)
            {
                if (child != null) element.Add(child);
            }
            return element;
        }

        public static Label Text(string text, string classes = null)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            AddClasses(label, classes);
            Fonts.Apply(label);
            return label;
        }

        public static IconElement Icon(string name, float size = 20f, string classes = null)
        {
            var icon = new IconElement(name, size);
            AddClasses(icon, classes);
            return icon;
        }

        /// <summary>Makes any element a button. Pressed state is the :active pseudo-class.</summary>
        public static T Tap<T>(T element, Action onClick) where T : VisualElement
        {
            element.AddToClassList("pressable");
            element.pickingMode = PickingMode.Position;
            element.AddManipulator(new Clickable(onClick));
            return element;
        }

        public static VisualElement Spacer()
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            return spacer;
        }

        public static void AddClasses(VisualElement element, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (string c in classes.Split(' '))
            {
                if (c.Length > 0) element.AddToClassList(c);
            }
        }

        /// <summary>Toggles a class; re-picks the font when it is a weight/family class.</summary>
        public static void SetClass(VisualElement element, string cls, bool on)
        {
            if (element.ClassListContains(cls) == on) return;
            element.EnableInClassList(cls, on);
            if (cls == "w500" || cls == "w600" || cls == "mono") Fonts.Apply(element);
        }

        /// <summary>
        /// Vertical gaps between children (the design's "gap"). UI Toolkit 2022 has no gap property and
        /// ".x > *" margin rules did not apply reliably inside scroll views, so gaps are set inline.
        /// </summary>
        private static readonly (string cls, float gap)[] stackGaps =
        {
            ("tab-scroll", 18f),
            ("group", 8f),
            ("detail-content", 16f),
            ("spectrum-card", 8f),
            ("mod-rows", 2f)
        };

        public static void ApplyGaps(VisualElement root)
        {
            foreach ((string cls, float gap) in stackGaps)
            {
                root.Query(className: cls).ForEach(parent =>
                {
                    // The desktop aside is denser (14) than the phone page (16).
                    float g = cls == "detail-content" && HasAncestorClass(parent, "detail-aside") ? 14f : gap;
                    int count = parent.childCount;
                    for (int i = 0; i < count; i++) parent[i].style.marginBottom = i < count - 1 ? g : 0f;
                });
            }
        }

        private static bool HasAncestorClass(VisualElement element, string cls)
        {
            for (VisualElement e = element.hierarchy.parent; e != null; e = e.hierarchy.parent)
            {
                if (e.ClassListContains(cls)) return true;
            }
            return false;
        }

        public static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    internal class Switch : VisualElement
    {
        private readonly Action<bool> onChanged;
        private bool value;

        public Switch(bool value, Action<bool> onChanged)
        {
            this.onChanged = onChanged;
            AddToClassList("switch");
            Add(UIB.Div("switch-knob"));
            SetValue(value);
            UIB.Tap(this, () =>
            {
                SetValue(!this.value);
                this.onChanged?.Invoke(this.value);
            });
        }

        public void SetValue(bool on)
        {
            value = on;
            EnableInClassList("on", on);
        }
    }

    /// <summary>Segmented control (radio group). Selected option: surface fill + weight 600.</summary>
    internal class Segmented : VisualElement
    {
        private readonly Label[] options;
        private readonly Action<int> onSelected;

        public Segmented(string[] labels, int selected, Action<int> onSelected, string optionClasses = "fs-body")
        {
            this.onSelected = onSelected;
            AddToClassList("seg");
            options = new Label[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                Label option = UIB.Text(labels[i], "seg-opt " + optionClasses);
                UIB.Tap(option, () =>
                {
                    if (option.ClassListContains("disabled")) return;
                    SetSelected(index);
                    this.onSelected?.Invoke(index);
                });
                options[i] = option;
                Add(option);
            }
            SetSelected(selected);
        }

        public Label Option(int index) => options[index];

        public void SetSelected(int index)
        {
            for (int i = 0; i < options.Length; i++)
            {
                UIB.SetClass(options[i], "on", i == index);
                UIB.SetClass(options[i], "w600", i == index);
            }
        }

        public void SetDisabled(int index, bool disabled) => options[index].EnableInClassList("disabled", disabled);
    }

    /// <summary>Slider drawn as track + accent fill + ring thumb.</summary>
    internal class SliderBar : VisualElement
    {
        private readonly VisualElement fill;
        private readonly VisualElement thumb;
        private readonly float min;
        private readonly float max;
        private readonly Action<float> onChanged;
        private float value;

        public SliderBar(float min, float max, float value, Action<float> onChanged)
        {
            this.min = min;
            this.max = max;
            this.onChanged = onChanged;
            AddToClassList("slider");
            pickingMode = PickingMode.Position;
            Add(UIB.Div("slider-track"));
            fill = UIB.Div("slider-fill");
            thumb = UIB.Div("slider-thumb");
            Add(fill);
            Add(thumb);
            fill.pickingMode = thumb.pickingMode = PickingMode.Ignore;
            SetValue(value);

            RegisterCallback<PointerDownEvent>(e =>
            {
                this.CapturePointer(e.pointerId);
                Drag(e.localPosition.x);
            });
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (this.HasPointerCapture(e.pointerId)) Drag(e.localPosition.x);
            });
            RegisterCallback<PointerUpEvent>(e => this.ReleasePointer(e.pointerId));
        }

        public float Value => value;

        private void Drag(float x)
        {
            float width = contentRect.width;
            if (width <= 0f) return;

            SetValue(Mathf.Lerp(min, max, Mathf.Clamp01(x / width)));
            onChanged?.Invoke(value);
        }

        public void SetValue(float v)
        {
            value = Mathf.Clamp(v, min, max);
            float t = (value - min) / (max - min);
            fill.style.width = Length.Percent(t * 100f);
            thumb.style.left = Length.Percent(t * 100f);
        }
    }

    /// <summary>Horizontal level meter with a peak marker. Values are 0..1.</summary>
    internal class LevelMeter : VisualElement
    {
        private readonly VisualElement fill;
        private readonly VisualElement marker;

        public LevelMeter(string classes = "meter")
        {
            UIB.AddClasses(this, classes);
            pickingMode = PickingMode.Ignore;
            fill = UIB.Div("meter-fill");
            marker = UIB.Div("meter-peak");
            Add(fill);
            Add(marker);
        }

        public void Set(float level, float peak, bool showPeak = true)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(level) * 100f);
            UIB.Show(marker, showPeak && peak > 0f);
            marker.style.left = Length.Percent(Mathf.Clamp01(peak) * 100f);
        }

        public VisualElement Marker => marker;
    }
}
