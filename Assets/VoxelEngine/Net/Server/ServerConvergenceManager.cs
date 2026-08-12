using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Interest;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Periodic semantic drift detection plus exact-checkpoint repair.
    /// A hash is issued only when its exact bounded semantic snapshot is retained.
    /// Region checkpoints are deterministically staggered across the configured interval so the
    /// server does not serialize every interested region on the same simulation tick.
    /// </summary>
    public sealed class ServerConvergenceManager
    {
        public const uint DefaultHashIntervalTicks = 30;
        public const uint CheckpointRetentionTicks = 90;
        public const int MaxRetainedSnapshotBytes = 8 * 1024 * 1024;
        public const int DefaultRepairPacketsPerTick = 2;

        private readonly ServerConvergenceInbox _inbox;
        private readonly ServerPlayerRegistry _players;
        private readonly uint _hashIntervalTicks;
        private readonly int _repairPacketsPerTick;
        private readonly Dictionary<CheckpointKey, uint> _issuedHashes = new Dictionary<CheckpointKey, uint>(256);
        private readonly Dictionary<SnapshotKey, byte[]> _snapshots = new Dictionary<SnapshotKey, byte[]>(128);
        private readonly List<PendingRepair> _pendingRepairs = new List<PendingRepair>(16);
        private readonly List<ServerConvergenceInbox.QueuedMismatch> _mismatchDrain = new List<ServerConvergenceInbox.QueuedMismatch>(32);
        private readonly List<CheckpointKey> _checkpointRemoveScratch = new List<CheckpointKey>(64);
        private readonly List<SnapshotKey> _snapshotRemoveScratch = new List<SnapshotKey>(32);
        private readonly HashSet<uint> _subscriberScratch = new HashSet<uint>();
        private int _retainedSnapshotBytes;

        public event Action<VerifiedRegionMismatch> VerifiedMismatch;
        public event Action<RegionResyncRequest> ResyncRequired;

        public long VerifiedMismatchCount { get; private set; }
        public long RejectedMismatchCount { get; private set; }
        public long ResyncRequiredCount { get; private set; }
        public long HashPacketsSent { get; private set; }
        public long HashesSkippedNoSnapshot { get; private set; }
        public long RepairPacketsSent { get; private set; }
        public long RepairSnapshotsCompleted { get; private set; }
        public int RetainedSnapshotBytes => _retainedSnapshotBytes;
        public int PendingRepairCount => _pendingRepairs.Count;

        public ServerConvergenceManager(
            ServerConvergenceInbox inbox,
            ServerPlayerRegistry players,
            uint hashIntervalTicks = DefaultHashIntervalTicks,
            int repairPacketsPerTick = DefaultRepairPacketsPerTick)
        {
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            if (hashIntervalTicks == 0) throw new ArgumentOutOfRangeException(nameof(hashIntervalTicks));
            if (repairPacketsPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(repairPacketsPerTick));
            _hashIntervalTicks = hashIntervalTicks;
            _repairPacketsPerTick = repairPacketsPerTick;
        }

        public int ProcessMismatchReports(uint serverTick, RegionSubscriptionIndex subscriptions)
        {
            if (subscriptions == null) throw new ArgumentNullException(nameof(subscriptions));

            _mismatchDrain.Clear();
            _inbox.Drain(_mismatchDrain);
            int verified = 0;

            for (int i = 0; i < _mismatchDrain.Count; i++)
            {
                var queued = _mismatchDrain[i];
                C_RegionHashMismatch report = queued.Mismatch;

                bool authenticatedAndInterested =
                    _players.TryGetByConnection(queued.ConnectionId, out _) &&
                    subscriptions.IsSubscribed(queued.ConnectionId, report.regionCoord) &&
                    report.clientHash != report.serverHash;

                if (!authenticatedAndInterested || report.hashTick > serverTick)
                {
                    RejectedMismatchCount++;
                    continue;
                }

                var checkpointKey = new CheckpointKey(queued.ConnectionId, report.regionCoord, report.hashTick);
                if (!_issuedHashes.TryGetValue(checkpointKey, out uint issuedHash))
                {
                    uint oldestRetained = serverTick > CheckpointRetentionTicks
                        ? serverTick - CheckpointRetentionTicks
                        : 0u;

                    // Inside the retention window, absence proves this checkpoint was never issued
                    // to this connection (wrong phase/region or fabricated report). Only history old
                    // enough to have legitimately fallen out of retention may escalate to BULK state.
                    if (report.hashTick >= oldestRetained)
                    {
                        RejectedMismatchCount++;
                        continue;
                    }

                    RequireFullResync(
                        queued.ConnectionId,
                        report.regionCoord,
                        report.hashTick,
                        S_RegionResyncRequired.Reason.CheckpointExpired);
                    continue;
                }

                if (issuedHash != report.serverHash)
                {
                    RejectedMismatchCount++;
                    continue;
                }

                var snapshotKey = new SnapshotKey(report.regionCoord, report.hashTick);
                if (!_snapshots.TryGetValue(snapshotKey, out byte[] snapshot))
                {
                    RequireFullResync(
                        queued.ConnectionId,
                        report.regionCoord,
                        report.hashTick,
                        S_RegionResyncRequired.Reason.SnapshotUnavailable);
                    continue;
                }

                if (!HasPendingRepair(queued.ConnectionId, report.regionCoord, report.hashTick))
                {
                    _pendingRepairs.Add(new PendingRepair(
                        queued.ConnectionId,
                        report.regionCoord,
                        report.hashTick,
                        report.serverHash,
                        snapshot));
                }

                verified++;
                VerifiedMismatchCount++;
                VerifiedMismatch?.Invoke(new VerifiedRegionMismatch(
                    queued.ConnectionId,
                    report.regionCoord,
                    report.hashTick,
                    report.clientHash,
                    report.serverHash,
                    repairQueued: true));
            }

            PruneCheckpoints(serverTick);
            return verified;
        }

        public int FlushRepairPackets(ServerNetworkRuntime network)
        {
            if (network == null) throw new ArgumentNullException(nameof(network));
            int sent = 0;

            for (int i = 0; i < _pendingRepairs.Count && sent < _repairPacketsPerTick;)
            {
                PendingRepair repair = _pendingRepairs[i];
                if (!network.ContainsConnection(repair.ConnectionId))
                {
                    _pendingRepairs.RemoveAt(i);
                    continue;
                }

                int chunkLength = Math.Min(
                    RegionRepairChunkPacket.MaxChunkBytes,
                    repair.Snapshot.Length - repair.Offset);
                Span<byte> packet = stackalloc byte[RegionRepairChunkPacket.MaxPacketSize];
                if (!RegionRepairChunkPacket.TryEncode(
                        packet,
                        repair.RegionCoord,
                        repair.SnapshotTick,
                        repair.SemanticHash,
                        repair.Snapshot.Length,
                        repair.Offset,
                        repair.Snapshot.AsSpan(repair.Offset, chunkLength),
                        out int bytesWritten) ||
                    !network.TrySend(
                        repair.ConnectionId,
                        UtpChannel.Repair,
                        packet.Slice(0, bytesWritten)))
                    break;

                repair.Offset += chunkLength;
                sent++;
                RepairPacketsSent++;

                if (repair.Offset == repair.Snapshot.Length)
                {
                    _pendingRepairs.RemoveAt(i);
                    RepairSnapshotsCompleted++;
                }
                else
                {
                    _pendingRepairs[i] = repair;
                    i++;
                }
            }

            return sent;
        }

        public int EmitHashes(
            uint serverTick,
            ref RegionTable table,
            in BrickPool pool,
            RegionSubscriptionIndex subscriptions,
            ServerNetworkRuntime network)
        {
            if (subscriptions == null) throw new ArgumentNullException(nameof(subscriptions));
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (serverTick == 0) return 0;

            PruneCheckpoints(serverTick);
            NativeArray<int3> regions = table.GetResidentCoords(Allocator.Temp);
            int sent = 0;
            try
            {
                for (int i = 0; i < regions.Length; i++)
                {
                    int3 coord = regions[i];
                    if (!ShouldCheckpointRegion(coord, serverTick))
                        continue;

                    _subscriberScratch.Clear();
                    subscriptions.AddSubscribers(coord, _subscriberScratch);
                    if (_subscriberScratch.Count == 0) continue;
                    if (!table.TryGetRegion(coord, out Region region) || !region.BrickRefs.IsCreated) continue;

                    if (!SemanticRegionSnapshotCodec.TryEncode(
                            in region,
                            in pool,
                            SemanticRegionSnapshotCodec.DefaultMaxSnapshotBytes,
                            out byte[] snapshot) ||
                        _retainedSnapshotBytes + snapshot.Length > MaxRetainedSnapshotBytes)
                    {
                        HashesSkippedNoSnapshot++;
                        continue;
                    }

                    uint semanticHash = SemanticRegionHasher.HashRegion(in region, in pool);
                    var message = new S_RegionHash(coord, serverTick, semanticHash);
                    int regionSent = 0;

                    foreach (uint connectionId in _subscriberScratch)
                    {
                        if (!network.SendRegionHash(connectionId, in message)) continue;
                        _issuedHashes[new CheckpointKey(connectionId, coord, serverTick)] = semanticHash;
                        regionSent++;
                        sent++;
                        HashPacketsSent++;
                    }

                    if (regionSent > 0)
                    {
                        var key = new SnapshotKey(coord, serverTick);
                        _snapshots[key] = snapshot;
                        _retainedSnapshotBytes += snapshot.Length;
                    }
                }
            }
            finally
            {
                regions.Dispose();
            }

            return sent;
        }

        public void RemoveConnection(uint connectionId)
        {
            _checkpointRemoveScratch.Clear();
            foreach (var pair in _issuedHashes)
                if (pair.Key.ConnectionId == connectionId)
                    _checkpointRemoveScratch.Add(pair.Key);
            for (int i = 0; i < _checkpointRemoveScratch.Count; i++)
                _issuedHashes.Remove(_checkpointRemoveScratch[i]);

            for (int i = _pendingRepairs.Count - 1; i >= 0; i--)
                if (_pendingRepairs[i].ConnectionId == connectionId)
                    _pendingRepairs.RemoveAt(i);

            _inbox.RemoveConnection(connectionId);
        }

        private bool ShouldCheckpointRegion(int3 coord, uint serverTick)
        {
            if (_hashIntervalTicks == 1)
                return true;

            uint hash = unchecked((uint)coord.x) * 0x9E3779B1u;
            hash ^= unchecked((uint)coord.y) * 0x85EBCA77u;
            hash ^= unchecked((uint)coord.z) * 0xC2B2AE3Du;
            hash ^= hash >> 16;
            return hash % _hashIntervalTicks == serverTick % _hashIntervalTicks;
        }

        private void RequireFullResync(
            uint connectionId,
            int3 regionCoord,
            uint failedTick,
            S_RegionResyncRequired.Reason reason)
        {
            ResyncRequiredCount++;
            ResyncRequired?.Invoke(new RegionResyncRequest(connectionId, regionCoord, failedTick, reason));
        }

        private bool HasPendingRepair(uint connectionId, int3 regionCoord, uint tick)
        {
            for (int i = 0; i < _pendingRepairs.Count; i++)
            {
                PendingRepair repair = _pendingRepairs[i];
                if (repair.ConnectionId == connectionId && repair.SnapshotTick == tick && repair.RegionCoord.Equals(regionCoord))
                    return true;
            }
            return false;
        }

        private void PruneCheckpoints(uint serverTick)
        {
            uint oldest = serverTick > CheckpointRetentionTicks ? serverTick - CheckpointRetentionTicks : 0;

            _checkpointRemoveScratch.Clear();
            foreach (var pair in _issuedHashes)
                if (pair.Key.Tick < oldest)
                    _checkpointRemoveScratch.Add(pair.Key);
            for (int i = 0; i < _checkpointRemoveScratch.Count; i++)
                _issuedHashes.Remove(_checkpointRemoveScratch[i]);

            _snapshotRemoveScratch.Clear();
            foreach (var pair in _snapshots)
                if (pair.Key.Tick < oldest && !SnapshotIsPendingRepair(pair.Key))
                    _snapshotRemoveScratch.Add(pair.Key);
            for (int i = 0; i < _snapshotRemoveScratch.Count; i++)
            {
                SnapshotKey key = _snapshotRemoveScratch[i];
                _retainedSnapshotBytes -= _snapshots[key].Length;
                _snapshots.Remove(key);
            }
        }

        private bool SnapshotIsPendingRepair(SnapshotKey key)
        {
            for (int i = 0; i < _pendingRepairs.Count; i++)
                if (_pendingRepairs[i].SnapshotTick == key.Tick && _pendingRepairs[i].RegionCoord.Equals(key.Region))
                    return true;
            return false;
        }

        private readonly struct CheckpointKey : IEquatable<CheckpointKey>
        {
            public readonly uint ConnectionId;
            public readonly int3 Region;
            public readonly uint Tick;
            public CheckpointKey(uint connectionId, int3 region, uint tick) { ConnectionId = connectionId; Region = region; Tick = tick; }
            public bool Equals(CheckpointKey other) => ConnectionId == other.ConnectionId && Tick == other.Tick && Region.Equals(other.Region);
            public override bool Equals(object obj) => obj is CheckpointKey other && Equals(other);
            public override int GetHashCode() => unchecked((((int)ConnectionId * 397) ^ Region.GetHashCode()) * 397 ^ (int)Tick);
        }

        private readonly struct SnapshotKey : IEquatable<SnapshotKey>
        {
            public readonly int3 Region;
            public readonly uint Tick;
            public SnapshotKey(int3 region, uint tick) { Region = region; Tick = tick; }
            public bool Equals(SnapshotKey other) => Tick == other.Tick && Region.Equals(other.Region);
            public override bool Equals(object obj) => obj is SnapshotKey other && Equals(other);
            public override int GetHashCode() => unchecked(Region.GetHashCode() * 397 ^ (int)Tick);
        }

        private struct PendingRepair
        {
            public uint ConnectionId;
            public int3 RegionCoord;
            public uint SnapshotTick;
            public uint SemanticHash;
            public byte[] Snapshot;
            public int Offset;

            public PendingRepair(uint connectionId, int3 regionCoord, uint snapshotTick, uint semanticHash, byte[] snapshot)
            {
                ConnectionId = connectionId;
                RegionCoord = regionCoord;
                SnapshotTick = snapshotTick;
                SemanticHash = semanticHash;
                Snapshot = snapshot;
                Offset = 0;
            }
        }

        public readonly struct VerifiedRegionMismatch
        {
            public readonly uint ConnectionId;
            public readonly int3 RegionCoord;
            public readonly uint HashTick;
            public readonly uint ClientHash;
            public readonly uint ServerHash;
            public readonly bool RepairQueued;

            public VerifiedRegionMismatch(uint connectionId, int3 regionCoord, uint hashTick, uint clientHash, uint serverHash, bool repairQueued)
            {
                ConnectionId = connectionId;
                RegionCoord = regionCoord;
                HashTick = hashTick;
                ClientHash = clientHash;
                ServerHash = serverHash;
                RepairQueued = repairQueued;
            }
        }

        public readonly struct RegionResyncRequest
        {
            public readonly uint ConnectionId;
            public readonly int3 RegionCoord;
            public readonly uint FailedHashTick;
            public readonly S_RegionResyncRequired.Reason Reason;

            public RegionResyncRequest(uint connectionId, int3 regionCoord, uint failedHashTick, S_RegionResyncRequired.Reason reason)
            {
                ConnectionId = connectionId;
                RegionCoord = regionCoord;
                FailedHashTick = failedHashTick;
                Reason = reason;
            }
        }
    }
}
