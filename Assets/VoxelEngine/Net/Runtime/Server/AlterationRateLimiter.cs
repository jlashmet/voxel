using System;
using System.Collections.Generic;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Server-owned alteration rate/allocation accounting. Validation queries without mutation;
    /// counters commit only after authoritative application succeeds.
    /// </summary>
    public sealed class AlterationRateLimiter
    {
        private readonly int _ticksPerSecond;
        private readonly int _maxAlterationsPerSecond;
        private readonly int _maxBricksPerTick;
        private readonly Dictionary<ushort, Queue<uint>> _acceptedTicks = new Dictionary<ushort, Queue<uint>>(64);
        private readonly Dictionary<ushort, TickAllocation> _allocation = new Dictionary<ushort, TickAllocation>(64);

        public AlterationRateLimiter(
            int ticksPerSecond = (int)AuthoritativeTickConfig.TickRateHz,
            int maxAlterationsPerSecond = Validation.k_MaxAlterationsPerSecond,
            int maxBricksPerTick = Validation.k_MaxBricksPerTick)
        {
            if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            if (maxAlterationsPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(maxAlterationsPerSecond));
            if (maxBricksPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(maxBricksPerTick));

            _ticksPerSecond = ticksPerSecond;
            _maxAlterationsPerSecond = maxAlterationsPerSecond;
            _maxBricksPerTick = maxBricksPerTick;
        }

        public bool WouldExceedRate(ushort playerId, uint serverTick)
        {
            if (playerId == 0) return true;
            if (!_acceptedTicks.TryGetValue(playerId, out Queue<uint> ticks)) return false;
            Prune(ticks, serverTick);
            return ticks.Count >= _maxAlterationsPerSecond;
        }

        public bool WouldExceedAllocation(ushort playerId, uint serverTick, int estimatedBricks)
        {
            if (playerId == 0 || estimatedBricks < 0 || estimatedBricks > _maxBricksPerTick) return true;
            if (!_allocation.TryGetValue(playerId, out TickAllocation allocation) || allocation.Tick != serverTick) return false;
            return allocation.UsedBricks + estimatedBricks > _maxBricksPerTick;
        }

        public void CommitAccepted(ushort playerId, uint serverTick, int estimatedBricks)
        {
            if (playerId == 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (estimatedBricks < 0 || estimatedBricks > _maxBricksPerTick) throw new ArgumentOutOfRangeException(nameof(estimatedBricks));

            if (!_acceptedTicks.TryGetValue(playerId, out Queue<uint> ticks))
            {
                ticks = new Queue<uint>(_maxAlterationsPerSecond + 1);
                _acceptedTicks.Add(playerId, ticks);
            }

            Prune(ticks, serverTick);
            ticks.Enqueue(serverTick);

            if (!_allocation.TryGetValue(playerId, out TickAllocation allocation) || allocation.Tick != serverTick)
                allocation = new TickAllocation(serverTick, 0);

            allocation.UsedBricks += estimatedBricks;
            _allocation[playerId] = allocation;
        }

        public void RemovePlayer(ushort playerId)
        {
            _acceptedTicks.Remove(playerId);
            _allocation.Remove(playerId);
        }

        public void Clear()
        {
            _acceptedTicks.Clear();
            _allocation.Clear();
        }

        private void Prune(Queue<uint> ticks, uint serverTick)
        {
            uint oldestInclusive = serverTick >= (uint)_ticksPerSecond
                ? serverTick - (uint)_ticksPerSecond + 1u
                : 0u;
            while (ticks.Count > 0 && ticks.Peek() < oldestInclusive) ticks.Dequeue();
        }

        private struct TickAllocation
        {
            public uint Tick;
            public int UsedBricks;
            public TickAllocation(uint tick, int usedBricks) { Tick = tick; UsedBricks = usedBricks; }
        }
    }
}
