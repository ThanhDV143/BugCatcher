using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ThanhDV.BugCatcher
{
    /// <summary>Splits a command line into arguments and converts each argument to a parameter type.</summary>
    internal static class CommandParser
    {
        // Each pair groups an argument that contains spaces: "a b", 'a b', (1 2 3), [1 2 3], {1 2 3}.
        private const string Openers = "\"'([{";
        private const string Closers = "\"')]}";
        private static readonly char[] EnumOperators = { '|', '&' };

        private static readonly Dictionary<Type, Func<string, object>> parsers = new Dictionary<Type, Func<string, object>>
        {
            { typeof(string), s => s },
            { typeof(bool), ParseBool },
            { typeof(char), s => s.Length == 1 ? (object)s[0] : null },
            { typeof(int), s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? (object)v : null },
            { typeof(uint), s => uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint v) ? (object)v : null },
            { typeof(long), s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? (object)v : null },
            { typeof(ulong), s => ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong v) ? (object)v : null },
            { typeof(short), s => short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out short v) ? (object)v : null },
            { typeof(ushort), s => ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort v) ? (object)v : null },
            { typeof(byte), s => byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte v) ? (object)v : null },
            { typeof(sbyte), s => sbyte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte v) ? (object)v : null },
            { typeof(float), s => TryFloat(s, out float v) ? (object)v : null },
            { typeof(double), s => double.TryParse(TrimSuffix(s), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? (object)v : null },
            { typeof(decimal), s => decimal.TryParse(TrimSuffix(s), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal v) ? (object)v : null },
            // Missing numbers keep these defaults: zero, except Quaternion.identity and opaque black.
            { typeof(Vector2), s => TryFloats(s, new float[2], out float[] v) ? (object)new Vector2(v[0], v[1]) : null },
            { typeof(Vector3), s => TryFloats(s, new float[3], out float[] v) ? (object)new Vector3(v[0], v[1], v[2]) : null },
            { typeof(Vector4), s => TryFloats(s, new float[4], out float[] v) ? (object)new Vector4(v[0], v[1], v[2], v[3]) : null },
            { typeof(Quaternion), s => TryFloats(s, new[] { 0f, 0f, 0f, 1f }, out float[] v) ? (object)new Quaternion(v[0], v[1], v[2], v[3]) : null },
            { typeof(Color), s => TryFloats(s, new[] { 0f, 0f, 0f, 1f }, out float[] v) ? (object)new Color(v[0], v[1], v[2], v[3]) : null },
            { typeof(Vector2Int), s => TryFloats(s, new float[2], out float[] v) ? (object)new Vector2Int(Mathf.RoundToInt(v[0]), Mathf.RoundToInt(v[1])) : null },
            { typeof(Vector3Int), s => TryFloats(s, new float[3], out float[] v) ? (object)new Vector3Int(Mathf.RoundToInt(v[0]), Mathf.RoundToInt(v[1]), Mathf.RoundToInt(v[2])) : null }
        };

        public static bool IsSupported(Type type) => type.IsEnum || parsers.ContainsKey(type);

        /// <summary>Converts <paramref name="input"/> to <paramref name="type"/>; false when it is not a valid value.</summary>
        public static bool TryParse(string input, Type type, out object value)
        {
            if (type.IsEnum) return TryParseEnum(input, type, out value);

            value = parsers.TryGetValue(type, out Func<string, object> parse) ? parse(input) : null;
            return value != null;
        }

        /// <summary>
        /// Splits on spaces; a group opened by one of <see cref="Openers"/> is one argument without its delimiters.
        /// A comma right after an argument is dropped, so "1, 2" gives "1" and "2".
        /// </summary>
        public static void Split(string line, List<string> output)
        {
            output.Clear();
            int i = 0;
            while (i < line.Length)
            {
                if (char.IsWhiteSpace(line[i]))
                {
                    i++;
                    continue;
                }

                int group = Openers.IndexOf(line[i]);
                if (group >= 0)
                {
                    int end = GroupEnd(line, group, i + 1);
                    output.Add(line.Substring(i + 1, end - i - 1));
                    i = end + 1;
                    if (i < line.Length && line[i] == ',') i++;
                    continue;
                }

                int space = i;
                while (space < line.Length && !char.IsWhiteSpace(line[space])) space++;
                int length = space - i;
                if (length > 1 && line[space - 1] == ',') length--;
                output.Add(line.Substring(i, length));
                i = space;
            }
        }

        /// <summary>Index of the closer matching the opener before <paramref name="start"/>, or the line's length.</summary>
        private static int GroupEnd(string line, int group, int start)
        {
            char opener = Openers[group];
            char closer = Closers[group];
            int depth = 1;
            for (int i = start; i < line.Length; i++)
            {
                if (line[i] == closer && --depth == 0) return i;
                if (line[i] == opener) depth++;
            }
            return line.Length;
        }

        private static object ParseBool(string s)
        {
            if (s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
            return null;
        }

        private static bool TryFloat(string s, out float value) =>
            float.TryParse(TrimSuffix(s), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>Accepts "1.5f" like C# does.</summary>
        private static string TrimSuffix(string s) =>
            s.Length > 1 && (s[s.Length - 1] == 'f' || s[s.Length - 1] == 'F') ? s.Substring(0, s.Length - 1) : s;

        /// <summary>
        /// Numbers separated by spaces or commas fill <paramref name="defaults"/> in order; extra numbers are
        /// ignored, but each one must still be a number.
        /// </summary>
        private static bool TryFloats(string s, float[] defaults, out float[] values)
        {
            values = defaults;
            string[] tokens = s.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!TryFloat(tokens[i], out float number)) return false;
                if (i < values.Length) values[i] = number;
            }
            return true;
        }

        /// <summary>
        /// A name (any case) or a number; several can be combined left to right with | and &amp;, as in "Read|Write".
        /// </summary>
        private static bool TryParseEnum(string input, Type type, out object value)
        {
            value = null;
            long result = 0;
            char operation = ' ';
            int start = 0;
            while (start <= input.Length)
            {
                int end = input.IndexOfAny(EnumOperators, start);
                if (end < 0) end = input.Length;

                string part = input.Substring(start, end - start).Trim();
                if (!long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
                {
                    try
                    {
                        number = Convert.ToInt64(Enum.Parse(type, part, true), CultureInfo.InvariantCulture);
                    }
                    catch (Exception e) when (e is ArgumentException || e is OverflowException)
                    {
                        return false;
                    }
                }

                if (operation == '|') result |= number;
                else if (operation == '&') result &= number;
                else result = number;

                if (end == input.Length) break;
                operation = input[end];
                start = end + 1;
            }

            value = Enum.ToObject(type, result);
            return true;
        }
    }
}
