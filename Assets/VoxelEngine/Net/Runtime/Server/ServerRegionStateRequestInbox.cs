using System;
using System.Collections.Generic;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Bounded frame-pump -> fixed-tick queue for region state requests. Requests are untrusted until
    /// the authoritative tick verifies connection identity, simulation interest, region residency,
    /// and transfer budget.
    /// </summary>
    public sealed class ServerRegionStateRequestInbox : IClientRegionRequestHandler
    {
        public const int DefaultMaxPendingPerConnection = 8;
        public const int DefaultMaxPendingGlobal = 256;

        private readonly int _maxPendingPerConnection;
        private readonly int _maxPendingGlobal;
        private readonly Queue<QueuedRegionRequest> _pending = new Queue<QueuedRegionRequest>(32);
        private readonly Dictionary<uint, int> _perConnection = new Dictionary<uint, int>(64);
        private ulong _arrivalOrdinal;

        public long DroppedRequests { get; private set; }
        public int PendingCount => _pending.Count;

        public ServerRegionStateRequestInbox(
            int maxPendingPerConnection = DefaultMaxPendingPerConnection,
            int maxPendingGlobal = DefaultMaxPendingGlobal)
        {
            if (maxPendingPerConnection <= 0) throw new ArgumentOutOfRangeException(nameof(maxPendingPerConnection));
            if (maxPendingGlobal <= 0 || maxPendingGlobal < maxPendingPerConnection)
                throw new ArgumentOutOfRangeException(nameof(maxPendingGlobal));

            _maxPendingPerConnection = maxPendingPerConnection;
            _maxPendingGlobal = maxPendingGlobal;
        }

        public void HandleRegionRequest(uint connectionId, in C_RegionRequest request)
        {
            if (connectionId == 0)
            {
                DroppedRequests++;
                return;
            }

            _perConnection.TryGetValue(connectionId, out int connectionCount);
            if (_pending.Count >= _maxPendingGlobal || connectionCount >= _maxPendingPerConnection)
            {
                DroppedRequests++;
                return;
            }

            _arrivalOrdinal++;
            if (_arrivalOrdinal == 0) _arrivalOrdinal = 1;
            _pending.Enqueue(new QueuedRegionRequest(connectionId, request, _arrivalOrdinal));
            _perConnection[connectionId] = connectionCount + 1;
        }

        public int Drain(List<QueuedRegionRequest> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int count = 0;
            while (_pending.Count > 0)
            {
                QueuedRegionRequest queued = _pending.Dequeue();
                destination.Add(queued);
                DecrementConnection(queued.ConnectionId);
                count++;
            }
            return count;
        }

        public void RemoveConnection(uint connectionId)
        {
            if (connectionId == 0 || _pending.Count == 0)
            {
                _perConnection.Remove(connectionId);
                return;
            }

            int count = _pending.Count;
            for (int i = 0; i < count; i++)
            {
                QueuedRegionRequest queued = _pending.Dequeue();
                if (queued.ConnectionId != connectionId)
                    _pending.Enqueue(queued);
            }
            _perConnection.Remove(connectionId);
        }

        private void DecrementConnection(uint connectionId)
        {
            if (!_perConnection.TryGetValue(connectionId, out int count)) return;
            if (count <= 1) _perConnection.Remove(connectionId);
            else _perConnection[connectionId] = count - 1;
        }

        public readonly struct QueuedRegionRequest
        {
            public readonly uint ConnectionId;
            public readonly C_RegionRequest Request;
            public readonly ulong ArrivalOrdinal;

            public QueuedRegionRequest(uint connectionId, C_RegionRequest request, ulong arrivalOrdinal)
            {
                ConnectionId = connectionId;
                Request = request;
                ArrivalOrdinal = arrivalOrdinal;
            }
        }
    }
}
