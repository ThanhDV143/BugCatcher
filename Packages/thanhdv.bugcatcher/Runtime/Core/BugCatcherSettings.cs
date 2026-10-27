using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThanhDV.BugCatcher
{
    /// <summary>HUD modules, in the order they stack inside a corner.</summary>
    public enum HudModule { Fps, Ram, Audio, Logs, System }

    public enum HudMode { Off, Basic, Text, Full }

    public enum HudCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    public enum TextSize { Small, Default, Large }

    [Serializable]
    public class HudLayout
    {
        public const int ModuleCount = 5;

        public HudMode[] modes = { HudMode.Full, HudMode.Text, HudMode.Basic, HudMode.Off, HudMode.Off };
        public HudCorner[] corners = { HudCorner.TopLeft, HudCorner.TopLeft, HudCorner.TopRight, HudCorner.TopRight, HudCorner.BottomLeft };
        public float scale = 1f;
        public float opacity = 0.78f;

        public HudLayout Clone()
        {
            return new HudLayout
            {
                modes = (HudMode[])modes.Clone(),
                corners = (HudCorner[])corners.Clone(),
                scale = scale,
                opacity = opacity
            };
        }

        internal void Sanitize()
        {
            var defaults = new HudLayout();
            // Saves from before the Logs module hold FPS, RAM, Audio, System: slot Logs (default) in under Audio.
            if (modes != null && modes.Length == 4) modes = new[] { modes[0], modes[1], modes[2], defaults.modes[(int)HudModule.Logs], modes[3] };
            if (corners != null && corners.Length == 4) corners = new[] { corners[0], corners[1], corners[2], defaults.corners[(int)HudModule.Logs], corners[3] };
            if (modes == null || modes.Length != ModuleCount) modes = defaults.modes;
            if (corners == null || corners.Length != ModuleCount) corners = defaults.corners;
            // System is either Off or Full; any other mode (quick menu, old saves) means Full.
            if (modes[(int)HudModule.System] != HudMode.Off) modes[(int)HudModule.System] = HudMode.Full;
            scale = Mathf.Clamp(scale, 0.75f, 1.5f);
            opacity = Mathf.Clamp01(opacity);
        }
    }

    [Serializable]
    public class LayoutPreset
    {
        public string name;
        public HudLayout layout;
    }

    /// <summary>Everything the viewer can change at runtime. Persisted as JSON in PlayerPrefs.</summary>
    [Serializable]
    public class BugCatcherSettings
    {
        private const string PrefsKey = "ThanhDV.BugCatcher.Settings";
        private const int MaxRecentCommands = 10;

        public string theme = "graphite";
        public bool showBubble = true;
        public bool hudVisible = true;
        public bool bubbleOnRight = true;
        public float bubbleY = 0.42f;
        public HudLayout hud = new HudLayout();
        public TextSize textSize = TextSize.Default;
        public List<LayoutPreset> presets = new List<LayoutPreset>();
        /// <summary>-1 = built-in Default, -2 = none (layout edited since), otherwise an index into <see cref="presets"/>.</summary>
        public int activePreset = -1;
        public List<string> recentCommands = new List<string>();
        public string audioProvider = "";
        public bool collapse = true;
        public bool showLog = true;
        public bool showWarning = true;
        public bool showError = true;
        public bool sheetHalf;

        /// <summary>Raised after any change made through <see cref="Changed"/>.</summary>
        public event Action OnChanged;

        public static BugCatcherSettings Load()
        {
            BugCatcherSettings settings = null;
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, null);
                if (!string.IsNullOrEmpty(json)) settings = JsonUtility.FromJson<BugCatcherSettings>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BugCatcher] Settings reset: " + e.Message);
            }

            settings ??= new BugCatcherSettings();
            settings.hud ??= new HudLayout();
            settings.hud.Sanitize();
            settings.presets ??= new List<LayoutPreset>();
            settings.recentCommands ??= new List<string>();
            foreach (var preset in settings.presets) preset.layout?.Sanitize();
            return settings;
        }

        /// <summary>Call after mutating fields: saves and notifies listeners.</summary>
        public void Changed()
        {
            hud.Sanitize();
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            OnChanged?.Invoke();
        }

        public void AddRecentCommand(string command)
        {
            recentCommands.Remove(command);
            recentCommands.Insert(0, command);
            if (recentCommands.Count > MaxRecentCommands) recentCommands.RemoveRange(MaxRecentCommands, recentCommands.Count - MaxRecentCommands);
            Changed();
        }
    }
}
