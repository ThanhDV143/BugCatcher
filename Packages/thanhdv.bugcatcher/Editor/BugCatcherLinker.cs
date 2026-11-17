using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.UnityLinker;

namespace ThanhDV.BugCatcher.Editor
{
    /// <summary>
    /// Keeps every [ConsoleMethod] method in builds with managed code stripping: nothing calls them directly, so the
    /// linker would remove them. Only for builds that contain Bug Catcher.
    /// </summary>
    /// <remarks>Finds the attribute by name, so this assembly needs no reference to the runtime.</remarks>
    internal sealed class BugCatcherLinker : IUnityLinkerProcessor
    {
        private const string AttributeType = "ThanhDV.BugCatcher.ConsoleMethodAttribute, ThanhDV.BugCatcher";
        private const string LinkFile = "Temp/BugCatcher/link.xml";

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            if (!BugCatcherBuild.ContainsBugCatcher(report)) return null;
            Type attribute = Type.GetType(AttributeType);
            if (attribute == null)
            {
                UnityEngine.Debug.LogWarning("[BugCatcher] BUGCATCHER_ENABLED is set for this build but not in the Editor, so the Editor cannot list the console commands to keep. Managed code stripping may remove them.");
                return null;
            }

            // Editor-only assemblies are not in the build; the linker must only see player assemblies.
            var playerAssemblies = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(a => a.name));
            playerAssemblies.Add(attribute.Assembly.GetName().Name);

            var xml = new StringBuilder("<linker>\n");
            IEnumerable<IGrouping<string, MethodInfo>> byAssembly = TypeCache.GetMethodsWithAttribute(attribute)
                .Where(m => m.DeclaringType != null && playerAssemblies.Contains(m.DeclaringType.Assembly.GetName().Name))
                .GroupBy(m => m.DeclaringType.Assembly.GetName().Name);
            foreach (IGrouping<string, MethodInfo> assembly in byAssembly)
            {
                xml.Append("  <assembly fullname=\"").Append(Escape(assembly.Key)).Append("\">\n");
                foreach (IGrouping<Type, MethodInfo> type in assembly.GroupBy(m => m.DeclaringType))
                {
                    // Nested types are written Outer/Inner in link.xml.
                    xml.Append("    <type fullname=\"").Append(Escape(type.Key.FullName.Replace('+', '/'))).Append("\">\n");
                    foreach (string name in type.Select(m => m.Name).Distinct())
                        xml.Append("      <method name=\"").Append(Escape(name)).Append("\" />\n");
                    xml.Append("    </type>\n");
                }
                xml.Append("  </assembly>\n");
            }
            xml.Append("</linker>\n");

            Directory.CreateDirectory(Path.GetDirectoryName(LinkFile));
            File.WriteAllText(LinkFile, xml.ToString());
            return Path.GetFullPath(LinkFile);
        }

        private static string Escape(string value) => SecurityElement.Escape(value);
    }
}
