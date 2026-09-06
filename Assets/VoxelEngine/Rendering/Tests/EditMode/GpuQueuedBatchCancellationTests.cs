using System;
using System.Collections;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuQueuedBatchCancellationTests
    {
        private static readonly Type Coordinator = typeof(GpuSurfaceMirrorCoordinator);
        private static readonly BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;
        private GpuSurfaceExtractionContext _first, _second;
        private GpuSurfacePageArena _arena;
        private ComputeShader _arenaShader;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            _first = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
            _second = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
            Assert.NotNull(_first);
            Assert.NotNull(_second);
            // This fixture isolates the host queue between admission and GPU submission. No
            // voxel geometry is evaluated or used as visual evidence; resources are real.
            typeof(GpuSurfaceExtractionContext).GetField("_hasStaged", Fields).SetValue(_first, true);
            typeof(GpuSurfaceExtractionContext).GetField("_hasStaged", Fields).SetValue(_second, true);
            _arenaShader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            _arena = new GpuSurfacePageArena(_arenaShader, 65536, 65536, 8);
            GpuSurfaceMirrorCoordinator.ConfigurePageArena(_arena);
            // Use the production dispatch budget to keep a full lane queued deterministically.
            Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, Time.frameCount);
        }

        [TearDown]
        public void TearDown()
        {
            _first?.Dispose();
            _second?.Dispose();
            GpuSurfaceMirrorCoordinator.DetachPageArena(_arena, Time.frameCount);
            _arena?.Dispose();
            if (_arenaShader != null) UnityEngine.Object.DestroyImmediate(_arenaShader);
        }

        [Test]
        public void ReleasedSoleRecordCannotLeaveADispatchableLane()
        {
            Queue(_first, 1);
            Assert.That(QueuedCount(), Is.EqualTo(1));
            _first.Release();
            Assert.That(QueuedCount(), Is.Zero,
                "Release must revoke the queued descriptor before relinquishing its resources.");
        }

        [Test]
        public void DisposedPrefixIsReplacedByTheSurvivingRecordOwner()
        {
            Queue(_first, 1);
            Queue(_second, 2);
            Assert.That(QueuedCount(), Is.EqualTo(2));
            _first.Dispose();
            Assert.That(QueuedCount(), Is.EqualTo(1));
            object lane = FirstLane();
            Assert.That(Get(lane, "PrefixExtractor"), Is.SameAs(_second.Extractor));
            Assert.That(Get(lane, "Tables"), Is.SameAs(_second.Tables));
            var requests = (GpuChunkExtraction[])Get(lane, "Requests");
            Assert.That(requests[0].ChunkOriginVoxel.x, Is.EqualTo(2));
            _second.Release();
            Assert.That(QueuedCount(), Is.Zero);
        }

        [Test]
        public void ReleasedRequestCannotReenterTheQueue()
        {
            Queue(_first, 1);
            _first.Release();
            Assert.That(GpuSurfaceMirrorCoordinator.TryDispatchCountBatch(_first, 0,
                _first.Extractor, _first.Tables,
                new GpuChunkExtraction(int3.zero, int3.zero, 1, 0.1f), Time.frameCount), Is.False);
            Assert.That(QueuedCount(), Is.Zero);
        }

        [Test]
        public void RepeatedCancellationPreservesTheOtherOwner()
        {
            Queue(_first, 1);
            Queue(_second, 2);
            _second.Release();
            _second.Release();
            Assert.That(QueuedCount(), Is.EqualTo(1));
            Assert.That(Get(FirstLane(), "PrefixExtractor"), Is.SameAs(_first.Extractor));
        }

        [Test]
        public void SubmissionPrunesAStaleTokenWithoutDispatchingIt()
        {
            Queue(_first, 1);
            object lane = FirstLane();
            // Independently invalidate identity to exercise the submission guard as well as
            // the explicit Release hook. The descriptor must never reach a GPU dispatch.
            typeof(GpuSurfaceExtractionContext).GetField("_countBatchToken", Fields).SetValue(_first, 1u);
            Coordinator.GetMethod("SealCountBatch", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new[] { lane });
            Assert.That(QueuedCount(), Is.Zero);
            Assert.That(Get(lane, "Submitted"), Is.False);
        }

        [Test]
        public void OldContextCleanupCannotRemoveNewWorldCoverageOrReaders()
        {
            int edge = _first.BrickCacheEdge;
            Assert.That(GpuSurfaceMirrorCoordinator.TryBeginExtraction(int3.zero, edge, out ulong oldEpoch), Is.True);
            ulong demandEpoch = GpuSurfaceMirrorCoordinator.RequestCoverage(int3.zero, edge, int3.zero, new int3(8));
            typeof(GpuSurfaceExtractionContext).GetField("_sharedExtractionActive", Fields).SetValue(_first, true);
            typeof(GpuSurfaceExtractionContext).GetField("_extractionWorldEpoch", Fields).SetValue(_first, oldEpoch);
            typeof(GpuSurfaceExtractionContext).GetField("_coverageRequested", Fields).SetValue(_first, true);
            typeof(GpuSurfaceExtractionContext).GetField("_coverageWorldEpoch", Fields).SetValue(_first, demandEpoch);
            Coordinator.GetMethod("ResetWorld", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { false });
            Assert.That(GpuSurfaceMirrorCoordinator.TryBeginExtraction(int3.zero, edge, out ulong newEpoch), Is.True);
            ulong newDemand = GpuSurfaceMirrorCoordinator.RequestCoverage(int3.zero, edge, int3.zero, new int3(8));
            try
            {
                Assert.That(_first.IsCurrentBatchRequest(0), Is.False);
                _first.Release();
                Assert.That(GpuSurfaceMirrorCoordinator.ActiveExtractions, Is.EqualTo(1));
                Assert.That(GpuSurfaceMirrorCoordinator.ActiveRegionCount, Is.GreaterThan(0));
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(1));
            }
            finally
            {
                GpuSurfaceMirrorCoordinator.EndExtraction(int3.zero, edge, newEpoch);
                GpuSurfaceMirrorCoordinator.ReleaseCoverage(int3.zero, edge, int3.zero, new int3(8), newDemand);
            }
        }

        [Test]
        public void HistoryInvalidationRejectsQueuedWorkAndSignalsRetry()
        {
            ulong epoch = GpuSurfaceMirrorCoordinator.RequestCoverage(int3.zero, _first.BrickCacheEdge, int3.zero, new int3(8));
            typeof(GpuSurfaceExtractionContext).GetField("_coverageRequested", Fields).SetValue(_first, true);
            typeof(GpuSurfaceExtractionContext).GetField("_coverageWorldEpoch", Fields).SetValue(_first, epoch);
            typeof(GpuSurfaceExtractionContext).GetField("_coverageEpoch", Fields)
                .SetValue(_first, GpuSurfaceMirrorCoordinator.CoverageEpoch);
            Queue(_first, 1);
            object lane = FirstLane();
            Coordinator.GetMethod("InvalidateAll", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 1UL });
            Coordinator.GetMethod("SealCountBatch", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { lane });
            Assert.That(QueuedCount(), Is.Zero);
            Assert.That(_first.TryTakePagedBatch(out _, out bool failed), Is.True,
                "Invalidation must wake the worker rather than leave it waiting for an impossible callback.");
            Assert.That(failed, Is.True);
        }

        [UnityTest]
        public IEnumerator WorldReplacementDuringARealSubmissionWaitsForTheCompletionCallback()
        {
            var mirror = _first.Mirror;
            uint[] empty = GpuMirrorClearLifetimeTests.ReadDirectory(mirror);
            Assert.That(mirror.Publish(VoxelBrickDelta.UniformAt(int3.zero, 1, 1),
                default, default, default, 0, false), Is.EqualTo(GpuBrickPublish.MetadataOnly));
            uint[] occupied = GpuMirrorClearLifetimeTests.ReadDirectory(mirror);
            Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, Time.frameCount);
            Queue(_first, 1); Queue(_second, 2);
            object lane = FirstLane();
            Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, -1);
            Coordinator.GetMethod("SealCountBatch", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { lane });
            Assert.That(Get(lane, "Submitted"), Is.True);
            using var replacement = VoxelEngineBootstrap.CreateStorage(1, 1);
            GpuSurfaceMirrorCoordinator.PrepareFrame(replacement.Reads, replacement.Changes, Time.frameCount, 1.0);
            Assert.That(mirror.IsClearPending, Is.True);
            Assert.That(GpuSurfaceMirrorCoordinator.TryBeginExtraction(int3.zero, _first.BrickCacheEdge, out _), Is.False);
            CollectionAssert.AreEqual(occupied, GpuMirrorClearLifetimeTests.ReadDirectory(mirror));
            double deadline = Time.realtimeSinceStartupAsDouble + 5.0;
            while (mirror.IsClearPending && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(mirror.IsClearPending, Is.False);
            CollectionAssert.AreEqual(empty, GpuMirrorClearLifetimeTests.ReadDirectory(mirror));
            Assert.That(_first.TryTakePagedBatch(out _, out bool failed), Is.True);
            Assert.That(failed, Is.True, "The retired world must not publish a candidate into its replacement.");
            GpuSurfaceMirrorCoordinator.PrepareFrame(replacement.Reads, replacement.Changes, Time.frameCount + 1, 1.0);
        }

        [UnityTest]
        public IEnumerator SubmittedResourcesSurviveContextAndWorldDisposalUntilGpuCompletion() =>
            ValidateSubmittedDisposal(false);

        [UnityTest]
        public IEnumerator SubmissionExceptionStillReleasesRetiredResourcesThroughCompletion() =>
            ValidateSubmittedDisposal(true);

        private IEnumerator ValidateSubmittedDisposal(bool failSubmission)
        {
            // Keep the world attached until both submitting contexts have released their own
            // readers, so the remaining protection must belong to the submitted batch itself.
            using var keeper = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
            Assert.NotNull(keeper);
            foreach (var context in new[] { _first, _second })
            {
                Assert.That(GpuSurfaceMirrorCoordinator.TryBeginExtraction(int3.zero, context.BrickCacheEdge, out ulong epoch), Is.True);
                typeof(GpuSurfaceExtractionContext).GetField("_sharedExtractionActive", Fields).SetValue(context, true);
                typeof(GpuSurfaceExtractionContext).GetField("_extractionWorldEpoch", Fields).SetValue(context, epoch);
            }
            // UnityTest may advance a frame after SetUp; establish the queue budget here.
            Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, Time.frameCount);
            Queue(_first, 1);
            Queue(_second, 2);
            object lane = FirstLane();
            ComputeBuffer mirror = _first.Mirror.Materials;
            ComputeBuffer tables = _first.Tables.CellClass;
            ComputeBuffer extractor = (ComputeBuffer)typeof(GpuSurfaceExtractor)
                .GetField("_density", Fields).GetValue(_first.Extractor);
            ComputeBuffer arena = _arena.Vertices;
            Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, -1);
            MethodInfo submit = Coordinator.GetMethod("SealCountBatch", BindingFlags.Static | BindingFlags.NonPublic);
            object resources = Get(lane, "Resources");
            if (failSubmission)
            {
                // Inject a bounded submission precondition failure after resource ownership is
                // acquired. Production must schedule its completion-only cleanup on this path.
                lane.GetType().GetField("Resources", Fields).SetValue(lane, null);
                try
                {
                    var failure = Assert.Throws<TargetInvocationException>(() => submit.Invoke(null, new[] { lane }));
                    Assert.That(failure.InnerException, Is.TypeOf<ArgumentNullException>());
                }
                finally { lane.GetType().GetField("Resources", Fields).SetValue(lane, resources); }
            }
            else submit.Invoke(null, new[] { lane });
            Assert.That(Get(lane, "Submitted"), Is.True, "The real GPU completion request must actually be submitted.");
            Assert.That(GpuSurfaceMirrorCoordinator.ActiveRegionCount, Is.GreaterThan(0));
            _first.Dispose();
            Assert.That(GpuSurfaceMirrorCoordinator.ActiveRegionCount, Is.GreaterThan(0),
                "The batch must retain mirror readers after its prefix owner is disposed.");
            _second.Dispose();
            Assert.That(GpuSurfaceMirrorCoordinator.ActiveExtractions, Is.Zero);
            Assert.That(GpuSurfaceMirrorCoordinator.ActiveRegionCount, Is.GreaterThan(0),
                "Only the submitted batch can protect the mirror after both contexts release their readers.");
            keeper.Dispose();
            GpuSurfaceMirrorCoordinator.DetachPageArena(_arena, Time.frameCount);
            _arena.Dispose();
            Assert.That(mirror.IsValid(), Is.True);
            Assert.That(tables.IsValid(), Is.True);
            Assert.That(extractor.IsValid(), Is.True);
            Assert.That(arena.IsValid(), Is.True,
                "Logical teardown must leave submitted GPU allocations owned by completion.");
            // Reuse the same footprint in the new ownership epoch before the old callback.
            int edge = _first.BrickCacheEdge;
            Assert.That(GpuSurfaceMirrorCoordinator.TryBeginExtraction(int3.zero, edge, out ulong currentEpoch), Is.True);
            int currentReaders = GpuSurfaceMirrorCoordinator.ActiveRegionCount;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 5.0;
                while ((mirror.IsValid() || tables.IsValid() || extractor.IsValid() || arena.IsValid())
                       && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(mirror.IsValid() || tables.IsValid() || extractor.IsValid() || arena.IsValid(), Is.False,
                    "The real completion callback must release every retired allocation without a leak.");
                Assert.That(GpuSurfaceMirrorCoordinator.ActiveRegionCount, Is.EqualTo(currentReaders),
                    "A retired batch callback must not decrement a new world's identical footprint.");
            }
            finally { GpuSurfaceMirrorCoordinator.EndExtraction(int3.zero, edge, currentEpoch); }

        }

        private static void Queue(GpuSurfaceExtractionContext context, int x)
        {
            Assert.That(GpuSurfaceMirrorCoordinator.TryDispatchCountBatch(context, 0,
                context.Extractor, context.Tables,
                new GpuChunkExtraction(new int3(x, 0, 0), int3.zero, 1, 0.1f), Time.frameCount), Is.True);
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
        private static Array Lanes() => (Array)Coordinator.GetField("s_CountBatchLanes",
            BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static object FirstLane()
        {
            foreach (object lane in Lanes())
                if (lane != null && (int)Get(lane, "Count") > 0) return lane;
            throw new AssertionException("No queued lane remains.");
        }
        private static int QueuedCount()
        {
            int count = 0;
            foreach (object lane in Lanes())
                if (lane != null && !(bool)Get(lane, "Submitted")) count += (int)Get(lane, "Count");
            return count;
        }
    }
}
