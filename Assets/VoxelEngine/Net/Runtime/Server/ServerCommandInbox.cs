using System;
using System.Collections.Generic;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Frame-pump -> fixed-tick handoff for untrusted client intent.
    ///
    /// UTP callbacks append decoded commands here; authoritative simulation drains them at a tick
    /// boundary. This prevents arbitrary frame/network timing from mutating deterministic world
    /// state and gives one bounded choke point for flood protection before expensive validation.
    ///
    /// This type assumes transport pump and simulation run on the same owning thread. If transport
    /// is moved to a worker thread later, replace this with an explicitly synchronized/SPSC queue.
    /// </summary>
    public sealed class ServerCommandInbox : IClientEventCommandHandler, IClientInputCommandHandler
    {
        public const int DefaultMaxPendingPerConnection = 256;
        public const int DefaultMaxPendingTotal = 4096;

        private readonly List<QueuedAlterationRequest> _alterations;
        private readonly List<QueuedPlayerInput> _inputs;
        private readonly Dictionary<uint, int> _pendingByConnection;
        private readonly int _maxPendingPerConnection;
        private readonly int _maxPendingTotal;

        private int _pendingTotal;
        private long _arrivalOrdinal;
        private long _droppedCommands;

        public ServerCommandInbox(
            int maxPendingPerConnection = DefaultMaxPendingPerConnection,
            int maxPendingTotal = DefaultMaxPendingTotal)
        {
            if (maxPendingPerConnection <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPendingPerConnection));
            if (maxPendingTotal < maxPendingPerConnection)
                throw new ArgumentOutOfRangeException(nameof(maxPendingTotal));

            _maxPendingPerConnection = maxPendingPerConnection;
            _maxPendingTotal = maxPendingTotal;
            _alterations = new List<QueuedAlterationRequest>(128);
            _inputs = new List<QueuedPlayerInput>(256);
            _pendingByConnection = new Dictionary<uint, int>(64);
        }

        public int PendingAlterations => _alterations.Count;
        public int PendingInputs => _inputs.Count;
        public int PendingTotal => _pendingTotal;
        public long DroppedCommands => _droppedCommands;

        public void HandleAlterationRequest(uint connectionId, in C_AlterationRequest request)
        {
            if (!TryReserve(connectionId))
                return;

            _alterations.Add(new QueuedAlterationRequest(connectionId, request, NextArrivalOrdinal()));
        }

        public void HandlePlayerInput(uint connectionId, in C_PlayerInput input)
        {
            if (!TryReserve(connectionId))
                return;

            _inputs.Add(new QueuedPlayerInput(connectionId, input, NextArrivalOrdinal()));
        }

        /// <summary>
        /// Append every currently pending alteration request to a caller-owned reusable list and
        /// remove them from the inbox. Commands keep their connection ID and arrival ordinal; the
        /// simulation must still authenticate/map identity and apply its deterministic arbitration.
        /// </summary>
        public int DrainAlterations(List<QueuedAlterationRequest> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            int count = _alterations.Count;
            for (int i = 0; i < count; i++)
            {
                QueuedAlterationRequest queued = _alterations[i];
                destination.Add(queued);
                Release(queued.ConnectionId);
            }

            _alterations.Clear();
            return count;
        }

        /// <summary>Drain pending input samples into a caller-owned reusable list.</summary>
        public int DrainInputs(List<QueuedPlayerInput> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            int count = _inputs.Count;
            for (int i = 0; i < count; i++)
            {
                QueuedPlayerInput queued = _inputs[i];
                destination.Add(queued);
                Release(queued.ConnectionId);
            }

            _inputs.Clear();
            return count;
        }

        /// <summary>
        /// Drop unprocessed commands from a dead connection. Authentication/session state may be
        /// gone by the next tick, so preserving not-yet-validated intent would be unsafe.
        /// </summary>
        public int RemoveConnection(uint connectionId)
        {
            int removed = RemoveAlterations(connectionId) + RemoveInputs(connectionId);
            _pendingByConnection.Remove(connectionId);
            _pendingTotal -= removed;
            if (_pendingTotal < 0)
                _pendingTotal = 0;
            return removed;
        }

        public void Clear()
        {
            _alterations.Clear();
            _inputs.Clear();
            _pendingByConnection.Clear();
            _pendingTotal = 0;
        }

        private bool TryReserve(uint connectionId)
        {
            if (connectionId == 0 || _pendingTotal >= _maxPendingTotal)
            {
                _droppedCommands++;
                return false;
            }

            _pendingByConnection.TryGetValue(connectionId, out int pendingForConnection);
            if (pendingForConnection >= _maxPendingPerConnection)
            {
                _droppedCommands++;
                return false;
            }

            _pendingByConnection[connectionId] = pendingForConnection + 1;
            _pendingTotal++;
            return true;
        }

        private void Release(uint connectionId)
        {
            if (!_pendingByConnection.TryGetValue(connectionId, out int pending))
                return;

            pending--;
            _pendingTotal--;
            if (pending <= 0)
                _pendingByConnection.Remove(connectionId);
            else
                _pendingByConnection[connectionId] = pending;
        }

        private int RemoveAlterations(uint connectionId)
        {
            int removed = 0;
            for (int i = _alterations.Count - 1; i >= 0; i--)
            {
                if (_alterations[i].ConnectionId != connectionId)
                    continue;

                _alterations.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private int RemoveInputs(uint connectionId)
        {
            int removed = 0;
            for (int i = _inputs.Count - 1; i >= 0; i--)
            {
                if (_inputs[i].ConnectionId != connectionId)
                    continue;

                _inputs.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private long NextArrivalOrdinal() => ++_arrivalOrdinal;

        public readonly struct QueuedAlterationRequest
        {
            public readonly uint ConnectionId;
            public readonly C_AlterationRequest Request;
            public readonly long ArrivalOrdinal;

            public QueuedAlterationRequest(uint connectionId, C_AlterationRequest request, long arrivalOrdinal)
            {
                ConnectionId = connectionId;
                Request = request;
                ArrivalOrdinal = arrivalOrdinal;
            }
        }

        public readonly struct QueuedPlayerInput
        {
            public readonly uint ConnectionId;
            public readonly C_PlayerInput Input;
            public readonly long ArrivalOrdinal;

            public QueuedPlayerInput(uint connectionId, C_PlayerInput input, long arrivalOrdinal)
            {
                ConnectionId = connectionId;
                Input = input;
                ArrivalOrdinal = arrivalOrdinal;
            }
        }
    }
}
