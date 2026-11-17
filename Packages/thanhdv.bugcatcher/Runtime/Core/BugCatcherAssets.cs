using UnityEngine;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher
{
    /// <summary>
    /// Every asset the overlay loads. Not in a Resources folder, so nothing of Bug Catcher ships in a build
    /// without BUGCATCHER_ENABLED: the build step in the Editor assembly adds this asset to Preloaded Assets
    /// only for builds that have the symbol. In the Editor it is loaded from the package path instead.
    /// </summary>
    public sealed class BugCatcherAssets : ScriptableObject
    {
        /// <summary>Keep in sync with the path in BugCatcherBuild (Editor assembly).</summary>
        internal const string AssetPath = "Packages/thanhdv.bugcatcher/Runtime/Assets/BugCatcherAssets.asset";

        private static BugCatcherAssets instance;

        public PanelSettings panelSettings;
        public StyleSheet styleSheet;
        [Tooltip("Regular, Medium, SemiBold")]
        public Font[] sansFonts = new Font[3];
        [Tooltip("Regular, Medium, SemiBold")]
        public Font[] monoFonts = new Font[3];

        internal static BugCatcherAssets Instance
        {
            get
            {
#if UNITY_EDITOR
                if (instance == null) instance = UnityEditor.AssetDatabase.LoadAssetAtPath<BugCatcherAssets>(AssetPath);
#endif
                return instance;
            }
        }

        // Preloaded Assets are loaded when the player starts, which runs this before BugCatcher bootstraps.
        private void OnEnable() => instance = this;
    }
}
