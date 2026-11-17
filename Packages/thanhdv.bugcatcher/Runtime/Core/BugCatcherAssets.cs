using UnityEngine;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher
{
    /// <summary>
    /// Every asset the overlay loads. Not in a Resources folder, so nothing of Bug Catcher ships in a build
    /// without BUGCATCHER_ENABLED: the build step in the Editor assembly adds the whole Runtime/Assets folder to
    /// Preloaded Assets only for builds that have the symbol. They are all Unity types, so the Editor can add them
    /// even when it has not compiled Bug Catcher. The player finds them by name; the Editor loads them by path.
    /// </summary>
    internal sealed class BugCatcherAssets
    {
        /// <summary>Keep in sync with BugCatcherBuild.Folder (Editor assembly).</summary>
        private const string Folder = "Packages/thanhdv.bugcatcher/Runtime/Assets/";

        private static BugCatcherAssets instance;

        public PanelSettings panelSettings;
        public StyleSheet styleSheet;
        /// <summary>Regular, Medium, SemiBold.</summary>
        public Font[] sansFonts;
        /// <summary>Regular, Medium, SemiBold.</summary>
        public Font[] monoFonts;

        /// <summary>Null if an asset is missing, for example in a build that did not preload them.</summary>
        internal static BugCatcherAssets Instance => instance ?? (instance = Find());

        private static BugCatcherAssets Find()
        {
            var assets = new BugCatcherAssets
            {
                panelSettings = Load<PanelSettings>("BugCatcherPanelSettings.asset"),
                styleSheet = Load<StyleSheet>("BugCatcher.uss"),
                sansFonts = new[]
                {
                    Load<Font>("Fonts/BugCatcher-IBMPlexSans-Regular.ttf"),
                    Load<Font>("Fonts/BugCatcher-IBMPlexSans-Medium.ttf"),
                    Load<Font>("Fonts/BugCatcher-IBMPlexSans-SemiBold.ttf")
                },
                monoFonts = new[]
                {
                    Load<Font>("Fonts/BugCatcher-IBMPlexMono-Regular.ttf"),
                    Load<Font>("Fonts/BugCatcher-IBMPlexMono-Medium.ttf"),
                    Load<Font>("Fonts/BugCatcher-IBMPlexMono-SemiBold.ttf")
                }
            };
            return assets.panelSettings != null && assets.styleSheet != null ? assets : null;
        }

        private static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(Folder + path);
#else
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (T asset in Resources.FindObjectsOfTypeAll<T>())
            {
                if (asset.name == name) return asset;
            }
            return null;
#endif
        }
    }
}
