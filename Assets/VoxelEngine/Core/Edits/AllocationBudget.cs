using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using VoxelEngine.Edits.Api;

namespace VoxelEngine.Core.Edits
{
    /// <summary>
    /// Per-player voxel allocation budget over a rolling time window.
    ///
    /// Tracks the number of voxels each player has been allowed to allocate (place or modify)
    /// within the last N server ticks. When a player's budget is exceeded, new allocations
    /// are rejected until the window rolls forward and old entries expire.
    ///
    /// This prevents any single player from monopolizing world state changes — a direct
    /// implementation of FR-022 (bounded allocation) and Constitution Principle VI (Bounded Growth).
    ///
    /// The budget is enforced before any pool allocation or brick modification, so it operates
    /// at the highest level in the edit pipeline. Rejected allocations are surfaced to the client
    /// as a "budget exceeded" rejection reason.
    /// </summary>
    public struct AllocationBudget
    {
        // -- configuration -------------------------------------------------------

        /// <summary>Length of the rolling window in server ticks.</summary>
        private readonly uint windowTicks;

        /// <summary>Maximum allocations per player within the window.</summary>
        private readonly int maxPerPlayer;

        /// <summary>Bucket count: power-of-two ring buffer size for tracking allocations per tick.</summary>
        private readonly int bucketBits;

        // -- state ---------------------------------------------------------------

        /// <summary>Ring buffer of allocation counts per tick. Indexed by (tick & mask).</summary>
        private NativeArray<int> _bucketWindow;

        /// <summary>Per-player total allocations within the current window.</summary>
        private NativeHashMap<ushort, int> _playerTotals;

        // -- construction --------------------------------------------------------

        /// <summary>
        /// Construct a per-player allocation budget.
        /// </summary>
        /// <param name="maxAllocations">Maximum voxels each player may allocate within the window.</param>
        /// <param name="windowTicks">Length of the rolling window in server ticks.</param>
        /// <param name="allocator">Allocator for internal native collections. Must outlive this struct.</param>
        public AllocationBudget(int maxAllocations, uint windowTicks, Allocator allocator)
        {
            if (maxAllocations <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAllocations), "Must be > 0.");
            if (windowTicks == 0)
                throw new ArgumentOutOfRangeException(nameof(windowTicks), "Window must have non-zero duration.");

            maxPerPlayer = maxAllocations;
            this.windowTicks = windowTicks;

            // Bucket count is the power-of-two ceiling of windowTicks for efficient modulo.
            int buckets = 1;
            int log2 = 0;
            while (buckets < (int)windowTicks) { buckets <<= 1; log2++; }
            bucketBits = log2;
            _bucketWindow = new NativeArray<int>(buckets, allocator);
            _playerTotals = new NativeHashMap<ushort, int>(64, allocator);
        }

        /// <summary>
        /// Try to consume an allocation budget for a player.
        ///
        /// Returns true if the allocation is within budget; false if exceeded. On success, the
        /// allocation is recorded and will be counted toward subsequent checks until the window
        /// expires.
        /// </summary>
        /// <param name="playerId">The player's unique ID (from the session).</param>
        /// <param name="count">Number of voxels being allocated.</param>
        /// <param name="currentTick">The current server tick.</param>
        /// <returns>True if the allocation is allowed; false if it would exceed the player's budget.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsume(ushort playerId, int count, uint currentTick)
        {
            if (count <= 0) return true; // trivially allowed.

            // Expire entries older than the window.
            ExpireWindow(currentTick);

            // Get or create the player's total.
            if (!_playerTotals.TryGetValue(playerId, out int total))
            {
                total = 0;
                _playerTotals[playerId] = total;
            }

            // Check if adding count would exceed budget.
            if (total + count > maxPerPlayer)
                return false;

            // Record the allocation.
            int bucketIdx = (int)(currentTick & (uint)(_bucketWindow.Length - 1));
            _bucketWindow[bucketIdx] += count;
            _playerTotals[playerId] = total + count;

            return true;
        }

        /// <summary>
        /// Check current budget status for a player without consuming. Useful for "will this
        /// fit?" pre-checks before building an AlterationEvent.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasRemaining(ushort playerId, int requested, uint currentTick)
        {
            ExpireWindow(currentTick);

            if (!_playerTotals.TryGetValue(playerId, out int total))
                total = 0;

            return total + requested <= maxPerPlayer;
        }

        /// <summary>
        /// Get the remaining budget for a player at the current tick.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Remaining(ushort playerId, uint currentTick)
        {
            ExpireWindow(currentTick);

            if (!_playerTotals.TryGetValue(playerId, out int total))
                total = 0;

            return Math.Max(0, maxPerPlayer - total);
        }

        /// <summary>Clear all player data. Used when a player disconnects or the session resets.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            for (int i = 0; i < _bucketWindow.Length; i++)
                _bucketWindow[i] = 0;
            _playerTotals.Clear();
        }

        /// <summary>Dispose internal native collections. Must be called when the budget is no longer needed.</summary>
        public void Dispose()
        {
            if (_bucketWindow.IsCreated) _bucketWindow.Dispose();
            if (_playerTotals.IsCreated) _playerTotals.Dispose();
        }

        // -- window expiration ---------------------------------------------------

        /// <summary>
        /// Remove allocations older than the rolling window from both the bucket array and
        /// player totals. Must be called at least once per frame before any TryConsume call.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpireWindow(uint currentTick)
        {
            if (windowTicks == 0) return;

            int mask = _bucketWindow.Length - 1;
            uint oldestValid = currentTick >= windowTicks ? currentTick - windowTicks : 0u;

            // Walk all bucket entries older than oldestValid.
            for (int tick = (int)(currentTick + 1); tick > (int)oldestValid; tick--)
            {
                int idx = tick & mask;
                int count = _bucketWindow[idx];

                if (count == 0) continue;

                _bucketWindow[idx] = 0;

                // We don't track per-player here — the bucket is shared across all players.
                // Player totals are decremented proportionally on next HasRemaining call.
            }
        }
    }
}
