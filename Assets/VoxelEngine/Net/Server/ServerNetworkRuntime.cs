using System;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Api;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Net.Server
{
    /// <summary>Composition root for authoritative UTP networking.</summary>
    public sealed class ServerNetworkRuntime : IDisposable, IAuthoritativeAlterationPublisher, IAlterationRejectionSink, IPlayerStateBundleSink
    {
        private readonly UtpServerHost _host;
        private readonly EventDrivenReplicationPipeline _replication;
        private readonly AlterationBatchPacketSink _packetSink;
        private readonly IClientEventCommandHandler _eventHandler;
        private readonly IClientInputCommandHandler _inputHandler;
        private readonly IClientConvergenceCommandHandler _convergenceHandler;
        private readonly IClientRegionRequestHandler _regionRequestHandler;
        private readonly ServerCommandInbox _commandInbox;
        private readonly ServerConvergenceInbox _convergenceInbox;
        private readonly ServerRegionStateRequestInbox _regionRequestInbox;
        private bool _disposed;

        public event Action<uint, NetworkEndpoint> ConnectionOpened;
        public event Action<uint> ConnectionClosed;
        public event Action<uint> ProtocolError;
        public event Action<uint, int> SendError;

        public ServerNetworkRuntime(
            ServerCommandInbox commandInbox,
            int maxConnections = 64,
            int initialEventCapacity = 64)
            : this(commandInbox, commandInbox, null, null, maxConnections, initialEventCapacity) { }

        public ServerNetworkRuntime(
            ServerCommandInbox commandInbox,
            ServerConvergenceInbox convergenceInbox,
            int maxConnections = 64,
            int initialEventCapacity = 64)
            : this(commandInbox, commandInbox, convergenceInbox, null, maxConnections, initialEventCapacity) { }

        public ServerNetworkRuntime(
            ServerCommandInbox commandInbox,
            ServerConvergenceInbox convergenceInbox,
            ServerRegionStateRequestInbox regionRequestInbox,
            int maxConnections = 64,
            int initialEventCapacity = 64)
            : this(commandInbox, commandInbox, convergenceInbox, regionRequestInbox, maxConnections, initialEventCapacity) { }

        public ServerNetworkRuntime(
            IClientEventCommandHandler eventHandler,
            int maxConnections = 64,
            int initialEventCapacity = 64)
            : this(eventHandler, null, null, null, maxConnections, initialEventCapacity) { }

        public ServerNetworkRuntime(
            IClientEventCommandHandler eventHandler,
            IClientInputCommandHandler inputHandler,
            IClientConvergenceCommandHandler convergenceHandler,
            int maxConnections = 64,
            int initialEventCapacity = 64)
            : this(eventHandler, inputHandler, convergenceHandler, null, maxConnections, initialEventCapacity) { }

        public ServerNetworkRuntime(
            IClientEventCommandHandler eventHandler,
            IClientInputCommandHandler inputHandler,
            IClientConvergenceCommandHandler convergenceHandler,
            IClientRegionRequestHandler regionRequestHandler,
            int maxConnections = 64,
            int initialEventCapacity = 64)
        {
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _inputHandler = inputHandler;
            _convergenceHandler = convergenceHandler;
            _regionRequestHandler = regionRequestHandler;
            _commandInbox = ReferenceEquals(eventHandler, inputHandler) ? eventHandler as ServerCommandInbox : null;
            _convergenceInbox = convergenceHandler as ServerConvergenceInbox;
            _regionRequestInbox = regionRequestHandler as ServerRegionStateRequestInbox;
            _host = new UtpServerHost(maxConnections);
            _replication = new EventDrivenReplicationPipeline(initialEventCapacity);
            _packetSink = new AlterationBatchPacketSink(_host);

            _host.ConnectionOpened += OnConnectionOpened;
            _host.ConnectionClosed += OnConnectionClosed;
            _host.ProtocolError += OnProtocolError;
            _host.SendError += OnSendError;
        }

        public bool IsListening => !_disposed && _host.IsListening;
        public int ConnectionCount => _disposed ? 0 : _host.ConnectionCount;
        public uint ReplicationTick => _replication.CurrentTick;
        public NetworkEndpoint LocalEndpoint => _disposed ? default : _host.LocalEndpoint;
        public EventDrivenReplicationPipeline Replication => _replication;
        public ServerCommandInbox CommandInbox => _commandInbox;
        public ServerConvergenceInbox ConvergenceInbox => _convergenceInbox;
        public ServerRegionStateRequestInbox RegionRequestInbox => _regionRequestInbox;

        public int Listen(NetworkEndpoint endpoint) { ThrowIfDisposed(); return _host.Listen(endpoint); }
        public bool ContainsConnection(uint connectionId) { ThrowIfDisposed(); return _host.ContainsConnection(connectionId); }

        public void PumpTransport()
        {
            ThrowIfDisposed();
            _host.Pump(_eventHandler, _inputHandler, _convergenceHandler, _regionRequestHandler);
        }

        public void BeginTick(uint tick) { ThrowIfDisposed(); _replication.BeginTick(tick); }
        public void PublishAlteration(in AlterationEvent evt) { ThrowIfDisposed(); _replication.PublishAlteration(in evt); }

        public void SendAlterationRejected(uint connectionId, in S_AlterationRejected rejection)
        {
            ThrowIfDisposed();
            Span<byte> packet = stackalloc byte[AlterationRejectedPacket.PacketSize];
            if (AlterationRejectedPacket.TryEncode(packet, in rejection))
                _host.TrySend(connectionId, UtpChannel.Event, packet);
        }

        public bool SendRegionHash(uint connectionId, in S_RegionHash hash)
        {
            ThrowIfDisposed();
            Span<byte> packet = stackalloc byte[RegionHashPacket.PacketSize];
            return RegionHashPacket.TryEncode(packet, in hash) &&
                   _host.TrySend(connectionId, UtpChannel.Event, packet);
        }

        public bool SendRegionResyncRequired(uint connectionId, in S_RegionResyncRequired message)
        {
            ThrowIfDisposed();
            Span<byte> packet = stackalloc byte[RegionResyncRequiredPacket.PacketSize];
            return RegionResyncRequiredPacket.TryEncode(packet, in message) &&
                   _host.TrySend(connectionId, UtpChannel.Event, packet);
        }

        public bool SendRegionStateFence(uint connectionId, in S_RegionStateFence fence)
        {
            ThrowIfDisposed();
            Span<byte> packet = stackalloc byte[RegionStateFencePacket.PacketSize];
            return RegionStateFencePacket.TryEncode(packet, in fence) &&
                   _host.TrySend(connectionId, UtpChannel.Event, packet);
        }

        public bool SendPlayerStateBundle(uint connectionId, ReadOnlySpan<S_PlayerState> states)
        {
            ThrowIfDisposed();
            Span<byte> packet = stackalloc byte[PlayerStateBundlePacket.MaxPacketSize];
            return PlayerStateBundlePacket.TryEncode(packet, states, out int bytesWritten) &&
                   _host.TrySend(connectionId, UtpChannel.Ephemeral, packet.Slice(0, bytesWritten));
        }

        public int UpdateConnectionPosition(uint connectionId, int3 playerVoxelPosition)
        {
            ThrowIfDisposed();
            return _host.ContainsConnection(connectionId)
                ? _replication.UpdateConnectionPosition(connectionId, playerVoxelPosition)
                : 0;
        }

        public void FlushReplication()
        {
            ThrowIfDisposed();
            _replication.Flush(_packetSink);
        }

        public void FlushSends()
        {
            ThrowIfDisposed();
            _host.FlushSends();
        }

        public void EndTick()
        {
            FlushReplication();
            FlushSends();
        }

        public bool TrySend(uint connectionId, UtpChannel channel, ReadOnlySpan<byte> packet)
        {
            ThrowIfDisposed();
            return _host.TrySend(connectionId, channel, packet);
        }

        public bool Disconnect(uint connectionId) { ThrowIfDisposed(); return _host.Disconnect(connectionId); }

        private void OnConnectionOpened(uint connectionId, NetworkEndpoint endpoint) => ConnectionOpened?.Invoke(connectionId, endpoint);

        private void OnConnectionClosed(uint connectionId)
        {
            _replication.RemoveConnection(connectionId);
            _commandInbox?.RemoveConnection(connectionId);
            _convergenceInbox?.RemoveConnection(connectionId);
            _regionRequestInbox?.RemoveConnection(connectionId);
            ConnectionClosed?.Invoke(connectionId);
        }

        private void OnProtocolError(uint connectionId) => ProtocolError?.Invoke(connectionId);
        private void OnSendError(uint connectionId, int errorCode) => SendError?.Invoke(connectionId, errorCode);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ServerNetworkRuntime));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _host.ConnectionOpened -= OnConnectionOpened;
            _host.ConnectionClosed -= OnConnectionClosed;
            _host.ProtocolError -= OnProtocolError;
            _host.SendError -= OnSendError;
            _host.Dispose();
            _disposed = true;
        }
    }
}
