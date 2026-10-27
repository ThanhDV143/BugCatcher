using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>
    /// IBM Plex Sans + Mono, turned into dynamic SDF font assets at startup.
    /// Weight and family are picked from the element's classes: "mono" (or "lbl"), "w500", "w600".
    /// </summary>
    internal static class Fonts
    {
        private static FontDefinition[] sans;
        private static FontDefinition[] mono;

        public static void Load()
        {
            if (sans != null) return;

            sans = new[] { Create("IBMPlexSans-Regular"), Create("IBMPlexSans-Medium"), Create("IBMPlexSans-SemiBold") };
            mono = new[] { Create("IBMPlexMono-Regular"), Create("IBMPlexMono-Medium"), Create("IBMPlexMono-SemiBold") };
        }

        private static FontDefinition Create(string name)
        {
            var font = Resources.Load<Font>("BugCatcher/Fonts/" + name);
            if (font == null)
            {
                Debug.LogWarning("[BugCatcher] Missing font " + name);
                return default;
            }

            FontAsset asset = FontAsset.CreateFontAsset(font);
            if (asset == null) return FontDefinition.FromFont(font);

            asset.name = name;
            return FontDefinition.FromSDFFont(asset);
        }

        /// <summary>Sets the font of <paramref name="text"/> from its "mono" / "w500" / "w600" classes.</summary>
        public static void Apply(VisualElement text)
        {
            if (sans == null) return;

            int weight = text.ClassListContains("w600") ? 2 : text.ClassListContains("w500") ? 1 : 0;
            bool isMono = text.ClassListContains("mono") || text.ClassListContains("lbl");
            FontDefinition definition = isMono ? mono[weight] : sans[weight];
            if (definition.fontAsset != null || definition.font != null) text.style.unityFontDefinition = definition;
        }
    }
}
