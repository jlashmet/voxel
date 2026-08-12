using System;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Client-side composition root. EVENT authority is ordered; REPAIR packets are assembled off
    /// the callback path and applied only during the explicit world-update call.
    /// </summary>
    public sealed class ClientNetworkRuntime : IDisposable, IUtpClientPacketHandler, IRegionHashMismatchSink
    {
        private readonly UtpClientHost _host;
        private readonly ClientAuthoritativeEventQueue _events;
        private readonly ClientRegionRepairAssembler _repair;
        private readonly IClientEventNotificationSink _notifications;
        private bool _disposed;

        public event Action Connected;
        public event Action Disconnected;
        public event Action PacketRejected;
        public event Action<int> SendError;
        public event Action<C_RegionHashMismatch> RegionHashMismatchDetected;
        public event Action<int3, uint> RegionRepairApplied;

        public ClientNetworkRuntime(
            IClientEventNotificationSink notifications = null,
            int maxPendingAuthoritativeEvents = ClientAuthoritativeEventQueue.DefaultMaxPendingEvents)
        {
            _notifications = notifications;
            _events = new ClientAuthoritativeEventQueue(maxPendingAuthoritativeEvents);
            _repair = new ClientRegionRepairAssembler();
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
        public bool RepairPending => _events.RepairPending;
        public bool RepairSnapshotComplete => _repair.IsComplete;

        public bool Connect(NetworkEndpoint endpoint) { ThrowIfDisposed(); return _host.Connect(endpoint); }
        public void PumpTransport() { ThrowIfDisposed(); _host.Pump(this); }

        public int ApplyReadyAuthoritativeEvents(
            ref RegionTable table,
            ref BrickPool pool,
            out int appliedEvents)
        {
            ThrowIfDisposed();

            if (_events.RepairPending)
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

        public bool TrySendPlayerInput(in C_PlayerInput input) { ThrowIfDisposed(); return _host.TrySendPlayerInput(in input); }
        public bool TrySendAlterationRequest(in C_AlterationRequest request) { ThrowIfDisposed(); return _host.TrySendAlterationRequest(in request); }
        public void FlushSends() { ThrowIfDisposed(); _host.FlushSends(); }

        public void ResetAfterAuthoritativeSnapshot()
        {
            ThrowIfDisposed();
            _repair.Reset();
            _events.ResetAfterAuthoritativeSnapshot();
        }

        public void Disconnect() { ThrowIfDisposed(); _host.Disconnect(); }

        bool IUtpClientPacketHandler.HandlePacket(UtpChannel channel, ReadOnlySpan<byte> packet)
        {
            switch (channel)
            {
                case UtpChannel.Event:
                    return _events.TryEnqueueEventPacket(packet, _notifications);
                case UtpChannel.Repair:
                    return _repair.TryAcceptPacket(packet);
                default:
                    return false;
            }
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
        private void OnDisconnected() => Disconnected?.Invoke();
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
            _repair.Reset();
            _events.ResetAfterAuthoritativeSnapshot();
            _disposed = true;
        }
    }
}
