using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    [NonParallelizable]
    public sealed class GpuPendingPublicationPumpTests
    {
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
            private readonly uint[] _words;

            internal Batch(int handle, ulong generation, uint unsupported = 0u)
            {
                Descriptors = new ComputeBuffer(
                    1, GpuSurfaceExtractor.BatchChunkDescriptor.Stride,
                    ComputeBufferType.Structured);
                Counters = new ComputeBuffer(
                    GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords,
                    sizeof(uint), ComputeBufferType.Structured);
                Descriptors.SetData(new[]
                {
                    new GpuSurfaceExtractor.BatchChunkDescriptor
                    {
                        OriginX = 0,
                        OriginY = 0,
                        OriginZ = 0,
                        SourceStep = 1,
                        VoxelSize = 0.1f,
                        Handle = unchecked((uint)handle),
                        GenerationLow = (uint)generation,
                        GenerationHigh = (uint)(generation >> 32),
                    }
                });
                _words = new uint[
                    GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords];
                int word = GpuSurfaceExtractor.BatchHeaderWords;
                _words[word + 0] = unsupported;
                _words[word + 2] = 12u;
                _words[word + 3] = 18u;
                Counters.SetData(_words);
            }

            internal uint Status
            {
                get
                {
                    Counters.GetData(_words);
                    return _words[GpuSurfaceExtractor.BatchHeaderWords + 10];
                }
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
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            _shader = UnityEngine.Object.Instantiate(
                Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            Assert.That(_shader, Is.Not.Null);
            _arena = new GpuSurfacePageArena(
                _shader,
                GpuSurfacePageArena.VertexPageSize * 4,
                GpuSurfacePageArena.IndexPageSize * 4,
                handleCapacity: 2);
        }

        [TearDown]
        public void TearDown()
        {
            if (_arena != null)
            {
                // Tiny bookkeeping readback only; this drains test dispatches before disposal.
                var drain = new GeometryRecord[_arena.HandleCapacity];
                _arena.LiveChunkGeometry.GetData(drain);
                _arena.Dispose();
            }
            if (_shader != null) UnityEngine.Object.DestroyImmediate(_shader);
            _arena = null;
            _shader = null;
        }

        private int Acquire(ulong generation, int frame)
        {
            Assert.That(_arena.TryAcquireHandle(out int handle), Is.True);
            _arena.QueueGeneration(handle, generation);
            _arena.FlushHandleCommands(frame);
            return handle;
        }

        private void AllocateAndFinalize(Batch batch, int frame)
        {
            _arena.AllocateBatch(
                batch.Descriptors, batch.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame);
            _arena.PublishBatch(
                batch.Descriptors, batch.Counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame + 1);
        }

        private GeometryRecord Read(ComputeBuffer buffer, int handle)
        {
            var values = new GeometryRecord[_arena.HandleCapacity];
            buffer.GetData(values);
            return values[handle];
        }

        [Test]
        public void FrameBoundaryPumpCommitsOnlyAfterFinalization()
        {
            const ulong generation = 0x100000002UL;
            int handle = Acquire(generation, frame: 1);
            using var batch = new Batch(handle, generation);
            AllocateAndFinalize(batch, frame: 2);

            Assert.That(batch.Status, Is.EqualTo(GpuPagedBatchOutcome.AllocationReady));
            Assert.That(Read(_arena.PendingChunkGeometry, handle).Ready, Is.EqualTo(1u));
            Assert.That(Read(_arena.LiveChunkGeometry, handle).Ready, Is.Zero,
                "Finalization itself must not publish the candidate.");

            GpuSurfacePageArena.CommitCurrentPendingForActiveArena(frame: 4);

            GeometryRecord live = Read(_arena.LiveChunkGeometry, handle);
            Assert.That(live.Ready, Is.EqualTo(1u));
            Assert.That(live.Generation, Is.EqualTo(generation));
            Assert.That(Read(_arena.PendingChunkGeometry, handle).Ready, Is.Zero);
        }

        [Test]
        public void SupersededDesiredGenerationCannotBePublishedByPump()
        {
            const ulong generationA = 10UL;
            const ulong generationB = 11UL;
            int handle = Acquire(generationA, frame: 5);
            using var batch = new Batch(handle, generationA);
            AllocateAndFinalize(batch, frame: 6);
            Assert.That(Read(_arena.PendingChunkGeometry, handle).Ready, Is.EqualTo(1u));

            _arena.QueueGeneration(handle, generationB);
            _arena.FlushHandleCommands(frame: 8);
            GpuSurfacePageArena.CommitCurrentPendingForActiveArena(frame: 9);

            Assert.That(Read(_arena.LiveChunkGeometry, handle).Ready, Is.Zero,
                "A pending candidate may not become live after the handle's desired generation changes.");
            Assert.That(Read(_arena.PendingChunkGeometry, handle).Ready, Is.EqualTo(1u),
                "Superseded candidate remains owned until the next allocation or explicit abort releases it.");
        }

        [Test]
        public void UnsupportedSemanticResultNeverAllocatesPendingGeometry()
        {
            const ulong generation = 20UL;
            int handle = Acquire(generation, frame: 10);
            using var batch = new Batch(handle, generation, unsupported: 1u);
            AllocateAndFinalize(batch, frame: 11);

            Assert.That(batch.Status, Is.EqualTo(GpuPagedBatchOutcome.AllocationUnsupported));
            Assert.That(Read(_arena.PendingChunkGeometry, handle).Ready, Is.Zero);
            GpuSurfacePageArena.CommitCurrentPendingForActiveArena(frame: 13);
            Assert.That(Read(_arena.LiveChunkGeometry, handle).Ready, Is.Zero);
        }
    }
}
