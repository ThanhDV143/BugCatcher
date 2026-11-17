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

        public static void Load(BugCatcherAssets assets)
        {
            if (sans != null) return;

            sans = Create(assets.sansFonts);
            mono = Create(assets.monoFonts);
        }

        /// <summary>Regular, Medium, SemiBold.</summary>
        private static FontDefinition[] Create(Font[] fonts)
        {
            var definitions = new FontDefinition[3];
            for (int i = 0; i < definitions.Length; i++)
            {
                if (fonts != null && i < fonts.Length) definitions[i] = Create(fonts[i]);
            }
            return definitions;
        }

        private static FontDefinition Create(Font font)
        {
            if (font == null)
            {
                Debug.LogWarning("[BugCatcher] Missing font in BugCatcherAssets");
                return default;
            }

            FontAsset asset = FontAsset.CreateFontAsset(font);
            if (asset == null) return FontDefinition.FromFont(font);

            asset.name = font.name;
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
