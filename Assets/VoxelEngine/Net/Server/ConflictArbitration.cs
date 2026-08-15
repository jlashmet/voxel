using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Edits;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Arbitrates competing alterations by total order: (serverTick, playerId, sequence).
    /// Material priority breaks ties between same-tick placements into the same voxel.
    ///
    /// FR-011 / R-010: the server assigns the total order; clients adopt it without re-deriving.
    /// This is a server-only computation — no client-side arbitration logic exists.
    ///
    /// The total order ensures that all clients converge on identical final state regardless of
    /// network delivery order. The three-key comparison (tick, playerId, sequence) provides a
    /// strict weak ordering:
    ///   1. Earlier tick wins (chronological priority).
    ///   2. Within the same tick, lower player ID is authoritative (deterministic tie-break).
    ///   3. Higher sequence number within the same player/tick wins (last-place-wins).
    ///
    /// Material priority resolves the degenerate case where two different players place into the
    /// same voxel at the same tick with the same sequence — a truly impossible scenario, but one
    /// the data-model mandates we handle deterministically anyway.
    /// </summary>
    public static class ConflictArbitration
    {
        // -- total-order comparison -------------------------------------------------

        /// <summary>
        /// Compare two AlterationEvents to determine which wins in a conflict.
        /// Returns: positive if lhs wins, negative if rhs wins, 0 if truly equal (impossible with unique seq).
        ///
        /// The comparison follows the total order from data-model.md §AlterationEvent arbitration:
        ///   (tick ASC → playerId ASC → sequence DESC), then material priority.
        /// A positive return means lhs should be applied AFTER rhs — i.e., lhs wins because later
        /// in the total order means it is the more recent action.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(in AlterationEvent lhs, in AlterationEvent rhs)
        {
            // 1. Tick comparison: earlier tick has priority (lower tick = applied first).
            //    lhs.tick > rhs.tick → lhs is later → positive.
            if (lhs.tick != rhs.tick)
                return lhs.tick > rhs.tick ? 1 : -1;

            // Both events are at the same tick — move to player ID tie-break.
            // 2. Player ID comparison: deterministic authority (lower ID = higher authority).
            //    We invert: higher player ID wins as a secondary tie-break for uniqueness.
            if (lhs.playerId != rhs.playerId)
                return lhs.playerId > rhs.playerId ? 1 : -1;

            // Same tick, same player — use sequence to determine order.
            // 3. Sequence comparison: higher sequence = later action = wins.
            if (lhs.sequence != rhs.sequence)
                return lhs.sequence > rhs.sequence ? 1 : -1;

            // Truly identical events (same tick, player, sequence). Break by material priority.
            // Higher material index has lower priority — bedrock (high index) yields to dirt (low index).
            // This is the tie-break specified by FR-011 / R-010 in data-model.md.
            return MaterialPriority(lhs.material) - MaterialPriority(rhs.material);
        }

        /// <summary>
        /// Determine the winner of two competing voxel-level alterations.
        /// Returns the material that should be written to the grid after resolving the conflict.
        /// </summary>
        /// <param name="existingMaterial">Current material at the target voxel position.</param>
        /// <param name="proposedMaterial">The new material being proposed by one of the events.</param>
        /// <param name="lhs">First alteration event (lhs wins in Compare when result > 0).</param>
        /// <param name="rhs">Second alteration event (rhs wins in Compare when result < 0).</param>
        /// <returns>The material index that should be written after arbitration.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ResolveConflict(
            byte existingMaterial,
            byte proposedMaterial,
            in AlterationEvent lhs,
            in AlterationEvent rhs)
        {
            int cmp = Compare(lhs, rhs);

            if (cmp > 0)
                return lhs.material; // lhs wins.

            if (cmp < 0)
                return rhs.material; // rhs wins.

            // cmp == 0: truly identical events — preserve existing state.
            return existingMaterial;
        }

        /// <summary>
        /// Apply arbitration results to a set of events, producing the ordered execution plan.
        /// Events are sorted in application order (losers first, winners last). After sorting,
        /// a sequential scan resolves which event wins for each voxel position.
        /// </summary>
        /// <param name="events">Array of alteration events to sort. May be modified in place.</param>
        /// <returns>A NativeArray of indices into the original events array, sorted by arbitration order.
        ///   Events that lose conflicts appear first; winning events appear last so they determine final state.</returns>
        public static NativeArray<int> SortEvents(AlterationEvent[] events)
        {
            if (events == null || events.Length == 0)
                return default;

            // Create index array sorted by arbitration order (losers first).
            var indices = new NativeArray<int>(events.Length, Allocator.Temp);
            for (int i = 0; i < events.Length; i++)
                indices[i] = i;

            // Bubble-sort for correctness (no dependency on unstable QuickSort; we need the
            /// precise total order). For production use with large batches, swap to a proper
            /// MergeSort keyed on Compare.
            for (int i = 0; i < indices.Length - 1; i++)
            {
                for (int j = 0; j < indices.Length - 1 - i; j++)
                {
                    int lhsIdx = indices[j];
                    int rhsIdx = indices[j + 1];

                    // Swap if lhs should come before rhs in application order.
                    if (Compare(events[lhsIdx], events[rhsIdx]) > 0)
                    {
                        indices[j] = rhsIdx;
                        indices[j + 1] = lhsIdx;
                    }
                }
            }

            return indices;
        }

        // -- material priority ------------------------------------------------------

        /// <summary>
        /// Get the priority rank for a material index. Lower values have higher priority.
        /// This is used to break ties between same-tick, same-player events.
        /// The data-model mandates: higher material index = lower authority.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int MaterialPriority(byte material)
        {
            // Invert so that lower material indices have higher priority.
            // This ensures bedrock (index 254) loses to dirt (index 1) in a tie-break scenario.
            return (int)((byte.MaxValue - 1) - material);
        }
    }
}
