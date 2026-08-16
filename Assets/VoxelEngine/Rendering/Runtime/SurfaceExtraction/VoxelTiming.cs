using System;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>A fixed-window timing snapshot. Values are wall-clock milliseconds.</summary>
    public readonly struct VoxelTimingSummary
    {
        public readonly ulong SampleCount;
        public readonly double LastMs;
        public readonly double P50Ms;
        public readonly double P95Ms;
        public readonly double P99Ms;
        public readonly double MaxMs;

        internal VoxelTimingSummary(ulong sampleCount, double lastMs, double p50Ms,
                                    double p95Ms, double p99Ms, double maxMs)
        {
            SampleCount = sampleCount;
            LastMs = lastMs;
            P50Ms = p50Ms;
            P95Ms = p95Ms;
            P99Ms = p99Ms;
            MaxMs = maxMs;
        }

        /// <summary>
        /// Conservative aggregation for independent worker windows. Counts are summed; timing
        /// values are the worst worker's statistic, not a fabricated global percentile.
        /// </summary>
        internal static VoxelTimingSummary WorstOf(in VoxelTimingSummary a,
                                                    in VoxelTimingSummary b) =>
            new(a.SampleCount + b.SampleCount,
                Math.Max(a.LastMs, b.LastMs), Math.Max(a.P50Ms, b.P50Ms),
                Math.Max(a.P95Ms, b.P95Ms), Math.Max(a.P99Ms, b.P99Ms),
                Math.Max(a.MaxMs, b.MaxMs));
    }

    /// <summary>
    /// Allocation-free rolling timing window. Sorting uses a permanently allocated scratch
    /// buffer and only occurs when a diagnostic snapshot is requested after a new sample.
    /// </summary>
    internal sealed class VoxelTimingWindow
    {
        private const int Capacity = 128;
        private readonly double[] _samples = new double[Capacity];
        private readonly double[] _scratch = new double[Capacity];
        private int _next;
        private int _count;
        private ulong _totalSamples;
        private bool _dirty;
        private VoxelTimingSummary _cached;

        public void Add(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds)) return;
            _samples[_next] = Math.Max(0.0, milliseconds);
            _next = (_next + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
            _totalSamples++;
            _dirty = true;
        }

        public VoxelTimingSummary Snapshot()
        {
            if (!_dirty) return _cached;
            Array.Copy(_samples, _scratch, _count);
            Array.Sort(_scratch, 0, _count);
            double last = _samples[(_next + Capacity - 1) % Capacity];
            double p50 = Percentile(0.50);
            double p95 = Percentile(0.95);
            double p99 = Percentile(0.99);
            double max = _count > 0 ? _scratch[_count - 1] : 0.0;
            _cached = new VoxelTimingSummary(_totalSamples, last, p50, p95, p99, max);
            _dirty = false;
            return _cached;
        }

        private double Percentile(double percentile)
        {
            if (_count == 0) return 0.0;
            int index = Math.Min(_count - 1,
                Math.Max(0, (int)Math.Ceiling(percentile * _count) - 1));
            return _scratch[index];
        }
    }
}
