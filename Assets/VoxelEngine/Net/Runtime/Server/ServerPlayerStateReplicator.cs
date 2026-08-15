using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Net.Runtime.Interest;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Net.Runtime.Server
{
    public interface IPlayerStateBundleSink
    {
        bool SendPlayerStateBundle(uint connectionId, ReadOnlySpan<S_PlayerState> states);
    }

    /// <summary>
    /// Samples game-owned authoritative kinematics after the fixed tick and routes supersedable
    /// snapshots over EPHEMERAL. Local owners always receive their own state for reconciliation;
    /// remote state is limited to subscribers of the player's current simulation region.
    /// </summary>
    public sealed class ServerPlayerStateReplicator
    {
        public const uint DefaultIntervalTicks = 2; // 15 Hz at a 30 Hz authoritative tick.

        private readonly ServerPlayerRegistry _players;
        private readonly IProcessedInputAckSource _ackSource;
        private readonly uint _intervalTicks;
        private readonly List<ServerPlayerRegistry.PlayerSession> _sessions =
            new List<ServerPlayerRegistry.PlayerSession>(64);
        private readonly Dictionary<ushort, ushort> _nextStateSequence = new Dictionary<ushort, ushort>(64);
        private readonly Dictionary<uint, List<S_PlayerState>> _routes = new Dictionary<uint, List<S_PlayerState>>(64);
        private readonly Stack<List<S_PlayerState>> _routePool = new Stack<List<S_PlayerState>>();
        private readonly HashSet<uint> _recipientScratch = new HashSet<uint>();

        public long SnapshotsAuthored { get; private set; }
        public long BundlesSent { get; private set; }
        public long SendFailures { get; private set; }

        public ServerPlayerStateReplicator(
            ServerPlayerRegistry players,
            IProcessedInputAckSource ackSource,
            uint intervalTicks = DefaultIntervalTicks)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _ackSource = ackSource ?? throw new ArgumentNullException(nameof(ackSource));
            if (intervalTicks == 0) throw new ArgumentOutOfRangeException(nameof(intervalTicks));
            _intervalTicks = intervalTicks;
        }

        public void Emit(
            uint serverTick,
            RegionSubscriptionIndex subscriptions,
            IPlayerStateBundleSink sink)
        {
            if (serverTick == 0) throw new ArgumentOutOfRangeException(nameof(serverTick));
            if (subscriptions == null) throw new ArgumentNullException(nameof(subscriptions));
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (serverTick % _intervalTicks != 0)
                return;

            ResetRoutes();
            _players.CopySessions(_sessions);

            for (int i = 0; i < _sessions.Count; i++)
            {
                ServerPlayerRegistry.PlayerSession player = _sessions[i];
                ushort stateSequence = NextSequence(player.PlayerId);
                bool hasAck = _ackSource.TryGetLastProcessedInputSequence(player.PlayerId, out ushort ack);

                S_PlayerState snapshot = S_PlayerState.Create(
                    player.PlayerId,
                    serverTick,
                    stateSequence,
                    player.PositionVoxelsExact,
                    player.VelocityVoxelsPerSecond,
                    player.ViewYaw,
                    player.StateFlags,
                    hasAck,
                    ack);

                SnapshotsAuthored++;
                _recipientScratch.Clear();
                int3 region = SimulationInterest.WorldVoxelToRegion(player.PositionVoxels);
                subscriptions.AddSubscribers(region, _recipientScratch);
                _recipientScratch.Add(player.ConnectionId);

                foreach (uint connectionId in _recipientScratch)
                    GetRoute(connectionId).Add(snapshot);
            }

            foreach (var pair in _routes)
            {
                List<S_PlayerState> route = pair.Value;
                int offset = 0;
                while (offset < route.Count)
                {
                    int count = Math.Min(PlayerStateBundlePacket.MaxStates, route.Count - offset);
                    Span<S_PlayerState> bundle = stackalloc S_PlayerState[PlayerStateBundlePacket.MaxStates];
                    for (int i = 0; i < count; i++)
                        bundle[i] = route[offset + i];

                    if (sink.SendPlayerStateBundle(pair.Key, bundle.Slice(0, count)))
                        BundlesSent++;
                    else
                        SendFailures++;

                    offset += count;
                }
            }
        }

        public void RemovePlayer(ushort playerId)
        {
            if (playerId != 0)
                _nextStateSequence.Remove(playerId);
        }

        private ushort NextSequence(ushort playerId)
        {
            if (!_nextStateSequence.TryGetValue(playerId, out ushort previous))
            {
                _nextStateSequence[playerId] = 1;
                return 1;
            }

            ushort next = unchecked((ushort)(previous + 1));
            _nextStateSequence[playerId] = next;
            return next;
        }

        private List<S_PlayerState> GetRoute(uint connectionId)
        {
            if (_routes.TryGetValue(connectionId, out List<S_PlayerState> route))
                return route;

            route = _routePool.Count > 0 ? _routePool.Pop() : new List<S_PlayerState>(8);
            _routes.Add(connectionId, route);
            return route;
        }

        private void ResetRoutes()
        {
            foreach (var pair in _routes)
            {
                pair.Value.Clear();
                _routePool.Push(pair.Value);
            }
            _routes.Clear();
        }
    }
}
