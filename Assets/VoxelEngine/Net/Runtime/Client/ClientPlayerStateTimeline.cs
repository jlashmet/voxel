using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    public readonly struct RemotePlayerSample
    {
        public readonly ushort PlayerId;
        public readonly uint ServerTick;
        public readonly float3 PositionVoxels;
        public readonly float3 VelocityVoxelsPerSecond;
        public readonly float ViewYawRadians;
        public readonly S_PlayerState.StateFlags Flags;

        public RemotePlayerSample(
            ushort playerId,
            uint serverTick,
            float3 positionVoxels,
            float3 velocityVoxelsPerSecond,
            float viewYawRadians,
            S_PlayerState.StateFlags flags)
        {
            PlayerId = playerId;
            ServerTick = serverTick;
            PositionVoxels = positionVoxels;
            VelocityVoxelsPerSecond = velocityVoxelsPerSecond;
            ViewYawRadians = viewYawRadians;
            Flags = flags;
        }
    }

    /// <summary>
    /// Per-player two-snapshot timeline for interpolation. Snapshot sequence is validated at the
    /// application layer so a stale/reordered EPHEMERAL packet cannot move a remote player backward.
    /// </summary>
    public sealed class ClientPlayerStateTimeline
    {
        private readonly Dictionary<ushort, Track> _tracks = new Dictionary<ushort, Track>(64);

        public int Count => _tracks.Count;
        public long StaleSnapshots { get; private set; }

        public bool TryAccept(in S_PlayerState state)
        {
            if (state.playerId == 0)
                return false;

            if (!_tracks.TryGetValue(state.playerId, out Track track))
            {
                _tracks.Add(state.playerId, new Track(state));
                return true;
            }

            if (!IsNewer(state.sequence, track.Current.sequence))
            {
                StaleSnapshots++;
                return false;
            }

            track.Previous = track.Current;
            track.Current = state;
            track.HasPrevious = true;
            _tracks[state.playerId] = track;
            return true;
        }

        public bool TrySample(ushort playerId, float alpha, out RemotePlayerSample sample)
        {
            sample = default;
            if (!_tracks.TryGetValue(playerId, out Track track))
                return false;

            S_PlayerState current = track.Current;
            if (!track.HasPrevious ||
                (current.Flags & (S_PlayerState.StateFlags.Teleport | S_PlayerState.StateFlags.Respawn)) != 0)
            {
                sample = FromState(in current);
                return true;
            }

            float t = math.clamp(alpha, 0f, 1f);
            float3 aPos = track.Previous.PositionVoxels();
            float3 bPos = current.PositionVoxels();
            float3 aVel = track.Previous.VelocityVoxelsPerSecond();
            float3 bVel = current.VelocityVoxelsPerSecond();
            float aYaw = track.Previous.ViewYawRadians();
            float bYaw = current.ViewYawRadians();
            float yawDelta = math.atan2(math.sin(bYaw - aYaw), math.cos(bYaw - aYaw));

            sample = new RemotePlayerSample(
                playerId,
                current.tick,
                math.lerp(aPos, bPos, t),
                math.lerp(aVel, bVel, t),
                aYaw + yawDelta * t,
                current.Flags);
            return true;
        }

        public void RemovePlayer(ushort playerId) => _tracks.Remove(playerId);
        public void Reset() => _tracks.Clear();

        private static RemotePlayerSample FromState(in S_PlayerState state) =>
            new RemotePlayerSample(
                state.playerId,
                state.tick,
                state.PositionVoxels(),
                state.VelocityVoxelsPerSecond(),
                state.ViewYawRadians(),
                state.Flags);

        private static bool IsNewer(ushort candidate, ushort reference)
        {
            ushort delta = unchecked((ushort)(candidate - reference));
            return delta != 0 && delta < 0x8000;
        }

        private struct Track
        {
            public S_PlayerState Previous;
            public S_PlayerState Current;
            public bool HasPrevious;

            public Track(S_PlayerState current)
            {
                Previous = default;
                Current = current;
                HasPrevious = false;
            }
        }
    }
}
