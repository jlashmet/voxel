using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    /// <summary>
    /// Regression surface for the paged GPU transaction contract. Readback is test-only and
    /// intentionally limited to the arena's tiny bookkeeping buffers; generated geometry is never
    /// copied back here.
    /// </summary>
    [NonParallelizable]
    public sealed class GpuPagedPublicationTransactionTests
    {
        private const uint AllocationReady = 0u;
        private const uint AllocationExhausted = 1u;
        private const uint AllocationStale = 2u;
        private const uint AllocationTooLarge = 3u;

        [StructLayout(LayoutKind.Sequential)]
        private struct GeometryRecord
        {
            public uint GenerationLow;
            public uint GenerationHigh;
            public uint Bank;
            public uint VertexCount;
            public uint IndexCount;
            public uint VertexPageCount;
            public uint IndexPageCount;
            public uint Ready;

            public ulong Generation => GenerationLow | ((ulong)GenerationHigh << 32);
        }

        private sealed class Batch : IDisposable
        {
            internal readonly ComputeBuffer Descriptors;
            internal readonly ComputeBuffer Counters;
            internal readonly uint[] Words;

            internal Batch(int handle, ulong generation, uint vertices, uint indices)
            {
                Descriptors = new ComputeBuffer(
                    1, GpuSurfaceExtractor.BatchChunkDescriptor.Stride,
                    ComputeBufferType.Structured);
                Counters = new ComputeBuffer(
                    GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords,
                    sizeof(uint), ComputeBufferType.Structured);
                Words = new uint[
                    GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords];

                var descriptor = new GpuSurfaceExtractor.BatchChunkDescriptor
                {
                    OriginX = 0,
                    OriginY = 0,
                    OriginZ = 0,
                    SourceStep = 1,
                    TransitionFaceMask = 0,
                    VoxelSize = 0.1f,
                    Handle = unchecked((uint)handle),
                    GenerationLow = (uint)generation,
                    GenerationHigh = (uint)(generation >> 32),
                    ProfileStart = 0,
                    ProfileCount = 0,
                };
                Descriptors.SetData(new[] { descriptor });

                int word = GpuSurfaceExtractor.BatchHeaderWords;
                Words[word + 2] = vertices;
                Words[word + 3] = indices;
                Counters.SetData(Words);
            }

            internal uint ReadAllocationStatus()
            {
                Counters.GetData(Words);
                return Words[GpuSurfaceExtractor.BatchHeaderWords + 10];
            }

            public void Dispose()
            {
                Descriptors?.Release();
                Counters?.Release();
            }
        }

        private ComputeShader _shader;
        private GpuSurfacePageArena _arena;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True,
                "This GPU transaction regression requires a real compute device.");
            ComputeShader asset = Resources.Load<ComputeShader>("GpuSurfacePageArena");
            Assert.That(asset, Is.Not.Null);
            _shader = UnityEngine.Object.Instantiate(asset);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_arena != null)
                {
                    // A bounded bookkeeping read drains prior test dispatches before disposal.
                    // Never move this synchronous test-only readback into production.
                    var records = new GeometryRecord[_arena.HandleCapacity];
                    _arena.LiveChunkGeometry.GetData(records);
                }
            }
            finally
            {
                _arena?.Dispose();
                _arena = null;
                if (_shader != null) UnityEngine.Object.DestroyImmediate(_shader);
                _shader = null;
            }
        }

        private GpuSurfacePageArena Create(int handles, int vertexPages = 2, int indexPages = 2)
        {
            _arena = new GpuSurfacePageArena(
                _shader,
                GpuSurfacePageArena.VertexPageSize * vertexPages,
                GpuSurfacePageArena.IndexPageSize * indexPages,
                handles);
            return _arena;
        }

        private static GeometryRecord ReadRecord(ComputeBuffer buffer, int handle, int capacity)
        {
            var records = new GeometryRecord[capacity];
            buffer.GetData(records);
            return records[handle];
        }

        private static int AcquireAndSelectGeneration(
            GpuSurfacePageArena arena, ulong generation, int frame)
        {
            Assert.That(arena.TryAcquireHandle(out int handle), Is.True);
            arena.QueueGeneration(handle, generation);
            arena.FlushHandleCommands(frame);
            return handle;
        }

        private static void AllocateAndFinalize(
            GpuSurfacePageArena arena, Batch batch, int frame)
        {
            arena.AllocateBatch(
                batch.Descriptors, batch.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame);
            Assert.That(batch.ReadAllocationStatus(), Is.EqualTo(AllocationReady));
            arena.PublishBatch(
                batch.Descriptors, batch.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame + 1);
            Assert.That(batch.ReadAllocationStatus(), Is.EqualTo(AllocationReady));
        }

        [Test]
        public void SuccessfulCandidateDoesNotBecomeLiveWithoutCpuCommit()
        {
            var arena = Create(handles: 1);
            const ulong generation = 0x0000000200000003UL;
            int handle = AcquireAndSelectGeneration(arena, generation, 1);
            using var batch = new Batch(handle, generation, vertices: 12, indices: 18);

            AllocateAndFinalize(arena, batch, 2);

            GeometryRecord pending = ReadRecord(
                arena.PendingChunkGeometry, handle, arena.HandleCapacity);
            GeometryRecord before = ReadRecord(
                arena.LiveChunkGeometry, handle, arena.HandleCapacity);
            Assert.That(pending.Ready, Is.EqualTo(1u),
                "A successful write must remain a pending candidate.");
            Assert.That(before.Ready, Is.Zero,
                "GPU write completion is not renderer-demand approval.");

            arena.CommitPending(handle, generation, 4);

            GeometryRecord after = ReadRecord(
                arena.LiveChunkGeometry, handle, arena.HandleCapacity);
            GeometryRecord pendingAfter = ReadRecord(
                arena.PendingChunkGeometry, handle, arena.HandleCapacity);
            Assert.That(after.Ready, Is.EqualTo(1u));
            Assert.That(after.Generation, Is.EqualTo(generation));
            Assert.That(pendingAfter.Ready, Is.Zero,
                "Commit must consume exactly the approved pending candidate.");
        }

        [Test]
        public void AbortPreservesPreviousLiveGeometryAndConsumesOnlyPendingCandidate()
        {
            var arena = Create(handles: 1, vertexPages: 4, indexPages: 4);
            const ulong liveGeneration = 30UL;
            const ulong rejectedGeneration = 31UL;
            int handle = AcquireAndSelectGeneration(arena, liveGeneration, 5);
            using (var initial = new Batch(handle, liveGeneration, vertices: 12, indices: 18))
            {
                AllocateAndFinalize(arena, initial, 6);
                arena.CommitPending(handle, liveGeneration, 8);
            }
            Assert.That(ReadRecord(
                arena.LiveChunkGeometry, handle, arena.HandleCapacity).Generation,
                Is.EqualTo(liveGeneration));

            arena.QueueGeneration(handle, rejectedGeneration);
            arena.FlushHandleCommands(9);
            using var replacement = new Batch(
                handle, rejectedGeneration, vertices: 20, indices: 30);
            AllocateAndFinalize(arena, replacement, 10);
            Assert.That(ReadRecord(
                arena.PendingChunkGeometry, handle, arena.HandleCapacity).Generation,
                Is.EqualTo(rejectedGeneration));

            arena.AbortPending(handle, rejectedGeneration, 12);

            GeometryRecord live = ReadRecord(
                arena.LiveChunkGeometry, handle, arena.HandleCapacity);
            GeometryRecord pending = ReadRecord(
                arena.PendingChunkGeometry, handle, arena.HandleCapacity);
            Assert.That(live.Ready, Is.EqualTo(1u));
            Assert.That(live.Generation, Is.EqualTo(liveGeneration),
                "Rejected replacement must not disturb the previous live representation.");
            Assert.That(pending.Ready, Is.Zero);
        }

        [Test]
        public void StaleAllocationReportsStatusAndOwnsNoPages()
        {
            var arena = Create(handles: 1);
            int handle = AcquireAndSelectGeneration(arena, generation: 9UL, frame: 13);
            using var stale = new Batch(handle, generation: 8UL, vertices: 8, indices: 12);

            arena.AllocateBatch(
                stale.Descriptors, stale.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 14);

            Assert.That(stale.ReadAllocationStatus(), Is.EqualTo(AllocationStale));
            Assert.That(ReadRecord(
                arena.PendingChunkGeometry, handle, arena.HandleCapacity).Ready, Is.Zero,
                "A stale request must not allocate pending pages.");
            Assert.That(ReadRecord(
                arena.LiveChunkGeometry, handle, arena.HandleCapacity).Ready, Is.Zero);
        }

        [Test]
        public void ExhaustedAllocationIsObservableAndDoesNotOverwriteAnotherPendingCandidate()
        {
            var arena = Create(handles: 2, vertexPages: 1, indexPages: 1);
            int first = AcquireAndSelectGeneration(arena, generation: 11UL, frame: 15);
            int second = AcquireAndSelectGeneration(arena, generation: 12UL, frame: 16);

            using var occupying = new Batch(
                first, 11UL,
                vertices: GpuSurfacePageArena.VertexPageSize,
                indices: GpuSurfacePageArena.IndexPageSize);
            arena.AllocateBatch(
                occupying.Descriptors, occupying.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 17);
            Assert.That(occupying.ReadAllocationStatus(), Is.EqualTo(AllocationReady));
            Assert.That(ReadRecord(
                arena.PendingChunkGeometry, first, arena.HandleCapacity).Ready, Is.EqualTo(1u));

            using var exhausted = new Batch(second, 12UL, vertices: 1, indices: 3);
            arena.AllocateBatch(
                exhausted.Descriptors, exhausted.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 18);

            Assert.That(exhausted.ReadAllocationStatus(), Is.EqualTo(AllocationExhausted),
                "Arena exhaustion is a retryable GPU result, not successful completion.");
            Assert.That(ReadRecord(
                arena.PendingChunkGeometry, second, arena.HandleCapacity).Ready, Is.Zero);
            Assert.That(ReadRecord(
                arena.PendingChunkGeometry, first, arena.HandleCapacity).Ready, Is.EqualTo(1u),
                "A rejected second allocation must not disturb an already-owned pending candidate.");
        }

        [Test]
        public void OversizedAllocationHasDistinctPermanentCapacityStatus()
        {
            var arena = Create(handles: 1, vertexPages: 1, indexPages: 1);
            int handle = AcquireAndSelectGeneration(arena, generation: 21UL, frame: 19);
            uint tooManyVertices = unchecked((uint)(
                GpuSurfacePageArena.VertexPageSize
                * (GpuSurfacePageArena.MaxVertexPagesPerChunk + 1)));
            using var oversized = new Batch(
                handle, 21UL, vertices: tooManyVertices, indices: 3);

            arena.AllocateBatch(
                oversized.Descriptors, oversized.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 20);

            Assert.That(oversized.ReadAllocationStatus(), Is.EqualTo(AllocationTooLarge),
                "TooLarge must stay distinct from transient exhaustion so production can take an "
              + "explicit supported action instead of retrying an impossible allocation forever.");
            Assert.That(ReadRecord(
                arena.PendingChunkGeometry, handle, arena.HandleCapacity).Ready, Is.Zero);
        }
    }
}
