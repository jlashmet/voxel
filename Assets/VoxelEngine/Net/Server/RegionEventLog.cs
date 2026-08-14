using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using VoxelEngine.Core.Edits;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Bounded per-region semantic event ring for rollback, moderation, and cheap repair suffixes.
    ///
    /// The original scaffold used a bitmask against a 960-entry non-power-of-two capacity and a
    /// tick->single-index map, corrupting wraparound and losing all but one event in a busy tick.
    /// This implementation uses modulo indexing and stores the tick beside every event. The ring is
    /// tiny (960 entries), so repair/history queries scan at most 960 records and remain predictable.
    /// </summary>
    public struct RegionEventLog
    {
        public const int MaxEventsPerLog = 960;

        private NativeArray<AlterationEvent> _events;
        private NativeArray<uint> _ticks;
        private uint _count;
        private uint _compactedThrough;

        public void Initialize(Allocator allocator)
        {
            _events = new NativeArray<AlterationEvent>(MaxEventsPerLog, allocator);
            _ticks = new NativeArray<uint>(MaxEventsPerLog, allocator);
            _count = 0;
            _compactedThrough = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(uint tick, in AlterationEvent evt)
        {
            if (!_events.IsCreated || !_ticks.IsCreated)
                throw new InvalidOperationException("RegionEventLog must be initialized before Push.");
            if (tick < _compactedThrough)
                throw new ArgumentOutOfRangeException(nameof(tick), "Cannot append behind compaction boundary.");

            int index = (int)(_count % MaxEventsPerLog);
            _events[index] = evt;
            _ticks[index] = tick;
            _count++;
        }

        /// <summary>Return the first retained event at exactly tick, preserving legacy API.</summary>
        public bool TryGetAtTick(uint tick, out AlterationEvent evt)
        {
            if (tick < _compactedThrough || !_events.IsCreated)
            {
                evt = default;
                return false;
            }

            int retained = RetainedSlotCount();
            uint firstOrdinal = _count > MaxEventsPerLog ? _count - MaxEventsPerLog : 0;
            for (int offset = 0; offset < retained; offset++)
            {
                uint ordinal = firstOrdinal + (uint)offset;
                int index = (int)(ordinal % MaxEventsPerLog);
                if (_ticks[index] == tick)
                {
                    evt = _events[index];
                    return true;
                }
            }

            evt = default;
            return false;
        }

        /// <summary>
        /// Append every retained event with fromExclusive &lt; tick &lt;= throughInclusive to the
        /// caller-owned list, in original authority order. Returns false when the requested suffix
        /// predates either compaction or the physical ring retention window.
        /// </summary>
        public bool TryCopyRange(
            uint fromExclusive,
            uint throughInclusive,
            NativeList<AlterationEvent> destination)
        {
            if (!destination.IsCreated)
                throw new ArgumentException("Destination must be created.", nameof(destination));
            if (throughInclusive <= fromExclusive)
                return true;
            if (fromExclusive < _compactedThrough)
                return false;

            int retained = RetainedSlotCount();
            if (retained == 0)
                return true;

            uint firstOrdinal = _count > MaxEventsPerLog ? _count - MaxEventsPerLog : 0;
            uint oldestRetainedTick = uint.MaxValue;

            for (int offset = 0; offset < retained; offset++)
            {
                uint ordinal = firstOrdinal + (uint)offset;
                int index = (int)(ordinal % MaxEventsPerLog);
                uint tick = _ticks[index];
                if (tick < oldestRetainedTick)
                    oldestRetainedTick = tick;
            }

            if (fromExclusive + 1u < oldestRetainedTick)
                return false;

            for (int offset = 0; offset < retained; offset++)
            {
                uint ordinal = firstOrdinal + (uint)offset;
                int index = (int)(ordinal % MaxEventsPerLog);
                uint tick = _ticks[index];
                if (tick > fromExclusive && tick <= throughInclusive && tick >= _compactedThrough)
                    destination.Add(_events[index]);
            }

            return true;
        }

        public void CompactUpTo(uint throughTick)
        {
            if (throughTick > _compactedThrough)
                _compactedThrough = throughTick;
        }

        public void Dispose()
        {
            if (_events.IsCreated) _events.Dispose();
            if (_ticks.IsCreated) _ticks.Dispose();
        }

        public uint Count => _count;
        public uint CompactedThrough
        {
            get => _compactedThrough;
            set => _compactedThrough = value;
        }

        public uint ActiveCount()
        {
            if (!_ticks.IsCreated)
                return 0;

            int retained = RetainedSlotCount();
            uint active = 0;
            uint firstOrdinal = _count > MaxEventsPerLog ? _count - MaxEventsPerLog : 0;
            for (int offset = 0; offset < retained; offset++)
            {
                int index = (int)((firstOrdinal + (uint)offset) % MaxEventsPerLog);
                if (_ticks[index] >= _compactedThrough)
                    active++;
            }
            return active;
        }

        private int RetainedSlotCount() => (int)Math.Min(_count, (uint)MaxEventsPerLog);
    }
}
