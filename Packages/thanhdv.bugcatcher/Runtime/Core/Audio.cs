using System.Collections.Generic;
using UnityEngine;

namespace ThanhDV.BugCatcher
{
    public struct AudioBusLevel
    {
        public string Name;
        /// <summary>RMS level in dB; float.NegativeInfinity when the bus is silent or inactive.</summary>
        public float LevelDb;
        public float PeakDb;
    }

    /// <summary>Engine-specific numbers shown under the spectrum (FMOD: CPU, instances, buses).</summary>
    public class AudioExtras
    {
        public string Title;
        public string Version;
        public float DspCpuPercent;
        public int Instances;
        public readonly List<AudioBusLevel> Buses = new List<AudioBusLevel>();
    }

    /// <summary>A source of master-output audio data. Implemented for Unity Audio and FMOD.</summary>
    public interface IAudioProvider
    {
        string Id { get; }
        /// <summary>Shown on the source switch and HUD header, e.g. "Unity Audio", "FMOD".</summary>
        string DisplayName { get; }
        /// <summary>Short label of the HUD header, e.g. "UNITY", "FMOD".</summary>
        string ShortName { get; }
        /// <summary>Right-hand caption of the spectrum card, e.g. "master · FFT 1024".</summary>
        string SpectrumCaption { get; }
        bool IsAvailable { get; }
        /// <summary>Linear magnitudes from 0 Hz to Nyquist.</summary>
        float[] Spectrum { get; }
        float SampleRate { get; }
        float RmsDb { get; }
        float PeakDb { get; }
        /// <summary>Null when the provider has nothing beyond spectrum and levels.</summary>
        AudioExtras Extras { get; }

        /// <summary>Called when the provider starts or stops being displayed; use it to add or remove DSPs.</summary>
        void SetActive(bool active);

        /// <summary>Called once per frame while active.</summary>
        void Update();
    }

    /// <summary>Reads Unity's mixer output through <see cref="AudioListener"/>.</summary>
    public class UnityAudioProvider : IAudioProvider
    {
        private const int FftSize = 1024;
        private readonly float[] spectrum = new float[FftSize];
        private readonly float[] samples = new float[FftSize];

        public string Id => "unity";
        public string DisplayName => "Unity Audio";
        public string ShortName => "UNITY";
        public string SpectrumCaption => "master · FFT " + FftSize;
        public bool IsAvailable => AudioSettings.GetConfiguration().sampleRate > 0;
        public float[] Spectrum => spectrum;
        public float SampleRate => AudioSettings.GetConfiguration().sampleRate;
        public float RmsDb { get; private set; } = float.NegativeInfinity;
        public float PeakDb { get; private set; } = float.NegativeInfinity;
        public AudioExtras Extras => null;

        public void SetActive(bool active)
        {
            if (active) return;

            System.Array.Clear(spectrum, 0, spectrum.Length);
            RmsDb = PeakDb = float.NegativeInfinity;
        }

        public void Update()
        {
            AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            AudioListener.GetOutputData(samples, 0);

            float sum = 0f;
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i];
                sum += s * s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }

            RmsDb = AudioService.ToDb(Mathf.Sqrt(sum / samples.Length));
            PeakDb = AudioService.ToDb(peak);
        }
    }

    /// <summary>Picks the active provider, runs it only while something is on screen, and derives display values.</summary>
    public class AudioService
    {
        public const float MinDb = -60f;
        private const float PeakHoldSeconds = 2f;

        private readonly List<IAudioProvider> providers = new List<IAudioProvider>();
        private readonly HashSet<object> viewers = new HashSet<object>();
        private readonly BugCatcherSettings settings;
        private IAudioProvider active;
        private bool running;
        private float heldPeakDb = float.NegativeInfinity;
        private float heldPeakAge;

        public AudioService(BugCatcherSettings settings)
        {
            this.settings = settings;
            Register(new UnityAudioProvider());
        }

        public IReadOnlyList<IAudioProvider> Providers => providers;

        public IAudioProvider Current
        {
            get
            {
                foreach (IAudioProvider provider in providers)
                {
                    if (provider.Id == settings.audioProvider && provider.IsAvailable) return provider;
                }

                // Default to the last registered available provider (FMOD over Unity Audio).
                for (int i = providers.Count - 1; i >= 0; i--)
                {
                    if (providers[i].IsAvailable) return providers[i];
                }
                return providers[0];
            }
        }

        /// <summary>Peak level held for 2 s.</summary>
        public float HeldPeakDb => heldPeakDb;

        public void Register(IAudioProvider provider)
        {
            if (providers.Exists(p => p.Id == provider.Id)) return;
            providers.Add(provider);
        }

        public void Select(string id)
        {
            settings.audioProvider = id;
            settings.Changed();
        }

        /// <summary>Views call this while they display audio; providers run only while at least one viewer is present.</summary>
        public void SetViewing(object viewer, bool viewing)
        {
            if (viewing) viewers.Add(viewer);
            else viewers.Remove(viewer);
        }

        public void Tick(float deltaTime)
        {
            IAudioProvider current = viewers.Count > 0 ? Current : null;
            // Current falls back to an unavailable provider when none is available; never read from it.
            if (current != null && !current.IsAvailable) current = null;
            if (current != active || (current != null) != running)
            {
                active?.SetActive(false);
                active = current;
                running = active != null;
                active?.SetActive(true);
                heldPeakDb = float.NegativeInfinity;
            }

            if (!running) return;

            active.Update();
            heldPeakAge += deltaTime;
            if (active.PeakDb >= heldPeakDb || heldPeakAge > PeakHoldSeconds)
            {
                heldPeakDb = active.PeakDb;
                heldPeakAge = 0f;
            }
        }

        /// <summary>
        /// Fills <paramref name="bandsDb"/> with log-spaced bands between <paramref name="minHz"/> and
        /// <paramref name="maxHz"/>; each band is the loudest bin it covers, in dB.
        /// </summary>
        public void FillBands(float[] bandsDb, float minHz, float maxHz)
        {
            IAudioProvider provider = active;
            float[] spectrum = provider?.Spectrum;
            if (spectrum == null || spectrum.Length == 0 || provider.SampleRate <= 0f)
            {
                for (int i = 0; i < bandsDb.Length; i++) bandsDb[i] = float.NegativeInfinity;
                return;
            }

            float binHz = provider.SampleRate * 0.5f / spectrum.Length;
            float ratio = Mathf.Log(maxHz / minHz);
            for (int band = 0; band < bandsDb.Length; band++)
            {
                float f0 = minHz * Mathf.Exp(ratio * band / bandsDb.Length);
                float f1 = minHz * Mathf.Exp(ratio * (band + 1) / bandsDb.Length);
                int b0 = Mathf.Clamp(Mathf.FloorToInt(f0 / binHz), 0, spectrum.Length - 1);
                int b1 = Mathf.Clamp(Mathf.CeilToInt(f1 / binHz), b0 + 1, spectrum.Length);
                float max = 0f;
                for (int b = b0; b < b1; b++) max = Mathf.Max(max, spectrum[b]);
                bandsDb[band] = ToDb(max * 2f);
            }
        }

        public static float ToDb(float linear) => linear > 1e-7f ? 20f * Mathf.Log10(linear) : float.NegativeInfinity;

        /// <summary>Maps dB to 0..1 over [<see cref="MinDb"/>, 0].</summary>
        public static float Normalize(float db, float minDb = MinDb) => float.IsNegativeInfinity(db) ? 0f : Mathf.Clamp01((db - minDb) / -minDb);
    }
}
