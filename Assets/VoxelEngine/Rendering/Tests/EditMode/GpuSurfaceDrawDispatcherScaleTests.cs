using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSurfaceDrawDispatcherScaleTests
    {
        private const int VisibleCount = 600;

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

        [StructLayout(LayoutKind.Sequential)]
        private struct DrawMetadata
        {
            public uint Handle;
            public uint IndexCount;
            public uint Bank;
            public uint Padding;
        }

        private ComputeShader _arenaShader;
        private ComputeShader _drawShader;
        private GpuSurfacePageArena _arena;
        private GpuSurfaceDrawDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            _arenaShader = UnityEngine.Object.Instantiate(
                Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            _drawShader = UnityEngine.Object.Instantiate(
                Resources.Load<ComputeShader>("GpuSurfaceDrawCompact"));
            Assert.That(_arenaShader, Is.Not.Null);
            Assert.That(_drawShader, Is.Not.Null);
            _arena = new GpuSurfacePageArena(
                _arenaShader,
                GpuSurfacePageArena.VertexPageSize * 2,
                GpuSurfacePageArena.IndexPageSize * 2,
                handleCapacity: 1024);
            _dispatcher = new GpuSurfaceDrawDispatcher(_drawShader, _arena);
        }

        [TearDown]
        public void TearDown()
        {
            // Reading the tiny args buffer drains the test's compute dispatches before disposal.
            if (_dispatcher?.ActiveIndirectArgs != null)
            {
                var drain = new uint[GpuSurfaceDrawDispatcher.BucketCount * 4];
                _dispatcher.ActiveIndirectArgs.GetData(drain);
            }
            _dispatcher?.Dispose();
            _arena?.Dispose();
            if (_drawShader != null) UnityEngine.Object.DestroyImmediate(_drawShader);
            if (_arenaShader != null) UnityEngine.Object.DestroyImmediate(_arenaShader);
            _dispatcher = null;
            _arena = null;
            _drawShader = null;
            _arenaShader = null;
        }

        [Test]
        public void SixHundredVisibleHandlesSurviveBucketPrefixAndScatterExactlyOnce()
        {
            var records = new GeometryRecord[_arena.HandleCapacity];
            var visible = new List<int>(VisibleCount);
            var expectedCounts = new uint[_arena.HandleCapacity];
            for (int handle = 0; handle < VisibleCount; handle++)
            {
                // Spread the workload across many logarithmic buckets instead of validating a
                // degenerate single-bucket fixture. Counts stay triangle-aligned but otherwise
                // vary enough to exercise bucket prefixes and nonzero startInstance values.
                uint exponent = (uint)(4 + handle % 13);
                uint lower = 1u << (int)exponent;
                uint quarter = Math.Max(1u, lower / 4u);
                uint raw = lower + (uint)(handle % 4) * quarter + (uint)(handle % 17);
                uint indexCount = Math.Max(3u, raw - raw % 3u);
                records[handle] = new GeometryRecord
                {
                    GenerationLow = (uint)(handle + 1),
                    GenerationHigh = 0u,
                    Bank = (uint)(handle & 1),
                    VertexCount = indexCount / 2u + 8u,
                    IndexCount = indexCount,
                    VertexPageCount = 1u,
                    IndexPageCount = 1u,
                    Ready = 1u,
                };
                expectedCounts[handle] = indexCount;
                visible.Add(handle);
            }
            _arena.LiveChunkGeometry.SetData(records);

            _dispatcher.Prepare(visible, frame: 7);

            var args = new uint[GpuSurfaceDrawDispatcher.BucketCount * 4];
            var metadata = new DrawMetadata[_arena.HandleCapacity];
            _dispatcher.ActiveIndirectArgs.GetData(args);
            _dispatcher.ActiveDrawMetadata.GetData(metadata);

            int totalInstances = 0;
            int expectedStart = 0;
            var seen = new bool[_arena.HandleCapacity];
            int nonEmptyBuckets = 0;
            for (int bucket = 0; bucket < GpuSurfaceDrawDispatcher.BucketCount; bucket++)
            {
                int word = bucket * 4;
                uint maxIndexCount = args[word + 0];
                int instanceCount = unchecked((int)args[word + 1]);
                uint startVertex = args[word + 2];
                int startInstance = unchecked((int)args[word + 3]);
                Assert.That(startVertex, Is.Zero);
                Assert.That(startInstance, Is.EqualTo(expectedStart),
                    $"bucket {bucket} did not point at its scattered metadata prefix");
                if (instanceCount > 0)
                {
                    nonEmptyBuckets++;
                    Assert.That(maxIndexCount, Is.GreaterThan(0u));
                }

                for (int i = 0; i < instanceCount; i++)
                {
                    DrawMetadata draw = metadata[startInstance + i];
                    Assert.That(draw.Handle, Is.LessThan((uint)VisibleCount));
                    int handle = unchecked((int)draw.Handle);
                    Assert.That(seen[handle], Is.False,
                        $"handle {handle} was scattered more than once");
                    seen[handle] = true;
                    Assert.That(draw.IndexCount, Is.EqualTo(expectedCounts[handle]));
                    Assert.That(draw.Bank, Is.EqualTo((uint)(handle & 1)));
                    Assert.That(draw.IndexCount, Is.LessThanOrEqualTo(maxIndexCount));
                }

                totalInstances += instanceCount;
                expectedStart += instanceCount;
            }

            Assert.That(nonEmptyBuckets, Is.GreaterThan(8),
                "Fixture must exercise many indirect bucket start-instance offsets.");
            Assert.That(totalInstances, Is.EqualTo(VisibleCount));
            Assert.That(expectedStart, Is.EqualTo(VisibleCount));
            for (int handle = 0; handle < VisibleCount; handle++)
                Assert.That(seen[handle], Is.True, $"handle {handle} disappeared during compaction");
        }
    }
}
