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
    /// Allocation-free current-frame diagnostic for writes into the shared solid geometry arena.
    /// The moving-player hitch investigation needs the wall time of the actual BeginWrite/copy/
    /// EndWrite calls, not a rolling per-worker percentile. Publication is main-thread-only, so a
    /// simple primitive accumulator is enough and does not need locking or managed containers.
    /// </summary>
    public readonly struct SurfaceGeometryUploadFrameSnapshot
    {
        public readonly int Frame;
        public readonly double WallMs;
        public readonly int Calls;
        public readonly long Bytes;

        public SurfaceGeometryUploadFrameSnapshot(int frame, double wallMs, int calls, long bytes)
        {
            Frame = frame;
            WallMs = wallMs;
            Calls = calls;
            Bytes = bytes;
        }
    }

    public static class SurfaceGeometryUploadTelemetry
    {
        private static int s_Frame = -1;
        private static double s_WallMs;
        private static int s_Calls;
        private static long s_Bytes;

        public static SurfaceGeometryUploadFrameSnapshot Snapshot =>
            new(s_Frame, s_WallMs, s_Calls, s_Bytes);

        internal static void BeginFrame(int frame)
        {
            if (s_Frame == frame) return;
            s_Frame = frame;
            s_WallMs = 0.0;
            s_Calls = 0;
            s_Bytes = 0;
        }

        internal static void Add(double wallMs, long bytes)
        {
            if (wallMs > 0.0) s_WallMs += wallMs;
            s_Calls++;
            if (bytes > 0) s_Bytes += bytes;
        }
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


        /// <summary>
        /// Frames a released range is quarantined before it may be handed out again. Unity queues
        /// at most <c>QualitySettings.maxQueuedFrames</c> frames ahead of the GPU; three covers
        /// the default of two with a frame in hand.
        /// </summary>
        private const int LeaseRetirementFrames = 3;

        private readonly struct PendingRelease
        {
            public readonly SurfaceGeometryLease Lease;
            public readonly int Frame;

            public PendingRelease(in SurfaceGeometryLease lease, int frame)
            {
                Lease = lease;
                Frame = frame;
            }
        }

        private readonly RangeAllocator _vertexRanges;
        private readonly RangeAllocator _indexRanges;
        private readonly RangeAllocator _argsRanges;
        private readonly Queue<PendingRelease> _pendingRelease = new();
        private int _frame;
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
                // SubUpdates, not the default immutable mode. These buffers are written a chunk
                // at a time while the GPU is drawing from them, and SetData on a buffer the GPU
                // holds forces the driver to rename it — to copy the whole allocation so the
                // write cannot be observed mid-frame. That makes an upload cost O(buffer size)
                // rather than O(bytes written): at a 1.28 GB arena the full showcase measured
                // p50 10.4 ms and p95 95.4 ms, and the same work against a 96 MB arena measured
                // p50 2.9 ms and p95 3.1 ms. SubUpdates gives a persistently mapped range and
                // BeginWrite/EndWrite write into it directly, with no rename at any size.
                vertices = new ComputeBuffer(vertexCapacity, SmoothSurfaceVertex.Stride,
                                             ComputeBufferType.Structured,
                                             ComputeBufferMode.SubUpdates);
                indices = new ComputeBuffer(indexCapacity, sizeof(uint),
                                            ComputeBufferType.Structured,
                                            ComputeBufferMode.SubUpdates);
                args = new ComputeBuffer(argsRecordCapacity * ArgsWordsPerDraw, sizeof(uint),
                                         ComputeBufferType.IndirectArguments,
                                         ComputeBufferMode.SubUpdates);
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

        /// <summary>
        /// Retires a lease, but not before the GPU can still be reading it.
        ///
        /// <para>A chunk publishes by swapping a freshly written staging lease over its live one
        /// and releasing the old range. The GPU is still rendering earlier frames whose draws
        /// reference that range, so reusing it immediately would let a new chunk's geometry
        /// overwrite triangles being read.</para>
        ///
        /// <para>This did not matter while uploads went through <c>SetData</c>: writing to a
        /// buffer the GPU held forced the driver to rename it, and the in-flight read kept the
        /// old copy. That protection was incidental, and it cost O(buffer size) per upload — the
        /// reason this arena moved to <see cref="ComputeBufferMode.SubUpdates"/>. Persistently
        /// mapped writes land in memory the GPU is reading, so the delay has to be explicit.</para>
        /// </summary>
        public void Release(in SurfaceGeometryLease lease)
        {
            if (!lease.IsValid || _disposed) return;
            _pendingRelease.Enqueue(new PendingRelease(lease, _frame));
        }

        /// <summary>
        /// Returns ranges retired far enough in the past that no in-flight frame can reference
        /// them. Call once per world frame, before any acquisition.
        /// </summary>
        public void RetireExpiredLeases(int frame)
        {
            _frame = frame;
            SurfaceGeometryUploadTelemetry.BeginFrame(frame);
            while (_pendingRelease.Count > 0)
            {
                PendingRelease pending = _pendingRelease.Peek();
                if (frame - pending.Frame < LeaseRetirementFrames) break;
                _pendingRelease.Dequeue();
                ReleaseImmediate(in pending.Lease);
            }
        }

        private void ReleaseImmediate(in SurfaceGeometryLease lease)
        {
            if (!lease.IsValid) return;
            _vertexRanges.Release(lease.VertexStart, lease.VertexCapacity);
            _indexRanges.Release(lease.IndexStart, lease.IndexCapacity);
            _argsRanges.Release(lease.ArgsWordStart, ArgsWordsPerDraw);
        }

        public void UploadVertices(NativeArray<SmoothSurfaceVertex> source, int sourceStart,
                                   in SurfaceGeometryLease lease, int count)
        {
            if (count <= 0) return;
            double start = Time.realtimeSinceStartupAsDouble;
            NativeArray<SmoothSurfaceVertex> destination =
                Vertices.BeginWrite<SmoothSurfaceVertex>(lease.VertexStart + sourceStart, count);
            NativeArray<SmoothSurfaceVertex>.Copy(source, sourceStart, destination, 0, count);
            Vertices.EndWrite<SmoothSurfaceVertex>(count);
            SurfaceGeometryUploadTelemetry.Add(
                (Time.realtimeSinceStartupAsDouble - start) * 1000.0,
                (long)count * SmoothSurfaceVertex.Stride);
        }

        public void UploadIndices(NativeArray<uint> source, int sourceStart,
                                  in SurfaceGeometryLease lease, int count)
        {
            if (count <= 0) return;
            double start = Time.realtimeSinceStartupAsDouble;
            NativeArray<uint> destination =
                Indices.BeginWrite<uint>(lease.IndexStart + sourceStart, count);
            NativeArray<uint>.Copy(source, sourceStart, destination, 0, count);
            Indices.EndWrite<uint>(count);
            SurfaceGeometryUploadTelemetry.Add(
                (Time.realtimeSinceStartupAsDouble - start) * 1000.0,
                (long)count * sizeof(uint));
        }

        /// <summary>
        /// Publishes a lease's draw record.
        ///
        /// The record carries the chunk's index base as the draw's start-vertex, and the page table
        /// carries its vertex base. Between them the draw needs no per-chunk material state, which
        /// is what lets the pass submit every visible chunk without copying a property block each
        /// time — the cost that dominated the frame when it did.
        /// </summary>
        public void UploadArgs(uint indexCount, in SurfaceGeometryLease lease)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            NativeArray<uint> destination =
                Args.BeginWrite<uint>(lease.ArgsWordStart, ArgsWordsPerDraw);
            destination[0] = indexCount;
            destination[1] = 1u;
            destination[2] = 0u;
            destination[3] = 0u;
            Args.EndWrite<uint>(ArgsWordsPerDraw);
            SurfaceGeometryUploadTelemetry.Add(
                (Time.realtimeSinceStartupAsDouble - start) * 1000.0,
                ArgsWordsPerDraw * sizeof(uint));
        }

        public long ReservedBytes(in SurfaceGeometryLease lease) => !lease.IsValid ? 0L :
            (long)lease.VertexCapacity * SmoothSurfaceVertex.Stride
            + (long)lease.IndexCapacity * sizeof(uint)
            + ArgsWordsPerDraw * sizeof(uint);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pendingRelease.Clear();
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
