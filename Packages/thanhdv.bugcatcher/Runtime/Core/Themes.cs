using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher
{
    /// <summary>
    /// A theme is a USS class that defines every colour token (see BugCatcher.uss, .theme-graphite).
    /// Custom themes ship their own StyleSheet with a class of the same name.
    /// </summary>
    public class ThemeInfo
    {
        public string Id;
        public string DisplayName;
        public string UssClass;
        /// <summary>RGB of bg/overlay; its alpha comes from the HUD "Background opacity" setting.</summary>
        public Color OverlayBase;
        /// <summary>Three swatches drawn on the theme picker: panel, status/good, accent.</summary>
        public Color[] Swatches;
        /// <summary>Extra stylesheet for custom themes; null for built-ins.</summary>
        public StyleSheet StyleSheet;
        public bool IsBuiltIn;
    }

    public static class Themes
    {
        private static readonly List<ThemeInfo> all = new List<ThemeInfo>
        {
            new ThemeInfo
            {
                Id = "graphite",
                DisplayName = "Graphite",
                UssClass = "theme-graphite",
                OverlayBase = Rgb(12, 14, 17),
                Swatches = new[] { Hex(0x121417), Hex(0x3DDC84), Hex(0x5AB0FF) },
                IsBuiltIn = true
            },
            new ThemeInfo
            {
                Id = "paper",
                DisplayName = "Paper",
                UssClass = "theme-paper",
                OverlayBase = Rgb(250, 251, 252),
                Swatches = new[] { Hex(0xF4F5F7), Hex(0x11823B), Hex(0x1F63D6) },
                IsBuiltIn = true
            }
        };

        public static IReadOnlyList<ThemeInfo> All => all;

        public static event Action OnRegistered;

        public static ThemeInfo Find(string id)
        {
            foreach (ThemeInfo theme in all)
            {
                if (string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase)) return theme;
            }
            return null;
        }

        public static ThemeInfo FindOrDefault(string id) => Find(id) ?? all[0];

        /// <summary>
        /// Adds a theme. <paramref name="styleSheet"/> must define the class "theme-{id}" with every token
        /// that .theme-graphite defines in BugCatcher.uss.
        /// </summary>
        public static void Register(string id, string displayName, StyleSheet styleSheet, Color overlayBase, Color panel, Color good, Color accent)
        {
            if (Find(id) != null) throw new ArgumentException("Theme already registered: " + id);

            all.Add(new ThemeInfo
            {
                Id = id.ToLowerInvariant(),
                DisplayName = displayName,
                UssClass = "theme-" + id.ToLowerInvariant(),
                OverlayBase = overlayBase,
                Swatches = new[] { panel, good, accent },
                StyleSheet = styleSheet
            });
            OnRegistered?.Invoke();
        }

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        private static Color Hex(int rgb) => Rgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }
}
