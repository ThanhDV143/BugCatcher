using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FMODUnity;
using UnityEngine;

namespace ThanhDV.BugCatcher.Fmod
{
    /// <summary>
    /// Master-output spectrum and levels from FMOD, plus DSP CPU, live event instances and per-bus meters.
    /// Compiled only when the scripting define BUGCATCHER_FMOD is set. Written against FMOD for Unity 2.02.
    /// </summary>
    public class FmodAudioProvider : IAudioProvider
    {
        private const int FftWindow = 1024;
        private const float ExtrasInterval = 0.5f;

        private readonly AudioExtras extras = new AudioExtras { Title = "FMOD" };
        private readonly List<FMOD.Studio.Bus> buses = new List<FMOD.Studio.Bus>();
        private readonly List<string> busNames = new List<string>();
        private float[] spectrum = new float[FftWindow / 2];
        private float[] fftBuffer = new float[FftWindow];
        private FMOD.DSP fft;
        private FMOD.DSP masterHead;
        private FMOD.ChannelGroup master;
        private bool attached;
        private float extrasTimer = ExtrasInterval;
        private float sampleRate = 48000f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => BugCatcher.RegisterAudioProvider(new FmodAudioProvider());

        public string Id => "fmod";
        public string DisplayName => "FMOD";
        public string ShortName => "FMOD";
        public string SpectrumCaption => "master · FFT " + FftWindow;
        public bool IsAvailable => RuntimeManager.IsInitialized;
        public float[] Spectrum => spectrum;
        public float SampleRate => sampleRate;
        public float RmsDb { get; private set; } = float.NegativeInfinity;
        public float PeakDb { get; private set; } = float.NegativeInfinity;
        public AudioExtras Extras => extras;

        public void SetActive(bool active)
        {
            if (active) Attach();
            else Detach();
        }

        private void Attach()
        {
            if (attached || !RuntimeManager.IsInitialized) return;

            FMOD.System core = RuntimeManager.CoreSystem;
            if (core.getMasterChannelGroup(out master) != FMOD.RESULT.OK) return;
            if (core.createDSPByType(FMOD.DSP_TYPE.FFT, out fft) != FMOD.RESULT.OK) return;

            fft.setParameterInt((int)FMOD.DSP_FFT.WINDOWSIZE, FftWindow);
            master.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, fft);
            if (master.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, out masterHead) == FMOD.RESULT.OK) masterHead.setMeteringEnabled(false, true);

