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

        private static IEnumerator PrepareSources(object lane)
        {
            var advance = lane.GetType().GetMethod("AdvanceSummaryPreparation", Fields);
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if ((bool)advance.Invoke(lane, new object[] { Time.frameCount })) yield break;
                Assert.That(Get(lane, "SummaryFailed"), Is.False);
                yield return null;
            }
            Assert.Fail("Real GPU source preparation did not complete within five seconds.");
        }

        [UnityTest]
        public IEnumerator ReadyNearGeometrySubmitsBeforeAnEarlierCoarsePreparationLane() => ValidateSubmissionPriority(false);

        [UnityTest]
        public IEnumerator ContinuousNearWorkStillAllowsBoundedCoarseProgress() => ValidateSubmissionPriority(true);

        private IEnumerator ValidateSubmissionPriority(bool coarseDue)
        {
            _first.Dispose();
            _first = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024, 10);
            Assert.NotNull(_first);
            using var storage = VoxelEngineBootstrap.CreateStorage(1, 1);
            storage.Residency.EnsureRegionResident(int3.zero);
            storage.PublishAllResidentRegions();
            GpuSurfaceMirrorCoordinator.PrepareFrame(storage.Reads, storage.Changes, Time.frameCount, 1.0);
            typeof(GpuSurfaceExtractionContext).GetField("_hasStaged", Fields).SetValue(_first, true);
            // Queue the coarse lane first, reproducing a ready near batch arriving while a
            // coarse request is preparing sources. Real GPU submissions prove slot ownership.
            Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, Time.frameCount);
            Assert.That(GpuSurfaceMirrorCoordinator.TryDispatchCountBatch(_first, 0, _first.Extractor,
                _first.Tables, new GpuChunkExtraction(int3.zero, int3.zero, 8, 0.1f), Time.frameCount), Is.True);
            object coarse = FirstLane();
            Queue(_second, 2);
            object near = Lanes().GetValue(1);
            Assert.That(Get(near, "Count"), Is.EqualTo(1));
            yield return PrepareSources(near);
            if (coarseDue)
                for (int i = 1; i <= 8; i++)
                    Assert.That(GpuSurfaceMirrorCoordinator.TryReserveExtractionDispatch(Time.frameCount + i), Is.True);
            Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, -1);
            Coordinator.GetMethod("AdvanceCountBatches", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Time.frameCount + 3 });
            Assert.That(Get(near, "Submitted"), Is.EqualTo(!coarseDue),
                "Ready near geometry wins unless the bounded coarse-service interval is due.");
            Assert.That(Get(coarse, "SummarySubmitted"), Is.EqualTo(coarseDue),
                "Only one GPU submission slot may be consumed, with no coarse starvation.");
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            if (coarseDue)
            {
                while ((bool)Get(coarse, "SummarySubmitted") && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(Get(coarse, "SummarySubmitted"), Is.False);
            }
            else
            {
                while (!(bool)Get(near, "OutcomeReady") && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(Get(near, "OutcomeReady"), Is.True);
            }
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

        [TestCase(false)]
        [TestCase(true)]
        public void QueuedRequestInvalidationIsLimitedToItsSourceFootprint(bool intersects)
        {
            int edge = _first.BrickCacheEdge;
            ulong world = GpuSurfaceMirrorCoordinator.RequestCoverage(int3.zero, edge, int3.zero, new int3(8));
            typeof(GpuSurfaceExtractionContext).GetField("_coverageRequested", Fields).SetValue(_first, true);
            typeof(GpuSurfaceExtractionContext).GetField("_coverageWorldEpoch", Fields).SetValue(_first, world);
            typeof(GpuSurfaceExtractionContext).GetField("_coverageEpoch", Fields)
                .SetValue(_first, GpuSurfaceMirrorCoordinator.CoverageEpochFor(int3.zero, edge));
            Queue(_first, 1);
            int3 min = intersects ? int3.zero : new int3(8192);
            var change = new VoxelEngine.Storage.Api.VoxelChangeRecord(1,
                min >> VoxelEngine.Storage.Api.VoxelGrid.RegionVoxelEdgeLog2, min, min + 1,
                VoxelEngine.Storage.Api.VoxelChangeKind.Occupancy);
            Coordinator.GetMethod("ApplyChange", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { change });
            Assert.That(_first.IsCurrentBatchRequest(0), Is.EqualTo(!intersects));
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
            using var initial = VoxelEngineBootstrap.CreateStorage(1, 1);
            initial.Residency.EnsureRegionResident(int3.zero);
            GpuSurfaceMirrorCoordinator.PrepareFrame(initial.Reads, initial.Changes, Time.frameCount, 1.0);
            var mirror = _first.Mirror;
            uint[] empty = GpuMirrorClearLifetimeTests.ReadDirectory(mirror);
            Assert.That(mirror.Publish(VoxelBrickDelta.UniformAt(int3.zero, 1, 1),
                default, default, default, 0, false), Is.EqualTo(GpuBrickPublish.MetadataOnly));
            uint[] occupied = GpuMirrorClearLifetimeTests.ReadDirectory(mirror);
            Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, Time.frameCount);
            Queue(_first, 1); Queue(_second, 2);
            object lane = FirstLane();
            yield return PrepareSources(lane);
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
            using var initial = VoxelEngineBootstrap.CreateStorage(1, 1);
            initial.Residency.EnsureRegionResident(int3.zero);
            GpuSurfaceMirrorCoordinator.PrepareFrame(initial.Reads, initial.Changes, Time.frameCount, 1.0);
            // Keep the world attached until both submitting contexts have released their own
            // readers, so the remaining protection must belong to the submitted batch itself.
            using var keeper = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
            Assert.NotNull(keeper);
            foreach (var context in new[] { _first, _second })
            {
                Assert.That(GpuSurfaceMirrorCoordinator.TryBeginExtraction(int3.zero, 0, out ulong epoch), Is.True);
                typeof(GpuSurfaceExtractionContext).GetField("_sharedExtractionActive", Fields).SetValue(context, true);
                typeof(GpuSurfaceExtractionContext).GetField("_extractionWorldEpoch", Fields).SetValue(context, epoch);
            }
            // UnityTest may advance a frame after SetUp; establish the queue budget here.
            Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, Time.frameCount);
            Queue(_first, 1);
            Queue(_second, 2);
            object lane = FirstLane();
            yield return PrepareSources(lane);
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

        [UnityTest]
        public IEnumerator StepEightLaneAccumulatesPortionsBeforePublishing() => ValidateSummaryLane(false);

        [UnityTest]
        public IEnumerator RetiredSummaryLaneRetainsBuffersUntilItsGpuCallback() => ValidateSummaryLane(true);

        [UnityTest]
        public IEnumerator StepEightSourceFootprintCanExceedMirrorCapacity() => ValidateSummaryLane(false, true);

        [UnityTest]
        public IEnumerator EditToCompletedSummaryPortionRejectsTheWholeCandidate() => ValidateSummaryLane(false, false, true);

        [UnityTest]
        public IEnumerator FineStepOnePublishesRealMixedStorageThroughGpuReadiness() => ValidateSummaryLane(false, step: 1);

        [UnityTest]
        public IEnumerator FineStepTwoPublishesRealMixedStorageThroughGpuReadiness() => ValidateSummaryLane(false, step: 2);

        [UnityTest]
        public IEnumerator FineStepFourPublishesRealMixedStorageThroughGpuReadiness() => ValidateSummaryLane(false, step: 4);

        [UnityTest]
        public IEnumerator FineCompletedSourceEditRejectsTheCandidate() => ValidateSummaryLane(false, edit: true, step: 4);

        [UnityTest]
        public IEnumerator FineSourceRetirementKeepsBuffersThroughCompletion() => ValidateSummaryLane(true, step: 4);

        private IEnumerator ValidateSummaryLane(bool retire, bool pressure = false, bool edit = false, int step = 8)
        {
            _first.Dispose();
            if (pressure)
            {
                _second.Dispose(); _second = null;
                // Configure a smaller instance of the production mirror before any consumer acquires
                // it. The renderer must stream 4,096 authoritative mixed bricks through 1,024 slots.
                Coordinator.GetField("s_Mirror", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, new GpuVoxelBrickMirror(1024, retainKnownEmpty: false));
            }
            int core = pressure ? 16 : 8, edge = core * step / 8 + 2;
            _first = GpuSurfaceExtractionContext.TryCreate(core, 2, 1024, edge);
            Assert.NotNull(_first);
            using var storage = VoxelEngineBootstrap.CreateStorage(edit ? 2 : 1, pressure ? 4096 : 1);
            storage.Residency.EnsureRegionResident(int3.zero);
            if (pressure)
            {
                for (int z = 0; z < core; z++)
                for (int y = 0; y < core; y++)
                for (int x = 0; x < core; x++)
                {
                    int3 block = new(x, y, z);
                    storage.Mutations.SetWholeBlock(block, 1, false);
                    Assert.That(storage.Mutations.TryBeginPartialBlock(block, 2, false, out var mutation), Is.True);
                    Assert.That(mutation.SetMaterial(0, 2), Is.True);
                    storage.Mutations.CompletePartialBlock(ref mutation, true);
                }
                Assert.That(core * core * core, Is.GreaterThan(_first.Mirror.SlotCapacity));
            }
            if (step != 8)
            {
                storage.Mutations.SetWholeBlock(int3.zero, 1, false);
                Assert.That(storage.Mutations.TryBeginPartialBlock(int3.zero, 2, false, out var mixed), Is.True);
                Assert.That(mixed.SetMaterial(0, 2), Is.True);
                storage.Mutations.CompletePartialBlock(ref mixed, true);
            }
            storage.PublishAllResidentRegions();
            GpuSurfaceMirrorCoordinator.PrepareFrame(storage.Reads, storage.Changes, Time.frameCount, 1.0);
            int handle = GpuSurfaceMirrorCoordinator.PrepareChunkHandle(int3.zero, step, out ulong generation);
            var request = new GpuChunkExtraction(int3.zero, new int3(-1), step, 0.1f,
                handle: handle, generation: generation);
            ulong world = step == 8
                ? GpuSurfaceMirrorCoordinator.RequestEditWatch(new int3(-1), edge)
                : GpuSurfaceMirrorCoordinator.RequestCoverage(new int3(-1), edge, int3.zero, new int3(core * step));
            foreach (var pair in new[] { ("_hasStaged", (object)true), ("_coverageRequested", (object)true),
                ("_staged", (object)request), ("_coverageWorldEpoch", (object)world),
                ("_coverageEpoch", (object)GpuSurfaceMirrorCoordinator.CoverageEpochFor(new int3(-1), edge)) })
                typeof(GpuSurfaceExtractionContext).GetField(pair.Item1, Fields).SetValue(_first, pair.Item2);
            Assert.That(GpuSurfaceMirrorCoordinator.TryDispatchCountBatch(_first, 0, _first.Extractor,
                _first.Tables, request, Time.frameCount), Is.True);
            object lane = FirstLane();
            bool sawSubmission = false, completed = false, edited = false;
            int frame = Time.frameCount;
            double deadline = Time.realtimeSinceStartupAsDouble + 5.0;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
                if (edit && !edited && ((int)Get(lane, "_summaryCursor") > 0 || (int)Get(lane, "_summaryRecord") > 0))
                {
                    // Edit the first completed Z plane, including its previously absent halo.
                    storage.Residency.EnsureRegionResident(new int3(-1));
                    storage.PublishAllResidentRegions();
                    edited = true;
                }
                // EditMode does not advance Time.frameCount like a player. Advance one bounded
                // scheduler slice explicitly, as existing queue tests do for dispatch admission.
                Coordinator.GetField("s_LastExtractionDispatchFrame", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, -1);
                lane.GetType().GetField("_lastSummaryFrame", Fields).SetValue(lane, -1);
                GpuSurfaceMirrorCoordinator.PrepareFrame(storage.Reads, storage.Changes, ++frame, 1.0);
                if ((bool)Get(lane, "SummarySubmitted"))
                {
                    sawSubmission = true;
                    Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount,
                        Is.EqualTo(step == 8 || math.all((int3)Get(lane, "_summaryExtent") == edge) ? 1 : 2),
                        "Fine mixed slots need whole-footprint demand in addition to the active portion.");
                    if (retire)
                    {
                        ComputeBuffer requests = (ComputeBuffer)Get(lane, "SummaryRequests");
                        ComputeBuffer missing = (ComputeBuffer)Get(lane, "SummaryMissing");
                        ComputeBuffer blockReferences = (ComputeBuffer)Get(lane, "SummaryBlockReferences");
                        _first.Dispose();
                        using var replacement = VoxelEngineBootstrap.CreateStorage(1, 1);
                        GpuSurfaceMirrorCoordinator.PrepareFrame(replacement.Reads, replacement.Changes, Time.frameCount + 1, 1.0);
                        Assert.That(requests.IsValid(), Is.True, "Retirement must not free an in-flight request buffer.");
                        Assert.That(blockReferences.IsValid(), Is.True);
                        while (requests.IsValid() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                        Assert.That(requests.IsValid(), Is.False);
                        Assert.That(missing.IsValid(), Is.False, "Missing-source feedback belongs to the same submission lifetime.");
                        Assert.That(blockReferences.IsValid(), Is.False, "Block metadata belongs to the same submission lifetime.");
                        Assert.That(GpuSurfaceMirrorCoordinator.ActiveRegionCount, Is.Zero);
                        yield break;
                    }
                }
                if (_first.TryTakePagedBatch(out _, out bool failed))
                {
                    Assert.That(failed, Is.EqualTo(edit));
                    if ((pressure || step != 8) && !edit)
                    {
                        var counters = new uint[GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords];
                        ((ComputeBuffer)Get(lane, "Counters")).GetData(counters, 0, 0, counters.Length);
                        Assert.That(counters[6], Is.GreaterThan(0), "Capacity-pressure case must produce real geometry.");
                    }
                    if (edit) Assert.That(edited, Is.True);
                    completed = true;
                    break;
                }
            }
            Assert.That(sawSubmission, Is.True, "Test must execute real asynchronous summary work.");
            Assert.That(completed, Is.True, "The bounded lane must reach publication within the test deadline.");
            _first.Release();
            Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.Zero);
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
