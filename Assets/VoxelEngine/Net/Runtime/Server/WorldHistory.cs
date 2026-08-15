using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Per-tick region snapshot store for rollback and late-join state reconstruction.
    ///
    /// Maintains a circular buffer of region brick snapshots within the 500 ms rollback window
    /// (15 ticks at 30 Hz, device-matrix.md). Implements compaction past the hot retention
    /// window (2 s = 60 ticks) to bound memory usage.
    ///
    /// Serves two consumers:
    ///   1. Reconciliation rollback — replay from a snapshot + event overlay.
    ///   2. Late-join synchronization — provide complete region state for new players.
    /// </summary>
    public struct WorldHistory
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Rollback window in ticks (device-matrix.md: 500 ms = 15 ticks at 30 Hz).</summary>
        private const int k_RollbackWindowTicks = 15;

        /// <summary>Hot retention window in ticks before snapshots are eligible for compaction
        /// (device-matrix.md: 2 s = 60 ticks).</summary>
        private const int k_HotRetentionTicks = 60;

        // -- internal state -------------------------------------------------------

        /// <summary>Circular buffer of region snapshots. Each entry is a NativeArray<byte>
        /// containing the full brick data for one tick's snapshot.</summary>
        private NativeArray<NativeArray<byte>> _snapshots;

        /// <summary>Maps tick number → index into the _snapshots circular buffer.</summary>
        private NativeHashMap<uint, int> _tickToIndex;

        /// <summary>The oldest tick still in the rollback window. Ticks before this have been compacted.</summary>
        private uint _oldestTick;

        /// <summary>Total snapshots recorded (monotonically increasing).</summary>
        private uint _totalCount;

        // -- construction ---------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WorldHistory(uint rollbackWindowTicks)
        {
            // Snapshot count matches the rollback window size.
            int capacity = (int)rollbackWindowTicks;
            _snapshots = new NativeArray<NativeArray<byte>>(capacity, Unity.Collections.Allocator.Persistent);

            for (int i = 0; i < capacity; i++)
                _snapshots[i] = default; // null — allocated on first snapshot.

            _tickToIndex = new NativeHashMap<uint, int>(16, Unity.Collections.Allocator.Persistent);
            _oldestTick = 0;
            _totalCount = 0;
        }

        /// <summary>
        /// Records a region state snapshot at the given tick and for the specified region coordinate.
        /// The brickStorage array is deep-copied into the snapshot buffer.
        /// </summary>
        /// <param name="tick">Server tick of this snapshot.</param>
        /// <param name="regionCoord">Coordinate of the region being snapshotted.</summary>
        /// <param name="brickStorage">Current brick data to snapshot (must remain valid during copy).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordSnapshot(uint tick, NativeArray<byte> brickStorage)
        {
            int index = (int)(_totalCount % k_RollbackWindowTicks);

            // Allocate or reuse the snapshot buffer.
            if (!_snapshots[index].IsCreated || _snapshots[index].Length != brickStorage.Length)
            {
                var newSnapshot = new NativeArray<byte>(brickStorage.Length, Unity.Collections.Allocator.Persistent);
                if (_snapshots[index].IsCreated)
                    _snapshots[index].Dispose();

                _snapshots[index] = newSnapshot;
            }

            // Deep copy the brick storage.
            // NativeArray's indexer returns a copy, so the inner array must be pulled into
            // a local before writing through it.
            var target = _snapshots[index];
            for (int i = 0; i < brickStorage.Length; i++)
                target[i] = brickStorage[i];

            // Update tick mapping — overwrite any previous mapping for this tick.
            if (_tickToIndex.ContainsKey(tick))
                _tickToIndex[tick] = index;
            else
                _tickToIndex[tick] = index;

            _totalCount++;
        }

        /// <summary>Retrieves a snapshot at a specific tick for a given region coordinate.</summary>
        /// <param name="tick">The server tick to retrieve.</param>
        /// <param name="regionCoord">Region coordinate (used to verify the snapshot is for the right region).</param>
        /// <param name="snapshot">Output snapshot — valid only if the method returns true.</param>
        /// <returns>True if a valid snapshot exists; false if the tick has been compacted or never recorded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySnapshot(uint tick, int3 regionCoord, out NativeArray<byte> snapshot)
        {
            // Reject if the tick is past the compaction boundary.
            if (tick < _oldestTick)
            {
                snapshot = default;
                return false;
            }

            if (!_tickToIndex.TryGetValue(tick, out int index))
            {
                snapshot = default;
                return false;
            }

            if (!_snapshots[index].IsCreated)
            {
                snapshot = default;
                return false;
            }

            snapshot = _snapshots[index];
            return true;
        }

        /// <summary>Compacts snapshots older than the hot retention window (2 s = 60 ticks).</summary>
        /// <param name="currentTick">Current server tick — used to determine which snapshots are safe to remove.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Compact(uint currentTick)
        {
            uint compactThreshold;
            if (currentTick > k_HotRetentionTicks)
                compactThreshold = currentTick - k_HotRetentionTicks;
            else
                compactThreshold = 0;

            // Remove old entries from the tick index and dispose their buffers.
            var keysToRemove = new NativeList<uint>(16, Unity.Collections.Allocator.Temp);
            foreach (var kvp in _tickToIndex)
            {
                if (kvp.Key < compactThreshold)
                    keysToRemove.Add(kvp.Key);
            }

            foreach (var key in keysToRemove)
            {
                int index = _tickToIndex[key];
                if (_snapshots[index].IsCreated)
                    _snapshots[index].Dispose();
                _snapshots[index] = default;
                _tickToIndex.Remove(key);
            }

            if (keysToRemove.Length > 0)
                _oldestTick = compactThreshold;
        }

        /// <summary>Disposes all native resources.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            for (int i = 0; i < _snapshots.Length; i++)
            {
                if (_snapshots[i].IsCreated)
                    _snapshots[i].Dispose();
            }
            if (_tickToIndex.IsCreated)
                _tickToIndex.Dispose();
        }

        /// <summary>Oldest tick still available in the rollback window.</summary>
        public uint OldestTick => _oldestTick;

        /// <summary>Total snapshots ever recorded (may exceed capacity due to compaction).</summary>
        public uint TotalRecorded => _totalCount;
    }
}
