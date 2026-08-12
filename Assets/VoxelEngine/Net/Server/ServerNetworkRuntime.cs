using System;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Composition root for the authoritative networking path.
    ///
    /// Transport is pumped from the Unity frame loop, while BeginTick/EndTick are called by the
    /// fixed authoritative simulation clock. This keeps socket progress independent from the
    /// deterministic 30 Hz simulation without letting NetworkDriver leak into gameplay systems.
    /// </summary>
    public sealed class ServerNetworkRuntime : IDisposable
    {
        private readonly UtpServerHost _host;
        private readonly EventDrivenReplicationPipeline _replication;
        private readonly AlterationBatchPacketSink _packetSink;
        private readonly IClientEventCommandHandler _eventHandler;
        private readonly IClientInputCommandHandler _inputHandler;
        private bool _disposed;

        public event Action<uint, NetworkEndpoint> ConnectionOpened;
        public event Action<uint> ConnectionClosed;
        public event Action<uint> ProtocolError;
        public event Action<uint, int> SendError;

        public ServerNetworkRuntime(
            IClientEventCommandHandler eventHandler,
            int maxConnections = 64,
            int initialEventCapacity = 64)
            : this(eventHandler, null, maxConnections, initialEventCapacity)
        {
        }

        public ServerNetworkRuntime(
            IClientEventCommandHandler eventHandler,
            IClientInputCommandHandler inputHandler,
            int maxConnections = 64,
            int initialEventCapacity = 64)
        {
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _inputHandler = inputHandler;
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

        public int Listen(NetworkEndpoint endpoint)
        {
            ThrowIfDisposed();
            return _host.Listen(endpoint);
        }

        /// <summary>
        /// Advance UTP connection/data state once. Reliable durable commands and unreliable
        /// sequenced player input are dispatched independently to their authoritative handlers.
        /// </summary>
        public void PumpTransport()
        {
            ThrowIfDisposed();
            _host.Pump(_eventHandler, _inputHandler);
        }

        public void BeginTick(uint tick)
        {
            ThrowIfDisposed();
            _replication.BeginTick(tick);
        }

        /// <summary>
        /// Publish a server-accepted alteration. Validation/application must happen before this call;
        /// networking is observing an authoritative fact, not deciding whether the request is legal.
        /// </summary>
        public void PublishAlteration(in AlterationEvent evt)
        {
            ThrowIfDisposed();
            _replication.PublishAlteration(in evt);
        }

        public int UpdateConnectionPosition(uint connectionId, int3 playerVoxelPosition)
        {
            ThrowIfDisposed();
            if (!_host.ContainsConnection(connectionId))
                return 0;

            return _replication.UpdateConnectionPosition(connectionId, playerVoxelPosition);
        }

        /// <summary>
        /// Seal the current authoritative tick, route/encode all accepted mutations, then flush the
        /// queued UTP sends once. Empty ticks are cheap and still seal ordering state correctly.
        /// </summary>
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

        private void OnConnectionOpened(uint connectionId, NetworkEndpoint endpoint)
        {
            ConnectionOpened?.Invoke(connectionId, endpoint);
        }

        private void OnConnectionClosed(uint connectionId)
        {
            _replication.RemoveConnection(connectionId);
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
