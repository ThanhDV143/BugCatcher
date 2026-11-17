using UnityEngine;
using UnityEngine.Profiling;

namespace ThanhDV.BugCatcher
{
    /// <summary>Fixed-size ring of samples, oldest first when read by index.</summary>
    public class SampleHistory
    {
        private readonly float[] values;
        private int start;

        public SampleHistory(int capacity)
        {
            values = new float[capacity];
        }

        public int Count { get; private set; }
        public int Capacity => values.Length;
        public float this[int index] => values[(start + index) % values.Length];

        public void Push(float value)
        {
            if (Count < values.Length)
            {
                values[(start + Count) % values.Length] = value;
                Count++;
            }
            else
            {
                values[start] = value;
                start = (start + 1) % values.Length;
            }
        }

        public float Max()
        {
            float max = 0f;
            for (int i = 0; i < Count; i++) max = Mathf.Max(max, this[i]);
            return max;
        }
    }

    /// <summary>FPS and RAM, plus the short histories the HUD graphs draw.</summary>
    public class PerformanceService
    {
        public const int GoodFps = 50;
        public const int CautionFps = 30;
        private const float GraphInterval = 0.1f;
        private const int GraphSamples = 60;
        /// <summary>Matches the HUD and bubble text refresh, so every refresh shows a fresh window.</summary>
        private const float FpsWindow = 0.25f;
        private const int FrameTimeSamples = 120;
        /// <summary>Frames behind the average and the 1% / 0.1% lows.</summary>
        private const int StatSamples = 1024;
        private const float BytesPerMb = 1048576f;

        private readonly float[] statFrameTimes = new float[StatSamples];
        private readonly float[] slowest = new float[Mathf.Max(1, Mathf.RoundToInt(StatSamples * 0.01f))];
        private int statCount;
        private int statIndex;
        private double statTotal;
        private int lowsComputedAt = -1;
        private int onePercentFps;
        private int pointOnePercentFps;

        private float graphTimer;
        private float worstFpsInInterval = float.MaxValue;
        private int windowFrames;
        private float windowTime;
        private int windowFps;
        private int lastFrameFps;

        /// <summary>
        /// Frames per second over the last <see cref="FpsWindow"/>. A single frame's rate is snapped by vsync to
        /// 60/30/20/15; counting frames over a window gives the real rate (59, 47, 23…).
        /// </summary>
        public int CurrentFps => windowFps > 0 ? windowFps : lastFrameFps;
        public int AverageFps => ToFps(statCount / statTotal);
        public int OnePercentFps
        {
            get
            {
                ComputeLows();
                return onePercentFps;
            }
        }
        public int PointOnePercentFps
        {
            get
            {
                ComputeLows();
                return pointOnePercentFps;
            }
        }

        public float AllocatedMb => Profiler.GetTotalAllocatedMemoryLong() / BytesPerMb;
        public float ReservedMb => Profiler.GetTotalReservedMemoryLong() / BytesPerMb;
        public float MonoMb => Profiler.GetMonoUsedSizeLong() / BytesPerMb;
        public int DeviceMemoryMb => SystemInfo.systemMemorySize;

        public SampleHistory FpsHistory { get; } = new SampleHistory(GraphSamples);

        /// <summary>Duration of each of the last frames, in milliseconds, oldest first.</summary>
        public SampleHistory FrameTimeHistory { get; } = new SampleHistory(FrameTimeSamples);
        public SampleHistory AllocatedHistory { get; } = new SampleHistory(GraphSamples);
        public SampleHistory ReservedHistory { get; } = new SampleHistory(GraphSamples);
        public SampleHistory MonoHistory { get; } = new SampleHistory(GraphSamples);

        /// <summary>Increments every time the histories receive a new sample.</summary>
        public int HistoryVersion { get; private set; }

        /// <summary>0 = good, 1 = caution, 2 = critical.</summary>
        public static int Status(int value)
        {
            if (value >= GoodFps) return 0;
            return value >= CautionFps ? 1 : 2;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime > 0f && !float.IsInfinity(unscaledDeltaTime))
            {
                lastFrameFps = ToFps(1d / unscaledDeltaTime);
                statTotal += unscaledDeltaTime - statFrameTimes[statIndex];
                statFrameTimes[statIndex] = unscaledDeltaTime;
                statIndex = (statIndex + 1) % StatSamples;
                if (statCount < StatSamples) statCount++;

                worstFpsInInterval = Mathf.Min(worstFpsInInterval, 1f / unscaledDeltaTime);
                FrameTimeHistory.Push(unscaledDeltaTime * 1000f);
                windowFrames++;
                windowTime += unscaledDeltaTime;
                if (windowTime >= FpsWindow)
                {
                    windowFps = Mathf.RoundToInt(windowFrames / windowTime);
                    windowFrames = 0;
                    windowTime = 0f;
                }
            }

            graphTimer += unscaledDeltaTime;
            if (graphTimer < GraphInterval) return;

            graphTimer = 0f;
            // The worst frame of the interval, so single-frame hitches still show on the graph.
            FpsHistory.Push(worstFpsInInterval == float.MaxValue ? 0f : worstFpsInInterval);
            worstFpsInInterval = float.MaxValue;
            AllocatedHistory.Push(AllocatedMb);
            ReservedHistory.Push(ReservedMb);
            MonoHistory.Push(MonoMb);
            HistoryVersion++;
        }

        /// <summary>Average rate of the slowest 1% and 0.1% of the sampled frames, at most once per frame.</summary>
        private void ComputeLows()
        {
            if (lowsComputedAt == Time.frameCount) return;
            lowsComputedAt = Time.frameCount;

            int onePercent = Mathf.Max(1, Mathf.RoundToInt(statCount * 0.01f));
            int pointOnePercent = Mathf.Max(1, Mathf.RoundToInt(statCount * 0.001f));
            for (int i = 0; i < onePercent; i++) slowest[i] = 0f;

            // Keeps the slowest frames sorted, longest first.
            for (int i = 0; i < statCount; i++)
            {
                float sample = statFrameTimes[i];
                if (sample <= slowest[onePercent - 1]) continue;

                int j = onePercent - 1;
                for (; j > 0 && sample > slowest[j - 1]; j--) slowest[j] = slowest[j - 1];
                slowest[j] = sample;
            }

            double total = 0;
            for (int i = 0; i < onePercent; i++)
            {
                total += slowest[i];
                if (i == pointOnePercent - 1) pointOnePercentFps = ToFps(pointOnePercent / total);
            }
            onePercentFps = ToFps(onePercent / total);
        }

        private static int ToFps(double fps)
        {
            if (double.IsNaN(fps) || double.IsInfinity(fps) || fps <= 0) return 0;
            return fps >= short.MaxValue ? short.MaxValue : Mathf.RoundToInt((float)fps);
        }
    }
}
