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
    /// Allocation-free rolling timing window. Maintain exact sample order incrementally so
    /// per-frame diagnostic snapshots do not repeatedly sort the entire window.
    /// </summary>
    internal sealed class VoxelTimingWindow
    {
        private const int Capacity = 128;
        private readonly double[] _samples = new double[Capacity];
        private readonly double[] _sorted = new double[Capacity];
        private int _next;
        private int _count;
        private ulong _totalSamples;
        private bool _dirty;
        private VoxelTimingSummary _cached;

        public void Add(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds)) return;
            milliseconds = Math.Max(0.0, milliseconds);
            if (_count == Capacity)
            {
                int removed = LowerBound(_samples[_next], _count);
                Array.Copy(_sorted, removed + 1, _sorted, removed, _count - removed - 1);
                _count--;
            }
            int inserted = LowerBound(milliseconds, _count);
            Array.Copy(_sorted, inserted, _sorted, inserted + 1, _count - inserted);
            _sorted[inserted] = milliseconds;
            _samples[_next] = milliseconds;
            _next = (_next + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
            _totalSamples++;
            _dirty = true;
        }

        public VoxelTimingSummary Snapshot()
        {
            if (!_dirty) return _cached;
            double last = _samples[(_next + Capacity - 1) % Capacity];
            double p50 = Percentile(0.50);
            double p95 = Percentile(0.95);
            double p99 = Percentile(0.99);
            double max = _count > 0 ? _sorted[_count - 1] : 0.0;
            _cached = new VoxelTimingSummary(_totalSamples, last, p50, p95, p99, max);
            _dirty = false;
            return _cached;
        }

        private int LowerBound(double value, int count)
        {
            int low = 0;
            int high = count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (_sorted[middle] < value) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        private double Percentile(double percentile)
        {
            if (_count == 0) return 0.0;
            int index = Math.Min(_count - 1,
                Math.Max(0, (int)Math.Ceiling(percentile * _count) - 1));
            return _sorted[index];
        }
    }
}
