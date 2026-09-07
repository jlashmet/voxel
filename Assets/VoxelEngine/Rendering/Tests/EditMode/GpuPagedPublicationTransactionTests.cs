using System;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
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
                // This bookkeeping fixture models a completed write; real-kernel coverage is separate.
                Words[word + 8] = vertices;
                Words[word + 9] = indices;
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

        private string DescribeAllocation(Batch batch, int handle)
        {
            var desired = new uint[_arena.HandleCapacity * 2];
            _arena.DesiredGenerations.GetData(desired);
            var arenaWords = new uint[_arena.ArenaState.count];
            _arena.ArenaState.GetData(arenaWords);
            return $"desired={desired[handle * 2]:X8}/{desired[handle * 2 + 1]:X8} "
                 + $"batch=[{string.Join(",", batch.Words)}] arena=[{string.Join(",", arenaWords)}]";
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

        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(8)]
        public void AllocationCapturesProductionBoundsOnGpu(int step)
        {
            var arena = Create(1);
            int handle = AcquireAndSelectGeneration(arena, 1, 0);
            using var batch = new Batch(handle, 1, 3, 3);
            batch.Descriptors.SetData(new[] { new GpuSurfaceExtractor.BatchChunkDescriptor {
                OriginX = -128, OriginY = 64, OriginZ = -256, SourceStep = step,
                VoxelSize = 0.1f, Handle = (uint)handle, GenerationLow = 1 } });
            AllocateAndFinalize(arena, batch, 0);
            var bounds = new Vector4[2];
            arena.ResidentBounds.GetData(bounds);
            Assert.That(bounds[0].x, Is.EqualTo(-12.8f + step * 3.2f).Within(0.0001));
            Assert.That(bounds[0].y, Is.EqualTo(6.4f + step * 3.2f).Within(0.0001));
            Assert.That(bounds[0].z, Is.EqualTo(-25.6f + step * 3.2f).Within(0.0001));
            for (int axis = 0; axis < 3; axis++)
                Assert.That(bounds[1][axis], Is.EqualTo(step * 3.3f).Within(0.0001));
        }

        [TestCase(1, 0, 16)] [TestCase(1, 1, 16)] [TestCase(2, 0, 16)] [TestCase(2, 1, 16)]
        [TestCase(1, 0, 1)] [TestCase(1, 1, 1)] [TestCase(2, 0, 1)] [TestCase(2, 1, 1)]
        public void CapacityPressureFiltersExactStepAndShardBeforeRanking(int step, int shard, int wanted)
        {
            const int count = 130;
            var arena = Create(count, count, count);
            var bounds = new Vector4[count * 2];
            var expected = new System.Collections.Generic.List<int>();
            for (int i = 0; i < count; i++)
            {
                Assert.That(arena.TryAcquireHandle(out int handle), Is.True);
                int ownerStep = i % 2 + 1;
                var coordinate = new Unity.Mathematics.int3(i - 65, i % 3 - 1, 2 - i % 5);
                uint ownerHash = Unity.Mathematics.math.hash(coordinate);
                arena.QueueGeneration(handle, (ulong)i + 1, (uint)ownerStep, ownerHash);
                arena.FlushHandleCommands(0);
                using var batch = new Batch(handle, (ulong)i + 1, 3, 3);
                AllocateAndFinalize(arena, batch, 0);
                arena.CommitPending(handle, (ulong)i + 1, 2);
                bounds[i * 2] = new Vector4(-10 * (i + 1), 0, 0, 0);
                bounds[i * 2 + 1] = Vector4.one;
                if (ownerStep == step && VoxelEngine.Rendering.Runtime.SurfaceExtraction.GpuSolidChunkCache
                    .ShardForChunk(coordinate, 2) == shard) expected.Add(handle);
            }
            expected.Reverse();
            using var residentBounds = new ComputeBuffer(count, 32);
            residentBounds.SetData(bounds);
            using var pressure = new GpuSurfacePressureDispatcher(arena);
            var planes = new Plane[6];
            for (int i = 0; i < 6; i++) planes[i] = new Plane(Vector3.right, 0);
            pressure.Dispatch(residentBounds, planes, Vector3.zero, wanted, 3, step, shard, 2);
            var results = new uint[64];
            pressure.Outcomes.GetData(results);
            Assert.That(expected.Count, Is.GreaterThan(16));
            for (int i = 0; i < wanted; i++)
            {
                Assert.That(results[i * 4], Is.EqualTo(1));
                Assert.That(results[i * 4 + 1], Is.EqualTo((uint)expected[i]));
            }
            for (int i = 0; i < count; i++)
                Assert.That(ReadRecord(arena.LiveChunkGeometry, i, count).Ready,
                    Is.EqualTo(expected.GetRange(0, wanted).Contains(i) ? 0u : 1u));
        }

        [TestCase(7)]
        [TestCase(67)]
        [TestCase(130)]
        public void GpuPressureRetiresFarthestOffscreenPublishedIdentitiesOnce(int count)
        {
            var arena = Create(count, count + 1, count + 1);
            var bounds = new Vector4[count * 2];
            for (int i = 0; i < count; i++)
            {
                int handle = AcquireAndSelectGeneration(arena, (1UL << 40) + (ulong)i, 0);
                using var batch = new Batch(handle, (1UL << 40) + (ulong)i, 3, 3);
                AllocateAndFinalize(arena, batch, 0);
                arena.CommitPending(handle, (1UL << 40) + (ulong)i, 2);
                bounds[i * 2] = new Vector4(i == 0 ? 10 : -10 * i, 0, 0, 0);
                bounds[i * 2 + 1] = Vector4.one;
            }
            // The farthest handle is replacing its live generation. The next farthest
            // already has a pending candidate; neither may be pressure-retired.
            arena.QueueGeneration(count - 1, 900);
            using var pending = new Batch(count - 2, (1UL << 40) + (ulong)(count - 2), 3, 3);
            AllocateAndFinalize(arena, pending, 2);
            using var residentBounds = new ComputeBuffer(count, 32);
            residentBounds.SetData(bounds);
            using var pressure = new GpuSurfacePressureDispatcher(arena);
            var planes = new Plane[6];
            for (int i = 0; i < planes.Length; i++) planes[i] = new Plane(Vector3.right, 0);
            pressure.Dispatch(residentBounds, planes, Vector3.zero, 16, 3);
            var outcomes = new uint[16 * 4];
            pressure.Outcomes.GetData(outcomes);
            int expected = Math.Min(16, count - 3);
            for (int i = 0; i < 16; i++)
            {
                Assert.That(outcomes[i * 4], Is.EqualTo(i < expected ? 1u : 0u));
                if (i >= expected) continue;
                int handle = count - 3 - i;
                Assert.That(outcomes[i * 4 + 1], Is.EqualTo((uint)handle));
                Assert.That(outcomes[i * 4 + 2], Is.EqualTo((uint)handle));
                Assert.That(outcomes[i * 4 + 3], Is.EqualTo(256u));
                Assert.That(ReadRecord(arena.LiveChunkGeometry, handle, count).Ready, Is.Zero);
            }
            Assert.That(ReadRecord(arena.LiveChunkGeometry, 0, count).Ready, Is.EqualTo(1));
            Assert.That(ReadRecord(arena.LiveChunkGeometry, count - 1, count).Ready, Is.EqualTo(1));
            Assert.That(ReadRecord(arena.LiveChunkGeometry, count - 2, count).Ready, Is.EqualTo(1));
            // Make every remaining chunk visible: another dispatch must not re-retire old pages.
            for (int i = 0; i < planes.Length; i++) planes[i] = new Plane(Vector3.up, 1000);
            pressure.Dispatch(residentBounds, planes, Vector3.zero, 16, 3);
            pressure.Outcomes.GetData(outcomes);
            for (int i = 0; i < 16; i++) Assert.That(outcomes[i * 4], Is.Zero);
            var state = new uint[7];
            arena.ArenaState.GetData(state);
            Assert.That(state[3], Is.EqualTo((uint)expected));
            Assert.That(state[5], Is.EqualTo((uint)expected));
        }

        [TestCase(13, 9)] [TestCase(9, 13)]
        public void MultiPagePendingSupersessionPreservesCapacity(int vertexPages, int indexPages)
        {
            var arena = Create(handles: 1, vertexPages: vertexPages, indexPages: indexPages);
            int handle = AcquireAndSelectGeneration(arena, 1, 1);
            var state = new uint[7];
            for (int cycle = 0; cycle < 16; cycle++)
            {
                ulong generation = (ulong)cycle + 1;
                arena.QueueGeneration(handle, generation); arena.FlushHandleCommands(cycle + 1);
                using var batch = new Batch(handle, generation,
                    (uint)(vertexPages * GpuSurfacePageArena.VertexPageSize),
                    (uint)(indexPages * GpuSurfacePageArena.IndexPageSize));
                AllocateAndFinalize(arena, batch, cycle + 1);
                arena.ArenaState.GetData(state);
                Assert.That(state[0], Is.Zero, $"Supersession corrupted vertex capacity at cycle {cycle}");
                Assert.That(state[1], Is.Zero, $"Supersession corrupted index capacity at cycle {cycle}");
                var record = ReadRecord(arena.PendingChunkGeometry, handle, 1);
                Assert.That(record.Generation, Is.EqualTo(generation));
                Assert.That(state[3] - state[2], Is.Zero, "Unpublished pages can be reused directly.");
            }
        }

        [TestCase(13, 9)] [TestCase(9, 13)] [TestCase(1, 1)]
        public void RepeatedMultiHandleReleaseConservesEveryVertexAndIndexPage(int vertexPages, int indexPages)
        {
            const int capacity = 64, count = 4;
            var arena = Create(handles: count, vertexPages: capacity, indexPages: capacity);
            var handles = new int[count];
            var state = new uint[7];
            for (int cycle = 0; cycle < 16; cycle++)
            {
                int frame = cycle * 20 + 1;
                ulong generation = (ulong)(cycle * 2 + 1);
                for (int i = 0; i < count; i++)
                {
                    handles[i] = AcquireAndSelectGeneration(arena, generation, frame);
                    using var batch = new Batch(handles[i], generation,
                        (uint)(vertexPages * GpuSurfacePageArena.VertexPageSize),
                        (uint)(indexPages * GpuSurfacePageArena.IndexPageSize));
                    AllocateAndFinalize(arena, batch, frame + 1);
                    arena.CommitPending(handles[i], generation, frame + 3);
                }
                for (int i = 0; i < count; i++) arena.QueueRelease(handles[i], generation);
                arena.FlushHandleCommands(frame + 4);
                arena.ArenaState.GetData(state);
                Assert.That(state[0] + state[3] - state[2], Is.EqualTo(capacity), $"Vertex retirement lost pages at cycle {cycle}");
                Assert.That(state[1] + state[5] - state[4], Is.EqualTo(capacity), $"Index retirement lost pages at cycle {cycle}");

                int reclaim = AcquireAndSelectGeneration(arena, generation + 1, frame + 10);
                using var empty = new Batch(reclaim, generation + 1, 0, 0);
                AllocateAndFinalize(arena, empty, frame + 11);
                arena.ArenaState.GetData(state);
                Assert.That(state[0], Is.EqualTo(capacity), $"Vertex reclamation lost pages at cycle {cycle}");
                Assert.That(state[1], Is.EqualTo(capacity), $"Index reclamation lost pages at cycle {cycle}");
                var pages = new uint[capacity];
                arena.FreeVertexPages.GetData(pages); CollectionAssert.AllItemsAreUnique(pages);
                foreach (uint page in pages) Assert.That(page, Is.LessThan(capacity));
                arena.FreeIndexPages.GetData(pages); CollectionAssert.AllItemsAreUnique(pages);
                foreach (uint page in pages) Assert.That(page, Is.LessThan(capacity));
                arena.QueueRelease(reclaim, generation + 1); arena.FlushHandleCommands(frame + 14);
            }
        }

        [Test]
        public void MultiRecordAllocationPreservesDescriptorStrideAndIdentity()
        {
            var arena = Create(handles: 2);
            var descriptors = new GpuSurfaceExtractor.BatchChunkDescriptor[2];
            for (int record = 0; record < descriptors.Length; record++)
            {
                Assert.That(arena.TryAcquireHandle(out int handle), Is.True);
                ulong generation = 0x0000000200000003UL + (uint)record;
                arena.QueueGeneration(handle, generation);
                descriptors[record] = new GpuSurfaceExtractor.BatchChunkDescriptor
                {
                    OriginX = record * -16, SourceStep = record + 1, VoxelSize = 0.1f,
                    Handle = (uint)handle, GenerationLow = (uint)generation,
                    GenerationHigh = (uint)(generation >> 32), ProfileStart = 0, ProfileCount = 0,
                };
            }
            arena.FlushHandleCommands(1);
            using var chunks = new ComputeBuffer(2, GpuSurfaceExtractor.BatchChunkDescriptor.Stride);
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords + 2 * GpuSurfaceExtractor.BatchRecordWords, sizeof(uint));
            chunks.SetData(descriptors);
            var words = new uint[counters.count];
            for (int record = 0; record < 2; record++)
            {
                int word = GpuSurfaceExtractor.BatchHeaderWords + record * GpuSurfaceExtractor.BatchRecordWords;
                words[word + 2] = 4;
                words[word + 3] = 6;
            }
            counters.SetData(words);
            arena.AllocateBatch(chunks, counters, 2, GpuSurfaceExtractor.BatchRecordWords, 2);
            counters.GetData(words); // bounded test-only verification, after production dispatch
            for (int record = 0; record < 2; record++)
            {
                int word = GpuSurfaceExtractor.BatchHeaderWords + record * GpuSurfaceExtractor.BatchRecordWords;
                var descriptor = descriptors[record];
                Assert.That(words[word + 11], Is.EqualTo(descriptor.Handle), $"Record {record} handle");
                Assert.That(words[word + 12], Is.EqualTo(descriptor.GenerationLow), $"Record {record} generation low");
                Assert.That(words[word + 13], Is.EqualTo(descriptor.GenerationHigh), $"Record {record} generation high");
                Assert.That(words[word + 10], Is.EqualTo(AllocationReady), $"Record {record} status");
                var pending = ReadRecord(arena.PendingChunkGeometry, (int)descriptor.Handle, arena.HandleCapacity);
                Assert.That(pending.Ready, Is.EqualTo(1u));
                Assert.That(pending.VertexCount, Is.EqualTo(4u));
                Assert.That(pending.IndexCount, Is.EqualTo(6u));
            }
        }

        [UnityTest]
        public IEnumerator CompactOutcomeReportsExhaustionThenSuccessfulRetry()
        {
            Assert.IsTrue(SystemInfo.supportsAsyncGPUReadback);
            var arena = Create(handles: 2, vertexPages: 1, indexPages: 1);
            const ulong generation = 0x0000000200000003UL;
            int occupied = AcquireAndSelectGeneration(arena, generation, 1);
            int retry = AcquireAndSelectGeneration(arena, generation + 1, 1);
            using var first = new Batch(occupied, generation, 4, 6);
            using var second = new Batch(retry, generation + 1, 4, 6);
            using var feedback = new ComputeBuffer(1, sizeof(uint) * 4, ComputeBufferType.Structured);
            AllocateAndFinalize(arena, first, 2);
            var expected = new GpuChunkExtraction(default, default, 1, 0.1f,
                handle: retry, generation: generation + 1);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt == 1)
                {
                    arena.QueueRelease(occupied, generation);
                    arena.FlushHandleCommands(20);
                }
                arena.AllocateBatch(second.Descriptors, second.Counters, 1,
                    GpuSurfaceExtractor.BatchRecordWords, attempt == 0 ? 4 : 25);
                arena.PublishBatch(second.Descriptors, second.Counters, 1,
                    GpuSurfaceExtractor.BatchRecordWords, attempt == 0 ? 5 : 26);
                arena.CopyBatchOutcomes(second.Counters, feedback, 1,
                    GpuSurfaceExtractor.BatchRecordWords);
                var request = AsyncGPUReadback.Request(feedback);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!request.done && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(request.done, "Bounded asynchronous status transfer did not complete.");
                Assert.IsFalse(request.hasError);
                var words = request.GetData<uint>().ToArray();
                Assert.AreEqual(4, words.Length, "Only status and identity may leave the GPU.");
                var outcome = GpuPagedBatchOutcome.ParseCompact(words, 0, expected);
                Assert.AreEqual(attempt == 0 ? GpuPagedBatchOutcomeKind.Exhausted
                    : GpuPagedBatchOutcomeKind.ReadyCandidate, outcome.Kind);
            }
        }

        [TestCase(0u, 0u)]
        [TestCase(11u, 18u)]
        [TestCase(12u, 17u)]
        [TestCase(13u, 18u)]
        [TestCase(12u, 19u)]
        public void IncompleteOrOverflowingWriteCannotReplaceLiveGeometry(uint writtenVertices, uint writtenIndices)
        {
            var arena = Create(handles: 1, vertexPages: 4, indexPages: 4);
            int handle = AcquireAndSelectGeneration(arena, 40UL, 1);
            using var live = new Batch(handle, 40UL, 4, 6);
            AllocateAndFinalize(arena, live, 2);
            arena.CommitPending(handle, 40UL, 4);
            arena.QueueGeneration(handle, 41UL);
            arena.FlushHandleCommands(5);
            using var replacement = new Batch(handle, 41UL, 12, 18);
            int word = GpuSurfaceExtractor.BatchHeaderWords;
            replacement.Words[word + 8] = writtenVertices;
            replacement.Words[word + 9] = writtenIndices;
            replacement.Counters.SetData(replacement.Words);
            arena.AllocateBatch(replacement.Descriptors, replacement.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 6);
            Assert.AreEqual(AllocationReady, replacement.ReadAllocationStatus());
            arena.PublishBatch(replacement.Descriptors, replacement.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, 7);
            Assert.AreEqual(5u, replacement.ReadAllocationStatus(),
                "A count/write mismatch must be reported as a failed write.");
            arena.CommitPending(handle, 41UL, 8);
            arena.AbortPending(handle, 41UL, 9); // repeated cleanup must be harmless
            Assert.AreEqual(0u, ReadRecord(arena.PendingChunkGeometry, handle, 1).Ready);
            Assert.AreEqual(40UL, ReadRecord(arena.LiveChunkGeometry, handle, 1).Generation);
            Assert.AreEqual(1u, ReadRecord(arena.LiveChunkGeometry, handle, 1).Ready);
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
                "Arena exhaustion is a retryable GPU result, not successful completion. "
                + DescribeAllocation(exhausted, second));
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
              + "explicit supported action instead of retrying an impossible allocation forever. "
                + DescribeAllocation(oversized, handle));
            Assert.That(ReadRecord(
                arena.PendingChunkGeometry, handle, arena.HandleCapacity).Ready, Is.Zero);
        }
    }
}
