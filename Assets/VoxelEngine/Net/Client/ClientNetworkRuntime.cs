using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Client-side composition root. Transport callbacks decode/assemble protocol state only;
    /// authoritative voxel replacement and local prediction rewind/replay occur from explicit update
    /// calls outside NetworkDriver dispatch.
    /// </summary>
    public sealed class ClientNetworkRuntime : IDisposable, IUtpClientPacketHandler, IRegionHashMismatchSink
    {
        private const int MaxPendingPlayerSnapshots = 64;

        private readonly UtpClientHost _host;
        private readonly ClientAuthoritativeEventQueue _events;
        private readonly ClientRegionRepairAssembler _repair;
        private readonly ClientRegionStateAssembler _fullState;
        private readonly ClientPredictionReconciler _prediction;
        private readonly ClientPlayerStateTimeline _playerTimeline;
        private readonly Dictionary<ushort, S_PlayerState> _pendingPlayerStates =
            new Dictionary<ushort, S_PlayerState>(16);
        private readonly IClientEventNotificationSink _notifications;
        private IClientPredictionAdapter _predictionAdapter;
        private ushort _localPlayerId;
        private bool _disposed;
        private bool _fullRegionResyncRequired;
        private S_RegionResyncRequired _lastResyncRequirement;
        private bool _automaticFullStateRequestPending;
        private int3 _automaticFullStateRequestRegion;
        private bool _resyncNotificationPending;
        private S_RegionResyncRequired _pendingResyncNotification;

        public event Action Connected;
        public event Action Disconnected;
        public event Action PacketRejected;
        public event Action<int> SendError;
        public event Action<C_RegionHashMismatch> RegionHashMismatchDetected;
        public event Action<int3, uint> RegionRepairApplied;
        public event Action<S_RegionResyncRequired> FullRegionResyncRequired;
        public event Action<int3, uint> FullRegionStateApplied;
        public event Action<S_PlayerState> PlayerStateReceived;
        public event Action<S_PlayerState, int> LocalPlayerReconciled;

        public ClientNetworkRuntime(
            IClientEventNotificationSink notifications = null,
            int maxPendingAuthoritativeEvents = ClientAuthoritativeEventQueue.DefaultMaxPendingEvents,
            int predictionHistoryCapacity = ClientPredictionReconciler.DefaultHistoryCapacity)
        {
            _notifications = notifications;
            _events = new ClientAuthoritativeEventQueue(maxPendingAuthoritativeEvents);
            _repair = new ClientRegionRepairAssembler();
            _fullState = new ClientRegionStateAssembler();
            _prediction = new ClientPredictionReconciler(predictionHistoryCapacity);
            _playerTimeline = new ClientPlayerStateTimeline();
            _host = new UtpClientHost();

            _host.Connected += OnConnected;
            _host.Disconnected += OnDisconnected;
            _host.PacketRejected += OnPacketRejected;
            _host.SendError += OnSendError;
        }

        public bool IsConnected => !_disposed && _host.IsConnected;
        public NetworkEndpoint LocalEndpoint => _disposed ? default : _host.LocalEndpoint;
        public int PendingAuthoritativeEvents => _events.PendingEventCount;
        public int PendingAuthoritativeBatches => _events.PendingBatchCount;
        public int PendingRegionHashes => _events.PendingHashCount;
        public int PendingRegionStateFences => _events.PendingFenceCount;
        public int PendingPredictionInputs => _prediction.Count;
        public int PendingPlayerStateUpdates => _pendingPlayerStates.Count;
        public ushort LocalPlayerId => _localPlayerId;
        public bool RepairPending => _events.RepairPending;
        public bool RepairSnapshotComplete => _repair.IsComplete;
        public bool FullSnapshotWaitPending => _events.FullSnapshotWaitPending;
        public bool SnapshotCatchupActive => _events.SnapshotCatchupActive;
        public bool IsFullRegionResyncRequired => _fullRegionResyncRequired;
        public S_RegionResyncRequired LastResyncRequirement => _lastResyncRequirement;
        public bool FullStateTransferReceiving => _fullState.IsReceiving;
        public int CompletedFullStateTransfers => _fullState.CompletedCount;

        public bool Connect(NetworkEndpoint endpoint) { ThrowIfDisposed(); return _host.Connect(endpoint); }

        /// <summary>Bind the authenticated local player identity to game-owned prediction code.</summary>
        public void ConfigureLocalPrediction(ushort localPlayerId, IClientPredictionAdapter adapter)
        {
            ThrowIfDisposed();
            if (localPlayerId == 0) throw new ArgumentOutOfRangeException(nameof(localPlayerId));
            _predictionAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _localPlayerId = localPlayerId;
            _prediction.Reset();
        }

        public void ClearLocalPrediction()
        {
            ThrowIfDisposed();
            _prediction.Reset();
            _predictionAdapter = null;
            _localPlayerId = 0;
        }

        public bool TrySampleRemotePlayer(ushort playerId, float interpolationAlpha, out RemotePlayerSample sample)
        {
            ThrowIfDisposed();
            return _playerTimeline.TrySample(playerId, interpolationAlpha, out sample);
        }

        public void PumpTransport()
        {
            ThrowIfDisposed();
            _host.Pump(this);

            if (_automaticFullStateRequestPending)
            {
                int3 region = _automaticFullStateRequestRegion;
                _automaticFullStateRequestPending = false;
                _automaticFullStateRequestRegion = default;
                if (!TryRequestFullRegionState(region))
                    PacketRejected?.Invoke();
            }

            if (_resyncNotificationPending)
            {
                S_RegionResyncRequired notification = _pendingResyncNotification;
                _resyncNotificationPending = false;
                _pendingResyncNotification = default;
                FullRegionResyncRequired?.Invoke(notification);
            }
        }

        /// <summary>
        /// Apply the newest pending state for each player outside the transport callback. Local state
        /// rewinds/replays prediction; remote state is already available through the interpolation
        /// timeline and is surfaced here as a notification.
        /// </summary>
        public int ApplyPlayerStateUpdates(out int replayedLocalInputs)
        {
            ThrowIfDisposed();
            replayedLocalInputs = 0;
            if (_pendingPlayerStates.Count == 0)
                return 0;

            int applied = 0;
            foreach (var pair in _pendingPlayerStates)
            {
                S_PlayerState state = pair.Value;
                if (state.playerId == _localPlayerId && _predictionAdapter != null)
                {
                    int replayed = _prediction.Reconcile(in state, _predictionAdapter);
                    replayedLocalInputs += replayed;
                    LocalPlayerReconciled?.Invoke(state, replayed);
                }

                PlayerStateReceived?.Invoke(state);
                applied++;
            }

            _pendingPlayerStates.Clear();
            return applied;
        }

        public int ApplyReadyAuthoritativeEvents(
            ref RegionTable table,
            ref BrickPool pool,
            out int appliedEvents)
        {
            ThrowIfDisposed();

            if (_fullState.TryDequeue(out var full))
            {
                if (!_events.FullSnapshotWaitPending ||
                    !_events.FullSnapshotWaitRegion.Equals(full.RegionCoord) ||
                    !SemanticRegionSnapshotCodec.TryComputeSemanticHash(
                        full.RegionCoord,
                        full.Snapshot,
                        out uint encodedHash) ||
                    encodedHash != full.SemanticHash)
                {
                    PacketRejected?.Invoke();
                    appliedEvents = 0;
                    return 0;
                }

                if (!table.TryGetRegion(full.RegionCoord, out _))
                    table.LoadRegion(full.RegionCoord);

                if (!SemanticRegionSnapshotCodec.TryApply(
                        ref table,
                        ref pool,
                        full.RegionCoord,
                        full.Snapshot) ||
                    !table.TryGetRegion(full.RegionCoord, out Region fullRegion) ||
                    SemanticRegionHasher.HashRegion(in fullRegion, in pool) != full.SemanticHash ||
                    !_events.CompleteFullRegionSnapshot(
                        full.TransferId,
                        full.RegionCoord,
                        full.SnapshotTick))
                {
                    PacketRejected?.Invoke();
                    appliedEvents = 0;
                    return 0;
                }

                if (_fullRegionResyncRequired &&
                    _lastResyncRequirement.regionCoord.Equals(full.RegionCoord))
                {
                    _fullRegionResyncRequired = false;
                    _lastResyncRequirement = default;
                }

                FullRegionStateApplied?.Invoke(full.RegionCoord, full.SnapshotTick);
            }

            if (_events.RepairPending && !_events.FullSnapshotWaitPending)
            {
                if (!_repair.IsComplete)
                {
                    appliedEvents = 0;
                    return 0;
                }

                int3 repairedRegion = _repair.RegionCoord;
                uint repairedTick = _repair.SnapshotTick;
                if (!_repair.TryApplyCompleted(ref table, ref pool, _events))
                {
                    appliedEvents = 0;
                    return 0;
                }

                RegionRepairApplied?.Invoke(repairedRegion, repairedTick);
            }

            return _events.DrainReady(
                ref table,
                ref pool,
                out appliedEvents,
                this,
                out _);
        }

        public bool TrySendPlayerInput(in C_PlayerInput input)
        {
            ThrowIfDisposed();
            if (!_host.TrySendPlayerInput(in input))
                return false;

            _prediction.RecordSentInput(in input);
            return true;
        }

        public bool TrySendAlterationRequest(in C_AlterationRequest request)
        {
            ThrowIfDisposed();
            return _host.TrySendAlterationRequest(in request);
        }

        public bool TryRequestFullRegionState(int3 regionCoord)
        {
            ThrowIfDisposed();
            if (_events.SnapshotCatchupActive || _fullState.IsReceiving)
                return false;
            if (_events.RepairPending && !_events.RepairRegion.Equals(regionCoord))
                return false;
            if (_events.FullSnapshotWaitPending && !_events.FullSnapshotWaitRegion.Equals(regionCoord))
                return false;

            var request = new C_RegionRequest(regionCoord, RegionRequestPacket.FullSemanticState);
            Span<byte> packet = stackalloc byte[RegionRequestPacket.PacketSize];
            if (!RegionRequestPacket.TryEncode(packet, in request) ||
                !_host.TrySend(UtpChannel.Event, packet) ||
                !_events.BeginFullRegionSnapshotWait(regionCoord))
                return false;

            _host.FlushSends();
            return true;
        }

        public void FlushSends() { ThrowIfDisposed(); _host.FlushSends(); }

        public void ResetAfterAuthoritativeSnapshot()
        {
            ThrowIfDisposed();
            ResetProtocolState();
        }

        public void Disconnect() { ThrowIfDisposed(); _host.Disconnect(); }

        bool IUtpClientPacketHandler.HandlePacket(UtpChannel channel, ReadOnlySpan<byte> packet)
        {
            switch (channel)
            {
                case UtpChannel.Event:
                    return HandleEventPacket(packet);
                case UtpChannel.Ephemeral:
                    return HandleEphemeralPacket(packet);
                case UtpChannel.Repair:
                    return _repair.TryAcceptPacket(packet);
                case UtpChannel.Bulk:
                    return _fullState.TryAcceptPacket(packet);
                default:
                    return false;
            }
        }

        private bool HandleEphemeralPacket(ReadOnlySpan<byte> packet)
        {
            Span<S_PlayerState> decoded = stackalloc S_PlayerState[PlayerStateBundlePacket.MaxStates];
            if (!PlayerStateBundlePacket.TryDecode(packet, decoded, out int count))
                return false;

            for (int i = 0; i < count; i++)
            {
                S_PlayerState state = decoded[i];
                if (!_playerTimeline.TryAccept(in state))
                    continue; // valid but stale/reordered supersedable snapshot.

                if (_pendingPlayerStates.Count >= MaxPendingPlayerSnapshots &&
                    !_pendingPlayerStates.ContainsKey(state.playerId))
                    continue;

                _pendingPlayerStates[state.playerId] = state;
            }

            return true;
        }

        private bool HandleEventPacket(ReadOnlySpan<byte> packet)
        {
            if (!ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out _))
                return false;

            if (kind != ProtocolMessageKind.S_RegionResyncRequired)
                return _events.TryEnqueueEventPacket(packet, _notifications);

            if (!RegionResyncRequiredPacket.TryDecode(packet, out S_RegionResyncRequired requirement) ||
                !_events.BeginFullRegionSnapshotWait(requirement.regionCoord))
                return false;

            _fullRegionResyncRequired = true;
            _lastResyncRequirement = requirement;
            _resyncNotificationPending = true;
            _pendingResyncNotification = requirement;

            if (requirement.reason != S_RegionResyncRequired.Reason.ServerStateUnavailable)
            {
                _automaticFullStateRequestPending = true;
                _automaticFullStateRequestRegion = requirement.regionCoord;
            }

            return true;
        }

        void IRegionHashMismatchSink.OnRegionHashMismatch(in C_RegionHashMismatch mismatch)
        {
            Span<byte> packet = stackalloc byte[RegionHashMismatchPacket.PacketSize];
            if (!RegionHashMismatchPacket.TryEncode(packet, in mismatch) ||
                !_host.TrySend(UtpChannel.Event, packet))
            {
                PacketRejected?.Invoke();
                return;
            }

            _host.FlushSends();
            RegionHashMismatchDetected?.Invoke(mismatch);
        }

        private void OnConnected() => Connected?.Invoke();

        private void OnDisconnected()
        {
            ResetProtocolState();
            Disconnected?.Invoke();
        }

        private void ResetProtocolState()
        {
            _repair.Reset();
            _fullState.Reset();
            _events.ResetAfterAuthoritativeSnapshot();
            _prediction.Reset();
            _playerTimeline.Reset();
            _pendingPlayerStates.Clear();
            _fullRegionResyncRequired = false;
            _lastResyncRequirement = default;
            _automaticFullStateRequestPending = false;
            _automaticFullStateRequestRegion = default;
            _resyncNotificationPending = false;
            _pendingResyncNotification = default;
        }

        private void OnPacketRejected() => PacketRejected?.Invoke();
        private void OnSendError(int errorCode) => SendError?.Invoke(errorCode);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ClientNetworkRuntime));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _host.Connected -= OnConnected;
            _host.Disconnected -= OnDisconnected;
            _host.PacketRejected -= OnPacketRejected;
            _host.SendError -= OnSendError;
            _host.Dispose();
            ResetProtocolState();
            _predictionAdapter = null;
            _localPlayerId = 0;
            _disposed = true;
        }
    }
}
