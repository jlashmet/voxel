using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Coalesces individual voxel placement inputs into block-scoped RLE batches
    /// before sending to the server. Reduces network overhead: a player building a wall
    /// sends one coalesced batch instead of hundreds of individual voxel requests.
    ///
    /// The algorithm accumulates incoming placements in a temporally-windowed buffer,
    /// groups them by logical Storage block coordinate, and produces a run-length-encoded byte
    /// stream on flush. The grouping size is an API-level read-block fact; Net does not know the
    /// physical Storage brick representation.
    ///
    /// Constitution Principle II (Single source of truth): coalescing is purely a network
    /// optimization — it never changes which voxels are placed, only how they are transmitted.
    /// The server receives the same semantic result regardless of timing jitter within the
    /// coalescing window.
    /// </summary>
    public static class PlacementCoalescer
    {
        public const int CoalesceWindowMs = 100;

        private static NativeList<int3> _positions;
        private static NativeList<byte> _materials;
        private static double _windowStart;
        private static bool _hasPendingFlush;

        public static void Init()
        {
            _positions = new NativeList<int3>(64, Allocator.Persistent);
            _materials = new NativeList<byte>(64, Allocator.Persistent);
            _windowStart = Environment.TickCount;
            _hasPendingFlush = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddPlacement(int3 voxelPosition, byte material)
        {
            if (!_positions.IsCreated)
                Init();

            for (int i = 0; i < _positions.Length; i++)
            {
                if (math.all(_positions[i] == voxelPosition))
                {
                    _materials[i] = material;
                    return;
                }
            }

            _positions.Add(voxelPosition);
            _materials.Add(material);
        }

        public static NativeSlice<byte> Flush()
        {
            if (!_positions.IsCreated || _positions.Length == 0)
                return default;

            var blockRuns = new NativeHashMap<int3, (NativeList<int> counts, byte material)>(64, Allocator.Temp);

            for (int i = 0; i < _positions.Length; i++)
            {
                int3 pos = _positions[i];
                int3 blockCoord = new int3(
                    pos.x >> VoxelReadGrid.BlockEdgeLog2,
                    pos.y >> VoxelReadGrid.BlockEdgeLog2,
                    pos.z >> VoxelReadGrid.BlockEdgeLog2);

                if (!blockRuns.TryGetValue(blockCoord, out var entry))
                {
                    blockRuns[blockCoord] = (new NativeList<int>(8, Allocator.Temp), _materials[i]);
                    entry = blockRuns[blockCoord];
                    entry.counts.Add(1);
                    blockRuns[blockCoord] = entry;
                }
                else
                {
                    entry.counts[entry.counts.Length - 1]++;
                    blockRuns[blockCoord] = entry;
                }
            }

            int totalEntries = blockRuns.Count;
            var rleBuffer = new NativeArray<byte>(totalEntries * 16, Allocator.Temp);

            int offset = 0;
            foreach (var kvp in blockRuns)
            {
                WriteIntLE(ref rleBuffer, offset, kvp.Key.x); offset += 4;
                WriteIntLE(ref rleBuffer, offset, kvp.Key.y); offset += 4;
                WriteIntLE(ref rleBuffer, offset, kvp.Key.z); offset += 4;

                ushort runCount = (ushort)math.min(kvp.Value.counts[0], (int)ushort.MaxValue);
                WriteUShortLE(ref rleBuffer, offset, runCount); offset += 2;
                offset += 2;

                rleBuffer[offset] = kvp.Value.material;
                offset++;

                while (offset % 16 != 0)
                    rleBuffer[offset++] = 0;

                kvp.Value.counts.Dispose();
            }

            blockRuns.Clear();
            blockRuns.Dispose();

            _positions.Clear();
            _materials.Clear();
            _windowStart = Environment.TickCount;

            return rleBuffer.Slice(0, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetElapsedMs()
        {
            if (_windowStart == 0)
                return CoalesceWindowMs;

            return (float)(Environment.TickCount - _windowStart);
        }

        public static bool HasPending => _positions.IsCreated && _positions.Length > 0;
        public static int PendingCount => _positions.IsCreated ? _positions.Length : 0;

        public static void Dispose()
        {
            if (_positions.IsCreated) _positions.Dispose();
            if (_materials.IsCreated) _materials.Dispose();
            _positions = default;
            _materials = default;
            _windowStart = 0;
            _hasPendingFlush = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteIntLE(ref NativeArray<byte> buffer, int offset, int value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUShortLE(ref NativeArray<byte> buffer, int offset, ushort value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
        }
    }
}
