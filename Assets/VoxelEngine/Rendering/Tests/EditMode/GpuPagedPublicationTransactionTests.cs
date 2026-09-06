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

        [Test]
        public void SuccessfulCandidateDoesNotBecomeLiveWithoutCpuCommit()
        {
            var arena = Create(handles: 1);
            const ulong generation = 0x0000000200000003UL;
            int handle = AcquireAndSelectGeneration(arena, generation, 1);
            using var batch = new Batch(handle, generation, vertices: 12, indices: 18);

            arena.AllocateBatch(
                batch.Descriptors, batch.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 2);
            Assert.That(batch.ReadAllocationStatus(), Is.EqualTo(AllocationReady));

            GeometryRecord pending = ReadRecord(
                arena.PendingChunkGeometry, handle, arena.HandleCapacity);
            GeometryRecord before = ReadRecord(
                arena.LiveChunkGeometry, handle, arena.HandleCapacity);
            Assert.That(pending.Ready, Is.EqualTo(1u),
                "A successful allocation must exist as pending candidate geometry.");
            Assert.That(before.Ready, Is.Zero,
                "Allocation alone must not manufacture live geometry.");

            // This is deliberately the current production arena call. The final contract requires
            // it to leave the candidate pending until the CPU validates the immutable render
            // request and sends an explicit identity-checked Commit (or Abort). On the reviewed
            // implementation this call swaps pending into live immediately, so this is the exact
            // fail-before discriminator for the publication defect.
            arena.PublishBatch(
                batch.Descriptors, batch.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 3);

            GeometryRecord after = ReadRecord(
                arena.LiveChunkGeometry, handle, arena.HandleCapacity);
            Assert.That(after.Ready, Is.Zero,
                "GPU write completion is only a candidate; pending geometry must never become live "
              + "until current CPU render demand explicitly commits that exact request identity.");
        }

        [Test]
        public void StaleAllocationReportsStatusAndOwnsNoPages()
        {
            var arena = Create(handles: 1);
            int handle = AcquireAndSelectGeneration(arena, generation: 9UL, frame: 4);
            using var stale = new Batch(handle, generation: 8UL, vertices: 8, indices: 12);

            arena.AllocateBatch(
                stale.Descriptors, stale.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 5);

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
            int first = AcquireAndSelectGeneration(arena, generation: 11UL, frame: 6);
            int second = AcquireAndSelectGeneration(arena, generation: 12UL, frame: 7);

            using var occupying = new Batch(
                first, 11UL,
                vertices: GpuSurfacePageArena.VertexPageSize,
                indices: GpuSurfacePageArena.IndexPageSize);
            arena.AllocateBatch(
                occupying.Descriptors, occupying.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 8);
            Assert.That(occupying.ReadAllocationStatus(), Is.EqualTo(AllocationReady));
            Assert.That(ReadRecord(
                arena.PendingChunkGeometry, first, arena.HandleCapacity).Ready, Is.EqualTo(1u));

            using var exhausted = new Batch(second, 12UL, vertices: 1, indices: 3);
            arena.AllocateBatch(
                exhausted.Descriptors, exhausted.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 9);

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
            int handle = AcquireAndSelectGeneration(arena, generation: 21UL, frame: 10);
            uint tooManyVertices = unchecked((uint)(
                GpuSurfacePageArena.VertexPageSize
                * (GpuSurfacePageArena.MaxVertexPagesPerChunk + 1)));
            using var oversized = new Batch(
                handle, 21UL, vertices: tooManyVertices, indices: 3);

            arena.AllocateBatch(
                oversized.Descriptors, oversized.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 11);

            Assert.That(oversized.ReadAllocationStatus(), Is.EqualTo(AllocationTooLarge),
                "TooLarge must stay distinct from transient exhaustion so production can take an "
              + "explicit supported action instead of retrying an impossible allocation forever.");
            Assert.That(ReadRecord(
                arena.PendingChunkGeometry, handle, arena.HandleCapacity).Ready, Is.Zero);
        }
    }
}
