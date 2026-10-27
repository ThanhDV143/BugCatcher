using System.Text;
using UnityEngine;

namespace ThanhDV.BugCatcher
{
    /// <summary>Device facts formatted the way the System tab and HUD module show them.</summary>
    public static class SystemInfoService
    {
        public static string Os
        {
            get
            {
                string os = SystemInfo.operatingSystem;
#if UNITY_ANDROID && !UNITY_EDITOR
                // "Android OS 14 / API-34 (UP1A.231005.007/...)" -> "Android 14 · API 34"
                string version = os.Replace("Android OS ", "");
                int slash = version.IndexOf(" / API-");
                if (slash > 0)
                {
                    string api = version.Substring(slash + 7);
                    int paren = api.IndexOf(' ');
                    if (paren > 0) api = api.Substring(0, paren);
                    return "Android " + version.Substring(0, slash) + " · API " + api;
                }
#endif
                int cut = os.IndexOf('(');
                return (cut > 0 ? os.Substring(0, cut) : os).Trim();
            }
        }

        public static string Cpu => SystemInfo.processorType + " · " + SystemInfo.processorCount + " cores";

        public static string CpuShort => SystemInfo.processorType + " · " + SystemInfo.processorCount + " c";

        public static string Gpu
        {
            get
            {
                string api = SystemInfo.graphicsDeviceVersion;
                int paren = api.IndexOf('[');
                if (paren > 0) api = api.Substring(0, paren);
                return SystemInfo.graphicsDeviceName + " · " + api.Trim();
            }
        }

        public static string GpuShort => SystemInfo.graphicsDeviceName + " · " + SystemInfo.graphicsDeviceType.ToString().Replace("Direct3D", "D3D");

        public static string Ram => FormatGb(SystemInfo.systemMemorySize);

        public static string Screen
        {
            get
            {
                Resolution r = UnityEngine.Screen.currentResolution;
                int hz = Mathf.RoundToInt((float)r.refreshRateRatio.value);
                return UnityEngine.Screen.width + " × " + UnityEngine.Screen.height + " · " + hz + " Hz";
            }
        }

        public static string Unity
        {
            get
            {
                string version = Application.unityVersion;
                int f = version.IndexOf('f');
                if (f > 0) version = version.Substring(0, f);
#if ENABLE_IL2CPP
                return version + " · IL2CPP";
#else
                return version + " · Mono";
#endif
            }
        }

        public static string FormatGb(int megabytes)
        {
            return Mathf.RoundToInt(megabytes / 1024f) + " GB";
        }

        public static string CopyText()
        {
            var sb = new StringBuilder();
            sb.Append("OS      ").AppendLine(Os);
            sb.Append("CPU     ").AppendLine(Cpu);
            sb.Append("GPU     ").AppendLine(Gpu);
            sb.Append("RAM     ").AppendLine(Ram);
            sb.Append("Screen  ").AppendLine(Screen);
            sb.Append("Unity   ").AppendLine(Unity);
            return sb.ToString();
        }
    }
}
