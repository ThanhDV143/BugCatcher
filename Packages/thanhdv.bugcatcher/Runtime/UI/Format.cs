using System;
using System.Globalization;

namespace ThanhDV.BugCatcher.UI
{
    /// <summary>Number formatting used across the UI. Small integers are cached to avoid garbage every update.</summary>
    internal static class Format
    {
        public const string Minus = "−";
        private static readonly string[] ints = new string[1000];
        private static readonly NumberFormatInfo grouped = new NumberFormatInfo { NumberGroupSeparator = " ", NumberDecimalSeparator = "." };

        public static string Int(int value)
        {
            if (value >= 0 && value < ints.Length) return ints[value] ??= value.ToString(CultureInfo.InvariantCulture);
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>"412.6", "1 204.8" (thin-space thousands).</summary>
        public static string Mb(float value) => value.ToString("#,0.0", grouped);

        /// <summary>"−14.2"; "−∞" for silence.</summary>
        public static string Db(float db)
        {
            if (float.IsNegativeInfinity(db) || db < -120f) return Minus + "∞";
            string text = Math.Abs(db).ToString("0.0", CultureInfo.InvariantCulture);
            return db < -0.05f ? Minus + text : text;
        }

        public static string Count(int value) => value > 99 ? "99+" : Int(value);

        public static string Repeat(int count) => "×" + Int(count);
    }
}
