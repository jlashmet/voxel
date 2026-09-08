using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuStepFourPublicationPhaseTests
    {
        private static readonly Type Coordinator = typeof(GpuSurfaceMirrorCoordinator);
        private static readonly BindingFlags InstanceFields = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags StaticFields = BindingFlags.NonPublic | BindingFlags.Static;

        private GpuSurfaceExtractionContext _context;
        private GpuSurfacePageArena _arena;
        private ComputeShader _arenaShader;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            Assert.That(SystemInfo.supportsAsyncGPUReadback, Is.True);
            _arenaShader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            _arena = new GpuSurfacePageArena(_arenaShader, 65536, 65536, 8);
            GpuSurfaceMirrorCoordinator.ConfigurePageArena(_arena);
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            GpuSurfaceMirrorCoordinator.DetachPageArena(_arena, Time.frameCount);
            _arena?.Dispose();
            if (_arenaShader != null) UnityEngine.Object.DestroyImmediate(_arenaShader);
        }

        [UnityTest]
        public IEnumerator StepFourMixedPublicationReportsItsTerminalPhaseWithinBound()
        {
            const int step = 4;
            const int core = 8;
            int edge = core * step / 8 + 2;
            _context = GpuSurfaceExtractionContext.TryCreate(core, 2, 1024, edge);
            Assert.NotNull(_context);

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
                typeof(GpuSurfaceExtractionContext).GetField(pair.Item1, InstanceFields).SetValue(_context, pair.Item2);

            Assert.That(GpuSurfaceMirrorCoordinator.TryDispatchCountBatch(
                _context, 0, _context.Extractor, _context.Tables, request, Time.frameCount), Is.True);
            object lane = FirstLane();
            int frame = Time.frameCount;
            bool sawSummarySubmission = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 5.0;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
                Coordinator.GetField("s_LastExtractionDispatchFrame", StaticFields).SetValue(null, -1);
                lane.GetType().GetField("_lastSummaryFrame", InstanceFields).SetValue(lane, -1);
                GpuSurfaceMirrorCoordinator.PrepareFrame(storage.Reads, storage.Changes, ++frame, 1.0);
                sawSummarySubmission |= (bool)Get(lane, "SummarySubmitted");
                if (!_context.TryTakePagedBatch(out _, out bool failed)) continue;

                Assert.That(failed, Is.False, "The unedited step-four mixed candidate must publish successfully.");
                var counters = new uint[GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords];
                ((ComputeBuffer)Get(lane, "Counters")).GetData(counters, 0, 0, counters.Length);
                Assert.That(counters[6], Is.GreaterThan(0), "The repro must produce real geometry.");
                _context.Release();
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.Zero);
                yield break;
            }

            bool fenceValid = (bool)Get(lane, "CompletionFenceValid");
            GraphicsFence fence = (GraphicsFence)Get(lane, "CompletionFence");
            string phase =
                $"step={step} sawSummary={sawSummarySubmission}"
                + $" preparing={Get(lane, "PreparingSummaries")}"
                + $" summarySubmitted={Get(lane, "SummarySubmitted")}"
                + $" summaryFailed={Get(lane, "SummaryFailed")}"
                + $" summaryRecord={Get(lane, "_summaryRecord")}"
                + $" summaryCursor={Get(lane, "_summaryCursor")}"
                + $" summaryCount={Get(lane, "_summaryCount")}"
                + $" recoveryWait={Get(lane, "_summaryWaitingForRecovery")}"
                + $" submitted={Get(lane, "Submitted")}"
                + $" outcomeReady={Get(lane, "OutcomeReady")}"
                + $" outcomeFailed={Get(lane, "OutcomeFailed")}"
                + $" fenceValid={fenceValid} fencePassed={(fenceValid && fence.passed)}"
                + $" readbacks={GpuSurfaceMirrorCoordinator.CountBatchReadbacks}"
                + $" arenaWaits={GpuSurfaceMirrorCoordinator.CountBatchArenaWaits}"
                + $" pending={GpuSurfaceMirrorCoordinator.PendingBlockCount}"
                + $" ready={GpuSurfaceMirrorCoordinator.ReadyBlockCount}"
                + $" mixed={GpuSurfaceMirrorCoordinator.ResidentMixedBrickCount}"
                + $" demand={GpuSurfaceMirrorCoordinator.DemandFootprintCount}"
                + $" readers={GpuSurfaceMirrorCoordinator.ActiveRegionCount}"
                + $" activeExtractions={GpuSurfaceMirrorCoordinator.ActiveExtractions}";
            Assert.Fail($"Step-four mixed publication exceeded the existing five-second bound. {phase}");
        }

        private static object Get(object target, string name) =>
            target.GetType().GetField(name, InstanceFields).GetValue(target);

        private static object FirstLane()
        {
            var lanes = (Array)Coordinator.GetField("s_CountBatchLanes", StaticFields).GetValue(null);
            foreach (object lane in lanes)
                if (lane != null && (int)Get(lane, "Count") > 0) return lane;
            throw new AssertionException("No queued lane remains.");
        }
    }
}
