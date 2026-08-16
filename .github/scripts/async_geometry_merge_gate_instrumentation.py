from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one match, found {count}\n--- needle ---\n{old}")
    p.write_text(text.replace(old, new, 1))


# Allocation-free P99 over the same fixed rolling window used by the existing timing summaries.
replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelTiming.cs",
    """        public readonly double P95Ms;\n        public readonly double MaxMs;\n\n        internal VoxelTimingSummary(ulong sampleCount, double lastMs, double p50Ms,\n                                    double p95Ms, double maxMs)\n""",
    """        public readonly double P95Ms;\n        public readonly double P99Ms;\n        public readonly double MaxMs;\n\n        internal VoxelTimingSummary(ulong sampleCount, double lastMs, double p50Ms,\n                                    double p95Ms, double p99Ms, double maxMs)\n""",
)
replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelTiming.cs",
    """            P95Ms = p95Ms;\n            MaxMs = maxMs;\n""",
    """            P95Ms = p95Ms;\n            P99Ms = p99Ms;\n            MaxMs = maxMs;\n""",
)
replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelTiming.cs",
    """            new(a.SampleCount + b.SampleCount,\n                Math.Max(a.LastMs, b.LastMs), Math.Max(a.P50Ms, b.P50Ms),\n                Math.Max(a.P95Ms, b.P95Ms), Math.Max(a.MaxMs, b.MaxMs));\n""",
    """            new(a.SampleCount + b.SampleCount,\n                Math.Max(a.LastMs, b.LastMs), Math.Max(a.P50Ms, b.P50Ms),\n                Math.Max(a.P95Ms, b.P95Ms), Math.Max(a.P99Ms, b.P99Ms),\n                Math.Max(a.MaxMs, b.MaxMs));\n""",
)
replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelTiming.cs",
    """            double p95 = Percentile(0.95);\n            double max = _count > 0 ? _scratch[_count - 1] : 0.0;\n            _cached = new VoxelTimingSummary(_totalSamples, last, p50, p95, max);\n""",
    """            double p95 = Percentile(0.95);\n            double p99 = Percentile(0.99);\n            double max = _count > 0 ? _scratch[_count - 1] : 0.0;\n            _cached = new VoxelTimingSummary(_totalSamples, last, p50, p95, p99, max);\n""",
)

# Shared fail-safe acknowledgement. Teardown may still call JobHandle.Complete directly; frame
# paths use this helper only after observing IsCompleted. If a future call site violates that
# convention it refuses to wait and records the attempted blocking completion.
guard = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/GeometryFrameJobCompletionGuard.cs")
if guard.exists():
    raise SystemExit(f"{guard}: already exists")
guard.write_text("""using Unity.Jobs;\n\nnamespace VoxelEngine.Rendering.Runtime.SurfaceExtraction\n{\n    /// <summary>\n    /// Frame-path synchronization acknowledgement. Geometry code must never wait for worker\n    /// execution: callers poll IsCompleted and use this only once ready. The defensive check\n    /// makes an accidental premature acknowledgement observable and non-blocking.\n    /// </summary>\n    internal static class GeometryFrameJobCompletionGuard\n    {\n        internal static bool TryCompleteReady(JobHandle handle, ref ulong violationCount)\n        {\n            if (!handle.IsCompleted)\n            {\n                violationCount++;\n                return false;\n            }\n\n            handle.Complete();\n            return true;\n        }\n    }\n}\n""")
Path(str(guard) + ".meta").write_text("fileFormatVersion: 2\nguid: a0b86ce6f8c64285ab7dc33aa3d374f1\n")

