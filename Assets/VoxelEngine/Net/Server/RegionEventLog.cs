using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Ring buffer of AlterationEvents with tick-indexed access for rollback and moderation.
    ///
    /// Implements the data-model.md §RegionEventLog: a compact event log that serves four consumers:
    ///   1. Moderation history (FR-023) — replay all events for auditing.
    ///   2. Lag compensation — find state at a past tick for client predictions.
    ///   3. Reconciliation rollback — replay events to recover from client divergence.
    ///   4. Compaction input — old events are folded into baked snapshots.
    ///
    /// Size: 500 ms = 15 ticks at 30 Hz (device-matrix.md §Frame and tick budgets).
    /// Events in the log never grow unbounded (data-model.md invariant).
    /// </summary>
    public struct RegionEventLog
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Ring buffer capacity in events. 15 ticks × max alterations per tick (64) = 960.</summary>
        private const int k_MaxEventsPerLog = 960;

        /// <summary>Mask for ring buffer index wrapping (capacity must be a power of two).</summary>
        private const int k_IndexMask = k_MaxEventsPerLog - 1;

        // -- internal state -------------------------------------------------------

        /// <summary>Ring buffer storage for alteration events.</summary>
        private NativeArray<AlterationEvent> _events;

        /// <summary>Tick-to-index lookup: maps server tick → ring buffer slot.
        /// NativeHashMap&lt;uint, int&gt; for O(1) tick-queryable access (data-model.md requirement).</summary>
        private NativeHashMap<uint, int> _tickIndex;

        /// <summary>Total number of events ever pushed to this log (monotonically increasing).</summary>
        private uint _count;

        /// <summary>
        /// Events at or below this tick have been folded into the baked snapshot.
        /// Query operations will return "not found" for ticks below this boundary.
        /// </summary>
        private uint _compactedThrough;

        // -- construction ---------------------------------------------------------

        /// <summary>Initialises the event log with capacity from device-matrix.md budgets.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(Unity.Collections.Allocator allocator)
        {
            _events = new NativeArray<AlterationEvent>(k_MaxEventsPerLog, allocator);
            _tickIndex = new NativeHashMap<uint, int>(64, allocator);
            _count = 0;
            _compactedThrough = 0;
        }

        /// <summary>Appends an alteration event to the log and updates the tick index.
        /// This is the primary write path — called when the server accepts an alteration.</summary>
        /// <param name="tick">Server tick at which this event occurred.</param>
        /// <param name="evt">The alteration event to record.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(uint tick, in AlterationEvent evt)
        {
            int index = (int)(_count & k_IndexMask);

            // Overwrite the oldest slot in the ring buffer — events past the rollback window
            // are no longer needed (data-model.md compaction invariant).
            _events[index] = evt;

            // Update or insert into the tick index for O(1) tick-based lookup.
            if (_tickIndex.ContainsKey(tick))
                _tickIndex[tick] = index;
            else
                _tickIndex[tick] = index;

            _count++;
        }

        /// <summary>Queries the log for an event at a specific tick.
        /// Returns false if the event has been compacted or was never recorded.</summary>
        /// <param name="tick">The server tick to query.</param>
        /// <param name="evt">Output alteration event, or default if not found.</param>
        /// <returns>True if an event exists at the requested tick; false if compacted/missing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAtTick(uint tick, out AlterationEvent evt)
        {
            // Reject if the tick is past the compaction boundary.
            if (tick < _compactedThrough)
            {
                evt = default;
                return false;
            }

            if (!_tickIndex.TryGetValue(tick, out int index))
            {
                evt = default;
                return false;
            }

            evt = _events[index];
            return true;
        }

        /// <summary>Compacts events up to (but not including) the given tick threshold.
        /// Compacted events are folded into the baked snapshot and removed from the log.</summary>
        /// <param name="throughTick">Compact all events at ticks below this value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompactUpTo(uint throughTick)
        {
            if (throughTick <= _compactedThrough)
                return; // nothing to compact.

            _compactedThrough = throughTick;

            // Remove compacted entries from the tick index.
            var keysToRemove = new NativeList<uint>(64, Unity.Collections.Allocator.Temp);
            foreach (var kvp in _tickIndex)
            {
                if (kvp.Key < throughTick)
                    keysToRemove.Add(kvp.Key);
            }

            foreach (var key in keysToRemove)
                _tickIndex.Remove(key);
        }

        /// <summary>Disposes all native resources in the event log.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_events.IsCreated) _events.Dispose();
            if (_tickIndex.IsCreated) _tickIndex.Dispose();
        }

        // -- properties -----------------------------------------------------------

        /// <summary>Total events ever pushed to this log (monotonically increasing).</summary>
        public uint Count => _count;

        /// <summary>
        /// The highest tick that has been compacted into the baked snapshot.
        /// Events at or below this tick are no longer in the active log.
        /// </summary>
        public uint CompactedThrough { get => _compactedThrough; set => _compactedThrough = value; }

        /// <summary>Number of events currently stored in the ring buffer (may be less than Count
        /// due to compaction and overwrite).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ActiveCount()
        {
            if (_count <= _compactedThrough)
                return 0;
            return _count - _compactedThrough;
        }
    }
}
