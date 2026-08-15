using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Server-owned connection -> authenticated player state. Client packets never establish
    /// identity, world position, velocity, reach, collision volume, or edit permission.
    /// </summary>
    public sealed class ServerPlayerRegistry
    {
        public static readonly int3 DefaultHalfExtentsVoxels = new int3(4, 9, 4);

        private readonly Dictionary<uint, PlayerSession> _byConnection = new Dictionary<uint, PlayerSession>(64);
        private readonly Dictionary<ushort, uint> _connectionByPlayer = new Dictionary<ushort, uint>(64);

        public int Count => _byConnection.Count;

        public bool TryRegisterAuthenticated(
            uint connectionId,
            ushort playerId,
            int3 authoritativePositionVoxels,
            int reachVoxels = Validation.k_DefaultReachVoxels,
            bool canAlterWorld = true)
        {
            if (connectionId == 0 || playerId == 0 || reachVoxels <= 0)
                return false;
            if (_byConnection.ContainsKey(connectionId) || _connectionByPlayer.ContainsKey(playerId))
                return false;

            var position = new float3(
                authoritativePositionVoxels.x,
                authoritativePositionVoxels.y,
                authoritativePositionVoxels.z);

            var session = new PlayerSession(
                connectionId,
                playerId,
                authoritativePositionVoxels,
                position,
                float3.zero,
                viewYaw: 0,
                stateFlags: S_PlayerState.StateFlags.None,
                DefaultHalfExtentsVoxels,
                reachVoxels,
                canAlterWorld);

            _byConnection.Add(connectionId, session);
            _connectionByPlayer.Add(playerId, connectionId);
            return true;
        }

        public bool TryGetByConnection(uint connectionId, out PlayerSession session) =>
            _byConnection.TryGetValue(connectionId, out session);

        public bool TryGetByPlayer(ushort playerId, out PlayerSession session)
        {
            if (_connectionByPlayer.TryGetValue(playerId, out uint connectionId))
                return _byConnection.TryGetValue(connectionId, out session);

            session = default;
            return false;
        }

        public void CopySessions(List<PlayerSession> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            foreach (PlayerSession session in _byConnection.Values)
                destination.Add(session);
        }

        public bool UpdateAuthoritativePosition(uint connectionId, int3 positionVoxels)
        {
            if (!_byConnection.TryGetValue(connectionId, out PlayerSession session))
                return false;

            session.PositionVoxels = positionVoxels;
            session.PositionVoxelsExact = new float3(positionVoxels.x, positionVoxels.y, positionVoxels.z);
            _byConnection[connectionId] = session;
            return true;
        }

        /// <summary>
        /// Update the kinematic state that will be sampled into S_PlayerState after the fixed tick.
        /// The game/simulation owns movement; networking only records and replicates its result.
        /// </summary>
        public bool UpdateAuthoritativeKinematics(
            uint connectionId,
            float3 positionVoxels,
            float3 velocityVoxelsPerSecond,
            ushort viewYaw,
            S_PlayerState.StateFlags stateFlags = S_PlayerState.StateFlags.None)
        {
            if (!_byConnection.TryGetValue(connectionId, out PlayerSession session) ||
                !IsFinite(positionVoxels) ||
                !IsFinite(velocityVoxelsPerSecond))
                return false;

            session.PositionVoxelsExact = positionVoxels;
            session.PositionVoxels = new int3(
                (int)math.floor(positionVoxels.x),
                (int)math.floor(positionVoxels.y),
                (int)math.floor(positionVoxels.z));
            session.VelocityVoxelsPerSecond = velocityVoxelsPerSecond;
            session.ViewYaw = viewYaw;
            session.StateFlags = stateFlags & ~S_PlayerState.StateFlags.HasInputAck;
            _byConnection[connectionId] = session;
            return true;
        }

        public bool SetCanAlterWorld(uint connectionId, bool canAlterWorld)
        {
            if (!_byConnection.TryGetValue(connectionId, out PlayerSession session))
                return false;

            session.CanAlterWorld = canAlterWorld;
            _byConnection[connectionId] = session;
            return true;
        }

        public bool SetReach(uint connectionId, int reachVoxels)
        {
            if (reachVoxels <= 0 || !_byConnection.TryGetValue(connectionId, out PlayerSession session))
                return false;

            session.ReachVoxels = reachVoxels;
            _byConnection[connectionId] = session;
            return true;
        }

        public bool SetCollisionHalfExtents(uint connectionId, int3 halfExtentsVoxels)
        {
            if (math.any(halfExtentsVoxels < 0) || !_byConnection.TryGetValue(connectionId, out PlayerSession session))
                return false;

            session.HalfExtentsVoxels = halfExtentsVoxels;
            _byConnection[connectionId] = session;
            return true;
        }

        public bool RemoveConnection(uint connectionId, out ushort playerId)
        {
            if (!_byConnection.TryGetValue(connectionId, out PlayerSession session))
            {
                playerId = 0;
                return false;
            }

            playerId = session.PlayerId;
            _byConnection.Remove(connectionId);
            _connectionByPlayer.Remove(session.PlayerId);
            return true;
        }

        public bool IntersectsPlayerVolume(int3 minVoxel, int3 maxVoxel)
        {
            foreach (PlayerSession player in _byConnection.Values)
            {
                int3 playerMin = player.PositionVoxels - player.HalfExtentsVoxels;
                int3 playerMax = player.PositionVoxels + player.HalfExtentsVoxels;

                if (AabbIntersects(minVoxel, maxVoxel, playerMin, playerMax))
                    return true;
            }

            return false;
        }

        private static bool IsFinite(float3 value) =>
            !math.any(math.isnan(value) | math.isinf(value));

        private static bool AabbIntersects(int3 aMin, int3 aMax, int3 bMin, int3 bMax) =>
            aMin.x <= bMax.x && aMax.x >= bMin.x &&
            aMin.y <= bMax.y && aMax.y >= bMin.y &&
            aMin.z <= bMax.z && aMax.z >= bMin.z;

        public struct PlayerSession
        {
            public uint ConnectionId;
            public ushort PlayerId;
            public int3 PositionVoxels;
            public float3 PositionVoxelsExact;
            public float3 VelocityVoxelsPerSecond;
            public ushort ViewYaw;
            public S_PlayerState.StateFlags StateFlags;
            public int3 HalfExtentsVoxels;
            public int ReachVoxels;
            public bool CanAlterWorld;

            public PlayerSession(
                uint connectionId,
                ushort playerId,
                int3 positionVoxels,
                float3 positionVoxelsExact,
                float3 velocityVoxelsPerSecond,
                ushort viewYaw,
                S_PlayerState.StateFlags stateFlags,
                int3 halfExtentsVoxels,
                int reachVoxels,
                bool canAlterWorld)
            {
                ConnectionId = connectionId;
                PlayerId = playerId;
                PositionVoxels = positionVoxels;
                PositionVoxelsExact = positionVoxelsExact;
                VelocityVoxelsPerSecond = velocityVoxelsPerSecond;
                ViewYaw = viewYaw;
                StateFlags = stateFlags;
                HalfExtentsVoxels = halfExtentsVoxels;
                ReachVoxels = reachVoxels;
                CanAlterWorld = canAlterWorld;
            }
        }
    }
}
