using System;
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
