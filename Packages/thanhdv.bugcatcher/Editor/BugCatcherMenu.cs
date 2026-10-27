using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;

namespace ThanhDV.BugCatcher.Editor
{
    /// <summary>
    /// Tools → ThanhDV → Bug Catcher: toggles the package's scripting define symbols on every platform.
    /// Bug Catcher compiles only with BUGCATCHER_ENABLED (Editor and every build alike); FMOD only with BUGCATCHER_FMOD as well.
    /// </summary>
    /// <remarks>
    /// This assembly has no define constraints and no reference to the runtime, so the menu still compiles
    /// while Bug Catcher is disabled.
    /// </remarks>
    [InitializeOnLoad]
    internal static class BugCatcherMenu
    {
        private const string Root = "Tools/ThanhDV/Bug Catcher/";
        private const string EnabledItem = Root + "Enable Bug Catcher";
        private const string FmodItem = Root + "Use FMOD";

        private const string EnabledSymbol = "BUGCATCHER_ENABLED";
        private const string FmodSymbol = "BUGCATCHER_FMOD";

        /// <summary>Written on the first import; kept in ProjectSettings so the whole team shares it.</summary>
        private const string ImportMarker = "ProjectSettings/ThanhDV.BugCatcher.json";

        /// <summary>A fresh import turns Bug Catcher on once; later the symbol is only changed from the menu.</summary>
        static BugCatcherMenu()
        {
            if (File.Exists(ImportMarker)) return;

            File.WriteAllText(ImportMarker, "{ \"note\": \"Bug Catcher was imported; BUGCATCHER_ENABLED was added once. Delete this file to repeat that.\" }\n");
            SetSymbol(EnabledSymbol, true);
        }

        [MenuItem(EnabledItem, priority = 0)]
        private static void ToggleEnabled() => SetSymbol(EnabledSymbol, !IsEnabled);

        [MenuItem(EnabledItem, true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(EnabledItem, IsEnabled);
            return true;
        }

        [MenuItem(FmodItem, priority = 1)]
        private static void ToggleFmod()
        {
            bool enable = !HasSymbol(FmodSymbol);
            if (enable && !IsFmodInstalled())
            {
                EditorUtility.DisplayDialog("Bug Catcher",
                    "FMOD for Unity was not found in this project (assembly \"FMODUnity\").\n\nInstall FMOD first, otherwise " + FmodSymbol + " breaks compilation.",
                    "OK");
                return;
            }
            SetSymbol(FmodSymbol, enable);
        }

        [MenuItem(FmodItem, true)]
        private static bool ToggleFmodValidate()
        {
            Menu.SetChecked(FmodItem, HasSymbol(FmodSymbol));
            return true;
        }

        private static bool IsEnabled => HasSymbol(EnabledSymbol);

        private static bool IsFmodInstalled() =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "FMODUnity");

        /// <summary>State shown in the menu: the symbols of the platform currently selected in Build Settings.</summary>
        private static bool HasSymbol(string symbol)
        {
            NamedBuildTarget current = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            return ReadSymbols(current).Contains(symbol);
        }

        /// <summary>Adds or removes the symbol on every platform, so switching platform keeps the choice.</summary>
        private static void SetSymbol(string symbol, bool present)
        {
            foreach (NamedBuildTarget target in AllTargets())
            {
                List<string> symbols = ReadSymbols(target);
                if (symbols.Contains(symbol) == present) continue;

                if (present) symbols.Add(symbol);
                else symbols.RemoveAll(s => s == symbol);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            }
        }

        private static List<string> ReadSymbols(NamedBuildTarget target) =>
            PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

        private static IEnumerable<NamedBuildTarget> AllTargets()
        {
            var seen = new HashSet<string>();
            // Walk the fields, not the values: obsolete aliases share a value with live groups (iPhone = iOS).
            foreach (FieldInfo field in typeof(BuildTargetGroup).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsDefined(typeof(ObsoleteAttribute), false)) continue;
                var group = (BuildTargetGroup)field.GetValue(null);
                if (group == BuildTargetGroup.Unknown) continue;

                NamedBuildTarget target;
                try
                {
                    target = NamedBuildTarget.FromBuildTargetGroup(group);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (seen.Add(target.TargetName)) yield return target;
            }

            // Dedicated Server shares the Standalone group but keeps its own symbols.
            if (seen.Add(NamedBuildTarget.Server.TargetName)) yield return NamedBuildTarget.Server;
        }
    }
}
