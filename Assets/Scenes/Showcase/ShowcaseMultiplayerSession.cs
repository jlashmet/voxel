using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Networking.Transport;
using UnityEngine;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Client;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Thin game adapter around the networking stack for the showcase. It owns no protocol rules:
    /// the server session remains authoritative, the client runtime owns prediction history and
    /// ordered event application, and the existing CharacterMotor remains the sole movement code.
    /// </summary>
    internal sealed class ShowcaseMultiplayerSession : IDisposable,
        IAuthoritativePlayerInputSink, IClientPredictionAdapter
    {
        private enum SessionMode : byte { Offline, Host, Client }

        private const float FixedDeltaSeconds = 1f / AuthoritativeTickConfig.TickRateHz;
        private const byte InputFlagSprint = 1 << 0;
        private const byte InputFlagJump = 1 << 1;
        private const int ShowcaseReachVoxels = 1024;

        private readonly ShowcaseWorld _world;
        private readonly CharacterMotor _localMotor;
        private readonly Dictionary<ushort, CharacterMotor> _serverMotors = new();
        private readonly Dictionary<ushort, uint> _connectionByPlayer = new();

        private AuthoritativeServerSession _server;
        private ClientNetworkRuntime _client;
        private SessionMode _mode;
        private ushort _localPlayerId;
        private uint _serverTick = 1;
        private uint _clientTick;
        private bool _clientTickAnchored;
        private ushort _nextInputSequence = 1;
        private ushort _nextAlterationSequence = 1;
        private float _fixedAccumulator;
        private float2 _movement;
        private float3 _viewDirection = new float3(0f, 0f, 1f);
        private bool _sprint;
        private bool _jump;
        private GameObject _remoteAvatar;
        private bool _disposed;

        public ShowcaseMultiplayerSession(ShowcaseWorld world, CharacterMotor localMotor)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _localMotor = localMotor ?? throw new ArgumentNullException(nameof(localMotor));
        }

        public bool IsActive => !_disposed && _mode != SessionMode.Offline;
        public bool IsHost => !_disposed && _mode == SessionMode.Host;
        public bool IsConnected => !_disposed && _client != null && _client.IsConnected;
        public ushort LocalPlayerId => _localPlayerId;
        public string Status { get; private set; } = "Offline";

        public bool StartHost(int port)
        {
            ThrowIfDisposed();
            Disconnect();
            if (!TryPort(port, out ushort networkPort))
            {
                Status = "Invalid port";
                return false;
            }

            try
            {
                _mode = SessionMode.Host;
                _localPlayerId = 1;
                _server = new AuthoritativeServerSession(
                    _world.Seed,
                    new Validation.DensityCap(1f, VoxelDimensions.BricksPerRegion),
                    maxConnections: 2);
                _server.ConnectionOpened += OnServerConnectionOpened;
                _server.ConnectionClosed += OnServerConnectionClosed;
                _server.ProtocolError += OnServerProtocolError;
                _server.SendError += OnServerSendError;

                NetworkEndpoint listen = NetworkEndpoint.AnyIpv4.WithPort(networkPort);
                if (_server.Listen(listen) != 0)
                {
                    Status = $"Could not listen on port {networkPort}";
                    CleanupNetworking();
                    return false;
                }

                CreateClient(localPlayerId: 1);
                if (!_client.Connect(NetworkEndpoint.LoopbackIpv4.WithPort(networkPort)))
                {
                    Status = "Could not connect host loopback client";
                    CleanupNetworking();
                    return false;
                }

                Status = $"Hosting on port {networkPort}; connecting local player";
                return true;
            }
            catch (Exception ex)
            {
                Status = $"Host failed: {ex.Message}";
                CleanupNetworking();
                return false;
            }
        }

        public bool StartClient(string address, int port)
        {
            ThrowIfDisposed();
            Disconnect();
            if (!TryPort(port, out ushort networkPort))
            {
                Status = "Invalid port";
                return false;
            }

            address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            if (string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase))
                address = "127.0.0.1";

            if (!NetworkEndpoint.TryParse(address, networkPort, out NetworkEndpoint endpoint, NetworkFamily.Ipv4) &&
                !NetworkEndpoint.TryParse(address, networkPort, out endpoint, NetworkFamily.Ipv6))
            {
                Status = $"Invalid address: {address}";
                return false;
            }

            try
            {
                _mode = SessionMode.Client;
                _localPlayerId = 2;
                CreateClient(localPlayerId: 2);
                if (!_client.Connect(endpoint))
                {
                    Status = $"Could not connect to {address}:{networkPort}";
                    CleanupNetworking();
                    return false;
                }

                Status = $"Connecting to {address}:{networkPort}";
                return true;
            }
            catch (Exception ex)
            {
                Status = $"Join failed: {ex.Message}";
                CleanupNetworking();
                return false;
            }
        }

        /// <summary>
        /// Pump transport and advance both client intent and host authority at 30 Hz. The caller
        /// supplies local input every render frame; this method samples the latest intent on each
        /// fixed network tick rather than emitting one packet per render frame.
        /// </summary>
        public void Pump(float deltaTime, float2 movement, bool sprint, bool jump, float3 viewDirection)
        {
            ThrowIfDisposed();
            if (!IsActive) return;

            _movement = math.clamp(movement, new float2(-1f), new float2(1f));
            _sprint = sprint;
            _jump = jump;
            _viewDirection = math.lengthsq(viewDirection) > 1e-8f
                ? math.normalize(viewDirection)
                : new float3(0f, 0f, 1f);

            PumpEndpointsAndApply();

            _fixedAccumulator = math.min(_fixedAccumulator + math.max(0f, deltaTime), 0.25f);
            while (_fixedAccumulator >= FixedDeltaSeconds)
            {
                StepNetworkTick();
                _fixedAccumulator -= FixedDeltaSeconds;
            }

            PumpEndpointsAndApply();
            UpdateRemoteAvatar();
        }

        public bool TryRequestExplosion(int3 originVoxel, int radiusVoxels)
        {
            ThrowIfDisposed();
            if (!IsActive || _client == null || !_client.IsConnected || !_clientTickAnchored)
                return false;

            uint radiusBricks = (uint)RadiusVoxelsToBricks(radiusVoxels);
            var request = new C_AlterationRequest(
                _clientTick,
                originVoxel,
                AlterationEvent.KindExplosion,
                VoxelDimensions.MaterialEmpty,
                AlterationEvent.KindExplosion,
                radiusBricks,
                seed: 0,
                sequence: _nextAlterationSequence);

            if (!_client.TrySendAlterationRequest(in request))
                return false;

            _nextAlterationSequence = unchecked((ushort)(_nextAlterationSequence + 1));
            _client.FlushSends();
            Status = $"Explosion request sent (r{radiusBricks} bricks)";
            return true;
        }

        public static int RadiusVoxelsToBricks(int radiusVoxels)
        {
            int positive = math.max(1, radiusVoxels);
            int bricks = (positive + VoxelDimensions.BrickEdge - 1) >> VoxelDimensions.BrickEdgeLog2;
            return math.clamp(bricks, 1, VoxelDimensions.RegionEdge - 1);
        }

        public void Disconnect()
        {
            if (_disposed) return;
            CleanupNetworking();
            _mode = SessionMode.Offline;
            _localPlayerId = 0;
            _serverTick = 1;
            _clientTick = 0;
            _clientTickAnchored = false;
            _nextInputSequence = 1;
            _nextAlterationSequence = 1;
            _fixedAccumulator = 0f;
            _serverMotors.Clear();
            _connectionByPlayer.Clear();
            DestroyRemoteAvatar();
            Status = "Offline";
        }

        private void CreateClient(ushort localPlayerId)
        {
            _client = new ClientNetworkRuntime();
            _client.Connected += OnClientConnected;
            _client.Disconnected += OnClientDisconnected;
            _client.PacketRejected += OnClientPacketRejected;
            _client.SendError += OnClientSendError;
            _client.PlayerStateReceived += OnPlayerStateReceived;
            _client.RegionRepairApplied += OnRegionStateReplaced;
            _client.FullRegionStateApplied += OnRegionStateReplaced;
            _client.ConfigureLocalPrediction(localPlayerId, this);
        }

        private void StepNetworkTick()
        {
            if (_client != null && _client.IsConnected && _clientTickAnchored)
                SendPredictedLocalInput();

            if (_server == null) return;

            // Host loopback input is flushed immediately above. Pump it before the authoritative
            // fixed step so the same tick can consume the command and acknowledge it in snapshots.
            _server.PumpTransport();
            ProtectedZones zones = default;
            _server.ProcessAuthoritativeTick(
                _serverTick,
                ref _world.Table,
                ref _world.Pool,
                in zones,
                this);

            _serverTick = unchecked(_serverTick + 1);
            if (_serverTick == 0) _serverTick = 1;
        }

        private void SendPredictedLocalInput()
        {
            C_PlayerInput.ActionBits actions = C_PlayerInput.ActionBits.Aim;
            if (math.lengthsq(_movement) > 1e-6f)
                actions |= C_PlayerInput.ActionBits.Move;

            byte flags = 0;
            if (_sprint) flags |= InputFlagSprint;
            if (_jump) flags |= InputFlagJump;

            uint tick = _clientTick;
            _clientTick = unchecked(_clientTick + 1);
            if (_clientTick == 0) _clientTick = 1;

            var input = new C_PlayerInput(
                tick,
                _nextInputSequence,
                _movement,
                _viewDirection,
                actions,
                toolMaterial: 0,
                flags: flags);

            if (!_client.TrySendPlayerInput(in input))
                return;

            _nextInputSequence = unchecked((ushort)(_nextInputSequence + 1));
            ReplayInput(in input);
            _client.FlushSends();
        }

        private void PumpEndpointsAndApply()
        {
            _server?.PumpTransport();
            if (_client == null) return;

            _client.PumpTransport();
            _client.ApplyPlayerStateUpdates(out _);
            _client.ApplyReadyAuthoritativeEvents(
                ref _world.Table,
                ref _world.Pool,
                out int appliedEvents);

            if (appliedEvents > 0)
                ShowcaseNetworkWorldBridge.PublishDirtyRegionsAround(_world, (float3)_localMotor.Position);
        }

        void IAuthoritativePlayerInputSink.ApplyInput(
            ushort playerId,
            in C_PlayerInput input,
            uint serverTick)
        {
            if (_server == null ||
                !_serverMotors.TryGetValue(playerId, out CharacterMotor motor) ||
                !_connectionByPlayer.TryGetValue(playerId, out uint connectionId))
                return;

            SimulateMotor(motor, in input);
            S_PlayerState.StateFlags stateFlags = motor.Grounded
                ? S_PlayerState.StateFlags.Grounded
                : S_PlayerState.StateFlags.None;

            _server.UpdateAuthoritativePlayerKinematics(
                connectionId,
                (float3)motor.Position / ShowcaseWorld.VoxelSize,
                (float3)motor.Velocity / ShowcaseWorld.VoxelSize,
                input.viewYaw,
                stateFlags);
        }

        void IClientPredictionAdapter.ApplyAuthoritativeState(in S_PlayerState state)
        {
            _localMotor.Position = (Vector3)(state.PositionVoxels() * ShowcaseWorld.VoxelSize);
            _localMotor.Velocity = (Vector3)(state.VelocityVoxelsPerSecond() * ShowcaseWorld.VoxelSize);
        }

        public void ReplayInput(in C_PlayerInput input)
        {
            SimulateMotor(_localMotor, in input);
        }

        private void SimulateMotor(CharacterMotor motor, in C_PlayerInput input)
        {
            if (!_world.IsGenerated(ShowcaseWorld.RegionAt(motor.Position)))
                return;

            float2 movement = input.Movement();
            float3 view = input.ViewDirection();
            Vector3 forward = new Vector3(view.x, 0f, view.z);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            else forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 wish = forward * movement.y + right * movement.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            bool sprint = (input.flags & InputFlagSprint) != 0;
            bool jump = (input.flags & InputFlagJump) != 0;
            motor.Step(_world, wish, sprint, jump, FixedDeltaSeconds);
        }

        private void OnServerConnectionOpened(uint connectionId, NetworkEndpoint endpoint)
        {
            ushort playerId = !_connectionByPlayer.ContainsKey(1)
                ? (ushort)1
                : !_connectionByPlayer.ContainsKey(2) ? (ushort)2 : (ushort)0;
            if (playerId == 0)
            {
                _server.Disconnect(connectionId);
                return;
            }

            CharacterMotor motor = CreateServerMotor(playerId);
            int3 initialPosition = (int3)math.round((float3)motor.Position / ShowcaseWorld.VoxelSize);
            if (!_server.AuthenticateConnection(
                    connectionId,
                    playerId,
                    initialPosition,
                    ShowcaseReachVoxels,
                    canAlterWorld: true))
            {
                _server.Disconnect(connectionId);
                return;
            }

            _serverMotors[playerId] = motor;
            _connectionByPlayer[playerId] = connectionId;
            _server.UpdateAuthoritativePlayerKinematics(
                connectionId,
                (float3)motor.Position / ShowcaseWorld.VoxelSize,
                float3.zero,
                viewYaw: 0,
                stateFlags: S_PlayerState.StateFlags.None);

            Status = playerId == 1
                ? "Host local player authenticated; waiting for player 2"
                : "Player 2 joined";
        }

        private CharacterMotor CreateServerMotor(ushort playerId)
        {
            var motor = new CharacterMotor
            {
                Height = _localMotor.Height,
                Radius = _localMotor.Radius,
                EyeHeight = _localMotor.EyeHeight,
                WalkSpeed = _localMotor.WalkSpeed,
                SprintMultiplier = _localMotor.SprintMultiplier,
                JumpSpeed = _localMotor.JumpSpeed,
                Gravity = _localMotor.Gravity,
                FlightRiseSpeed = _localMotor.FlightRiseSpeed,
                FlightAcceleration = _localMotor.FlightAcceleration,
                FlightHoldDelay = _localMotor.FlightHoldDelay,
                StepHeight = _localMotor.StepHeight,
            };

            if (playerId == 1)
            {
                motor.Position = _localMotor.Position;
                motor.Velocity = Vector3.zero;
                return motor;
            }

            Vector3 spawn = _world.SpawnPosition() + Vector3.right * 2f;
            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(spawn));
            motor.SnapToGround(_world, spawn);
            return motor;
        }

        private void OnServerConnectionClosed(uint connectionId)
        {
            ushort removedPlayer = 0;
            foreach (var pair in _connectionByPlayer)
            {
                if (pair.Value != connectionId) continue;
                removedPlayer = pair.Key;
                break;
            }

            if (removedPlayer != 0)
            {
                _connectionByPlayer.Remove(removedPlayer);
                _serverMotors.Remove(removedPlayer);
            }

            if (_mode == SessionMode.Host && removedPlayer == 2)
            {
                DestroyRemoteAvatar();
                Status = "Player 2 disconnected; hosting";
            }
        }

        private void OnPlayerStateReceived(S_PlayerState state)
        {
            if (state.playerId != _localPlayerId)
                return;

            uint nextAuthoritativeTick = unchecked(state.tick + 1);
            if (nextAuthoritativeTick == 0) nextAuthoritativeTick = 1;

            if (_clientTickAnchored)
            {
                if (IsTickNewer(nextAuthoritativeTick, _clientTick))
                    _clientTick = nextAuthoritativeTick;
                return;
            }

            _clientTick = nextAuthoritativeTick;
            _clientTickAnchored = true;
            Status = _mode == SessionMode.Host
                ? "Hosting; local prediction synchronized"
                : "Connected; prediction synchronized";
        }

        private void OnRegionStateReplaced(int3 regionCoord, uint _)
        {
            ShowcaseNetworkWorldBridge.PublishRegion(_world, regionCoord);
        }

        private void OnClientConnected()
        {
            Status = _mode == SessionMode.Host
                ? "Host loopback connected; authenticating"
                : "Connected; waiting for authoritative state";
        }

        private void OnClientDisconnected()
        {
            _clientTickAnchored = false;
            Status = _mode == SessionMode.Host ? "Host loopback disconnected" : "Disconnected";
            DestroyRemoteAvatar();
        }

        private void OnClientPacketRejected() => Status = "Network packet rejected";
        private void OnClientSendError(int errorCode) => Status = $"Client send error {errorCode}";
        private void OnServerProtocolError(uint connectionId) => Status = $"Server protocol error on {connectionId}";
        private void OnServerSendError(uint connectionId, int errorCode) =>
            Status = $"Server send error {errorCode} on {connectionId}";

        private void UpdateRemoteAvatar()
        {
            if (_client == null || !_client.IsConnected || _localPlayerId == 0)
            {
                if (_remoteAvatar != null) _remoteAvatar.SetActive(false);
                return;
            }

            ushort remoteId = _localPlayerId == 1 ? (ushort)2 : (ushort)1;
            if (!_client.TrySampleRemotePlayer(remoteId, 1f, out RemotePlayerSample sample))
            {
                if (_remoteAvatar != null) _remoteAvatar.SetActive(false);
                return;
            }

            EnsureRemoteAvatar(remoteId);
            _remoteAvatar.SetActive(true);
            Vector3 feet = (Vector3)(sample.PositionVoxels * ShowcaseWorld.VoxelSize);
            _remoteAvatar.transform.position = feet + Vector3.up * (_localMotor.Height * 0.5f);
            _remoteAvatar.transform.rotation = Quaternion.Euler(
                0f,
                sample.ViewYawRadians * Mathf.Rad2Deg,
                0f);
        }

        private void EnsureRemoteAvatar(ushort remoteId)
        {
            if (_remoteAvatar != null) return;
            _remoteAvatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _remoteAvatar.name = $"Remote Player {remoteId}";
            _remoteAvatar.transform.localScale = new Vector3(
                _localMotor.Radius * 2f,
                _localMotor.Height * 0.5f,
                _localMotor.Radius * 2f);
            Collider collider = _remoteAvatar.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
            }
        }

        private void DestroyRemoteAvatar()
        {
            if (_remoteAvatar == null) return;
            UnityEngine.Object.Destroy(_remoteAvatar);
            _remoteAvatar = null;
        }

        private void CleanupNetworking()
        {
            if (_client != null)
            {
                _client.Connected -= OnClientConnected;
                _client.Disconnected -= OnClientDisconnected;
                _client.PacketRejected -= OnClientPacketRejected;
                _client.SendError -= OnClientSendError;
                _client.PlayerStateReceived -= OnPlayerStateReceived;
                _client.RegionRepairApplied -= OnRegionStateReplaced;
                _client.FullRegionStateApplied -= OnRegionStateReplaced;
                if (_client.IsConnected) _client.Disconnect();
                _client.Dispose();
                _client = null;
            }

            if (_server != null)
            {
                _server.ConnectionOpened -= OnServerConnectionOpened;
                _server.ConnectionClosed -= OnServerConnectionClosed;
                _server.ProtocolError -= OnServerProtocolError;
                _server.SendError -= OnServerSendError;
                _server.Dispose();
                _server = null;
            }

            _mode = SessionMode.Offline;
            _localPlayerId = 0;
            _clientTickAnchored = false;
            _serverMotors.Clear();
            _connectionByPlayer.Clear();
            DestroyRemoteAvatar();
        }

        private static bool IsTickNewer(uint candidate, uint reference)
        {
            uint delta = unchecked(candidate - reference);
            return delta != 0 && delta < 0x80000000u;
        }

        private static bool TryPort(int port, out ushort networkPort)
        {
            if (port < 1 || port > ushort.MaxValue)
            {
                networkPort = 0;
                return false;
            }

            networkPort = (ushort)port;
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShowcaseMultiplayerSession));
        }

        public void Dispose()
        {
            if (_disposed) return;
            Disconnect();
            _disposed = true;
        }
    }
}
