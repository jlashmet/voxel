using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Interest;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Periodic semantic drift detection and authenticated mismatch verification.
    ///
    /// This intentionally stops before state repair. The legacy WorldHistory does not preserve a
    /// semantic per-region snapshot at the advertised hash tick, so dispatching current/raw pool
    /// state would be incorrect. VerifiedMismatch is the explicit seam for the forthcoming snapshot
    /// repair store.
    /// </summary>
    public sealed class ServerConvergenceManager
    {
        public const uint DefaultHashIntervalTicks = 30; // 1 second at 30 Hz
        public const uint CheckpointRetentionTicks = 90; // 3 seconds

        private readonly ServerConvergenceInbox _inbox;
        private readonly ServerPlayerRegistry _players;
        private readonly uint _hashIntervalTicks;
        private readonly Dictionary<CheckpointKey, uint> _issuedHashes =
            new Dictionary<CheckpointKey, uint>(256);
        private readonly List<ServerConvergenceInbox.QueuedMismatch> _mismatchDrain =
            new List<ServerConvergenceInbox.QueuedMismatch>(32);
        private readonly List<CheckpointKey> _removeScratch = new List<CheckpointKey>(64);
        private readonly HashSet<uint> _subscriberScratch = new HashSet<uint>();

        public event Action<VerifiedRegionMismatch> VerifiedMismatch;

        public long VerifiedMismatchCount { get; private set; }
        public long RejectedMismatchCount { get; private set; }
        public long HashPacketsSent { get; private set; }

        public ServerConvergenceManager(
            ServerConvergenceInbox inbox,
            ServerPlayerRegistry players,
            uint hashIntervalTicks = DefaultHashIntervalTicks)
        {
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            if (hashIntervalTicks == 0) throw new ArgumentOutOfRangeException(nameof(hashIntervalTicks));
            _hashIntervalTicks = hashIntervalTicks;
        }

        /// <summary>
        /// Drain mismatch reports and verify they refer to a hash actually issued to that live,
        /// authenticated, still-interested connection. Clients cannot manufacture repair checkpoints.
        /// </summary>
        public int ProcessMismatchReports(
            uint serverTick,
            RegionSubscriptionIndex subscriptions)
        {
            if (subscriptions == null) throw new ArgumentNullException(nameof(subscriptions));

            _mismatchDrain.Clear();
            _inbox.Drain(_mismatchDrain);
            int verified = 0;

            for (int i = 0; i < _mismatchDrain.Count; i++)
            {
                var queued = _mismatchDrain[i];
                C_RegionHashMismatch report = queued.Mismatch;
                var key = new CheckpointKey(queued.ConnectionId, report.regionCoord, report.hashTick);

                bool valid =
                    _players.TryGetByConnection(queued.ConnectionId, out _) &&
                    subscriptions.IsSubscribed(queued.ConnectionId, report.regionCoord) &&
                    report.clientHash != report.serverHash &&
                    _issuedHashes.TryGetValue(key, out uint issuedHash) &&
                    issuedHash == report.serverHash;

                if (!valid)
                {
                    RejectedMismatchCount++;
                    continue;
                }

                verified++;
                VerifiedMismatchCount++;
                VerifiedMismatch?.Invoke(new VerifiedRegionMismatch(
                    queued.ConnectionId,
                    report.regionCoord,
                    report.hashTick,
                    report.clientHash,
                    report.serverHash));
            }

            PruneCheckpoints(serverTick);
            return verified;
        }

        /// <summary>
        /// After authoritative mutation batches for serverTick have been queued on EVENT, append a
        /// semantic hash checkpoint for each resident region that has subscribers.
        /// </summary>
        public int EmitHashes(
            uint serverTick,
            ref RegionTable table,
            in BrickPool pool,
            RegionSubscriptionIndex subscriptions,
            ServerNetworkRuntime network)
        {
            if (subscriptions == null) throw new ArgumentNullException(nameof(subscriptions));
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (serverTick == 0 || serverTick % _hashIntervalTicks != 0)
                return 0;

            NativeArray<int3> regions = table.GetResidentCoords(Allocator.Temp);
            int sent = 0;
            try
            {
                for (int i = 0; i < regions.Length; i++)
                {
                    int3 coord = regions[i];
                    _subscriberScratch.Clear();
                    subscriptions.AddSubscribers(coord, _subscriberScratch);
                    if (_subscriberScratch.Count == 0)
                        continue;
                    if (!table.TryGetRegion(coord, out Region region) || !region.BrickRefs.IsCreated)
                        continue;

                    uint semanticHash = SemanticRegionHasher.HashRegion(in region, in pool);
                    var message = new S_RegionHash(coord, serverTick, semanticHash);

                    foreach (uint connectionId in _subscriberScratch)
                    {
                        if (!network.SendRegionHash(connectionId, in message))
                            continue;

                        _issuedHashes[new CheckpointKey(connectionId, coord, serverTick)] = semanticHash;
                        sent++;
                        HashPacketsSent++;
                    }
                }
            }
            finally
            {
                regions.Dispose();
            }

            PruneCheckpoints(serverTick);
            return sent;
        }

        public void RemoveConnection(uint connectionId)
        {
            _removeScratch.Clear();
            foreach (var pair in _issuedHashes)
                if (pair.Key.ConnectionId == connectionId)
                    _removeScratch.Add(pair.Key);
            for (int i = 0; i < _removeScratch.Count; i++)
                _issuedHashes.Remove(_removeScratch[i]);
            _inbox.RemoveConnection(connectionId);
        }

        private void PruneCheckpoints(uint serverTick)
        {
            uint oldest = serverTick > CheckpointRetentionTicks
                ? serverTick - CheckpointRetentionTicks
                : 0;

            _removeScratch.Clear();
            foreach (var pair in _issuedHashes)
                if (pair.Key.Tick < oldest)
                    _removeScratch.Add(pair.Key);
            for (int i = 0; i < _removeScratch.Count; i++)
                _issuedHashes.Remove(_removeScratch[i]);
        }

        private readonly struct CheckpointKey : IEquatable<CheckpointKey>
        {
            public readonly uint ConnectionId;
            public readonly int3 Region;
            public readonly uint Tick;

            public CheckpointKey(uint connectionId, int3 region, uint tick)
            {
                ConnectionId = connectionId;
                Region = region;
                Tick = tick;
            }

            public bool Equals(CheckpointKey other) =>
                ConnectionId == other.ConnectionId && Tick == other.Tick && Region.Equals(other.Region);
            public override bool Equals(object obj) => obj is CheckpointKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)ConnectionId;
                    hash = (hash * 397) ^ Region.GetHashCode();
                    hash = (hash * 397) ^ (int)Tick;
                    return hash;
                }
            }
        }

        public readonly struct VerifiedRegionMismatch
        {
            public readonly uint ConnectionId;
            public readonly int3 RegionCoord;
            public readonly uint HashTick;
            public readonly uint ClientHash;
            public readonly uint ServerHash;

            public VerifiedRegionMismatch(
                uint connectionId,
                int3 regionCoord,
                uint hashTick,
                uint clientHash,
                uint serverHash)
            {
                ConnectionId = connectionId;
                RegionCoord = regionCoord;
                HashTick = hashTick;
                ClientHash = clientHash;
                ServerHash = serverHash;
            }
        }
    }
}
