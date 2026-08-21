using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Read-only renderer measurements exposed to application diagnostics without handing scene
    /// code a Runtime scheduler, render pass, GPU buffer, or mutable renderer bridge.
    /// </summary>
    public readonly struct SolidRenderBenchmarkSnapshot
    {
        public readonly ulong SampleCount;
        public readonly double StagingP50Ms;
        public readonly double StagingP95Ms;
        public readonly double StagingP99Ms;
        public readonly double StagingMaxMs;
        public readonly double SubmissionP50Ms;
        public readonly double SubmissionP95Ms;
        public readonly double SubmissionP99Ms;
        public readonly double SubmissionMaxMs;
        public readonly double VisibilityP50Ms;
        public readonly double VisibilityP95Ms;
        public readonly double VisibilityP99Ms;
        public readonly double VisibilityMaxMs;
        public readonly int LastVisibleSolidCount;
        public readonly int LastUnitySubmissionCalls;
        public readonly double MeanVisibleSolidCount;
        public readonly double MeanUnitySubmissionCalls;

        internal SolidRenderBenchmarkSnapshot(
            ulong sampleCount,
            double stagingP50Ms, double stagingP95Ms, double stagingP99Ms, double stagingMaxMs,
            double submissionP50Ms, double submissionP95Ms, double submissionP99Ms,
            double submissionMaxMs,
            double visibilityP50Ms, double visibilityP95Ms, double visibilityP99Ms,
            double visibilityMaxMs,
            int lastVisibleSolidCount, int lastUnitySubmissionCalls,
            double meanVisibleSolidCount, double meanUnitySubmissionCalls)
        {
            SampleCount = sampleCount;
            StagingP50Ms = stagingP50Ms;
            StagingP95Ms = stagingP95Ms;
            StagingP99Ms = stagingP99Ms;
            StagingMaxMs = stagingMaxMs;
            SubmissionP50Ms = submissionP50Ms;
            SubmissionP95Ms = submissionP95Ms;
            SubmissionP99Ms = submissionP99Ms;
            SubmissionMaxMs = submissionMaxMs;
            VisibilityP50Ms = visibilityP50Ms;
            VisibilityP95Ms = visibilityP95Ms;
            VisibilityP99Ms = visibilityP99Ms;
            VisibilityMaxMs = visibilityMaxMs;
            LastVisibleSolidCount = lastVisibleSolidCount;
            LastUnitySubmissionCalls = lastUnitySubmissionCalls;
            MeanVisibleSolidCount = meanVisibleSolidCount;
            MeanUnitySubmissionCalls = meanUnitySubmissionCalls;
        }
    }

    /// <summary>Allocation-free convergence state sampled by the stationary player benchmark.</summary>
    public readonly struct SurfaceBenchmarkState
    {
        public readonly int VisibleSolidChunks;
        public readonly int MissingVisibleSolidChunks;
        public readonly int RunningSolidJobs;
        public readonly int SolidMeshesAwaitingUpload;

        internal SurfaceBenchmarkState(int visibleSolidChunks, int missingVisibleSolidChunks,
                                       int runningSolidJobs, int solidMeshesAwaitingUpload)
        {
            VisibleSolidChunks = visibleSolidChunks;
            MissingVisibleSolidChunks = missingVisibleSolidChunks;
            RunningSolidJobs = runningSolidJobs;
            SolidMeshesAwaitingUpload = solidMeshesAwaitingUpload;
        }

        public bool IsConverged => VisibleSolidChunks > 0
            && MissingVisibleSolidChunks == 0
            && RunningSolidJobs == 0
            && SolidMeshesAwaitingUpload == 0;
    }

    public static class RenderingDiagnosticsComposition
    {
        /// <summary>
        /// Starts a fresh solid staging/submission window. Scheduler visibility is already a
        /// fixed 128-frame rolling window; after 128 stationary frames its contents are entirely
        /// inside the benchmark interval as well.
        /// </summary>
        public static void ResetSolidRenderBenchmark() => VoxelSolidRenderTelemetry.Reset();

        /// <summary>
        /// Snapshot after the measurement interval. This is intentionally not a per-frame API:
        /// the fixed timing windows sort their preallocated scratch buffers when read.
        /// </summary>
        public static SolidRenderBenchmarkSnapshot GetSolidRenderBenchmark()
        {
            VoxelSolidRenderDiagnostics solid = VoxelSolidRenderTelemetry.Snapshot;
            var visibility = VoxelRenderBridge.SurfaceMetrics.VisibilityTiming;
            return new SolidRenderBenchmarkSnapshot(
                solid.SampleCount,
                solid.StagingTiming.P50Ms, solid.StagingTiming.P95Ms,
                solid.StagingTiming.P99Ms, solid.StagingTiming.MaxMs,
                solid.SubmissionTiming.P50Ms, solid.SubmissionTiming.P95Ms,
                solid.SubmissionTiming.P99Ms, solid.SubmissionTiming.MaxMs,
                visibility.P50Ms, visibility.P95Ms, visibility.P99Ms, visibility.MaxMs,
                solid.LastVisibleSolidCount, solid.LastUnitySubmissionCalls,
                solid.MeanVisibleSolidCount, solid.MeanUnitySubmissionCalls);
        }

        public static SurfaceBenchmarkState GetSurfaceBenchmarkState()
        {
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            return new SurfaceBenchmarkState(
                metrics.VisibleSolidChunks,
                metrics.MissingVisibleSolidChunks,
                metrics.RunningSolidJobs,
                metrics.SolidMeshesAwaitingUpload);
        }
    }
}
