using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEngine;

namespace ThanhDV.BugCatcher.Editor
{
    /// <summary>
    /// Keeps every [ConsoleMethod] method in builds with managed code stripping: nothing calls them directly, so the
    /// linker would remove them. Only for builds that contain Bug Catcher.
    /// </summary>
    /// <remarks>
    /// The methods are read from the assemblies the build compiled, not from the Editor's, so they are all kept even
    /// when the build has defines the Editor has not compiled with (BuildPlayerOptions.extraScriptingDefines, or a
    /// symbol set right before the build).
    /// </remarks>
    internal sealed class BugCatcherLinker : IUnityLinkerProcessor
    {
        internal const string AttributeType = "ThanhDV.BugCatcher.ConsoleMethodAttribute";
        private const string LinkFile = "Temp/BugCatcher/link.xml";

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            if (!BugCatcherBuild.ContainsBugCatcher(report)) return null;

            IEnumerable<string> assemblies = report.GetFiles()
                .Select(f => f.path)
                .Where(p => string.Equals(Path.GetExtension(p), ".dll", StringComparison.OrdinalIgnoreCase));
            Directory.CreateDirectory(Path.GetDirectoryName(LinkFile));
            File.WriteAllText(LinkFile, LinkXml(assemblies));
            return Path.GetFullPath(LinkFile);
        }

        /// <summary>
        /// link.xml that keeps every [ConsoleMethod] method of these assemblies. An assembly that cannot be read is
        /// kept whole, with a warning, so no command is lost.
        /// </summary>
        internal static string LinkXml(IEnumerable<string> assemblyPaths)
        {
            // Assembly name -> type -> method names. Sorted so the file is the same from build to build.
            var keep = new SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>(StringComparer.Ordinal);
            var keepWhole = new SortedSet<string>(StringComparer.Ordinal);

            foreach (string path in assemblyPaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                AssemblyMetadata metadata;
                List<(string Type, string Method)> methods;
                try
                {
                    metadata = AssemblyMetadata.Read(path);
                    if (metadata == null || metadata.Name == null) continue; // Native library or bare module.
                    methods = metadata.MethodsWithAttribute(AttributeType);
                }
                catch (Exception e)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    Debug.LogWarning("[BugCatcher] Could not read the console commands of " + path + " (" + e.Message + "). Keeping all of " + name + " from managed code stripping instead.");
                    keepWhole.Add(name);
                    continue;
                }

                foreach ((string type, string method) in methods)
                {
                    if (!keep.TryGetValue(metadata.Name, out SortedDictionary<string, SortedSet<string>> types))
                        keep.Add(metadata.Name, types = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal));
                    if (!types.TryGetValue(type, out SortedSet<string> names))
                        types.Add(type, names = new SortedSet<string>(StringComparer.Ordinal));
                    names.Add(method);
                }
            }

            var xml = new StringBuilder("<linker>\n");
            foreach (string assembly in keepWhole)
                xml.Append("  <assembly fullname=\"").Append(Escape(assembly)).Append("\" preserve=\"all\" />\n");
            foreach (KeyValuePair<string, SortedDictionary<string, SortedSet<string>>> assembly in keep)
            {
                if (keepWhole.Contains(assembly.Key)) continue;
                xml.Append("  <assembly fullname=\"").Append(Escape(assembly.Key)).Append("\">\n");
                foreach (KeyValuePair<string, SortedSet<string>> type in assembly.Value)
                {
                    xml.Append("    <type fullname=\"").Append(Escape(type.Key)).Append("\">\n");
                    // A name keeps every overload.
                    foreach (string method in type.Value)
                        xml.Append("      <method name=\"").Append(Escape(method)).Append("\" />\n");
                    xml.Append("    </type>\n");
                }
                xml.Append("  </assembly>\n");
            }
            xml.Append("</linker>\n");
            return xml.ToString();
        }

        private static string Escape(string value) => SecurityElement.Escape(value);
    }
}
