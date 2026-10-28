using System.Collections.Generic;

namespace ThanhDV.BugCatcher
{
    /// <summary>
    /// Fixed capture presets: short screen edge in pixels × captures per second.
    /// Each pair costs about the same; lower resolutions allow more frames per second.
    /// </summary>
    public enum CaptureMode
    {
        P144_24 = 0,
        P144_12 = 1,
        P144_6 = 2,
        P240_16 = 3,
        P240_8 = 4,
        P240_4 = 5,
        P360_8 = 6,
        P360_4 = 7,
        P360_2 = 8,
        P720_4 = 9,
        P720_2 = 10
    }

    internal static class CaptureModes
    {
        public const CaptureMode Default = CaptureMode.P360_4;

        /// <summary>Resolutions offered in Settings, smallest first.</summary>
        public static readonly int[] Resolutions = { 144, 240, 360, 720 };

        private static readonly Dictionary<CaptureMode, (int height, int fps)> specs = new Dictionary<CaptureMode, (int, int)>
        {
            { CaptureMode.P144_24, (144, 24) },
            { CaptureMode.P144_12, (144, 12) },
            { CaptureMode.P144_6, (144, 6) },
            { CaptureMode.P240_16, (240, 16) },
            { CaptureMode.P240_8, (240, 8) },
            { CaptureMode.P240_4, (240, 4) },
            { CaptureMode.P360_8, (360, 8) },
            { CaptureMode.P360_4, (360, 4) },
            { CaptureMode.P360_2, (360, 2) },
            { CaptureMode.P720_4, (720, 4) },
            { CaptureMode.P720_2, (720, 2) }
        };

        /// <summary>Short edge of the captured image, in pixels.</summary>
        public static int Height(CaptureMode mode) => Spec(mode).height;

        public static int Fps(CaptureMode mode) => Spec(mode).fps;

        public static string Label(CaptureMode mode) => Height(mode) + "p · " + Fps(mode) + " fps";

        /// <summary>The modes of one resolution, highest fps first.</summary>
        public static List<CaptureMode> ForResolution(int height)
        {
            var modes = new List<CaptureMode>();
            foreach (KeyValuePair<CaptureMode, (int height, int fps)> pair in specs)
            {
                if (pair.Value.height == height) modes.Add(pair.Key);
            }
            modes.Sort((a, b) => Fps(b).CompareTo(Fps(a)));
            return modes;
        }

        private static (int height, int fps) Spec(CaptureMode mode) => specs.TryGetValue(mode, out var spec) ? spec : specs[Default];
    }
}
