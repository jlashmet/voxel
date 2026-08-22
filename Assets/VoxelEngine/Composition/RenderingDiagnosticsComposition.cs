using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

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
    /// Rolling worker-prepare phase timings for the real-player harness. Only primitive values
    /// cross the Composition boundary; renderer timing-window types remain private to Runtime.
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
            double workerP95Ms, double workerP99Ms, double workerMaxMs,
            double ruleSyncP95Ms, double ruleSyncP99Ms, double ruleSyncMaxMs,
            double residencyP95Ms, double residencyP99Ms, double residencyMaxMs,
            double capacityP95Ms, double capacityP99Ms, double capacityMaxMs,
            double selectionP95Ms, double selectionP99Ms, double selectionMaxMs,
            double snapshotP95Ms, double snapshotP99Ms, double snapshotMaxMs,
            double compactP95Ms, double compactP99Ms, double compactMaxMs,
            double facetedMergeP95Ms, double facetedMergeP99Ms, double facetedMergeMaxMs,
            double profileP95Ms, double profileP99Ms, double profileMaxMs,
            double uploadP95Ms, double uploadP99Ms, double uploadMaxMs)
        {
            WorkerP95Ms = workerP95Ms;
            WorkerP99Ms = workerP99Ms;
            WorkerMaxMs = workerMaxMs;
            RuleSyncP95Ms = ruleSyncP95Ms;
            RuleSyncP99Ms = ruleSyncP99Ms;
            RuleSyncMaxMs = ruleSyncMaxMs;
            ResidencyP95Ms = residencyP95Ms;
            ResidencyP99Ms = residencyP99Ms;
            ResidencyMaxMs = residencyMaxMs;
            CapacityP95Ms = capacityP95Ms;
            CapacityP99Ms = capacityP99Ms;
            CapacityMaxMs = capacityMaxMs;
            SelectionP95Ms = selectionP95Ms;
            SelectionP99Ms = selectionP99Ms;
            SelectionMaxMs = selectionMaxMs;
            SnapshotP95Ms = snapshotP95Ms;
            SnapshotP99Ms = snapshotP99Ms;
            SnapshotMaxMs = snapshotMaxMs;
            CompactP95Ms = compactP95Ms;
            CompactP99Ms = compactP99Ms;
            CompactMaxMs = compactMaxMs;
            FacetedMergeP95Ms = facetedMergeP95Ms;
            FacetedMergeP99Ms = facetedMergeP99Ms;
            FacetedMergeMaxMs = facetedMergeMaxMs;
            ProfileP95Ms = profileP95Ms;
            ProfileP99Ms = profileP99Ms;
            ProfileMaxMs = profileMaxMs;
            UploadP95Ms = uploadP95Ms;
            UploadP99Ms = uploadP99Ms;
            UploadMaxMs = uploadMaxMs;
        }
    }

    /// <summary>
    /// Current-frame solid arena write telemetry copied across the Composition boundary as
    /// primitives so showcase diagnostics do not depend on renderer Runtime namespaces.
    /// </summary>
    public readonly struct SurfaceArenaUploadFrameSnapshot
    {
        public readonly int Frame;
        public readonly double WallMs;
        public readonly int Calls;
        public readonly long Bytes;

        internal SurfaceArenaUploadFrameSnapshot(int frame, double wallMs, int calls, long bytes)
        {
            Frame = frame;
            WallMs = wallMs;
            Calls = calls;
            Bytes = bytes;
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
        /// Returns existing fixed-window timing summaries for worker admission and its instrumented
        /// sub-phases. Intended for sparse benchmark logging rather than per-frame gameplay reads.
        /// </summary>
        public static SurfacePrepareTimingSnapshot GetSurfacePrepareTiming()
        {
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            var worker = metrics.WorkerPrepareTiming;
            var rule = metrics.RuleSyncTiming;
            var residency = metrics.ResidencyPruneTiming;
            var capacity = metrics.CapacityTiming;
            var selection = metrics.BuildSelectionTiming;
            var snapshot = metrics.SnapshotTiming;
            var compact = metrics.TopologyCompactTiming;
            var facetedMerge = metrics.FacetedMergeTiming;
            var profile = metrics.ProfileEmitTiming;
            var upload = metrics.UploadTiming;
            return new SurfacePrepareTimingSnapshot(
                worker.P95Ms, worker.P99Ms, worker.MaxMs,
                rule.P95Ms, rule.P99Ms, rule.MaxMs,
                residency.P95Ms, residency.P99Ms, residency.MaxMs,
                capacity.P95Ms, capacity.P99Ms, capacity.MaxMs,
                selection.P95Ms, selection.P99Ms, selection.MaxMs,
                snapshot.P95Ms, snapshot.P99Ms, snapshot.MaxMs,
                compact.P95Ms, compact.P99Ms, compact.MaxMs,
                facetedMerge.P95Ms, facetedMerge.P99Ms, facetedMerge.MaxMs,
                profile.P95Ms, profile.P99Ms, profile.MaxMs,
                upload.P95Ms, upload.P99Ms, upload.MaxMs);
        }

        public static SurfaceArenaUploadFrameSnapshot GetSurfaceArenaUploadFrame()
        {
            var upload = SurfaceGeometryUploadTelemetry.Snapshot;
            return new SurfaceArenaUploadFrameSnapshot(
                upload.Frame, upload.WallMs, upload.Calls, upload.Bytes);
        }
    }
}