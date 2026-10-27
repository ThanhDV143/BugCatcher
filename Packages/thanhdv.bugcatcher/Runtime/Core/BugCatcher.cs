using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ThanhDV.BugCatcher.UI;
using UnityEngine;

namespace ThanhDV.BugCatcher
{
    /// <summary>Public entry point. BugCatcher starts itself before the first scene loads.</summary>
    public static class BugCatcher
    {
        private static BugCatcherHost host;
        private static readonly List<IAudioProvider> pendingProviders = new List<IAudioProvider>();

        public static BugCatcherSettings Settings { get; private set; }
        public static LogStore Logs { get; private set; }
        public static CommandService Commands { get; private set; }
        public static PerformanceService Performance { get; private set; }
        public static AudioService Audio { get; private set; }

        public static bool IsOpen => host != null && host.Ui.IsPanelOpen;

        /// <summary>
        /// Replaces the default "Share" behaviour (copy text to the clipboard and keep the file).
        /// Arguments: the text, and the path of the file it was also saved to.
        /// Hook a native share plugin here.
        /// </summary>
        public static Action<string, string> ShareHandler;

        public static void Open() => host?.Ui.OpenPanel();

        public static void Close() => host?.Ui.ClosePanel();

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>Adds an audio source to the Audio tab and HUD module. Safe to call before BugCatcher starts.</summary>
        public static void RegisterAudioProvider(IAudioProvider provider)
        {
            if (Audio != null) Audio.Register(provider);
            else pendingProviders.Add(provider);
        }

        public static void Share(string text, string fileName)
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            try
            {
                File.WriteAllText(path, text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BugCatcher] Could not write " + path + ": " + e.Message);
            }

            if (ShareHandler != null)
            {
                ShareHandler(text, path);
                return;
            }

            GUIUtility.systemCopyBuffer = text;
            Debug.Log("[BugCatcher] Copied to clipboard and saved to " + path);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
#endif
        }

        public static void CaptureScreenshot()
        {
            if (host != null) host.StartCoroutine(host.CaptureScreenshot());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Logs?.Detach();
            host = null;
            Settings = null;
            Logs = null;
            Commands = null;
            Performance = null;
            Audio = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Logs = new LogStore();
            Logs.Attach();
            Settings = BugCatcherSettings.Load();

            var go = new GameObject("[BugCatcher]");
            UnityEngine.Object.DontDestroyOnLoad(go);

            Commands = new CommandService(Settings);
            Performance = new PerformanceService(go);
            Audio = new AudioService(Settings);
            foreach (IAudioProvider provider in pendingProviders) Audio.Register(provider);
            pendingProviders.Clear();

            host = go.AddComponent<BugCatcherHost>();
        }

        internal static void OnHostDestroyed(BugCatcherHost destroyed)
        {
            if (host == destroyed) host = null;
        }
    }

    /// <summary>Drives every service once per frame and owns the UI.</summary>
    [DefaultExecutionOrder(-1000)]
    internal class BugCatcherHost : MonoBehaviour
    {
        private readonly OpenTriggers triggers = new OpenTriggers();

        public BugCatcherUI Ui { get; private set; }

        private void Awake()
        {
            Ui = new BugCatcherUI(gameObject, triggers);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            BugCatcher.Logs.Drain();
            BugCatcher.Performance.Tick(dt);
            BugCatcher.Audio.Tick(dt);

            triggers.GestureEnabled = !Ui.IsPanelOpen;
            if (triggers.TickGesture()) Ui.OpenPanelAfterGesture();
            if (triggers.TickKeys(Ui.IsTyping())) Ui.TogglePanel();

            Ui.Tick(dt);
        }

        private void OnDestroy()
        {
            BugCatcher.Logs?.Detach();
            BugCatcher.OnHostDestroyed(this);
        }

        public IEnumerator CaptureScreenshot()
        {
            Ui.SetHidden(true);
            yield return new WaitForEndOfFrame();

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            Ui.SetHidden(false);
            string path = Path.Combine(Application.persistentDataPath, "BugCatcher_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            try
            {
                File.WriteAllBytes(path, shot.EncodeToPNG());
                Debug.Log("[BugCatcher] Screenshot saved: " + path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BugCatcher] Screenshot failed: " + e.Message);
            }
            Destroy(shot);
        }
    }
}
