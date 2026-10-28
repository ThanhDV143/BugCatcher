using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace ThanhDV.BugCatcher
{
    /// <summary>What the recording bubble shows.</summary>
    public enum RecorderState
    {
        /// <summary>Nothing is captured; the recording bubble is hidden.</summary>
        Off,
        /// <summary>Auto screenshot keeps the last seconds in memory and waits for a signal.</summary>
        Waiting,
        /// <summary>Auto screenshot is writing an incident to disk.</summary>
        Incident,
        /// <summary>A <see cref="ScreenRecorder.Start"/> session is writing to disk.</summary>
        Recording,
        /// <summary>Stopped because files could not be written (usually a full disk).</summary>
        Error
    }

    /// <summary>
    /// Low-resolution continuous screenshots.
    /// Auto screenshot keeps the last 2 s in memory; a signal (a hitch, or an error seen for the first time) writes those
    /// 2 s to disk and keeps writing until 2 s pass without a new signal. <see cref="Start"/>/<see cref="Stop"/> record a
    /// session straight to disk, independent of auto screenshot (which pauses meanwhile).
    /// Frames are copied and scaled on the GPU, read back asynchronously, and encoded/written on a background thread.
    /// </summary>
    public sealed class ScreenRecorder
    {
        private const double BeforeSeconds = 2.0;
        private const double AfterSeconds = 2.0;
        /// <summary>Memory buffer length: <see cref="BeforeSeconds"/> plus a margin for frames still being encoded.</summary>
        private const double BufferSeconds = 2.5;
        private const float HitchFactor = 3f;
        private const float HitchMinSeconds = 0.05f;
        private const double BaselineSeconds = 5.0;
        private const double BaselineWarmup = 1.0;
        /// <summary>How long a closing incident/session waits for frames still on the GPU or being encoded.</summary>
        private const double FlushTimeout = 1.5;
        private const int MaxPendingEncodes = 8;

        private sealed class Incident
        {
            public string Folder;
            public double Start;
            public double End;
            public DateTime StartedAt;
            public long LastSequence = -1;
            public int Frames;
            public readonly List<string> Reasons = new List<string>();
            public readonly StringBuilder Signals = new StringBuilder();
        }

        private sealed class Session
        {
            public string Folder;
            public string Label;
            public CaptureMode Mode;
            public DateTime StartedAt;
            public readonly Stopwatch Watch = new Stopwatch();
            public int Frames;
            public bool Stopping;
            public double StopTime;
        }

        private readonly BugCatcherSettings settings;
        private readonly FrameCapturer capturer = new FrameCapturer();
        private readonly JpegWorker worker;
        private readonly List<EncodedFrame> buffer = new List<EncodedFrame>();
        private readonly HashSet<string> seenErrors = new HashSet<string>();
        private readonly Queue<(double time, float dt)> baseline = new Queue<(double, float)>();
        private MonoBehaviour host;
        private Incident incident;
        private Session session;
        private TimeSpan lastElapsed;
        private double baselineSum;
        private double baselineSince = -1;
        private double nextCapture;
        private long sequence;
        private int generation;
        private bool loopRunning;
        private bool captureResources;
        private bool paused;
        private bool suppressed;
        private bool stoppedForSession;
        private bool diskError;
        private RecorderState lastState;

        internal ScreenRecorder(BugCatcherSettings settings, LogStore logs)
        {
            this.settings = settings;
            worker = new JpegWorker(capturer.Return);
            capturer.FrameReady += OnFrameReady;
            logs.OnNewEntry += OnNewLogEntry;
            Folder = Application.persistentDataPath + "/BugCatcher/Recordings";
        }

        /// <summary>Where incidents and sessions are saved, one folder each.</summary>
        public string Folder { get; }

        /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>.</summary>
        public bool IsRecording => session != null && !session.Stopping;

        /// <summary>Length of the current session, or of the last one once stopped.</summary>
        public TimeSpan Elapsed => session != null ? session.Watch.Elapsed : lastElapsed;

        internal RecorderState State
        {
            get
            {
                if (diskError) return RecorderState.Error;
                if (session != null) return RecorderState.Recording;
                if (incident != null) return RecorderState.Incident;
                return AutoActive ? RecorderState.Waiting : RecorderState.Off;
            }
        }

        /// <summary>Raised on the main thread when <see cref="State"/> changes.</summary>
        internal event Action StateChanged;

        /// <summary>The Settings switch: the saved setting, unless stopped from the bubble for this session.</summary>
        internal bool AutoEnabled => settings.autoScreenshot && !stoppedForSession && !diskError;

        private bool AutoActive => AutoEnabled && session == null;

        private bool ShouldCapture => !diskError && (session != null ? !session.Stopping : AutoActive);

        private CaptureMode Mode => session != null ? session.Mode : settings.autoMode;

        // --- Public API ------------------------------------------------------------------------

        /// <summary>
        /// Starts writing every frame of <paramref name="mode"/> to a new folder until <see cref="Stop"/>.
        /// Auto screenshot pauses meanwhile and its memory buffer is freed.
        /// </summary>
        public void Start(string label, CaptureMode mode)
        {
            if (session != null)
            {
                Debug.LogWarning("[BugCatcher] Recorder.Start ignored: already recording \"" + session.Label + "\".");
                return;
            }

            ClearDiskError();
            if (incident != null) FinishIncident();
            buffer.Clear();
            generation++;

            DateTime now = DateTime.Now;
            session = new Session
            {
                Label = string.IsNullOrWhiteSpace(label) ? "Recording" : label.Trim(),
                Mode = mode,
                StartedAt = now
            };
            session.Folder = Join(Folder, Stamp(now) + "_Rec_" + SafeName(session.Label));
            worker.CreateDirectory(session.Folder);
            session.Watch.Start();
            nextCapture = 0;
            NotifyIfChanged();
        }

        /// <summary>Stops the session started by <see cref="Start"/>. Returns its folder, or null if nothing was recording.</summary>
        public string Stop()
        {
            if (session == null || session.Stopping)
            {
                Debug.LogWarning("[BugCatcher] Recorder.Stop ignored: not recording.");
                return null;
            }

            session.Stopping = true;
            session.StopTime = Time.realtimeSinceStartupAsDouble;
            session.Watch.Stop();
            lastElapsed = session.Watch.Elapsed;
            return session.Folder;
        }

        /// <summary>
        /// Saves the last seconds held in memory by auto screenshot to a new folder and returns it.
        /// Returns null (with a warning) when auto screenshot is off or has nothing yet.
        /// </summary>
        public string SaveRecent(string reason)
        {
            if (!AutoActive || buffer.Count == 0)
            {
                Debug.LogWarning("[BugCatcher] Recorder.SaveRecent ignored: " + (AutoActive ? "nothing captured yet." : "auto screenshot is off."));
                return null;
            }

            reason = string.IsNullOrWhiteSpace(reason) ? "Saved" : reason.Trim();
            DateTime now = DateTime.Now;
            string folder = Join(Folder, Stamp(now) + "_Saved_" + SafeName(reason));
            worker.CreateDirectory(folder);
            foreach (EncodedFrame frame in buffer) worker.Write(FramePath(folder, frame), frame.Jpg);

            var info = Header("BugCatcher · saved recent frames", now, CaptureModes.Label(Mode), buffer.Count);
            info.AppendLine("Reason: " + reason);
            AppendDevice(info);
            worker.WriteText(Join(folder, "info.txt"), info.ToString());
            Debug.Log("[BugCatcher] Saved " + buffer.Count + " recent screenshots to " + folder);
            return folder;
        }

        /// <summary>Stops hitches from being signals (loading screens, big instantiations). Errors still count.</summary>
        public void Suppress(string reason)
        {
            suppressed = true;
        }

        /// <summary>Hitches count again; the average frame time is measured from scratch.</summary>
        public void Resume()
        {
            suppressed = false;
            ResetBaseline();
        }

        // --- Internal hooks ----------------------------------------------------------------------

        internal void Attach(MonoBehaviour owner) => host = owner;

        /// <summary>The Settings switch: saved, and clears a stop made from the bubble.</summary>
        internal void SetAutoEnabled(bool on)
        {
            settings.autoScreenshot = on;
            stoppedForSession = false;
            if (on) ClearDiskError();
            settings.Changed();
            NotifyIfChanged();
        }

        /// <summary>Hold on the recording bubble: stop whatever is running. Auto screenshot stays off until the next launch.</summary>
        internal void StopFromBubble()
        {
            if (session != null)
            {
                if (!session.Stopping) Stop();
            }
            else
            {
                if (incident != null) FinishIncident();
                stoppedForSession = true;
                ClearDiskError();
            }
            NotifyIfChanged();
        }

        internal void SetPaused(bool pause)
        {
            paused = pause;
            // The frame after coming back is huge and says nothing about the game.
            ResetBaseline();
            nextCapture = 0;
        }

        internal void Tick(float deltaTime)
        {
            CheckWorkerError();
            while (worker.TryTakeEncoded(out EncodedFrame frame)) Route(frame);

            if (AutoActive && settings.autoHitch && !suppressed && !paused) DetectHitch(deltaTime);
            else ResetBaseline();

            double now = Time.realtimeSinceStartupAsDouble;
            if (incident != null && (now > incident.End && Flushed(now - incident.End) || session != null || !AutoActive))
            {
                FinishIncident();
            }
            if (session != null && session.Stopping && Flushed(now - session.StopTime)) FinishSession();

            if (!AutoActive && session == null && buffer.Count > 0) buffer.Clear();
            if (ShouldCapture && !loopRunning && host != null && host.isActiveAndEnabled) host.StartCoroutine(CaptureLoop());
            if (!ShouldCapture && captureResources && capturer.InFlight == 0)
            {
                // Nothing is captured any more: give the textures and pixel buffers back.
                capturer.Release();
                captureResources = false;
            }

            NotifyIfChanged();
        }

        internal void Dispose()
        {
            if (incident != null) FinishIncident();
            if (session != null)
            {
                if (!session.Stopping) Stop();
                FinishSession();
            }
            worker.Dispose();
            capturer.Dispose();
        }

        // --- Capture -------------------------------------------------------------------------------

        private IEnumerator CaptureLoop()
        {
            loopRunning = true;
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (!ShouldCapture) break;
                if (paused) continue;

                double now = Time.realtimeSinceStartupAsDouble;
                if (now < nextCapture) continue;
                // Steady rate; when the game runs slower than the capture rate, one frame per game frame.
                nextCapture = Math.Max(nextCapture + 1.0 / CaptureModes.Fps(Mode), now);
                if (worker.PendingEncodes > MaxPendingEncodes) continue;

                captureResources = true;
                capturer.Capture(CaptureModes.Height(Mode), now, ++sequence, generation);
            }
            loopRunning = false;
        }

        private void OnFrameReady(CapturedFrame frame)
        {
            if (frame.Generation != generation)
            {
                capturer.Return(frame.Pixels);
                return;
            }
            worker.Encode(frame);
        }

        /// <summary>Main thread: each encoded frame goes to the session, or to the memory buffer (and the open incident).</summary>
        private void Route(EncodedFrame frame)
        {
            if (frame.Generation != generation) return;

            if (session != null)
            {
                worker.Write(FramePath(session.Folder, frame), frame.Jpg);
                session.Frames++;
                return;
            }

            buffer.Add(frame);
            int stale = 0;
            while (stale < buffer.Count && buffer[stale].Time < frame.Time - BufferSeconds) stale++;
            if (stale > 0) buffer.RemoveRange(0, stale);

            if (incident != null && frame.Sequence > incident.LastSequence && frame.Time >= incident.Start - BeforeSeconds && frame.Time <= incident.End)
            {
                WriteIncidentFrame(frame);
            }
        }

        /// <summary>True once no frame from before the deadline can still arrive, or the wait timed out.</summary>
        private bool Flushed(double sinceDeadline) =>
            (capturer.InFlight == 0 && worker.PendingEncodes == 0) || sinceDeadline > FlushTimeout;

        // --- Signals -------------------------------------------------------------------------------

        private void DetectHitch(float dt)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (baselineSince < 0) baselineSince = now;

            if (baseline.Count > 0 && now - baselineSince >= BaselineWarmup)
            {
                float average = (float)(baselineSum / baseline.Count);
                if (dt >= Mathf.Max(HitchFactor * average, HitchMinSeconds))
                {
                    // Hitch frames stay out of the average, so one long stall cannot raise the bar.
                    Signal("Hitch", $"frame {dt * 1000f:0} ms (average {average * 1000f:0.0} ms, {BugCatcher.Performance.CurrentFps} fps)");
                    return;
                }
            }

            baseline.Enqueue((now, dt));
            baselineSum += dt;
            while (baseline.Count > 0 && now - baseline.Peek().time > BaselineSeconds) baselineSum -= baseline.Dequeue().dt;
        }

        private void ResetBaseline()
        {
            baseline.Clear();
            baselineSum = 0;
            baselineSince = -1;
        }

        private void OnNewLogEntry(LogEntry entry)
        {
            if (entry.Kind != LogKind.Error) return;
            // "New" means new in this play session, even after the console was cleared.
            if (!seenErrors.Add(entry.Message + "\n" + entry.StackTrace)) return;
            if (!AutoActive || !settings.autoErrors) return;

            string detail = entry.Message;
            if (entry.StackTrace.Length > 0) detail += "\n" + entry.StackTrace.TrimEnd();
            Signal(LogStore.TypeLabel(entry.Type), detail);
        }

        /// <summary>Opens an incident, or extends the open one to 2 s after this signal.</summary>
        private void Signal(string reason, string detail)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (incident != null && now > incident.End) FinishIncident();
            if (incident == null) OpenIncident(now, reason);

            incident.End = now + AfterSeconds;
            if (!incident.Reasons.Contains(reason)) incident.Reasons.Add(reason);
            incident.Signals.Append('+').Append((now - incident.Start).ToString("0.000")).Append(" s  ").Append(reason).Append("  ")
                .AppendLine(detail.Replace("\n", "\n        "));
            NotifyIfChanged();
        }

        private void OpenIncident(double now, string reason)
        {
            DateTime startedAt = DateTime.Now;
            incident = new Incident
            {
                Folder = Join(Folder, Stamp(startedAt) + "_" + reason),
                Start = now,
                End = now + AfterSeconds,
                StartedAt = startedAt
            };
            worker.CreateDirectory(incident.Folder);
            foreach (EncodedFrame frame in buffer)
            {
                if (frame.Time >= now - BeforeSeconds) WriteIncidentFrame(frame);
            }
        }

        private void WriteIncidentFrame(EncodedFrame frame)
        {
            worker.Write(FramePath(incident.Folder, frame), frame.Jpg);
            incident.LastSequence = frame.Sequence;
            incident.Frames++;
        }

        private void FinishIncident()
        {
            Incident done = incident;
            incident = null;
            if (diskError) return;

            StringBuilder info = Header("BugCatcher · incident", done.StartedAt, CaptureModes.Label(settings.autoMode), done.Frames);
            info.AppendLine("Reasons: " + string.Join(", ", done.Reasons));
            info.AppendLine("Duration: " + (done.End - done.Start + BeforeSeconds).ToString("0.0") + " s (2 s before the first signal, 2 s after the last)");
            info.AppendLine().AppendLine("Signals:").Append(done.Signals);
            AppendDevice(info);
            worker.WriteText(Join(done.Folder, "info.txt"), info.ToString());
            Debug.Log("[BugCatcher] Saved " + done.Frames + " screenshots (" + string.Join(", ", done.Reasons) + ") to " + done.Folder);
        }

        private void FinishSession()
        {
            Session done = session;
            session = null;
            // Frames still on their way belong to the finished session: drop them.
            generation++;
            if (diskError) return;

            StringBuilder info = Header("BugCatcher · recording", done.StartedAt, CaptureModes.Label(done.Mode), done.Frames);
            info.AppendLine("Label: " + done.Label);
            info.AppendLine("Duration: " + done.Watch.Elapsed.TotalSeconds.ToString("0.0") + " s");
            AppendDevice(info);
            worker.WriteText(Join(done.Folder, "info.txt"), info.ToString());
            Debug.Log("[BugCatcher] Saved recording \"" + done.Label + "\" (" + done.Frames + " screenshots) to " + done.Folder);
        }

        // --- Disk errors ---------------------------------------------------------------------------

        private void CheckWorkerError()
        {
            string error = worker.Error;
            if (error == null || diskError) return;

            diskError = true;
            incident = null;
            if (session != null)
            {
                if (!session.Stopping) Stop();
                session = null;
            }
            generation++;
            buffer.Clear();
            Debug.LogWarning("[BugCatcher] Screenshots stopped: could not write to " + Folder + " (" + error + "). " +
                             "Free some space, then hold the recording bubble to dismiss.");
            NotifyIfChanged();
        }

        private void ClearDiskError()
        {
            diskError = false;
            worker.ClearError();
        }

        // --- Helpers -----------------------------------------------------------------------------

        private void NotifyIfChanged()
        {
            RecorderState state = State;
            if (state == lastState) return;
            lastState = state;
            StateChanged?.Invoke();
        }

        // Frame files sort by capture order.
        private static string FramePath(string folder, EncodedFrame frame) => Join(folder, frame.Sequence.ToString("D7") + ".jpg");

        // Forward slashes on every platform, so logged paths read the same everywhere.
        private static string Join(string folder, string name) => folder + "/" + name;

        private static string Stamp(DateTime time) => time.ToString("yyyyMMdd_HHmmss_fff");

        private static string SafeName(string text)
        {
            var sb = new StringBuilder(Math.Min(text.Length, 40));
            foreach (char c in text)
            {
                if (sb.Length == 40) break;
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }
            return sb.ToString();
        }

        private static StringBuilder Header(string title, DateTime time, string mode, int frames)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine("Time: " + time.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.AppendLine("Scene: " + SceneManager.GetActiveScene().name);
            sb.AppendLine("Capture: " + mode + ", " + frames + " frames");
            sb.AppendLine("FPS: " + BugCatcher.Performance.CurrentFps + " (average " + BugCatcher.Performance.AverageFps + ")");
            sb.AppendLine("RAM: " + BugCatcher.Performance.AllocatedMb.ToString("0") + " MB allocated / " + BugCatcher.Performance.ReservedMb.ToString("0") + " MB reserved");
            return sb;
        }

        private static void AppendDevice(StringBuilder sb) => sb.AppendLine().AppendLine("Device:").AppendLine(SystemInfoService.CopyText().TrimEnd());
    }
}
