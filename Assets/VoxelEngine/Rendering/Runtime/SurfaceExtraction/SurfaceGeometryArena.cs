using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Stable ranges inside the renderer's shared GPU geometry buffers. A lease is immutable
    /// while published; replacement geometry acquires a second lease and swaps only after its
    /// entire payload has uploaded.
    /// </summary>
    internal readonly struct SurfaceGeometryLease
    {
        public readonly int VertexStart;
        public readonly int VertexCapacity;
        public readonly int IndexStart;
        public readonly int IndexCapacity;
        public readonly int ArgsWordStart;

        public SurfaceGeometryLease(int vertexStart, int vertexCapacity,
                                    int indexStart, int indexCapacity, int argsWordStart)
        {
            VertexStart = vertexStart;
            VertexCapacity = vertexCapacity;
            IndexStart = indexStart;
            IndexCapacity = indexCapacity;
            ArgsWordStart = argsWordStart;
        }

        public bool IsValid => VertexCapacity > 0 && IndexCapacity > 0 && ArgsWordStart >= 0;
    }

    /// <summary>
    /// One eagerly allocated vertex/index/indirect-args arena shared by every solid surface
    /// worker. Streaming never creates a ComputeBuffer: if a replacement cannot obtain a range,
    /// publication waits and the previous ready geometry remains live until space is reclaimed.
    /// </summary>
    internal sealed class SurfaceGeometryArena : IDisposable
    {
        public const int ArgsWordsPerDraw = 4;
        private const int VertexAlignment = 256;
        private const int IndexAlignment = 512;

        private readonly RangeAllocator _vertexRanges;
        private readonly RangeAllocator _indexRanges;
        private readonly RangeAllocator _argsRanges;
        private NativeArray<uint> _argsScratch;
        private bool _disposed;

        public ComputeBuffer Vertices { get; }
        public ComputeBuffer Indices { get; }
        public ComputeBuffer Args { get; }
        public int VertexCapacity { get; }
        public int IndexCapacity { get; }
        public int ArgsRecordCapacity { get; }
        public int UsedVertices => _vertexRanges.Used;
        public int UsedIndices => _indexRanges.Used;
        public int UsedArgsRecords => _argsRanges.Used / ArgsWordsPerDraw;
        private int _maxActiveLeases = int.MaxValue;
        /// <summary>
        /// Soft publication-pressure ceiling. This never resizes the GPU buffers; it only makes
        /// new staging leases observe backpressure once the configured number of live/staging
        /// draws is reached. Production defaults to unlimited relative to the fixed arena.
        /// </summary>
        public int MaxActiveLeases
        {
            get => _maxActiveLeases;
            set => _maxActiveLeases = math.max(1, value);
        }
        public ulong AllocationFailureCount { get; private set; }
        public long UsedGpuBytes =>
            (long)UsedVertices * SmoothSurfaceVertex.Stride
            + (long)UsedIndices * sizeof(uint)
            + (long)UsedArgsRecords * ArgsWordsPerDraw * sizeof(uint);
        public long CommittedGpuBytes =>
            (long)VertexCapacity * SmoothSurfaceVertex.Stride
            + (long)IndexCapacity * sizeof(uint)
            + (long)ArgsRecordCapacity * ArgsWordsPerDraw * sizeof(uint);

        public SurfaceGeometryArena(int vertexCapacity, int indexCapacity, int argsRecordCapacity)
        {
            if (vertexCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(vertexCapacity));
            if (indexCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(indexCapacity));
            if (argsRecordCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(argsRecordCapacity));

            VertexCapacity = vertexCapacity;
            IndexCapacity = indexCapacity;
            ArgsRecordCapacity = argsRecordCapacity;
            _vertexRanges = new RangeAllocator(vertexCapacity, 4096);
            _indexRanges = new RangeAllocator(indexCapacity, 4096);
            _argsRanges = new RangeAllocator(argsRecordCapacity * ArgsWordsPerDraw, 4096);
            _argsScratch = new NativeArray<uint>(ArgsWordsPerDraw, Allocator.Persistent,
                                                 NativeArrayOptions.UninitializedMemory);

            ComputeBuffer vertices = null;
            ComputeBuffer indices = null;
            ComputeBuffer args = null;
            try
            {
                vertices = new ComputeBuffer(vertexCapacity, SmoothSurfaceVertex.Stride,
                                             ComputeBufferType.Structured);
                indices = new ComputeBuffer(indexCapacity, sizeof(uint),
                                            ComputeBufferType.Structured);
                args = new ComputeBuffer(argsRecordCapacity * ArgsWordsPerDraw, sizeof(uint),
                                         ComputeBufferType.IndirectArguments);
                Vertices = vertices;
                Indices = indices;
                Args = args;
            }
            catch
            {
                vertices?.Release();
                indices?.Release();
                args?.Release();
                throw;
            }
        }

        public bool TryAcquire(int vertexCount, int indexCount, out SurfaceGeometryLease lease)
        {
            lease = default;
            if (_disposed) return false;
            if (UsedArgsRecords >= _maxActiveLeases)
            {
                AllocationFailureCount++;
                return false;
            }

            int vertices = Align(math.max(1, vertexCount), VertexAlignment);
            int indices = Align(math.max(1, indexCount), IndexAlignment);
            if (!_vertexRanges.TryAllocate(vertices, out int vertexStart))
            {
                AllocationFailureCount++;
                return false;
            }
            if (!_indexRanges.TryAllocate(indices, out int indexStart))
            {
                _vertexRanges.Release(vertexStart, vertices);
                AllocationFailureCount++;
                return false;
            }
            if (!_argsRanges.TryAllocate(ArgsWordsPerDraw, out int argsStart))
            {
                _indexRanges.Release(indexStart, indices);
                _vertexRanges.Release(vertexStart, vertices);
                AllocationFailureCount++;
                return false;
            }

            lease = new SurfaceGeometryLease(vertexStart, vertices, indexStart, indices, argsStart);
            return true;
        }

        public void Release(in SurfaceGeometryLease lease)
        {
            if (!lease.IsValid || _disposed) return;
            _vertexRanges.Release(lease.VertexStart, lease.VertexCapacity);
            _indexRanges.Release(lease.IndexStart, lease.IndexCapacity);
            _argsRanges.Release(lease.ArgsWordStart, ArgsWordsPerDraw);
        }

        public void UploadVertices(NativeArray<SmoothSurfaceVertex> source, int sourceStart,
                                   in SurfaceGeometryLease lease, int count)
        {
            Vertices.SetData(source, sourceStart, lease.VertexStart + sourceStart, count);
        }

        public void UploadIndices(NativeArray<uint> source, int sourceStart,
                                  in SurfaceGeometryLease lease, int count)
        {
            Indices.SetData(source, sourceStart, lease.IndexStart + sourceStart, count);
        }

        public void UploadArgs(uint indexCount, in SurfaceGeometryLease lease)
        {
            _argsScratch[0] = indexCount;
            _argsScratch[1] = 1u;
            _argsScratch[2] = 0u;
            _argsScratch[3] = 0u;
            Args.SetData(_argsScratch, 0, lease.ArgsWordStart, ArgsWordsPerDraw);
        }

        public long ReservedBytes(in SurfaceGeometryLease lease) => !lease.IsValid ? 0L :
            (long)lease.VertexCapacity * SmoothSurfaceVertex.Stride
            + (long)lease.IndexCapacity * sizeof(uint)
            + ArgsWordsPerDraw * sizeof(uint);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Vertices?.Release();
            Indices?.Release();
            Args?.Release();
            if (_argsScratch.IsCreated) _argsScratch.Dispose();
        }

        private static int Align(int value, int alignment) =>
            ((value + alignment - 1) / alignment) * alignment;

        private sealed class RangeAllocator
        {
            private struct Range
            {
                public int Start;
                public int Count;

                public Range(int start, int count)
                {
                    Start = start;
                    Count = count;
                }
            }

            private readonly List<Range> _free;
            private readonly int _capacity;
            public int Used { get; private set; }

            public RangeAllocator(int capacity, int expectedFragments)
            {
                _capacity = capacity;
                _free = new List<Range>(math.max(1, expectedFragments))
                {
                    new Range(0, capacity)
                };
            }

            public bool TryAllocate(int count, out int start)
            {
                for (int i = 0; i < _free.Count; i++)
                {
                    Range range = _free[i];
                    if (range.Count < count) continue;

                    start = range.Start;
                    range.Start += count;
                    range.Count -= count;
                    if (range.Count == 0) _free.RemoveAt(i);
                    else _free[i] = range;
                    Used += count;
                    return true;
                }

                start = -1;
                return false;
            }

            public void Release(int start, int count)
            {
                if (count <= 0) return;
                if (start < 0 || start + count > _capacity)
                    throw new ArgumentOutOfRangeException(nameof(start));

                int insert = 0;
                while (insert < _free.Count && _free[insert].Start < start) insert++;
                _free.Insert(insert, new Range(start, count));
                Used -= count;

                int first = math.max(0, insert - 1);
                for (int i = first; i < _free.Count - 1;)
                {
                    Range left = _free[i];
                    Range right = _free[i + 1];
                    if (left.Start + left.Count < right.Start)
                    {
                        i++;
                        continue;
                    }

                    int end = math.max(left.Start + left.Count, right.Start + right.Count);
                    left.Count = end - left.Start;
                    _free[i] = left;
                    _free.RemoveAt(i + 1);
                }
            }
        }
    }
}
