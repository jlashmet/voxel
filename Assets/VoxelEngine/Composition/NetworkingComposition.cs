using System;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Api;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Canonical, application-facing player input after the networking protocol has
    /// quantized it. Gameplay replay consumes this value so prediction semantics remain
    /// identical without exposing Net.Runtime protocol types to scene assemblies.
    /// </summary>
    public readonly struct NetworkPlayerInput
    {
        public readonly uint Tick;
        public readonly ushort Sequence;
        public readonly float2 Movement;
        public readonly float3 ViewDirection;
        public readonly ushort ViewYaw;
        public readonly byte Flags;

        internal NetworkPlayerInput(in C_PlayerInput input)
        {
            Tick = input.tick;
            Sequence = input.sequence;
            Movement = input.Movement();
            ViewDirection = input.ViewDirection();
            ViewYaw = input.viewYaw;
            Flags = input.flags;
        }
    }

    /// <summary>Stable player snapshot presented to application prediction code.</summary>
    public readonly struct NetworkPlayerState
    {
        public readonly ushort PlayerId;
        public readonly uint Tick;
        public readonly float3 PositionVoxels;
        public readonly float3 VelocityVoxelsPerSecond;

        internal NetworkPlayerState(in S_PlayerState state)
        {
            PlayerId = state.playerId;
            Tick = state.tick;
            PositionVoxels = state.PositionVoxels();
            VelocityVoxelsPerSecond = state.VelocityVoxelsPerSecond();
        }
    }

    /// <summary>Interpolated remote-player presentation sample.</summary>
    public readonly struct NetworkRemotePlayerSample
    {
        public readonly float3 PositionVoxels;
        public readonly float ViewYawRadians;

        internal NetworkRemotePlayerSample(in RemotePlayerSample sample)
        {
            PositionVoxels = sample.PositionVoxels;
            ViewYawRadians = sample.ViewYawRadians;
        }
    }

    public interface IAuthoritativeNetworkInputSink
    {
        void ApplyInput(ushort playerId, in NetworkPlayerInput input, uint serverTick);
    }

    public interface IClientNetworkPredictionAdapter
    {
        void ApplyAuthoritativeState(in NetworkPlayerState state);
        void ReplayInput(in NetworkPlayerInput input);
    }

    /// <summary>
    /// Composition-owned authoritative networking lifetime. Scene/application code sees
    /// only stable values and Storage.Api capabilities; Net.Runtime and UTP stay here.
    /// </summary>
    public sealed class NetworkServerFacade : IDisposable, IAuthoritativePlayerInputSink
    {
        private readonly AuthoritativeServerSession _runtime;
        private IAuthoritativeNetworkInputSink _inputSink;
        private bool _disposed;

        internal NetworkServerFacade(AuthoritativeServerSession runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _runtime.ConnectionOpened += OnConnectionOpened;
            _runtime.ConnectionClosed += OnConnectionClosed;
            _runtime.ProtocolError += OnProtocolError;
            _runtime.SendError += OnSendError;
        }

        public event Action<uint> ConnectionOpened;
        public event Action<uint> ConnectionClosed;
        public event Action<uint> ProtocolError;
        public event Action<uint, int> SendError;

        public bool Listen(ushort port)
        {
            ThrowIfDisposed();
            return _runtime.Listen(NetworkEndpoint.AnyIpv4.WithPort(port)) == 0;
        }

        public void PumpTransport()
        {
            ThrowIfDisposed();
            _runtime.PumpTransport();
        }

        public bool AuthenticateConnection(
            uint connectionId,
            ushort playerId,
            int3 authoritativePositionVoxels,
            int reachVoxels,
            bool canAlterWorld)
        {
            ThrowIfDisposed();
            return _runtime.AuthenticateConnection(
                connectionId,
                playerId,
                authoritativePositionVoxels,
                reachVoxels,
                canAlterWorld);
        }

        public bool UpdateAuthoritativePlayerKinematics(
            uint connectionId,
            float3 positionVoxels,
            float3 velocityVoxelsPerSecond,
            ushort viewYaw,
            bool grounded)
        {
            ThrowIfDisposed();
            S_PlayerState.StateFlags flags = grounded
                ? S_PlayerState.StateFlags.Grounded
                : S_PlayerState.StateFlags.None;
            return _runtime.UpdateAuthoritativePlayerKinematics(
                connectionId,
                positionVoxels,
                velocityVoxelsPerSecond,
                viewYaw,
                flags);
        }

        public void ProcessAuthoritativeTick(
            uint serverTick,
            IRegionReadSource readStorage,
            IRegionMutationStore mutationStorage,
            IRegionSnapshotSource snapshots,
            IAuthoritativeNetworkInputSink inputSink)
        {
            ThrowIfDisposed();
            if (inputSink == null) throw new ArgumentNullException(nameof(inputSink));

            ProtectedZones zones = default;
            _inputSink = inputSink;
            try
            {
                _runtime.ProcessAuthoritativeTick(
                    serverTick,
                    readStorage,
                    mutationStorage,
                    snapshots,
                    in zones,
                    this);
            }
            finally
            {
                _inputSink = null;
            }
        }

        public bool Disconnect(uint connectionId)
        {
            ThrowIfDisposed();
            return _runtime.Disconnect(connectionId);
        }

        void IAuthoritativePlayerInputSink.ApplyInput(
            ushort playerId,
            in C_PlayerInput input,
            uint serverTick)
        {
            IAuthoritativeNetworkInputSink sink = _inputSink;
            if (sink == null) return;
            var publicInput = new NetworkPlayerInput(in input);
            sink.ApplyInput(playerId, in publicInput, serverTick);
        }

        private void OnConnectionOpened(uint connectionId, NetworkEndpoint _) =>
            ConnectionOpened?.Invoke(connectionId);
        private void OnConnectionClosed(uint connectionId) => ConnectionClosed?.Invoke(connectionId);
        private void OnProtocolError(uint connectionId) => ProtocolError?.Invoke(connectionId);
        private void OnSendError(uint connectionId, int errorCode) => SendError?.Invoke(connectionId, errorCode);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NetworkServerFacade));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _runtime.ConnectionOpened -= OnConnectionOpened;
            _runtime.ConnectionClosed -= OnConnectionClosed;
            _runtime.ProtocolError -= OnProtocolError;
            _runtime.SendError -= OnSendError;
            _runtime.Dispose();
            _inputSink = null;
            _disposed = true;
        }
    }

    /// <summary>
    /// Composition-owned client networking lifetime. Protocol packets, prediction
    /// history and UTP endpoints remain Net.Runtime details.
    /// </summary>
    public sealed class NetworkClientFacade : IDisposable, IClientPredictionAdapter
    {
        private readonly ClientNetworkRuntime _runtime;
        private IClientNetworkPredictionAdapter _predictionAdapter;
        private bool _disposed;

        internal NetworkClientFacade(ClientNetworkRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _runtime.Connected += OnConnected;
            _runtime.Disconnected += OnDisconnected;
            _runtime.PacketRejected += OnPacketRejected;
            _runtime.SendError += OnSendError;
            _runtime.PlayerStateReceived += OnPlayerStateReceived;
            _runtime.RegionRepairApplied += OnRegionRepairApplied;
            _runtime.FullRegionStateApplied += OnFullRegionStateApplied;
        }

        public event Action Connected;
        public event Action Disconnected;
        public event Action PacketRejected;
        public event Action<int> SendError;
        public event Action<NetworkPlayerState> PlayerStateReceived;
        public event Action<int3, uint> RegionRepairApplied;
        public event Action<int3, uint> FullRegionStateApplied;

        public bool IsConnected => !_disposed && _runtime.IsConnected;

        public bool Connect(string address, ushort port)
        {
            ThrowIfDisposed();
            return NetworkingComposition.TryParseEndpoint(address, port, out NetworkEndpoint endpoint) &&
                   _runtime.Connect(endpoint);
        }

        public bool ConnectLoopback(ushort port)
        {
            ThrowIfDisposed();
            return _runtime.Connect(NetworkEndpoint.LoopbackIpv4.WithPort(port));
        }

        public void ConfigureLocalPrediction(
            ushort localPlayerId,
            IClientNetworkPredictionAdapter adapter)
        {
            ThrowIfDisposed();
            _predictionAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _runtime.ConfigureLocalPrediction(localPlayerId, this);
        }

        public bool TrySendPlayerInput(
            uint tick,
            ushort sequence,
            float2 movement,
            float3 viewDirection,
            byte flags,
            out NetworkPlayerInput canonicalInput)
        {
            ThrowIfDisposed();
            C_PlayerInput.ActionBits actions = C_PlayerInput.ActionBits.Aim;
            if (math.lengthsq(movement) > 1e-6f)
                actions |= C_PlayerInput.ActionBits.Move;

            var input = new C_PlayerInput(
                tick,
                sequence,
                movement,
                viewDirection,
                actions,
                toolMaterial: 0,
                flags: flags);
            canonicalInput = new NetworkPlayerInput(in input);
            return _runtime.TrySendPlayerInput(in input);
        }

        public bool TrySendExplosionRequest(
            uint tick,
            int3 originVoxel,
            uint radiusBricks,
            ushort sequence)
        {
            ThrowIfDisposed();
            var request = new C_AlterationRequest(
                tick,
                originVoxel,
                AlterationEvent.KindExplosion,
                VoxelGrid.MaterialEmpty,
                AlterationEvent.KindExplosion,
                radiusBricks,
                seed: 0,
                sequence: sequence);
            return _runtime.TrySendAlterationRequest(in request);
        }

        public void PumpTransport()
        {
            ThrowIfDisposed();
            _runtime.PumpTransport();
        }

        public int ApplyPlayerStateUpdates(out int replayedLocalInputs)
        {
            ThrowIfDisposed();
            return _runtime.ApplyPlayerStateUpdates(out replayedLocalInputs);
        }

        public int ApplyReadyAuthoritativeEvents(
            IRegionMutationStore mutations,
            IRegionSnapshotSource snapshots,
            IRegionSnapshotMutationStore snapshotMutations,
            out int appliedEvents)
        {
            ThrowIfDisposed();
            return _runtime.ApplyReadyAuthoritativeEvents(
                mutations,
                snapshots,
                snapshotMutations,
                out appliedEvents);
        }

        public bool TrySampleRemotePlayer(
            ushort playerId,
            float interpolationAlpha,
            out NetworkRemotePlayerSample sample)
        {
            ThrowIfDisposed();
            if (_runtime.TrySampleRemotePlayer(playerId, interpolationAlpha, out RemotePlayerSample runtimeSample))
            {
                sample = new NetworkRemotePlayerSample(in runtimeSample);
                return true;
            }

            sample = default;
            return false;
        }

        public void FlushSends()
        {
            ThrowIfDisposed();
            _runtime.FlushSends();
        }

        public void Disconnect()
        {
            ThrowIfDisposed();
            _runtime.Disconnect();
        }

        void IClientPredictionAdapter.ApplyAuthoritativeState(in S_PlayerState state)
        {
            IClientNetworkPredictionAdapter adapter = _predictionAdapter;
            if (adapter == null) return;
            var publicState = new NetworkPlayerState(in state);
            adapter.ApplyAuthoritativeState(in publicState);
        }

        void IClientPredictionAdapter.ReplayInput(in C_PlayerInput input)
        {
            IClientNetworkPredictionAdapter adapter = _predictionAdapter;
            if (adapter == null) return;
            var publicInput = new NetworkPlayerInput(in input);
            adapter.ReplayInput(in publicInput);
        }

        private void OnConnected() => Connected?.Invoke();
        private void OnDisconnected() => Disconnected?.Invoke();
        private void OnPacketRejected() => PacketRejected?.Invoke();
        private void OnSendError(int errorCode) => SendError?.Invoke(errorCode);
        private void OnPlayerStateReceived(S_PlayerState state)
        {
            var publicState = new NetworkPlayerState(in state);
            PlayerStateReceived?.Invoke(publicState);
        }
        private void OnRegionRepairApplied(int3 regionCoord, uint tick) =>
            RegionRepairApplied?.Invoke(regionCoord, tick);
        private void OnFullRegionStateApplied(int3 regionCoord, uint tick) =>
            FullRegionStateApplied?.Invoke(regionCoord, tick);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NetworkClientFacade));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _runtime.Connected -= OnConnected;
            _runtime.Disconnected -= OnDisconnected;
            _runtime.PacketRejected -= OnPacketRejected;
            _runtime.SendError -= OnSendError;
            _runtime.PlayerStateReceived -= OnPlayerStateReceived;
            _runtime.RegionRepairApplied -= OnRegionRepairApplied;
            _runtime.FullRegionStateApplied -= OnFullRegionStateApplied;
            _runtime.Dispose();
            _predictionAdapter = null;
            _disposed = true;
        }
    }

    /// <summary>Composition entry point for the concrete Net.Runtime implementation.</summary>
    public static class NetworkingComposition
    {
        public static int AuthoritativeTickRateHz => (int)AuthoritativeTickConfig.TickRateHz;

        public static NetworkServerFacade CreateServer(uint serverSeed, int maxConnections = 64)
        {
            var runtime = new AuthoritativeServerSession(
                serverSeed,
                new Validation.DensityCap(1f, VoxelReadGrid.BlocksPerRegion),
                EditsComposition.CreateAlterationApplier(),
                maxConnections);
            return new NetworkServerFacade(runtime);
        }

        public static NetworkClientFacade CreateClient() =>
            new NetworkClientFacade(
                new ClientNetworkRuntime(EditsComposition.CreateAlterationApplier()));

        public static bool IsValidAddress(string address, ushort port) =>
            TryParseEndpoint(address, port, out _);

        internal static bool TryParseEndpoint(
            string address,
            ushort port,
            out NetworkEndpoint endpoint)
        {
            address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            if (string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase))
                address = "127.0.0.1";

            return NetworkEndpoint.TryParse(address, port, out endpoint, NetworkFamily.Ipv4) ||
                   NetworkEndpoint.TryParse(address, port, out endpoint, NetworkFamily.Ipv6);
        }
    }
}
