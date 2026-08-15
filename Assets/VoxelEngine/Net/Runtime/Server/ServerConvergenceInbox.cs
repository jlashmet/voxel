using System;
using System.Collections.Generic;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Bounded frame-pump -> fixed-tick handoff for drift reports. Clients cannot force repair work
    /// directly from a socket callback; reports are authenticated, deduplicated and validated later.
    /// </summary>
    public sealed class ServerConvergenceInbox : IClientConvergenceCommandHandler
    {
        public const int DefaultMaxPendingPerConnection = 16;
        public const int DefaultMaxPendingTotal = 256;

        private readonly List<QueuedMismatch> _pending = new List<QueuedMismatch>(64);
        private readonly Dictionary<uint, int> _countsByConnection = new Dictionary<uint, int>(64);
        private readonly int _maxPerConnection;
        private readonly int _maxTotal;
        private long _dropped;
        private long _arrivalOrdinal;

        public ServerConvergenceInbox(
            int maxPendingPerConnection = DefaultMaxPendingPerConnection,
            int maxPendingTotal = DefaultMaxPendingTotal)
        {
            if (maxPendingPerConnection <= 0) throw new ArgumentOutOfRangeException(nameof(maxPendingPerConnection));
            if (maxPendingTotal < maxPendingPerConnection) throw new ArgumentOutOfRangeException(nameof(maxPendingTotal));
            _maxPerConnection = maxPendingPerConnection;
            _maxTotal = maxPendingTotal;
        }

        public int PendingCount => _pending.Count;
        public long DroppedCount => _dropped;

        public void HandleRegionHashMismatch(uint connectionId, in C_RegionHashMismatch mismatch)
        {
            if (connectionId == 0 || _pending.Count >= _maxTotal)
            {
                _dropped++;
                return;
            }

            _countsByConnection.TryGetValue(connectionId, out int count);
            if (count >= _maxPerConnection)
            {
                _dropped++;
                return;
            }

            // Replace an older report for the same connection/region rather than queueing repair
            // storms. A later hash tick contains at least as much convergence information.
            for (int i = 0; i < _pending.Count; i++)
            {
                QueuedMismatch existing = _pending[i];
                if (existing.ConnectionId == connectionId &&
                    existing.Mismatch.regionCoord.Equals(mismatch.regionCoord))
                {
                    if (mismatch.hashTick >= existing.Mismatch.hashTick)
                        _pending[i] = new QueuedMismatch(connectionId, mismatch, ++_arrivalOrdinal);
                    return;
                }
            }

            _pending.Add(new QueuedMismatch(connectionId, mismatch, ++_arrivalOrdinal));
            _countsByConnection[connectionId] = count + 1;
        }

        public int Drain(List<QueuedMismatch> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int count = _pending.Count;
            for (int i = 0; i < count; i++)
                destination.Add(_pending[i]);
            _pending.Clear();
            _countsByConnection.Clear();
            return count;
        }

        public int RemoveConnection(uint connectionId)
        {
            int removed = 0;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].ConnectionId != connectionId)
                    continue;
                _pending.RemoveAt(i);
                removed++;
            }
            _countsByConnection.Remove(connectionId);
            return removed;
        }

        public readonly struct QueuedMismatch
        {
            public readonly uint ConnectionId;
            public readonly C_RegionHashMismatch Mismatch;
            public readonly long ArrivalOrdinal;

            public QueuedMismatch(uint connectionId, C_RegionHashMismatch mismatch, long arrivalOrdinal)
            {
                ConnectionId = connectionId;
                Mismatch = mismatch;
                ArrivalOrdinal = arrivalOrdinal;
            }
        }
    }
}
