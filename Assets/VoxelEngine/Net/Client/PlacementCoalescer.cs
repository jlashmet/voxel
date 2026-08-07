using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Coalesces individual voxel placement inputs into brick-scoped RLE batches
    /// before sending to the server. Reduces network overhead: a player building a wall
    /// sends one coalesced batch instead of hundreds of individual voxel requests.
    ///
    /// The algorithm accumulates incoming placements in a temporally-windowed buffer,
    /// groups them by brick coordinate (since multiple voxels within the same brick should
    /// use RLE encoding), and produces a run-length-encoded byte stream on flush.
    ///
    /// Constitution Principle II (Single source of truth): coalescing is purely a network
    /// optimization — it never changes which voxels are placed, only how they are transmitted.
    /// The server receives the same semantic result regardless of timing jitter within the
    /// coalescing window.
    /// </summary>
    public static class PlacementCoalescer
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Default coalescing window: 100 ms.</summary>
        public const int CoalesceWindowMs = 100;

        // -- state ----------------------------------------------------------------

        /// <summary>Accumulated placement positions during the current window.</summary>
        private static NativeList<int3> _positions;

        /// <summary>Materials corresponding to each position in <see cref="_positions"/>.</summary>
        private static NativeList<byte> _materials;

        /// <summary>Wall-clock timestamp (ms) when the current window started.</summary>
        private static double _windowStart;

        /// <summary>Whether a flush has occurred that needs to be reported as an RLE batch.</summary>
        private static bool _hasPendingFlush;

        // -- construction ----------------------------------------------------------

        /// <summary>
        /// Initialize the coalescer. Must be called once at startup or after each Flush.
        /// </summary>
        public static void Init()
        {
            _positions = new NativeList<int3>(64, Allocator.Persistent);
            _materials = new NativeList<byte>(64, Allocator.Persistent);
            _windowStart = Environment.TickCount;
            _hasPendingFlush = false;
        }

        // -- public API -------------------------------------------------------------

        /// <summary>
        /// Add a single voxel placement to the current coalescing buffer.
        /// Multiple calls within the coalescing window will be batched together on Flush.
        /// </summary>
        /// <param name="voxelPosition">World-space voxel coordinate to place.</param>
        /// <param name="material">Material index for the placed voxel (0 = empty).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddPlacement(int3 voxelPosition, byte material)
        {
            if (!_positions.IsCreated)
                Init();

            // Avoid duplicate positions within the same window — merge them with latest material.
            for (int i = 0; i < _positions.Length; i++)
            {
                if (math.all(_positions[i] == voxelPosition))
                {
                    _materials[i] = material; // overwrite — last placement wins per brick.
                    return;
                }
            }

            _positions.Add(voxelPosition);
            _materials.Add(material);
        }

        /// <summary>
        /// Flush the current coalescing window, returning an RLE batch for the server.
        /// The RLE format is: [brickCoordX (int), brickCoordY (int), brickCoordZ (int),
        /// runLength (ushort), material (byte), repeat...].
        /// </summary>
        /// <returns>A NativeSlice of RLE-encoded bytes, or an empty slice if no placements.</returns>
        public static NativeSlice<byte> Flush()
        {
            if (!_positions.IsCreated || _positions.Length == 0)
                return default;

            // Group by brick coordinate for RLE encoding.
            var brickRuns = new NativeHashMap<int3, (NativeList<int> counts, byte material)>(64, Allocator.Temp);

            for (int i = 0; i < _positions.Length; i++)
            {
                int3 pos = _positions[i];
                // Convert voxel coordinate to brick coordinate.
                int3 brickCoord = new int3(
                    pos.x >> VoxelDimensions.BrickEdgeLog2,
                    pos.y >> VoxelDimensions.BrickEdgeLog2,
                    pos.z >> VoxelDimensions.BrickEdgeLog2);

                if (!brickRuns.TryGetValue(brickCoord, out var entry))
                {
                    brickRuns[brickCoord] = (new NativeList<int>(8, Allocator.Temp), _materials[i]);
                    entry = brickRuns[brickCoord];
                    entry.counts.Add(1);
                    brickRuns[brickCoord] = entry;
                }
                else
                {
                    entry.counts[entry.counts.Length - 1]++; // increment last run.
                    brickRuns[brickCoord] = entry;
                }
            }

            // Encode to RLE byte stream.
            // Each entry: int3 brickCoord (12 bytes) + ushort runCount (2 bytes) + byte material (1 byte) = 15 bytes.
            // Pad to 16 bytes for alignment.
            int totalEntries = brickRuns.Count;
            var rleBuffer = new NativeArray<byte>(totalEntries * 16, Allocator.Temp);

            int offset = 0;
            foreach (var kvp in brickRuns)
            {
                // Brick coordinate — little-endian int3 per data-model.md wire format.
                WriteIntLE(ref rleBuffer, offset, kvp.Key.x); offset += 4;
                WriteIntLE(ref rleBuffer, offset, kvp.Key.y); offset += 4;
                WriteIntLE(ref rleBuffer, offset, kvp.Key.z); offset += 4;

                // Run count (clamped to ushort max).
                ushort runCount = (ushort)math.min(kvp.Value.counts[0], (int)ushort.MaxValue);
                WriteUShortLE(ref rleBuffer, offset, runCount); offset += 2;
                offset += 2;

                // Material index.
                rleBuffer[offset] = kvp.Value.material;
                offset++;

                // Pad to 16-byte entry for alignment.
                while (offset % 16 != 0)
                    rleBuffer[offset++] = 0;

                kvp.Value.counts.Dispose();
            }

            brickRuns.Clear();
            brickRuns.Dispose();

            // Clear local state.
            _positions.Clear();
            _materials.Clear();
            _windowStart = Environment.TickCount;

            return rleBuffer.Slice(0, offset);
        }

        /// <summary>Get elapsed time since last flush in milliseconds.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetElapsedMs()
        {
            if (_windowStart == 0)
                return CoalesceWindowMs; // not yet started — always eligible.

            return (float)(Environment.TickCount - _windowStart);
        }

        /// <summary>True when the current window has accumulated placements but hasn't been flushed.</summary>
        public static bool HasPending => _positions.IsCreated && _positions.Length > 0;

        /// <summary>Number of pending voxel placements in the current window.</summary>
        public static int PendingCount => _positions.IsCreated ? _positions.Length : 0;

        /// <summary>
        /// Reset all state. Called on disconnect or when switching build modes.
        /// </summary>
        public static void Dispose()
        {
            if (_positions.IsCreated) _positions.Dispose();
            if (_materials.IsCreated) _materials.Dispose();
            _positions = default;
            _materials = default;
            _windowStart = 0;
            _hasPendingFlush = false;
        }

        // -- private helpers ------------------------------------------------------

        /// <summary>Write a 32-bit integer as little-endian bytes into the buffer.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteIntLE(ref NativeArray<byte> buffer, int offset, int value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 24);
        }

        /// <summary>Write a 16-bit unsigned integer as little-endian bytes into the buffer.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUShortLE(ref NativeArray<byte> buffer, int offset, ushort value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
        }
    }
}
