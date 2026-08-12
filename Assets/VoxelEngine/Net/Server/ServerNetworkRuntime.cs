using System;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Net.Server
{
    /// <summary>Composition root for authoritative UTP networking.</summary>
    public sealed class ServerNetworkRuntime : IDisposable, IAuthoritativeAlterationPublisher, IAlterationRejectionSink
    {
        private readonly UtpServerHost _host;
        private readonly EventDrivenReplicationPipeline _replication;
        private readonly AlterationBatchPacketSink _packetSink;
        private readonly IClientEventCommandHandler _eventHandler;
        private readonly IClientInputCommandHandler _inputHandler;
        private readonly IClientConvergenceCommandHandler _convergenceHandler;
        private readonly ServerCommandInbox _commandInbox;
        private readonly ServerConvergenceInbox _convergenceInbox;
        private bool _disposed;

        public event Action<uint, NetworkEndpoint> ConnectionOpened;
        public event Action<uint> ConnectionClosed;
        public event Action<uint> ProtocolError;
        public event Action<uint, int> SendError;

        public ServerNetworkRuntime(
            ServerCommandInbox commandInbox,
            int maxConnections = 64,
            int initialEventCapacity = 64)
            : this(commandInbox, commandInbox, null, maxConnections, initialEventCapacity)
        {
        }

        public ServerNetworkRuntime(
            ServerCommandInbox commandInbox,
            ServerConvergenceInbox convergenceInbox,
            int maxConnections = 64,
            int initialEventCapacity = 64)
            : this(commandInbox, commandInbox, convergenceInbox, maxConnections, initialEventCapacity)
        {
        }

        public ServerNetworkRuntime(
            IClientEventCommandHandler eventHandler,
            int maxConnections = 64,
            int initialEventCapacity = 64)
            : this(eventHandler, null, null, maxConnections, initialEventCapacity)
        {
        }

        public ServerNetworkRuntime(
            IClientEventCommandHandler eventHandler,
            IClientInputCommandHandler inputHandler,
            IClientConvergenceCommandHandler convergenceHandler,
            int maxConnections = 64,
            int initialEventCapacity = 64)
        {
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _inputHandler = inputHandler;
            _convergenceHandler = convergenceHandler;
            _commandInbox = ReferenceEquals(eventHandler, inputHandler) ? eventHandler as ServerCommandInbox : null;
            _convergenceInbox = convergenceHandler as ServerConvergenceInbox;
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

        public int Listen(NetworkEndpoint endpoint)
        {
            ThrowIfDisposed();
            return _host.Listen(endpoint);
        }

        public bool ContainsConnection(uint connectionId)
        {
            ThrowIfDisposed();
            return _host.ContainsConnection(connectionId);
        }

        public void PumpTransport()
        {
            ThrowIfDisposed();
            _host.Pump(_eventHandler, _inputHandler, _convergenceHandler);
        }

        public void BeginTick(uint tick)
        {
            ThrowIfDisposed();
            _replication.BeginTick(tick);
        }

        public void PublishAlteration(in AlterationEvent evt)
        {
            ThrowIfDisposed();
            _replication.PublishAlteration(in evt);
        }

        public void SendAlterationRejected(uint connectionId, in S_AlterationRejected rejection)
        {
            ThrowIfDisposed();
            Span<byte> packet = stackalloc byte[AlterationRejectedPacket.PacketSize];
            if (AlterationRejectedPacket.TryEncode(packet, in rejection))
                _host.TrySend(connectionId, UtpChannel.Event, packet);
        }

        /// <summary>Queue one tick-scoped semantic hash behind earlier EVENT mutations.</summary>
        public bool SendRegionHash(uint connectionId, in S_RegionHash hash)
        {
            ThrowIfDisposed();
            Span<byte> packet = stackalloc byte[RegionHashPacket.PacketSize];
            return RegionHashPacket.TryEncode(packet, in hash) &&
                   _host.TrySend(connectionId, UtpChannel.Event, packet);
        }

        public int UpdateConnectionPosition(uint connectionId, int3 playerVoxelPosition)
        {
            ThrowIfDisposed();
            if (!_host.ContainsConnection(connectionId))
                return 0;
            return _replication.UpdateConnectionPosition(connectionId, playerVoxelPosition);
        }

        public void EndTick()
        {
            ThrowIfDisposed();
            _replication.Flush(_packetSink);
            _host.FlushSends();
        }

        public bool TrySend(uint connectionId, UtpChannel channel, ReadOnlySpan<byte> packet)
        {
            ThrowIfDisposed();
            return _host.TrySend(connectionId, channel, packet);
        }

        public bool Disconnect(uint connectionId)
        {
            ThrowIfDisposed();
            return _host.Disconnect(connectionId);
        }

        private void OnConnectionOpened(uint connectionId, NetworkEndpoint endpoint) =>
            ConnectionOpened?.Invoke(connectionId, endpoint);

        private void OnConnectionClosed(uint connectionId)
        {
            _replication.RemoveConnection(connectionId);
            _commandInbox?.RemoveConnection(connectionId);
            _convergenceInbox?.RemoveConnection(connectionId);
            ConnectionClosed?.Invoke(connectionId);
        }

        private void OnProtocolError(uint connectionId) => ProtocolError?.Invoke(connectionId);
        private void OnSendError(uint connectionId, int errorCode) => SendError?.Invoke(connectionId, errorCode);

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServerNetworkRuntime));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _host.ConnectionOpened -= OnConnectionOpened;
            _host.ConnectionClosed -= OnConnectionClosed;
            _host.ProtocolError -= OnProtocolError;
            _host.SendError -= OnSendError;
            _host.Dispose();
            _disposed = true;
        }
    }
}
