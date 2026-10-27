using Tayx.Graphy.Fps;
using Tayx.Graphy.Ram;
using UnityEngine;

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

    /// <summary>FPS and RAM from Graphy's monitors, plus the short histories the HUD graphs draw.</summary>
    public class PerformanceService
    {
        public const int GoodFps = 50;
        public const int CautionFps = 30;
        private const float GraphInterval = 0.1f;
        private const int GraphSamples = 60;

        private readonly G_FpsMonitor fps;
        private readonly G_RamMonitor ram;
        private float graphTimer;
        private float worstFpsInInterval = float.MaxValue;

        public PerformanceService(GameObject host)
        {
            fps = host.AddComponent<G_FpsMonitor>();
            ram = host.AddComponent<G_RamMonitor>();
        }

        public int CurrentFps => fps.CurrentFPS;
        public int AverageFps => fps.AverageFPS;
        public int OnePercentFps => fps.OnePercentFPS;
        public int PointOnePercentFps => fps.Zero1PercentFps;

        public float AllocatedMb => ram.AllocatedRam;
        public float ReservedMb => ram.ReservedRam;
        public float MonoMb => ram.MonoRam;
        public int DeviceMemoryMb => SystemInfo.systemMemorySize;

        public SampleHistory FpsHistory { get; } = new SampleHistory(GraphSamples);
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
            if (unscaledDeltaTime > 0f) worstFpsInInterval = Mathf.Min(worstFpsInInterval, 1f / unscaledDeltaTime);

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
    }
}
