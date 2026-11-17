using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ThanhDV.BugCatcher.Editor
{
    /// <summary>
    /// Adds every asset in Runtime/Assets to Preloaded Assets for builds with BUGCATCHER_ENABLED, and only for the
    /// duration of the build, so a build without the symbol contains no Bug Catcher asset at all.
    /// </summary>
    /// <remarks>
    /// Preloaded Assets must be set before the player scripts are compiled, so the symbol is read from Player Settings,
    /// the active Build Profile (Unity 6) and BuildPlayerOptions.extraScriptingDefines. Once the scripts are compiled,
    /// the build fails if that guess does not match whether the runtime assembly was compiled. The assets are all Unity
    /// types, so they load even when the Editor has not compiled the runtime, and this assembly needs no reference to it.
    /// </remarks>
    internal sealed class BugCatcherBuild : BuildPlayerProcessor, IPreprocessBuildWithReport, IPostBuildPlayerScriptDLLs, IPostprocessBuildWithReport
    {
        /// <summary>Keep in sync with BugCatcherAssets.Folder.</summary>
        private const string Folder = "Packages/thanhdv.bugcatcher/Runtime/Assets";
        private const string EnabledSymbol = "BUGCATCHER_ENABLED";
        /// <summary>The runtime assembly; it is only compiled when BUGCATCHER_ENABLED is set.</summary>
        private const string RuntimeAssembly = "ThanhDV.BugCatcher.dll";

        /// <summary>Whether the coming build has the symbol; set by PrepareForBuild, which scripts may bypass.</summary>
        private static bool? expected;
        private static bool added;

        public override int callbackOrder => 0;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            BuildPlayerOptions options = context.BuildPlayerOptions;
            expected = HasSymbol(SymbolTarget(options.target, options.targetGroup, options.subtarget)) ||
                       Contains(options.extraScriptingDefines) ||
                       InActiveBuildProfile(options.target);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            // Clears entries left by an earlier build that did not get to clean up.
            Remove();
            added = expected ?? (HasSymbol(SymbolTarget(report.summary.platform, report.summary.platformGroup, 0)) ||
                                 InActiveBuildProfile(report.summary.platform));
            expected = null;
            if (!added) return;

            UnityEngine.Object[] assets = AssetDatabase.FindAssets("t:Object", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Select(AssetDatabase.LoadMainAssetAtPath)
                .Where(asset => asset != null)
                .ToArray();
            if (assets.Length == 0) throw new BuildFailedException("[BugCatcher] Missing " + Folder);

            PlayerSettings.SetPreloadedAssets(PlayerSettings.GetPreloadedAssets().Concat(assets).ToArray());
            // A failed build never calls OnPostprocessBuild: remove the entries once the build has ended either way.
            EditorApplication.delayCall += RemoveAfterBuild;
        }

        public void OnPostBuildPlayerScriptDLLs(BuildReport report)
        {
            bool compiled = ContainsBugCatcher(report);
            if (compiled && !added)
                throw new BuildFailedException("[BugCatcher] Bug Catcher is compiled into this build, but BUGCATCHER_ENABLED is not in Player Settings, the active Build Profile or BuildPlayerOptions.extraScriptingDefines, so its assets were not added. Set the symbol in one of them.");
            if (!compiled && added)
                throw new BuildFailedException("[BugCatcher] BUGCATCHER_ENABLED is set but Bug Catcher was not compiled into this build, so its assets would ship unused.");
        }

        public void OnPostprocessBuild(BuildReport report) => Remove();

        /// <summary>Whether the build compiled the runtime assembly. Valid once the player scripts are compiled.</summary>
        internal static bool ContainsBugCatcher(BuildReport report) =>
            report.GetFiles().Any(f => Path.GetFileName(f.path) == RuntimeAssembly);

        /// <summary>
        /// Where Player Settings keep the symbols this build compiles with: Server for a dedicated server build,
        /// otherwise the target's group. Build scripts often leave targetGroup unset, so it is derived from target.
        /// </summary>
        /// <param name="subtarget">BuildPlayerOptions.subtarget; 0 means the subtarget selected in Build Settings.</param>
        private static NamedBuildTarget SymbolTarget(BuildTarget target, BuildTargetGroup group, int subtarget)
        {
            if (group == BuildTargetGroup.Unknown) group = BuildPipeline.GetBuildTargetGroup(target);
            if (group == BuildTargetGroup.Standalone)
            {
                var standalone = subtarget != 0 ? (StandaloneBuildSubtarget)subtarget : EditorUserBuildSettings.standaloneBuildSubtarget;
                if (standalone == StandaloneBuildSubtarget.Server) return NamedBuildTarget.Server;
            }
            return NamedBuildTarget.FromBuildTargetGroup(group);
        }

        private static bool HasSymbol(NamedBuildTarget target)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] symbols);
            return Contains(symbols);
        }

        /// <summary>
        /// Unity 6 Build Profiles add their own Scripting Defines, which are not in BuildPlayerOptions. The profile being
        /// built is the active one, also while a script builds a profile with BuildPlayerWithProfileOptions.
        /// </summary>
        private static bool InActiveBuildProfile(BuildTarget target)
        {
#if UNITY_6000_0_OR_NEWER
            // The active profile's platform is the active build target.
            UnityEditor.Build.Profile.BuildProfile profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            return profile != null && target == EditorUserBuildSettings.activeBuildTarget && Contains(profile.scriptingDefines);
#else
            return false;
#endif
        }

        private static bool Contains(string[] symbols) => symbols != null && symbols.Any(s => s.Trim() == EnabledSymbol);

        /// <summary>Also runs when the Editor loads, for entries left by a build that ended with the Editor (batch mode, crash).</summary>
        [InitializeOnLoadMethod]
        private static void RemoveLeftover() => EditorApplication.delayCall += RemoveAfterBuild;

        private static void RemoveAfterBuild()
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                EditorApplication.delayCall += RemoveAfterBuild;
                return;
            }
            Remove();
        }

        private static void Remove()
        {
            UnityEngine.Object[] preloaded = PlayerSettings.GetPreloadedAssets();
            UnityEngine.Object[] kept = preloaded.Where(a => a == null || !AssetDatabase.GetAssetPath(a).StartsWith(Folder + "/")).ToArray();
            if (kept.Length == preloaded.Length) return;

            PlayerSettings.SetPreloadedAssets(kept);
            // The build saved Player Settings with the entries; save again so ProjectSettings.asset is left unchanged.
            AssetDatabase.SaveAssets();
        }
    }
}
