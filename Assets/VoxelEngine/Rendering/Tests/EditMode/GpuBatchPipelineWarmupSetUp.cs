using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    /// <summary>
    /// Editor/Metal lazily compiles the VoxelBrickMesher batch kernels on their first successful
    /// dispatch. Persistent module validation must not charge that one-time editor compiler work
    /// against per-transaction five-second liveness assertions. Warm the exact production batch
    /// path once for this namespace, while still requiring the cold path itself to publish real
    /// geometry within a bounded setup interval. Player startup/performance acceptance remains in
    /// the standalone scenarios and is not inferred from this fixture.
    /// </summary>
    [SetUpFixture]
    public sealed class GpuBatchPipelineWarmupSetUp
    {
        private const double ColdCompilerWarmupSeconds = 30.0;
        private static readonly Type Coordinator = typeof(GpuSurfaceMirrorCoordinator);
        private static readonly BindingFlags InstanceFields = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags StaticFields = BindingFlags.NonPublic | BindingFlags.Static;

        [OneTimeSetUp]
        public void WarmProductionBatchPipelines()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback) return;

            GpuSurfaceExtractionContext context = null, keeper = null;
            GpuSurfacePageArena arena = null;
            ComputeShader arenaShader = null;
            try
            {
                // Match the production-path fixtures: two consumers share the mirror, then only
                // the publishing consumer is replaced with the exact step-four cache layout.
                context = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                keeper = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                Assert.NotNull(context);
                Assert.NotNull(keeper);
                typeof(GpuSurfaceExtractionContext).GetField("_hasStaged", InstanceFields).SetValue(context, true);
                typeof(GpuSurfaceExtractionContext).GetField("_hasStaged", InstanceFields).SetValue(keeper, true);

                arenaShader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
                arena = new GpuSurfacePageArena(arenaShader, 65536, 65536, 8);
                GpuSurfaceMirrorCoordinator.ConfigurePageArena(arena);

                const int step = 4;
                const int core = 8;
                int edge = core * step / 8 + 2;
                context.Dispose();
                context = GpuSurfaceExtractionContext.TryCreate(core, 2, 1024, edge);
                Assert.NotNull(context);

                using var storage = VoxelEngineBootstrap.CreateStorage(1, 1);
                storage.Residency.EnsureRegionResident(int3.zero);
                storage.Mutations.SetWholeBlock(int3.zero, 1, false);
                Assert.That(storage.Mutations.TryBeginPartialBlock(int3.zero, 2, false, out var mixed), Is.True);
                Assert.That(mixed.SetMaterial(0, 2), Is.True);
                storage.Mutations.CompletePartialBlock(ref mixed, true);
                storage.PublishAllResidentRegions();
                GpuSurfaceMirrorCoordinator.PrepareFrame(storage.Reads, storage.Changes, Time.frameCount, 1.0);

                int handle = GpuSurfaceMirrorCoordinator.PrepareChunkHandle(int3.zero, step, out ulong generation);
                Assert.That(handle, Is.GreaterThanOrEqualTo(0));
                var request = new GpuChunkExtraction(int3.zero, new int3(-1), step, 0.1f,
                    handle: handle, generation: generation);
                ulong world = GpuSurfaceMirrorCoordinator.RequestCoverage(
                    new int3(-1), edge, int3.zero, new int3(core * step));
                foreach (var pair in new[]
                {
                    ("_hasStaged", (object)true),
                    ("_coverageRequested", (object)true),
                    ("_staged", (object)request),
                    ("_coverageWorldEpoch", (object)world),
                    ("_coverageEpoch", (object)GpuSurfaceMirrorCoordinator.CoverageEpochFor(new int3(-1), edge)),
                })
                    typeof(GpuSurfaceExtractionContext).GetField(pair.Item1, InstanceFields).SetValue(context, pair.Item2);

                Assert.That(GpuSurfaceMirrorCoordinator.TryDispatchCountBatch(
                    context, 0, context.Extractor, context.Tables, request, Time.frameCount), Is.True);
                object lane = FirstLane();
                int frame = Time.frameCount;
                bool sawSummary = false, sawFull = false, completed = false;
                double started = Time.realtimeSinceStartupAsDouble;
                double deadline = started + ColdCompilerWarmupSeconds;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    Coordinator.GetField("s_LastExtractionDispatchFrame", StaticFields).SetValue(null, -1);
                    lane.GetType().GetField("_lastSummaryFrame", InstanceFields).SetValue(lane, -1);
                    GpuSurfaceMirrorCoordinator.PrepareFrame(storage.Reads, storage.Changes, ++frame, 1.0);

                    // This setup is intentionally synchronous. Drain only the readbacks issued by
                    // the real coordinator so their callbacks can advance the next production phase.
                    AsyncGPUReadback.WaitAllRequests();
                    sawSummary |= (bool)Get(lane, "SummarySubmitted");
                    sawFull |= (bool)Get(lane, "Submitted");
                    if (!context.TryTakePagedBatch(out _, out bool failed)) continue;

                    Assert.That(failed, Is.False, "Cold pipeline warm-up must publish the real candidate.");
                    var counters = new uint[GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords];
                    ((ComputeBuffer)Get(lane, "Counters")).GetData(counters, 0, 0, counters.Length);
                    Assert.That(counters[6], Is.GreaterThan(0), "Cold pipeline warm-up must produce real geometry.");
                    completed = true;
                    break;
                }

                Assert.That(sawSummary, Is.True, "Warm-up must execute real asynchronous source resolution.");
                Assert.That(sawFull, Is.True, "Warm-up must execute the real batch count/write/page chain.");
                Assert.That(completed, Is.True,
                    $"Cold Editor/Metal batch pipeline warm-up exceeded {ColdCompilerWarmupSeconds:0} seconds.");
                context.Release();
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.Zero);
                Debug.Log($"GPU BATCH PIPELINE WARMUP PASS elapsedMs={(Time.realtimeSinceStartupAsDouble - started) * 1000.0:0.000}");
            }
            finally
            {
                context?.Dispose();
                keeper?.Dispose();
                if (arena != null) GpuSurfaceMirrorCoordinator.DetachPageArena(arena, Time.frameCount);
                arena?.Dispose();
                if (arenaShader != null) UnityEngine.Object.DestroyImmediate(arenaShader);
                AsyncGPUReadback.WaitAllRequests();
            }
        }

        private static object Get(object target, string name) =>
            target.GetType().GetField(name, InstanceFields).GetValue(target);

        private static object FirstLane()
        {
            var lanes = (Array)Coordinator.GetField("s_CountBatchLanes", StaticFields).GetValue(null);
            foreach (object lane in lanes)
                if (lane != null && (int)Get(lane, "Count") > 0) return lane;
            throw new AssertionException("No queued warm-up lane remains.");
        }
    }
}
