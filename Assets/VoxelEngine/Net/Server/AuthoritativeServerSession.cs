using System;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Safe composition root for live authoritative networking.
    ///
    /// Frame loop:
    ///   PumpTransport() -> decode only -> bounded ServerCommandInbox.
    ///
    /// Fixed simulation tick:
    ///   ProcessAuthoritativeTick() -> begin event stream -> resolve authenticated identity ->
    ///   validate -> apply -> publish -> interest route/batch -> flush UTP.
    ///
    /// Authentication remains an external game/session concern. A connection cannot influence
    /// simulation until AuthenticateConnection succeeds here.
    /// </summary>
    public sealed class AuthoritativeServerSession : IDisposable
    {
        private readonly ServerCommandInbox _inbox;
        private readonly ServerPlayerRegistry _players;
        private readonly AlterationRateLimiter _rateLimiter;
        private readonly ServerNetworkRuntime _network;
        private readonly ServerCommandProcessor _processor;
        private bool _disposed;

        public event Action<uint, NetworkEndpoint> ConnectionOpened;
        public event Action<uint> ConnectionClosed;
        public event Action<uint> ProtocolError;
        public event Action<uint, int> SendError;

        public AuthoritativeServerSession(
            uint serverSeed,
            Validation.DensityCap densityCap,
            int maxConnections = 64,
            int initialEventCapacity = 64)
        {
            _inbox = new ServerCommandInbox();
            _players = new ServerPlayerRegistry();
            _rateLimiter = new AlterationRateLimiter();
            _network = new ServerNetworkRuntime(_inbox, maxConnections, initialEventCapacity);
            _processor = new ServerCommandProcessor(
                _inbox,
                _players,
                _rateLimiter,
                serverSeed,
                densityCap);

            _network.ConnectionOpened += OnConnectionOpened;
            _network.ConnectionClosed += OnConnectionClosed;
            _network.ProtocolError += OnProtocolError;
            _network.SendError += OnSendError;
        }

        public ServerPlayerRegistry Players => _players;
        public ServerCommandInbox CommandInbox => _inbox;
        public ServerCommandProcessor Processor => _processor;
        public int ConnectionCount => _disposed ? 0 : _network.ConnectionCount;
        public NetworkEndpoint LocalEndpoint => _disposed ? default : _network.LocalEndpoint;

        public int Listen(NetworkEndpoint endpoint)
        {
            ThrowIfDisposed();
            return _network.Listen(endpoint);
        }

        public void PumpTransport()
        {
            ThrowIfDisposed();
            _network.PumpTransport();
        }

        public bool AuthenticateConnection(
            uint connectionId,
            ushort playerId,
            int3 authoritativePositionVoxels,
            int reachVoxels = Validation.k_DefaultReachVoxels,
            bool canAlterWorld = true)
        {
            ThrowIfDisposed();
            if (!_network.ContainsConnection(connectionId))
                return false;

            if (!_players.TryRegisterAuthenticated(
                    connectionId,
                    playerId,
                    authoritativePositionVoxels,
                    reachVoxels,
                    canAlterWorld))
            {
                return false;
            }

            _network.UpdateConnectionPosition(connectionId, authoritativePositionVoxels);
            SessionLifecycle.PlayerJoin();
            return true;
        }

        public bool UpdateAuthoritativePlayerPosition(uint connectionId, int3 positionVoxels)
        {
            ThrowIfDisposed();
            if (!_network.ContainsConnection(connectionId) ||
                !_players.UpdateAuthoritativePosition(connectionId, positionVoxels))
            {
                return false;
            }

            _network.UpdateConnectionPosition(connectionId, positionVoxels);
            return true;
        }

        public void ProcessAuthoritativeTick(
            uint serverTick,
            ref RegionTable table,
            ref BrickPool pool,
            in ProtectedZones zones,
            IAuthoritativePlayerInputSink inputSink,
            IAuthoritativeAlterationApplier applier)
        {
            ThrowIfDisposed();

            _network.BeginTick(serverTick);
            _processor.ProcessTick(
                serverTick,
                ref table,
                ref pool,
                in zones,
                inputSink,
                applier,
                _network,
                _network);
            _network.EndTick();
        }

        public bool Disconnect(uint connectionId)
        {
            ThrowIfDisposed();
            return _network.Disconnect(connectionId);
        }

        private void OnConnectionOpened(uint connectionId, NetworkEndpoint endpoint) =>
            ConnectionOpened?.Invoke(connectionId, endpoint);

        private void OnConnectionClosed(uint connectionId)
        {
            if (_players.RemoveConnection(connectionId, out ushort playerId))
            {
                _processor.RemovePlayer(playerId);
                SessionLifecycle.PlayerLeave();
            }

            ConnectionClosed?.Invoke(connectionId);
        }

        private void OnProtocolError(uint connectionId) => ProtocolError?.Invoke(connectionId);
        private void OnSendError(uint connectionId, int errorCode) => SendError?.Invoke(connectionId, errorCode);

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AuthoritativeServerSession));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _network.ConnectionOpened -= OnConnectionOpened;
            _network.ConnectionClosed -= OnConnectionClosed;
            _network.ProtocolError -= OnProtocolError;
            _network.SendError -= OnSendError;
            _network.Dispose();
            _rateLimiter.Clear();
            _disposed = true;
        }
    }
}
