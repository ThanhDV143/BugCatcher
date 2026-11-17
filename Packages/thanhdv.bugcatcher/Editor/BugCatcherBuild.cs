using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ThanhDV.BugCatcher.Editor
{
    /// <summary>
    /// Adds BugCatcherAssets to Preloaded Assets for builds with BUGCATCHER_ENABLED, and only for the duration of
    /// the build, so a build without the symbol contains no Bug Catcher asset at all.
    /// </summary>
    /// <remarks>
    /// Reads the symbols from Player Settings; symbols passed only through BuildPlayerOptions.extraScriptingDefines
    /// are not seen. Loads the asset by path, so this assembly needs no reference to the runtime.
    /// </remarks>
    internal sealed class BugCatcherBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>Keep in sync with BugCatcherAssets.AssetPath.</summary>
        private const string AssetPath = "Packages/thanhdv.bugcatcher/Runtime/Assets/BugCatcherAssets.asset";
        private const string EnabledSymbol = "BUGCATCHER_ENABLED";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Always remove first: a failed build skips the postprocess and would leave the entry behind.
            Remove();

            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup);
            bool enabled = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(s => s.Trim() == EnabledSymbol);
            if (!enabled) return;

            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            if (asset == null) throw new BuildFailedException("[BugCatcher] Missing " + AssetPath);

            PlayerSettings.SetPreloadedAssets(PlayerSettings.GetPreloadedAssets().Append(asset).ToArray());
        }

        public void OnPostprocessBuild(BuildReport report) => Remove();

        private static void Remove()
        {
            UnityEngine.Object[] preloaded = PlayerSettings.GetPreloadedAssets();
            UnityEngine.Object[] kept = preloaded.Where(a => a == null || AssetDatabase.GetAssetPath(a) != AssetPath).ToArray();
            if (kept.Length != preloaded.Length) PlayerSettings.SetPreloadedAssets(kept);
        }
    }
}