solid = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs"
replace_once(
    solid,
    """        public ulong CapacityPressureCount { get; private set; }\n        public int RunningJobCount => _exactMetadataJobScheduled || _exactClassificationJobScheduled\n""",
    """        public ulong CapacityPressureCount { get; private set; }\n        private ulong _framePathBlockingCompletionViolations;\n        public ulong FramePathBlockingCompletionViolations => _framePathBlockingCompletionViolations;\n        public int RunningJobCount => _exactMetadataJobScheduled || _exactClassificationJobScheduled\n""",
)
# Every non-teardown solid completion already has an IsCompleted gate. Keep those gates and route
# the acknowledgement through the fail-safe helper.
replace_once(solid, "                _exactMetadataJobHandle.Complete();\n", """                if (!GeometryFrameJobCompletionGuard.TryCompleteReady(\n                        _exactMetadataJobHandle, ref _framePathBlockingCompletionViolations))\n                {\n                    AccumulateSnapshotSlice(sliceStart, completed: false);\n                    return false;\n                }\n""")
replace_once(solid, "            _exactClassificationJobHandle.Complete();\n", """            if (!GeometryFrameJobCompletionGuard.TryCompleteReady(\n                    _exactClassificationJobHandle, ref _framePathBlockingCompletionViolations))\n            {\n                AccumulateSnapshotSlice(sliceStart, completed: false);\n                return false;\n            }\n""")
replace_once(solid, """                    _topologyCompactJobHandle.Complete();\n                    _facetedMergeJobHandle.Complete();\n""", """                    if (!GeometryFrameJobCompletionGuard.TryCompleteReady(\n                            _topologyCompactJobHandle, ref _framePathBlockingCompletionViolations)\n                        || !GeometryFrameJobCompletionGuard.TryCompleteReady(\n                            _facetedMergeJobHandle, ref _framePathBlockingCompletionViolations))\n                        break;\n""")
replace_once(solid, """                    _facetedMergeJobHandle.Complete();\n                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n""", """                    if (!GeometryFrameJobCompletionGuard.TryCompleteReady(\n                            _facetedMergeJobHandle, ref _framePathBlockingCompletionViolations))\n                        break;\n                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n""")
replace_once(solid, """                _transitionJobHandle.Complete();\n                _transitionJobScheduled = false;\n                _transitionResultPending = true;\n""", """                if (!GeometryFrameJobCompletionGuard.TryCompleteReady(\n                        _transitionJobHandle, ref _framePathBlockingCompletionViolations))\n                    return false;\n                _transitionJobScheduled = false;\n                _transitionResultPending = true;\n""")

water = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuWaterSurfaceChunkCache.cs"
replace_once(
    water,
    """        public ulong StaleBuildCount { get; private set; }\n        public ulong UploadedGeometryBytes { get; private set; }\n""",
    """        public ulong StaleBuildCount { get; private set; }\n        public ulong UploadedGeometryBytes { get; private set; }\n        private ulong _framePathBlockingCompletionViolations;\n        public ulong FramePathBlockingCompletionViolations => _framePathBlockingCompletionViolations;\n        public int RunningJobCount => _waterMeshJobScheduled ? 1 : 0;\n""",
)
replace_once(water, """                _waterMeshJobHandle.Complete();\n                _waterMeshJobScheduled = false;\n                _waterBatchCount = 0;\n""", """                if (!GeometryFrameJobCompletionGuard.TryCompleteReady(\n                        _waterMeshJobHandle, ref _framePathBlockingCompletionViolations))\n                    return false;\n                _waterMeshJobScheduled = false;\n                _waterBatchCount = 0;\n""")
replace_once(water, """                if (_waterMeshJobScheduled)\n                {\n                    _waterMeshJobHandle.Complete();\n                    _waterMeshJobScheduled = false;\n                }\n""", """                if (_waterMeshJobScheduled)\n                {\n                    if (!GeometryFrameJobCompletionGuard.TryCompleteReady(\n                            _waterMeshJobHandle, ref _framePathBlockingCompletionViolations))\n                    {\n                        _discardBuildAfterMeshJob = true;\n                        return;\n                    }\n                    _waterMeshJobScheduled = false;\n                }\n""")

