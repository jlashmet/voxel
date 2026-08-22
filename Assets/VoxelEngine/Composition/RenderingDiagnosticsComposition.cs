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
        public readonly double SchedulerPrepareP50Ms;
        public readonly double SchedulerPrepareP95Ms;
        public readonly double SchedulerPrepareP99Ms;
        public readonly double SchedulerPrepareMaxMs;
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
            double schedulerPrepareP50Ms, double schedulerPrepareP95Ms,
            double schedulerPrepareP99Ms, double schedulerPrepareMaxMs,
            double stagingP50Ms, double stagingP95Ms, double stagingP99Ms, double stagingMaxMs,
            double submissionP50Ms, double submissionP95Ms, double submissionP99Ms,
            double submissionMaxMs,
            double visibilityP50Ms, double visibilityP95Ms, double visibilityP99Ms,
            double visibilityMaxMs,
            int lastVisibleSolidCount, int lastUnitySubmissionCalls,
            double meanVisibleSolidCount, double meanUnitySubmissionCalls)
        {
            SampleCount = sampleCount;
            SchedulerPrepareP50Ms = schedulerPrepareP50Ms;
            SchedulerPrepareP95Ms = schedulerPrepareP95Ms;
            SchedulerPrepareP99Ms = schedulerPrepareP99Ms;
            SchedulerPrepareMaxMs = schedulerPrepareMaxMs;
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

        /// <summary>
        /// Convergence for a fixed visible view. Background prefetch jobs are intentionally not
        /// part of this predicate: the renderer's visibility-reuse contract allows them to run
        /// behind the camera as long as every in-frustum chunk is already published. If a
        /// background publication changes the drawn set, the visibility timing in the benchmark
        /// will expose that by leaving the cheap reuse path.
        /// </summary>
        public bool IsConverged => VisibleSolidChunks > 0 && MissingVisibleSolidChunks == 0;
    }

    /// <summary>
    /// Rolling worker-prepare phase timings for the real-player harness. These are the renderer's
    /// existing fixed-window measurements, surfaced as primitives so scene diagnostics never take
    /// a dependency on Rendering.Runtime timing types.
    /// </summary>
    public readonly struct SurfacePrepareTimingSnapshot
    {
        public readonly double WorkerP95Ms;
        public readonly double WorkerP99Ms;
        public readonly double WorkerMaxMs;
        public readonly double RuleSyncP95Ms;
        public readonly double RuleSyncP99Ms;
        public readonly double RuleSyncMaxMs;
        public readonly double ResidencyP95Ms;
        public readonly double ResidencyP99Ms;
        public readonly double ResidencyMaxMs;
        public readonly double CapacityP95Ms;
        public readonly double CapacityP99Ms;
        public readonly double CapacityMaxMs;
        public readonly double SelectionP95Ms;
        public readonly double SelectionP99Ms;
        public readonly double SelectionMaxMs;
        public readonly double SnapshotP95Ms;
        public readonly double SnapshotP99Ms;
        public readonly double SnapshotMaxMs;
        public readonly double CompactP95Ms;
        public readonly double CompactP99Ms;
        public readonly double CompactMaxMs;
        public readonly double FacetedMergeP95Ms;
        public readonly double FacetedMergeP99Ms;
        public readonly double FacetedMergeMaxMs;
        public readonly double ProfileP95Ms;
        public readonly double ProfileP99Ms;
        public readonly double ProfileMaxMs;
        public readonly double UploadP95Ms;
        public readonly double UploadP99Ms;
        public readonly double UploadMaxMs;

        internal SurfacePrepareTimingSnapshot(
            VoxelTimingSummary worker,
            VoxelTimingSummary ruleSync,
            VoxelTimingSummary residency,
            VoxelTimingSummary capacity,
            VoxelTimingSummary selection,
            VoxelTimingSummary snapshot,
            VoxelTimingSummary compact,
            VoxelTimingSummary facetedMerge,
            VoxelTimingSummary profile,
            VoxelTimingSummary upload)
        {
            WorkerP95Ms = worker.P95Ms;
            WorkerP99Ms = worker.P99Ms;
            WorkerMaxMs = worker.MaxMs;
            RuleSyncP95Ms = ruleSync.P95Ms;
            RuleSyncP99Ms = ruleSync.P99Ms;
            RuleSyncMaxMs = ruleSync.MaxMs;
            ResidencyP95Ms = residency.P95Ms;
            ResidencyP99Ms = residency.P99Ms;
            ResidencyMaxMs = residency.MaxMs;
            CapacityP95Ms = capacity.P95Ms;
            CapacityP99Ms = capacity.P99Ms;
            CapacityMaxMs = capacity.MaxMs;
            SelectionP95Ms = selection.P95Ms;
            SelectionP99Ms = selection.P99Ms;
            SelectionMaxMs = selection.MaxMs;
            SnapshotP95Ms = snapshot.P95Ms;
            SnapshotP99Ms = snapshot.P99Ms;
            SnapshotMaxMs = snapshot.MaxMs;
            CompactP95Ms = compact.P95Ms;
            CompactP99Ms = compact.P99Ms;
            CompactMaxMs = compact.MaxMs;
            FacetedMergeP95Ms = facetedMerge.P95Ms;
            FacetedMergeP99Ms = facetedMerge.P99Ms;
            FacetedMergeMaxMs = facetedMerge.MaxMs;
            ProfileP95Ms = profile.P95Ms;
            ProfileP99Ms = profile.P99Ms;
            ProfileMaxMs = profile.MaxMs;
            UploadP95Ms = upload.P95Ms;
            UploadP99Ms = upload.P99Ms;
            UploadMaxMs = upload.MaxMs;
        }
    }

    public static class RenderingDiagnosticsComposition
    {
        /// <summary>
        /// Starts a fresh solid staging/submission window. Scheduler prepare/visibility are fixed
        /// 128-frame rolling windows; after 128 stationary frames their contents are entirely
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
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            var prepare = metrics.SchedulerPrepareTiming;
            var visibility = metrics.VisibilityTiming;
            return new SolidRenderBenchmarkSnapshot(
                solid.SampleCount,
                prepare.P50Ms, prepare.P95Ms, prepare.P99Ms, prepare.MaxMs,
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

        /// <summary>
        /// Returns the existing fixed-window timing summaries for worker admission and its
        /// instrumented sub-phases. Intended for low-frequency benchmark logging, not per-frame
        /// gameplay sampling, because snapshotting timing windows sorts preallocated scratch data.
        /// </summary>
        public static SurfacePrepareTimingSnapshot GetSurfacePrepareTiming()
        {
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            return new SurfacePrepareTimingSnapshot(
                metrics.WorkerPrepareTiming,
                metrics.RuleSyncTiming,
                metrics.ResidencyPruneTiming,
                metrics.CapacityTiming,
                metrics.BuildSelectionTiming,
                metrics.SnapshotTiming,
                metrics.TopologyCompactTiming,
                metrics.FacetedMergeTiming,
                metrics.ProfileEmitTiming,
                metrics.UploadTiming);
        }
    }
}