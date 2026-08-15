using System;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Safe composition root for live authoritative networking.
    /// </summary>
    public sealed class AuthoritativeServerSession : IDisposable
    {
        private readonly ServerCommandInbox _inbox;
        private readonly ServerConvergenceInbox _convergenceInbox;
        private readonly ServerRegionStateRequestInbox _regionStateInbox;
        private readonly ServerPlayerRegistry _players;
        private readonly AlterationRateLimiter _rateLimiter;
        private readonly ServerNetworkRuntime _network;
        private readonly ServerCommandProcessor _processor;
        private readonly ServerConvergenceManager _convergence;
        private readonly ServerBulkRegionStateManager _bulkRegionState;
        private readonly ServerPlayerStateReplicator _playerStates;
        private readonly IAlterationApplier _defaultAlterationApplier;
        private bool _disposed;

        public event Action<uint, NetworkEndpoint> ConnectionOpened;
        public event Action<uint> ConnectionClosed;
        public event Action<uint> ProtocolError;
        public event Action<uint, int> SendError;
        public event Action<ServerConvergenceManager.VerifiedRegionMismatch> VerifiedRegionMismatch;
        public event Action<ServerConvergenceManager.RegionResyncRequest> RegionResyncRequired;

        public AuthoritativeServerSession(
            uint serverSeed,
            Validation.DensityCap densityCap,
            IAlterationApplier alterationApplier = null,
            int maxConnections = 64,
            int initialEventCapacity = 64,
            uint hashIntervalTicks = ServerConvergenceManager.DefaultHashIntervalTicks,
            uint playerStateIntervalTicks = ServerPlayerStateReplicator.DefaultIntervalTicks)
        {
            _inbox = new ServerCommandInbox();
            _convergenceInbox = new ServerConvergenceInbox();
            _regionStateInbox = new ServerRegionStateRequestInbox();
            _players = new ServerPlayerRegistry();
            _rateLimiter = new AlterationRateLimiter();
            _network = new ServerNetworkRuntime(
                _inbox,
                _convergenceInbox,
                _regionStateInbox,
                maxConnections,
                initialEventCapacity);
            _processor = new ServerCommandProcessor(_inbox, _players, _rateLimiter, serverSeed, densityCap);
            _convergence = new ServerConvergenceManager(_convergenceInbox, _players, hashIntervalTicks);
            _bulkRegionState = new ServerBulkRegionStateManager(_regionStateInbox, _players);
            _playerStates = new ServerPlayerStateReplicator(_players, _processor, playerStateIntervalTicks);
            _defaultAlterationApplier = alterationApplier;

            _network.ConnectionOpened += OnConnectionOpened;
            _network.ConnectionClosed += OnConnectionClosed;
            _network.ProtocolError += OnProtocolError;
            _network.SendError += OnSendError;
            _convergence.VerifiedMismatch += OnVerifiedMismatch;
            _convergence.ResyncRequired += OnRegionResyncRequired;
        }

        public ServerPlayerRegistry Players => _players;
        public ServerCommandInbox CommandInbox => _inbox;
        public ServerConvergenceInbox ConvergenceInbox => _convergenceInbox;
        public ServerRegionStateRequestInbox RegionStateInbox => _regionStateInbox;
        public ServerCommandProcessor Processor => _processor;
        public ServerConvergenceManager Convergence => _convergence;
        public ServerBulkRegionStateManager BulkRegionState => _bulkRegionState;
        public ServerPlayerStateReplicator PlayerStates => _playerStates;
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

        /// <summary>
        /// Game/simulation hook used from the authoritative fixed-tick path after movement resolves.
        /// The resulting absolute state is what the next player-state snapshot samples.
        /// </summary>
        public bool UpdateAuthoritativePlayerKinematics(
            uint connectionId,
            float3 positionVoxels,
            float3 velocityVoxelsPerSecond,
            ushort viewYaw,
            S_PlayerState.StateFlags stateFlags = S_PlayerState.StateFlags.None)
        {
            ThrowIfDisposed();
            if (!_network.ContainsConnection(connectionId) ||
                !_players.UpdateAuthoritativeKinematics(
                    connectionId,
                    positionVoxels,
                    velocityVoxelsPerSecond,
                    viewYaw,
                    stateFlags))
                return false;

            if (!_players.TryGetByConnection(connectionId, out var player))
                return false;

            _network.UpdateConnectionPosition(connectionId, player.PositionVoxels);
            return true;
        }

        public void ProcessAuthoritativeTick(
            uint serverTick,
            ref RegionTable table,
            ref BrickPool pool,
            in ProtectedZones zones,
            IAuthoritativePlayerInputSink inputSink)
        {
            if (_defaultAlterationApplier == null)
                throw new InvalidOperationException("An Edits alteration applier must be supplied for the default authoritative tick path.");
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
            IAlterationApplier applier)
        {
            ThrowIfDisposed();
            if (inputSink == null) throw new ArgumentNullException(nameof(inputSink));
            if (applier == null) throw new ArgumentNullException(nameof(applier));

            _network.BeginTick(serverTick);

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

            // Game-owned ApplyInput may update ServerPlayerRegistry kinematics through
            // UpdateAuthoritativePlayerKinematics. Sample only after the fixed-tick input work is done.
            _playerStates.Emit(serverTick, _network.Replication.Subscriptions, _network);

            _network.FlushReplication();
            _convergence.EmitHashes(
                serverTick,
                ref table,
                in pool,
                _network.Replication.Subscriptions,
                _network);

            _bulkRegionState.ProcessRequests(
                serverTick,
                ref table,
                in pool,
                _network.Replication.Subscriptions,
                _network);

            _convergence.FlushRepairPackets(_network);
            _bulkRegionState.Flush(serverTick, _network);
            _network.FlushSends();
        }

        public bool Disconnect(uint connectionId) { ThrowIfDisposed(); return _network.Disconnect(connectionId); }

        private void OnConnectionOpened(uint connectionId, NetworkEndpoint endpoint) => ConnectionOpened?.Invoke(connectionId, endpoint);

        private void OnConnectionClosed(uint connectionId)
        {
            _convergence.RemoveConnection(connectionId);
            _bulkRegionState.RemoveConnection(connectionId);
            if (_players.RemoveConnection(connectionId, out ushort playerId))
            {
                _processor.RemovePlayer(playerId);
                _playerStates.RemovePlayer(playerId);
                SessionLifecycle.PlayerLeave();
            }
            ConnectionClosed?.Invoke(connectionId);
        }

        private void OnVerifiedMismatch(ServerConvergenceManager.VerifiedRegionMismatch mismatch) =>
            VerifiedRegionMismatch?.Invoke(mismatch);

        private void OnRegionResyncRequired(ServerConvergenceManager.RegionResyncRequest request)
        {
            var message = new S_RegionResyncRequired(
                request.RegionCoord,
                request.FailedHashTick,
                request.Reason);
            _network.SendRegionResyncRequired(request.ConnectionId, in message);
            RegionResyncRequired?.Invoke(request);
        }

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
            _convergence.ResyncRequired -= OnRegionResyncRequired;
            _network.Dispose();
            _rateLimiter.Clear();
            _disposed = true;
        }
    }
}
