using System;
using System.Diagnostics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// Allocation-free rolling diagnostics for CPU work immediately around solid rendering.
    /// Values describe command encoding on the CPU, not GPU execution time.
    /// </summary>
    public readonly struct VoxelSolidRenderDiagnostics
    {
        public readonly ulong SampleCount;
        public readonly VoxelTimingSummary StagingTiming;
        public readonly VoxelTimingSummary SubmissionTiming;
        public readonly int LastVisibleSolidCount;
        public readonly int LastUnitySubmissionCalls;
        public readonly double MeanVisibleSolidCount;
        public readonly double MeanUnitySubmissionCalls;

        internal VoxelSolidRenderDiagnostics(
            ulong sampleCount,
            in VoxelTimingSummary stagingTiming,
            in VoxelTimingSummary submissionTiming,
            int lastVisibleSolidCount,
            int lastUnitySubmissionCalls,
            double meanVisibleSolidCount,
            double meanUnitySubmissionCalls)
        {
            SampleCount = sampleCount;
            StagingTiming = stagingTiming;
            SubmissionTiming = submissionTiming;
            LastVisibleSolidCount = lastVisibleSolidCount;
            LastUnitySubmissionCalls = lastUnitySubmissionCalls;
            MeanVisibleSolidCount = meanVisibleSolidCount;
            MeanUnitySubmissionCalls = meanUnitySubmissionCalls;
        }
    }

    /// <summary>
    /// Diagnostics-only observation point for the production solid draw path. Recording is fixed
    /// cost and allocation-free; Snapshot sorts only the fixed 128-sample timing scratch arrays.
    /// </summary>
    public static class VoxelSolidRenderTelemetry
    {
        private sealed class Accumulator
        {
            internal readonly VoxelTimingWindow StagingTiming = new();
            internal readonly VoxelTimingWindow SubmissionTiming = new();
            internal ulong SampleCount;
            internal ulong VisibleSolidTotal;
            internal ulong SubmissionCallTotal;
            internal int LastVisibleSolidCount;
            internal int LastUnitySubmissionCalls;
        }

        private static readonly double s_TimestampToMilliseconds = 1000.0 / Stopwatch.Frequency;
        private static Accumulator s_Accumulator = new();

        internal static long Timestamp() => Stopwatch.GetTimestamp();

        internal static double ElapsedMilliseconds(long startTimestamp) =>
            (Stopwatch.GetTimestamp() - startTimestamp) * s_TimestampToMilliseconds;

        internal static void Record(double stagingMs, double submissionMs,
                                    int visibleSolidCount, int submissionCalls)
        {
            Accumulator accumulator = s_Accumulator;
            accumulator.StagingTiming.Add(stagingMs);
            accumulator.SubmissionTiming.Add(submissionMs);
            accumulator.SampleCount++;
            accumulator.LastVisibleSolidCount = Math.Max(0, visibleSolidCount);
            accumulator.LastUnitySubmissionCalls = Math.Max(0, submissionCalls);
            accumulator.VisibleSolidTotal += (ulong)accumulator.LastVisibleSolidCount;
            accumulator.SubmissionCallTotal += (ulong)accumulator.LastUnitySubmissionCalls;
        }

        public static VoxelSolidRenderDiagnostics Snapshot
        {
            get
            {
                Accumulator accumulator = s_Accumulator;
                double divisor = accumulator.SampleCount > 0 ? accumulator.SampleCount : 1.0;
                VoxelTimingSummary staging = accumulator.StagingTiming.Snapshot();
                VoxelTimingSummary submission = accumulator.SubmissionTiming.Snapshot();
                return new VoxelSolidRenderDiagnostics(
                    accumulator.SampleCount, in staging, in submission,
                    accumulator.LastVisibleSolidCount, accumulator.LastUnitySubmissionCalls,
                    accumulator.VisibleSolidTotal / divisor,
                    accumulator.SubmissionCallTotal / divisor);
            }
        }

        public static void Reset() => s_Accumulator = new Accumulator();
    }
}
