using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Interest;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Fixed-tick producer and throttled sender for semantic full-region BULK snapshots.
    /// A transfer is fenced on reliable EVENT before it is allowed onto BULK.
    /// </summary>
    public sealed class ServerBulkRegionStateManager
    {
        public const int MaxPendingSnapshotBytes = 64 * 1024 * 1024;
        public const int MaxDeferredRequests = 256;
        public const int DefaultMaxTransfersPerConnection = 1;
        public const int DefaultMaxPacketsPerTick = 8;

        private readonly ServerRegionStateRequestInbox _inbox;
        private readonly ServerPlayerRegistry _players;
        private readonly int _maxTransfersPerConnection;
        private readonly int _maxPacketsPerTick;
        private readonly List<ServerRegionStateRequestInbox.QueuedRegionRequest> _requests =
            new List<ServerRegionStateRequestInbox.QueuedRegionRequest>(32);
        private readonly List<PendingTransfer> _transfers = new List<PendingTransfer>(16);
        private readonly Dictionary<uint, BulkThrottle> _throttles = new Dictionary<uint, BulkThrottle>(64);
        private readonly HashSet<uint> _sentThisTick = new HashSet<uint>();

        private uint _nextTransferId = 1;
        private int _pendingSnapshotBytes;

        public long AcceptedRequests { get; private set; }
        public long RejectedRequests { get; private set; }
        public long DroppedDeferredRequests { get; private set; }
        public long SnapshotsTooLarge { get; private set; }
        public long FenceSendDeferrals { get; private set; }
        public long BulkPacketsSent { get; private set; }
        public long BulkBytesSent { get; private set; }
        public long CompletedTransfers { get; private set; }
        public int PendingTransferCount => _transfers.Count;
        public int PendingSnapshotBytes => _pendingSnapshotBytes;
        public int DeferredRequestCount => _requests.Count;

        public ServerBulkRegionStateManager(
            ServerRegionStateRequestInbox inbox,
            ServerPlayerRegistry players,
            int maxTransfersPerConnection = DefaultMaxTransfersPerConnection,
            int maxPacketsPerTick = DefaultMaxPacketsPerTick)
        {
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            if (maxTransfersPerConnection <= 0) throw new ArgumentOutOfRangeException(nameof(maxTransfersPerConnection));
            if (maxPacketsPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(maxPacketsPerTick));
            _maxTransfersPerConnection = maxTransfersPerConnection;
            _maxPacketsPerTick = maxPacketsPerTick;
        }

        public int ProcessRequests(
            uint serverTick,
            IRegionSnapshotSource snapshots,
            RegionSubscriptionIndex subscriptions,
            ServerNetworkRuntime network)
        {
            if (serverTick == 0) throw new ArgumentOutOfRangeException(nameof(serverTick));
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            if (subscriptions == null) throw new ArgumentNullException(nameof(subscriptions));
            if (network == null) throw new ArgumentNullException(nameof(network));

            _inbox.Drain(_requests);
            while (_requests.Count > MaxDeferredRequests)
            {
                _requests.RemoveAt(_requests.Count - 1);
                DroppedDeferredRequests++;
            }

            int accepted = 0;
            for (int i = 0; i < _requests.Count;)
            {
                var queued = _requests[i];
                C_RegionRequest request = queued.Request;

                if (request.haveMipLevel != RegionRequestPacket.FullSemanticState ||
                    !_players.TryGetByConnection(queued.ConnectionId, out _) ||
                    !subscriptions.IsSubscribed(queued.ConnectionId, request.regionCoord))
                {
                    RejectedRequests++;
                    _requests.RemoveAt(i);
                    continue;
                }

                if (HasPendingTransfer(queued.ConnectionId, request.regionCoord))
                {
                    _requests.RemoveAt(i);
                    continue;
                }

                // The current client fence/catch-up state machine intentionally supports one
                // current-state transfer per connection. Additional requests remain bounded/deferred
                // until the active fence is consumed and transfer completes.
                if (CountTransfers(queued.ConnectionId) >= _maxTransfersPerConnection ||
                    _pendingSnapshotBytes >= MaxPendingSnapshotBytes)
                {
                    i++;
                    continue;
                }

                RegionSnapshotCaptureResult captureResult = snapshots.CaptureSemanticSnapshot(
                    request.regionCoord,
                    RegionStateChunkPacket.MaxSnapshotBytes,
                    out RegionSemanticSnapshot semanticSnapshot);

                if (captureResult == RegionSnapshotCaptureResult.NotResident)
                {
                    SendUnavailable(queued.ConnectionId, request.regionCoord, serverTick, network);
                    RejectedRequests++;
                    _requests.RemoveAt(i);
                    continue;
                }

                if (captureResult == RegionSnapshotCaptureResult.TooLarge)
                {
                    SnapshotsTooLarge++;
                    SendUnavailable(queued.ConnectionId, request.regionCoord, serverTick, network);
                    _requests.RemoveAt(i);
                    continue;
                }

                byte[] snapshot = semanticSnapshot.Bytes;
                if (snapshot == null)
                {
                    SendUnavailable(queued.ConnectionId, request.regionCoord, serverTick, network);
                    RejectedRequests++;
                    _requests.RemoveAt(i);
                    continue;
                }

                if (_pendingSnapshotBytes + snapshot.Length > MaxPendingSnapshotBytes)
                {
                    i++;
                    continue;
                }

                uint transferId = AllocateTransferId();
                var fence = new S_RegionStateFence(transferId, request.regionCoord, serverTick);

                if (!network.SendRegionStateFence(queued.ConnectionId, in fence))
                {
                    FenceSendDeferrals++;
                    i++;
                    continue;
                }

                _transfers.Add(new PendingTransfer(
                    queued.ConnectionId,
                    transferId,
                    request.regionCoord,
                    serverTick,
                    semanticSnapshot.SemanticHash,
                    snapshot));
                _pendingSnapshotBytes += snapshot.Length;
                AcceptedRequests++;
                accepted++;
                _requests.RemoveAt(i);
            }

            return accepted;
        }

        /// <summary>
        /// Send at most one BULK packet per connection this tick and at most the global packet cap.
        /// The per-connection rolling throttle preserves latency headroom for EVENT/EPHEMERAL.
        /// </summary>
        public int Flush(uint serverTick, ServerNetworkRuntime network)
        {
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (serverTick == 0) throw new ArgumentOutOfRangeException(nameof(serverTick));

            _sentThisTick.Clear();
            Span<byte> packet = stackalloc byte[RegionStateChunkPacket.MaxPacketSize];
            int sent = 0;

            for (int i = 0; i < _transfers.Count && sent < _maxPacketsPerTick;)
            {
                PendingTransfer transfer = _transfers[i];
                if (!network.ContainsConnection(transfer.ConnectionId))
                {
                    RemoveTransferAt(i);
                    continue;
                }

                if (_sentThisTick.Contains(transfer.ConnectionId))
                {
                    i++;
                    continue;
                }

                int chunkLength = Math.Min(
                    RegionStateChunkPacket.MaxChunkBytes,
                    transfer.Snapshot.Length - transfer.Offset);
                int packetLength = RegionStateChunkPacket.HeaderSize + chunkLength;

                BulkThrottle throttle = GetThrottle(transfer.ConnectionId);
                if (!throttle.TryAllow(packetLength, serverTick))
                {
                    _throttles[transfer.ConnectionId] = throttle;
                    i++;
                    continue;
                }

                if (!RegionStateChunkPacket.TryEncode(
                        packet,
                        transfer.TransferId,
                        transfer.RegionCoord,
                        transfer.SnapshotTick,
                        transfer.SemanticHash,
                        transfer.Snapshot.Length,
                        transfer.Offset,
                        transfer.Snapshot.AsSpan(transfer.Offset, chunkLength),
                        out int bytesWritten) ||
                    !network.TrySend(
                        transfer.ConnectionId,
                        UtpChannel.Bulk,
                        packet.Slice(0, bytesWritten)))
                {
                    _throttles[transfer.ConnectionId] = throttle;
                    i++;
                    continue;
                }

                throttle.MarkUsed(bytesWritten);
                _throttles[transfer.ConnectionId] = throttle;
                _sentThisTick.Add(transfer.ConnectionId);
                transfer.Offset += chunkLength;
                sent++;
                BulkPacketsSent++;
                BulkBytesSent += bytesWritten;

                if (transfer.Offset == transfer.Snapshot.Length)
                {
                    RemoveTransferAt(i);
                    CompletedTransfers++;
                }
                else
                {
                    _transfers[i] = transfer;
                    i++;
                }
            }

            return sent;
        }

        public void RemoveConnection(uint connectionId)
        {
            for (int i = _transfers.Count - 1; i >= 0; i--)
                if (_transfers[i].ConnectionId == connectionId)
                    RemoveTransferAt(i);

            for (int i = _requests.Count - 1; i >= 0; i--)
                if (_requests[i].ConnectionId == connectionId)
                    _requests.RemoveAt(i);

            _throttles.Remove(connectionId);
            _inbox.RemoveConnection(connectionId);
        }

        private BulkThrottle GetThrottle(uint connectionId)
        {
            if (_throttles.TryGetValue(connectionId, out BulkThrottle throttle))
                return throttle;

            throttle = new BulkThrottle(
                ChannelSetup.k_SustainedDownstreamWiredKb,
                ChannelSetup.k_EventShareWired);
            _throttles.Add(connectionId, throttle);
            return throttle;
        }

        private int CountTransfers(uint connectionId)
        {
            int count = 0;
            for (int i = 0; i < _transfers.Count; i++)
                if (_transfers[i].ConnectionId == connectionId)
                    count++;
            return count;
        }

        private bool HasPendingTransfer(uint connectionId, int3 regionCoord)
        {
            for (int i = 0; i < _transfers.Count; i++)
            {
                PendingTransfer transfer = _transfers[i];
                if (transfer.ConnectionId == connectionId && transfer.RegionCoord.Equals(regionCoord))
                    return true;
            }
            return false;
        }

        private void RemoveTransferAt(int index)
        {
            _pendingSnapshotBytes -= _transfers[index].Snapshot.Length;
            if (_pendingSnapshotBytes < 0) _pendingSnapshotBytes = 0;
            _transfers.RemoveAt(index);
        }

        private uint AllocateTransferId()
        {
            uint id = _nextTransferId++;
            if (_nextTransferId == 0) _nextTransferId = 1;
            return id == 0 ? AllocateTransferId() : id;
        }

        private static void SendUnavailable(
            uint connectionId,
            int3 regionCoord,
            uint failedTick,
            ServerNetworkRuntime network)
        {
            var unavailable = new S_RegionResyncRequired(
                regionCoord,
                failedTick,
                S_RegionResyncRequired.Reason.ServerStateUnavailable);
            network.SendRegionResyncRequired(connectionId, in unavailable);
        }

        private struct PendingTransfer
        {
            public uint ConnectionId;
            public uint TransferId;
            public int3 RegionCoord;
            public uint SnapshotTick;
            public uint SemanticHash;
            public byte[] Snapshot;
            public int Offset;

            public PendingTransfer(
                uint connectionId,
                uint transferId,
                int3 regionCoord,
                uint snapshotTick,
                uint semanticHash,
                byte[] snapshot)
            {
                ConnectionId = connectionId;
                TransferId = transferId;
                RegionCoord = regionCoord;
                SnapshotTick = snapshotTick;
                SemanticHash = semanticHash;
                Snapshot = snapshot;
                Offset = 0;
            }
        }
    }
}