scheduler = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs"
replace_once(
    scheduler,
    """        public readonly int RunningSolidJobs;\n        public readonly int SolidMeshesAwaitingUpload;\n""",
    """        public readonly int RunningSolidJobs;\n        public readonly int RunningGeometryJobs;\n        public readonly ulong FramePathBlockingCompletionViolations;\n        public readonly int SolidMeshesAwaitingUpload;\n""",
)
replace_once(
    scheduler,
    """            RunningSolidJobs = solids.RunningJobCount;\n            SolidMeshesAwaitingUpload = solids.PendingUploadCount;\n""",
    """            RunningSolidJobs = solids.RunningJobCount;\n            RunningGeometryJobs = solids.RunningJobCount + water.RunningJobCount;\n            FramePathBlockingCompletionViolations =\n                solids.FramePathBlockingCompletionViolations\n                + water.FramePathBlockingCompletionViolations;\n            SolidMeshesAwaitingUpload = solids.PendingUploadCount;\n""",
)
replace_once(
    scheduler,
    """                                     in VoxelTimingSummary workerPrepare,\n                                     in VoxelTimingSummary visibility)\n""",
    """                                     in VoxelTimingSummary workerPrepare,\n                                     in VoxelTimingSummary visibility,\n                                     int schedulerRunningJobs,\n                                     ulong schedulerCompletionViolations)\n""",
)
replace_once(
    scheduler,
    """            ulong completed = 0, stale = 0, uploadedBytes = water.UploadedGeometryBytes;\n            ulong decorations = 0, pressure = 0;\n""",
    """            ulong completed = 0, stale = 0, uploadedBytes = water.UploadedGeometryBytes;\n            ulong decorations = 0, pressure = 0;\n            ulong completionViolations = water.FramePathBlockingCompletionViolations;\n""",
)
replace_once(
    scheduler,
    """                pressure += worker.CapacityPressureCount;\n                geometryBytes += worker.ResidentGpuBytes;\n""",
    """                pressure += worker.CapacityPressureCount;\n                completionViolations += worker.FramePathBlockingCompletionViolations;\n                geometryBytes += worker.ResidentGpuBytes;\n""",
)
replace_once(
    scheduler,
    """            RunningSolidJobs = running;\n            SolidMeshesAwaitingUpload = uploads;\n""",
    """            RunningSolidJobs = running;\n            RunningGeometryJobs = running + water.RunningJobCount + schedulerRunningJobs;\n            FramePathBlockingCompletionViolations =\n                completionViolations + schedulerCompletionViolations;\n            SolidMeshesAwaitingUpload = uploads;\n""",
)
replace_once(
    scheduler,
    """        private readonly VoxelTimingWindow _visibilityTiming = new();\n""",
    """        private readonly VoxelTimingWindow _visibilityTiming = new();\n        private ulong _framePathBlockingCompletionViolations;\n""",
)
replace_once(
    scheduler,
    """            _invalidationTiming.Snapshot(), _discoveryTiming.Snapshot(),\n            _workerPrepareTiming.Snapshot(), _visibilityTiming.Snapshot());\n""",
    """            _invalidationTiming.Snapshot(), _discoveryTiming.Snapshot(),\n            _workerPrepareTiming.Snapshot(), _visibilityTiming.Snapshot(),\n            _surfaceDiscoveryJobScheduled ? 1 : 0,\n            _framePathBlockingCompletionViolations);\n""",
)
replace_once(
    scheduler,
    """                    if (!_surfaceDiscoveryJobHandle.IsCompleted)\n                        return;\n\n                    // IsCompleted guarantees this Complete is a synchronization acknowledgement,\n                    // not a frame stall waiting for worker execution.\n                    _surfaceDiscoveryJobHandle.Complete();\n                    _surfaceDiscoveryJobScheduled = false;\n""",
    """                    if (!_surfaceDiscoveryJobHandle.IsCompleted)\n                        return;\n\n                    // This is an acknowledgement only. The shared guard refuses to wait if a\n                    // future refactor accidentally reaches it before the worker is complete.\n                    if (!GeometryFrameJobCompletionGuard.TryCompleteReady(\n                            _surfaceDiscoveryJobHandle,\n                            ref _framePathBlockingCompletionViolations))\n                        return;\n                    _surfaceDiscoveryJobScheduled = false;\n""",
)