            if (core.getSoftwareFormat(out int rate, out _, out _) == FMOD.RESULT.OK) sampleRate = rate;
            if (core.getVersion(out uint version) == FMOD.RESULT.OK)
            {
                extras.Version = "Studio " + ((version >> 16) & 0xFFFF).ToString("X") + "." + ((version >> 8) & 0xFF).ToString("X2");
            }
            attached = true;
            extrasTimer = ExtrasInterval;
        }

        private void Detach()
        {
            if (!attached) return;

            attached = false;
            if (master.hasHandle() && fft.hasHandle()) master.removeDSP(fft);
            if (fft.hasHandle()) fft.release();
            fft.clearHandle();
            Array.Clear(spectrum, 0, spectrum.Length);
            RmsDb = PeakDb = float.NegativeInfinity;
        }

        public void Update()
        {
            if (!attached)
            {
                Attach();
                if (!attached) return;
            }

            ReadSpectrum();
            ReadMaster();

            extrasTimer += Time.unscaledDeltaTime;
            if (extrasTimer < ExtrasInterval) return;
            extrasTimer = 0f;
            ReadExtras();
        }

        private void ReadSpectrum()
        {
            if (fft.getParameterData((int)FMOD.DSP_FFT.SPECTRUMDATA, out IntPtr data, out _) != FMOD.RESULT.OK || data == IntPtr.Zero) return;

            var parameter = (FMOD.DSP_PARAMETER_FFT)Marshal.PtrToStructure(data, typeof(FMOD.DSP_PARAMETER_FFT));
            if (parameter.numchannels <= 0 || parameter.length <= 0) return;

            if (fftBuffer.Length != parameter.length) fftBuffer = new float[parameter.length];
            parameter.getSpectrum(0, ref fftBuffer);

            // The second half mirrors the first: keep 0 Hz .. Nyquist.
            int half = parameter.length / 2;
            if (spectrum.Length != half) spectrum = new float[half];
            Array.Copy(fftBuffer, spectrum, half);
        }

        private void ReadMaster()
        {
            if (!masterHead.hasHandle()) return;
            if (!Meter(masterHead, out float rms, out float peak)) return;

            RmsDb = rms;
            PeakDb = peak;
        }

        private static bool Meter(FMOD.DSP dsp, out float rmsDb, out float peakDb)
        {
            rmsDb = peakDb = float.NegativeInfinity;
            if (dsp.getMeteringInfo(IntPtr.Zero, out FMOD.DSP_METERING_INFO info) != FMOD.RESULT.OK || info.numchannels <= 0) return false;

            float sum = 0f;
            float peak = 0f;
            for (int c = 0; c < info.numchannels; c++)
            {
                sum += info.rmslevel[c] * info.rmslevel[c];
                peak = Mathf.Max(peak, info.peaklevel[c]);
            }

            rmsDb = AudioService.ToDb(Mathf.Sqrt(sum / info.numchannels));
            peakDb = AudioService.ToDb(peak);
            return true;
        }

        private void ReadExtras()
        {
            FMOD.System core = RuntimeManager.CoreSystem;
            if (core.getCPUUsage(out FMOD.CPU_USAGE usage) == FMOD.RESULT.OK) extras.DspCpuPercent = usage.dsp;

            FMOD.Studio.System studio = RuntimeManager.StudioSystem;
            int instances = 0;
            buses.Clear();
            busNames.Clear();
            if (studio.getBankList(out FMOD.Studio.Bank[] banks) == FMOD.RESULT.OK)
            {
                foreach (FMOD.Studio.Bank bank in banks)
                {
                    if (bank.getEventList(out FMOD.Studio.EventDescription[] events) == FMOD.RESULT.OK)
                    {
                        foreach (FMOD.Studio.EventDescription description in events)
                        {
                            if (description.getInstanceCount(out int count) == FMOD.RESULT.OK) instances += count;
                        }
                    }

                    if (bank.getBusList(out FMOD.Studio.Bus[] bankBuses) != FMOD.RESULT.OK) continue;
                    foreach (FMOD.Studio.Bus bus in bankBuses)
                    {
                        if (bus.getPath(out string path) != FMOD.RESULT.OK || busNames.Contains(path)) continue;
                        busNames.Add(path);
                        buses.Add(bus);
                    }
                }
            }
            extras.Instances = instances;

            extras.Buses.Clear();
            for (int i = 0; i < buses.Count; i++)
            {
                var level = new AudioBusLevel { Name = BusName(busNames[i]), LevelDb = float.NegativeInfinity, PeakDb = float.NegativeInfinity };
                // A bus has a channel group only while something plays through it; otherwise it is silent.
                if (buses[i].getChannelGroup(out FMOD.ChannelGroup group) == FMOD.RESULT.OK &&
                    group.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, out FMOD.DSP head) == FMOD.RESULT.OK)
                {
                    head.setMeteringEnabled(false, true);
                    Meter(head, out level.LevelDb, out level.PeakDb);
                }

                // Master first, matching the design.
                if (level.Name == "Master") extras.Buses.Insert(0, level);
                else extras.Buses.Add(level);
            }
        }

        /// <summary>"bus:/" → "Master", "bus:/SFX/UI" → "UI".</summary>
        private static string BusName(string path)
        {
            if (path == "bus:/" || path == "bus:") return "Master";
            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }
    }
}
