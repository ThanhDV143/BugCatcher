using UnityEngine;
using UnityEngine.UIElements;
using static ThanhDV.BugCatcher.UI.UIB;

namespace ThanhDV.BugCatcher.UI
{
    internal class SystemTab : IPanelTab
    {
        public VisualElement Build(LayoutMode mode)
        {
            var root = Div("tab-page");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var rows = Div(mode == LayoutMode.Desktop ? "tab-narrow" : null);
            rows.Add(Row("OS", SystemInfoService.Os));
            rows.Add(Row("CPU", SystemInfoService.Cpu));
            rows.Add(Row("GPU", SystemInfoService.Gpu));
            rows.Add(Row("RAM", SystemInfoService.Ram));
            rows.Add(Row("Screen", SystemInfoService.Screen));
            rows.Add(Row("Unity", SystemInfoService.Unity, last: true));
            scroll.Add(rows);
            root.Add(scroll);

            root.Add(Div("footer tab-footer",
                Tap(Div("btn btn-accent grow", Icon("copy"), Text("Copy all", "fs-body w600")), () => GUIUtility.systemCopyBuffer = SystemInfoService.CopyText())));
            return root;
        }

        private static VisualElement Row(string key, string value, bool last = false) =>
            Div("sys-row" + (last ? " last" : ""), Text(key, "fs-body c-secondary sys-key"), Text(value, "mono fs-body grow"));

        public void SetVisible(bool visible) { }

        public void Tick(float deltaTime) { }

        public void ApplySettings() { }
    }
}
