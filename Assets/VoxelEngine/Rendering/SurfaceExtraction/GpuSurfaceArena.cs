using System;
using UnityEngine;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// One preallocated home for every resident surface chunk's geometry.
    ///
    /// Chunks used to own their buffers: a vertex, index and indirect-args buffer each, created on
    /// first extraction and released on eviction. At full residency that is several thousand live
    /// <see cref="ComputeBuffer"/> objects, churning as the camera moves, with a total footprint
    /// nobody declared anywhere. On a unified-memory machine that is precisely the shape of
    /// allocation this project has been burned by before, and it is invisible until it is not.
    ///
    /// Here the footprint is stated once, up front, and never moves: slots are handed out from a
    /// free list and returned on eviction. Nothing allocates while the camera flies. Because every
    /// chunk now indexes into the same buffers, the index data is written in arena-global vertex
    /// numbers, which also leaves a single compacted draw open as a later step.
    /// </summary>
    public sealed class GpuSurfaceArena : IDisposable
    {
        /// <summary>Indirect draw arguments are four uints per chunk.</summary>
        public const int ArgsStride = 4;
        public const long MaxArenaBytes = 288L * 1024L * 1024L;

        private readonly int[] _freeSlots;
        private int _freeCount;

        public GpuSurfaceArena(int slotCount, int cellsPerChunk, int indicesPerChunk)
        {
            if (slotCount < 1) throw new ArgumentOutOfRangeException(nameof(slotCount));
            if (cellsPerChunk < 1) throw new ArgumentOutOfRangeException(nameof(cellsPerChunk));
            if (indicesPerChunk < 6) throw new ArgumentOutOfRangeException(nameof(indicesPerChunk));

            SlotCount = slotCount;
            CellsPerChunk = cellsPerChunk;
            // Quads are six indices; a partial quad can never be drawn.
            IndicesPerChunk = indicesPerChunk / 6 * 6;

            long requestedBytes = ComputeByteSize(slotCount, cellsPerChunk, IndicesPerChunk);
            if (requestedBytes > MaxArenaBytes)
                throw new ArgumentOutOfRangeException(nameof(slotCount),
                    $"GPU surface arena requests {requestedBytes / (1024 * 1024)} MiB; " +
                    $"the hard limit is {MaxArenaBytes / (1024 * 1024)} MiB.");

            try
            {
                Vertices = new ComputeBuffer(checked(slotCount * cellsPerChunk),
                                             SmoothSurfaceVertex.Stride,
                                             ComputeBufferType.Structured);
                Indices = new ComputeBuffer(checked(slotCount * IndicesPerChunk), sizeof(uint),
                                            ComputeBufferType.Structured);
                Args = new ComputeBuffer(checked(slotCount * ArgsStride), sizeof(uint),
                                         ComputeBufferType.IndirectArguments);
            }
            catch
            {
                Vertices?.Release();
                Indices?.Release();
                Args?.Release();
                Vertices = null;
                Indices = null;
                Args = null;
                throw;
            }

            // Every slot starts as an empty, valid draw. An unextracted slot that is somehow
            // drawn then renders nothing rather than reading uninitialised counts.
            var emptyArgs = new uint[slotCount * ArgsStride];
            for (int slot = 0; slot < slotCount; slot++)
                emptyArgs[slot * ArgsStride + 1] = 1u; // instance count
            try
            {
                Args.SetData(emptyArgs);
            }
            catch
            {
                Vertices?.Release();
                Indices?.Release();
                Args?.Release();
                Vertices = null;
                Indices = null;
                Args = null;
                throw;
            }

            _freeSlots = new int[slotCount];
            for (int i = 0; i < slotCount; i++) _freeSlots[i] = slotCount - 1 - i;
            _freeCount = slotCount;
        }

        public ComputeBuffer Vertices { get; private set; }
        public ComputeBuffer Indices { get; private set; }
        public ComputeBuffer Args { get; private set; }

        public int SlotCount { get; }
        public int CellsPerChunk { get; }
        public int IndicesPerChunk { get; }
        public int FreeSlots => _freeCount;

        /// <summary>Bytes held by this arena. Stated so a budget can be asserted against it.</summary>
        public long ByteSize => ComputeByteSize(SlotCount, CellsPerChunk, IndicesPerChunk);

        public static long ComputeByteSize(int slotCount, int cellsPerChunk, int indicesPerChunk)
            => checked((long)slotCount
                     * ((long)cellsPerChunk * SmoothSurfaceVertex.Stride
                        + (long)indicesPerChunk * sizeof(uint)
                        + ArgsStride * sizeof(uint)));

        public bool IsCreated => Vertices != null && Vertices.IsValid();

        public bool TryAcquire(out int slot)
        {
            if (_freeCount == 0)
            {
                slot = -1;
                return false;
            }
            slot = _freeSlots[--_freeCount];
            return true;
        }

        public void Release(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            if (_freeCount >= SlotCount)
                throw new InvalidOperationException("Released more arena slots than exist.");
            _freeSlots[_freeCount++] = slot;
        }

        public int VertexBase(int slot) => slot * CellsPerChunk;
        public int IndexBase(int slot) => slot * IndicesPerChunk;
        public int ArgsBase(int slot) => slot * ArgsStride;

        /// <summary>Byte offset of a slot's arguments, for indirect draw submission.</summary>
        public int ArgsByteOffset(int slot) => slot * ArgsStride * sizeof(uint);

        public void Dispose()
        {
            Vertices?.Release();
            Indices?.Release();
            Args?.Release();
            Vertices = null;
            Indices = null;
            Args = null;
            _freeCount = 0;
        }
    }
}
