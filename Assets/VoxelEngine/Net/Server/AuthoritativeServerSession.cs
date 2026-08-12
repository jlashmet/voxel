using System;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Safe composition root for live authoritative networking.
    ///
    /// Per fixed tick: authenticate/validate/apply client intent -> queue mutation EVENT packets ->
    /// append semantic hash barriers -> queue bounded exact-checkpoint REPAIR chunks -> one transport
    /// flush. Socket callbacks only decode/copy into bounded inboxes; world mutation stays on the tick.
    /// </summary>
    public sealed class AuthoritativeServerSession : IDisposable
    {
        private readonly ServerCommandInbox _inbox;
        private readonly ServerConvergenceInbox _convergenceInbox;
        private readonly ServerPlayerRegistry _players;
        private readonly AlterationRateLimiter _rateLimiter;
        private readonly ServerNetworkRuntime _network;
        private readonly ServerCommandProcessor _processor;
        private readonly ServerConvergenceManager _convergence;
        private readonly ServerDeterministicAlterationApplier _defaultAlterationApplier;
        private bool _disposed;

        public event Action<uint, NetworkEndpoint> ConnectionOpened;
        public event Action<uint> ConnectionClosed;
        public event Action<uint> ProtocolError;
        public event Action<uint, int> SendError;
        public event Action<ServerConvergenceManager.VerifiedRegionMismatch> VerifiedRegionMismatch;

        public AuthoritativeServerSession(
            uint serverSeed,
            Validation.DensityCap densityCap,
            int maxConnections = 64,
            int initialEventCapacity = 64,
            uint hashIntervalTicks = ServerConvergenceManager.DefaultHashIntervalTicks)
        {
            _inbox = new ServerCommandInbox();
            _convergenceInbox = new ServerConvergenceInbox();
            _players = new ServerPlayerRegistry();
            _rateLimiter = new AlterationRateLimiter();
            _network = new ServerNetworkRuntime(_inbox, _convergenceInbox, maxConnections, initialEventCapacity);
            _processor = new ServerCommandProcessor(_inbox, _players, _rateLimiter, serverSeed, densityCap);
            _convergence = new ServerConvergenceManager(_convergenceInbox, _players, hashIntervalTicks);
            _defaultAlterationApplier = new ServerDeterministicAlterationApplier();

            _network.ConnectionOpened += OnConnectionOpened;
            _network.ConnectionClosed += OnConnectionClosed;
            _network.ProtocolError += OnProtocolError;
            _network.SendError += OnSendError;
            _convergence.VerifiedMismatch += OnVerifiedMismatch;
        }

        public ServerPlayerRegistry Players => _players;
        public ServerCommandInbox CommandInbox => _inbox;
        public ServerConvergenceInbox ConvergenceInbox => _convergenceInbox;
        public ServerCommandProcessor Processor => _processor;
        public ServerConvergenceManager Convergence => _convergence;
        public int ConnectionCount => _disposed ? 0 : _network.ConnectionCount;
        public NetworkEndpoint LocalEndpoint => _disposed ? default : _network.LocalEndpoint;

        public int Listen(NetworkEndpoint endpoint) { ThrowIfDisposed(); return _network.Listen(endpoint); }
        public void PumpTransport() { ThrowIfDisposed(); _network.PumpTransport(); }

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
                return false;

            _network.UpdateConnectionPosition(connectionId, authoritativePositionVoxels);
            SessionLifecycle.PlayerJoin();
            return true;
        }

        public bool UpdateAuthoritativePlayerPosition(uint connectionId, int3 positionVoxels)
        {
            ThrowIfDisposed();
            if (!_network.ContainsConnection(connectionId) ||
                !_players.UpdateAuthoritativePosition(connectionId, positionVoxels))
                return false;

            _network.UpdateConnectionPosition(connectionId, positionVoxels);
            return true;
        }

        public void ProcessAuthoritativeTick(
            uint serverTick,
            ref RegionTable table,
            ref BrickPool pool,
            in ProtectedZones zones,
            IAuthoritativePlayerInputSink inputSink)
        {
            ProcessAuthoritativeTick(
                serverTick,
                ref table,
                ref pool,
                in zones,
                inputSink,
                _defaultAlterationApplier);
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
            if (inputSink == null) throw new ArgumentNullException(nameof(inputSink));
            if (applier == null) throw new ArgumentNullException(nameof(applier));

            _network.BeginTick(serverTick);

            // Reports were decoded during frame pumps. Verification/repair scheduling happens here,
            // never from a transport callback.
            _convergence.ProcessMismatchReports(serverTick, _network.Replication.Subscriptions);

            _processor.ProcessTick(
                serverTick,
                ref table,
                ref pool,
                in zones,
                inputSink,
                applier,
                _network,
                _network);

            // EVENT ordering is load-bearing: same-tick mutations must precede their semantic hash.
            _network.FlushReplication();
            _convergence.EmitHashes(
                serverTick,
                ref table,
                in pool,
                _network.Replication.Subscriptions,
                _network);

            // REPAIR is a separate reliable pipeline. Chunking/rate limiting prevents convergence
            // traffic from dumping an entire checkpoint snapshot into one frame.
            _convergence.FlushRepairPackets(_network);
            _network.FlushSends();
        }

        public bool Disconnect(uint connectionId) { ThrowIfDisposed(); return _network.Disconnect(connectionId); }

        private void OnConnectionOpened(uint connectionId, NetworkEndpoint endpoint) => ConnectionOpened?.Invoke(connectionId, endpoint);

        private void OnConnectionClosed(uint connectionId)
        {
            _convergence.RemoveConnection(connectionId);
            if (_players.RemoveConnection(connectionId, out ushort playerId))
            {
                _processor.RemovePlayer(playerId);
                SessionLifecycle.PlayerLeave();
            }
            ConnectionClosed?.Invoke(connectionId);
        }

        private void OnVerifiedMismatch(ServerConvergenceManager.VerifiedRegionMismatch mismatch) =>
            VerifiedRegionMismatch?.Invoke(mismatch);
        private void OnProtocolError(uint connectionId) => ProtocolError?.Invoke(connectionId);
        private void OnSendError(uint connectionId, int errorCode) => SendError?.Invoke(connectionId, errorCode);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AuthoritativeServerSession));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _network.ConnectionOpened -= OnConnectionOpened;
            _network.ConnectionClosed -= OnConnectionClosed;
            _network.ProtocolError -= OnProtocolError;
            _network.SendError -= OnSendError;
            _convergence.VerifiedMismatch -= OnVerifiedMismatch;
            _network.Dispose();
            _rateLimiter.Clear();
            _disposed = true;
        }
    }
}