# Stress gate: verify the global upload cap, nonblocking contract and a meaningful P99 orchestration
# ceiling during combined camera movement + destruction. 12 ms leaves headroom inside a 60 Hz frame
# while this test intentionally raises the upload wall-clock budget to 5 ms.
stress = "Assets/Tests/PlayMode/AsyncGeometryStressTests.cs"
replace_once(
    stress,
    """        private const string ScenePath = \"Assets/Scenes/VoxelShowcase.unity\";\n""",
    """        private const string ScenePath = \"Assets/Scenes/VoxelShowcase.unity\";\n        private const double MaxGeometryOrchestrationP99Ms = 12.0;\n""",
)
replace_once(
    stress,
    """            bool sawPendingReplacement = false;\n            int maxVisible = 0;\n""",
    """            bool sawPendingReplacement = false;\n            int maxVisible = 0;\n            double peakSchedulerP99Ms = 0.0;\n""",
)
replace_once(
    stress,
    """                    Assert.GreaterOrEqual(metrics.LastFrameSolidUploadedBytes, 0);\n                    maxVisible = Mathf.Max(maxVisible, metrics.VisibleSolidChunks);\n""",
    """                    Assert.GreaterOrEqual(metrics.LastFrameSolidUploadedBytes, 0);\n                    Assert.AreEqual(0UL, metrics.FramePathBlockingCompletionViolations,\n                        \"A geometry frame path attempted to wait for an unfinished JobHandle.\");\n                    peakSchedulerP99Ms = Math.Max(peakSchedulerP99Ms,\n                                                   metrics.SchedulerPrepareTiming.P99Ms);\n                    maxVisible = Mathf.Max(maxVisible, metrics.VisibleSolidChunks);\n""",
)
# Add System for Math.
replace_once(stress, "using System.Collections;\n", "using System;\nusing System.Collections;\n")
replace_once(
    stress,
    """                Assert.True(sawPendingReplacement,\n                    \"A 16 KiB frame cap should produce queued replacement geometry under stress.\");\n""",
    """                Assert.True(sawPendingReplacement,\n                    \"A 16 KiB frame cap should produce queued replacement geometry under stress.\");\n                Assert.Greater(VoxelRenderBridge.SurfaceMetrics.SchedulerPrepareTiming.SampleCount, 0UL,\n                    \"Scheduler timing instrumentation recorded no stressed frames.\");\n                Assert.LessOrEqual(peakSchedulerP99Ms, MaxGeometryOrchestrationP99Ms,\n                    $\"Geometry scheduler P99 {peakSchedulerP99Ms:F3} ms exceeded the {MaxGeometryOrchestrationP99Ms:F1} ms stress gate.\");\n""",
)

# Static regression: direct Complete calls are allowed only in explicit teardown sections; all frame
# path acknowledgements must route through the fail-safe helper.
arch = "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs"
p = Path(arch)
text = p.read_text()
needle = "\n        [Test]\n        public void SolidVisibilityTraversesBoundedClipmapCoordinatesOncePerRing()"
if text.count(needle) != 1:
    raise SystemExit(f"{arch}: could not locate insertion point")
insert = """
        [Test]
        public void FramePathJobCompletionIsNonBlockingAndObservable()
        {
            string solid = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string water = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuWaterSurfaceChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            string guard = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GeometryFrameJobCompletionGuard.cs"));
            string timing = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelTiming.cs"));

            StringAssert.Contains("if (!handle.IsCompleted)", guard);
            StringAssert.Contains("violationCount++", guard);
            StringAssert.Contains("handle.Complete()", guard);
            StringAssert.Contains("P99Ms", timing);
            StringAssert.Contains("FramePathBlockingCompletionViolations", scheduler);
            StringAssert.Contains("RunningGeometryJobs", scheduler);
            StringAssert.Contains("GeometryFrameJobCompletionGuard.TryCompleteReady", solid);
            StringAssert.Contains("GeometryFrameJobCompletionGuard.TryCompleteReady", water);
            StringAssert.Contains("GeometryFrameJobCompletionGuard.TryCompleteReady", scheduler);

            int solidTeardown = solid.IndexOf("private void CompleteJobs()", StringComparison.Ordinal);
            int waterTeardown = water.IndexOf("public void Dispose()", StringComparison.Ordinal);
            int schedulerTeardown = scheduler.IndexOf("public void Dispose()", StringComparison.Ordinal);
            Assert.Greater(solidTeardown, 0);
            Assert.Greater(waterTeardown, 0);
            Assert.Greater(schedulerTeardown, 0);
            StringAssert.DoesNotContain(".Complete();", solid.Substring(0, solidTeardown));
            StringAssert.DoesNotContain(".Complete();", water.Substring(0, waterTeardown));
            StringAssert.DoesNotContain(".Complete();", scheduler.Substring(0, schedulerTeardown));
        }
"""
p.write_text(text.replace(needle, insert + needle, 1))

print("async geometry merge-gate instrumentation applied")
