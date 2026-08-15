using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    public interface IRegionHashMismatchSink
    {
        void OnRegionHashMismatch(in C_RegionHashMismatch mismatch);
    }

    /// <summary>
    /// Ordered client-side queue for authoritative EVENT packets. Mutation batches, region hashes,
    /// and full-state fences share one FIFO. Exact repair pauses at a hash barrier. Current-state
    /// replacement pauses until BULK state is installed, then replays pre-fence authority everywhere
    /// except the replaced region until the matching EVENT fence is reached.
    /// </summary>
    public sealed class ClientAuthoritativeEventQueue
    {
        public const int DefaultMaxPendingEvents = 4096;

        private readonly Queue<PendingAuthority> _authority = new Queue<PendingAuthority>(64);
        private readonly IAlterationApplier _applier;
        private readonly int _maxPendingEvents;
        private int _pendingEvents;
        private int _pendingBatches;
        private int _pendingHashes;
        private int _pendingFences;
        private bool _hasLastReceivedEvent;
        private AlterationEvent _lastReceivedEvent;
        private uint _lastBarrierTick;

        private bool _repairPending;
        private int3 _repairRegion;
        private uint _repairTick;
        private uint _repairHash;

        private bool _fullSnapshotWaitPending;
        private int3 _fullSnapshotWaitRegion;

        private bool _snapshotCatchupActive;
        private uint _snapshotCatchupTransferId;
        private int3 _snapshotCatchupRegion;
        private uint _snapshotCatchupTick;
        private RegionMutationStore _mutationStorage;

        public ClientAuthoritativeEventQueue(
            IAlterationApplier applier,
            int maxPendingEvents = DefaultMaxPendingEvents)
        {
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            if (maxPendingEvents <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPendingEvents));
            _maxPendingEvents = maxPendingEvents;
        }

        public int PendingBatchCount => _pendingBatches;
        public int PendingEventCount => _pendingEvents;
        public int PendingHashCount => _pendingHashes;
        public int PendingFenceCount => _pendingFences;
        public int PendingAuthorityCount => _authority.Count;
        public bool RepairPending => _repairPending;
        public int3 RepairRegion => _repairRegion;
        public uint RepairTick => _repairTick;
        public uint RepairHash => _repairHash;
        public bool FullSnapshotWaitPending => _fullSnapshotWaitPending;
        public int3 FullSnapshotWaitRegion => _fullSnapshotWaitRegion;
        public bool SnapshotCatchupActive => _snapshotCatchupActive;
        public int3 SnapshotCatchupRegion => _snapshotCatchupRegion;
        public uint SnapshotCatchupTick => _snapshotCatchupTick;

        public bool TryEnqueueEventPacket(ReadOnlySpan<byte> packet, IClientEventNotificationSink notifications = null)
        {
            if (!ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset))
                return false;

            switch (kind)
            {
                case ProtocolMessageKind.S_AlterationRejected:
                    if (!AlterationRejectedPacket.TryDecode(packet, out S_AlterationRejected rejection))
                        return false;
                    notifications?.OnAlterationRejected(in rejection);
                    return true;

                case ProtocolMessageKind.S_RegionHash:
                    return TryEnqueueHash(packet);

                case ProtocolMessageKind.S_AlterationEventBatch:
                    return TryEnqueueBatch(packet.Slice(payloadOffset));

                case ProtocolMessageKind.S_RegionStateFence:
                    return TryEnqueueFence(packet);

                default:
                    return false;
            }
        }

        private bool TryEnqueueHash(ReadOnlySpan<byte> packet)
        {
            if (!RegionHashPacket.TryDecode(packet, out S_RegionHash hash))
                return false;

            uint lastEventTick = _hasLastReceivedEvent ? _lastReceivedEvent.tick : 0u;
            if (hash.serverTick < lastEventTick || hash.serverTick < _lastBarrierTick)
                return false;

            _authority.Enqueue(PendingAuthority.ForHash(hash));
            _pendingHashes++;
            _lastBarrierTick = hash.serverTick;
            return true;
        }

        private bool TryEnqueueFence(ReadOnlySpan<byte> packet)
        {
            if (!RegionStateFencePacket.TryDecode(packet, out S_RegionStateFence fence))
                return false;

            uint lastEventTick = _hasLastReceivedEvent ? _lastReceivedEvent.tick : 0u;
            if (fence.snapshotTick < lastEventTick || fence.snapshotTick < _lastBarrierTick)
                return false;

            _authority.Enqueue(PendingAuthority.ForFence(fence));
            _pendingFences++;
            _lastBarrierTick = fence.snapshotTick;
            return true;
        }

        private bool TryEnqueueBatch(ReadOnlySpan<byte> payload)
        {
            if (!S_AlterationEventBatch.TryDecodeHeader(payload, out var header) ||
                header.count <= 0 || _pendingEvents + header.count > _maxPendingEvents ||
                header.tick < _lastBarrierTick)
                return false;

            var decoded = new AlterationEvent[header.count];
            AlterationEvent prior = _lastReceivedEvent;
            bool hasPrior = _hasLastReceivedEvent;

            for (int i = 0; i < header.count; i++)
            {
                if (!S_AlterationEventBatch.TryDecodeEvent(payload, in header, i, out AlterationEvent evt) ||
                    !_applier.Supports(in evt) || evt.tick < _lastBarrierTick ||
                    (hasPrior && CompareAuthority(in evt, in prior) < 0))
                    return false;

                decoded[i] = evt;
                prior = evt;
                hasPrior = true;
            }

            _authority.Enqueue(PendingAuthority.ForBatch(decoded));
            _pendingEvents += decoded.Length;
            _pendingBatches++;
            _lastReceivedEvent = prior;
            _hasLastReceivedEvent = hasPrior;
            return true;
        }

        public int DrainReady(ref RegionTable table, ref BrickPool pool, out int appliedEvents) =>
            DrainReady(ref table, ref pool, out appliedEvents, null, out _);

        public int DrainReady(
            ref RegionTable table,
            ref BrickPool pool,
            out int appliedEvents,
            IRegionHashMismatchSink mismatchSink,
            out int comparedHashes)
        {
            appliedEvents = 0;
            comparedHashes = 0;
            int appliedBatches = 0;

            _mutationStorage ??= new RegionMutationStore(in table, in pool);
            _mutationStorage.Refresh(in table, in pool);

            if (_repairPending || _fullSnapshotWaitPending)
                return 0;

            while (_authority.Count > 0)
            {
                PendingAuthority item = _authority.Peek();

                if (item.Kind == PendingKind.Fence)
                {
                    S_RegionStateFence fence = item.Fence;
                    if (_snapshotCatchupActive)
                    {
                        if (fence.transferId != _snapshotCatchupTransferId ||
                            !fence.regionCoord.Equals(_snapshotCatchupRegion) ||
                            fence.snapshotTick != _snapshotCatchupTick)
                        {
                            break;
                        }

                        _snapshotCatchupActive = false;
                        _snapshotCatchupTransferId = 0;
                        _snapshotCatchupRegion = default;
                        _snapshotCatchupTick = 0;
                    }

                    _authority.Dequeue();
                    _pendingFences--;
                    continue;
                }

                if (item.Kind == PendingKind.Hash)
                {
                    S_RegionHash checkpoint = item.Hash;

                    if (_snapshotCatchupActive && checkpoint.serverTick > _snapshotCatchupTick)
                        break;

                    if (_snapshotCatchupActive &&
                        checkpoint.serverTick <= _snapshotCatchupTick &&
                        checkpoint.regionCoord.Equals(_snapshotCatchupRegion))
                    {
                        _authority.Dequeue();
                        _pendingHashes--;
                        continue;
                    }

                    if (!table.TryGetRegion(checkpoint.regionCoord, out Region region) || !region.BrickRefs.IsCreated)
                        break;

                    uint localHash = SemanticRegionHasher.HashRegion(in region, in pool);
                    _authority.Dequeue();
                    _pendingHashes--;
                    comparedHashes++;

                    if (localHash != checkpoint.mipHash)
                    {
                        _repairPending = true;
                        _repairRegion = checkpoint.regionCoord;
                        _repairTick = checkpoint.serverTick;
                        _repairHash = checkpoint.mipHash;

                        var mismatch = new C_RegionHashMismatch(
                            checkpoint.regionCoord,
                            checkpoint.serverTick,
                            localHash,
                            checkpoint.mipHash);
                        mismatchSink?.OnRegionHashMismatch(in mismatch);
                        break;
                    }

                    continue;
                }

                AlterationEvent[] events = item.Events;
                uint batchTick = events.Length > 0 ? events[0].tick : 0u;
                bool catchupBatch = _snapshotCatchupActive && batchTick <= _snapshotCatchupTick;

                if (_snapshotCatchupActive && batchTick > _snapshotCatchupTick)
                    break;

                if (!HasRequiredResidency(
                        _mutationStorage,
                        events,
                        catchupBatch,
                        _snapshotCatchupRegion))
                    break;

                for (int i = 0; i < events.Length; i++)
                {
                    AlterationEvent evt = events[i];
                    NativeList<int3> affectedBricks;
                    if (catchupBatch)
                    {
                        _applier.TryApplyExceptRegion(
                            _mutationStorage,
                            in evt,
                            _snapshotCatchupRegion,
                            out affectedBricks);
                    }
                    else
                    {
                        _applier.TryApply(
                            _mutationStorage,
                            in evt,
                            out affectedBricks);
                    }

                    if (affectedBricks.IsCreated) affectedBricks.Dispose();
                    appliedEvents++;
                }

                _authority.Dequeue();
                _pendingEvents -= events.Length;
                _pendingBatches--;
                appliedBatches++;
            }

            return appliedBatches;
        }

        public bool CompleteRepair(int3 regionCoord, uint snapshotTick, uint semanticHash)
        {
            if (_fullSnapshotWaitPending || !_repairPending || !_repairRegion.Equals(regionCoord) ||
                _repairTick != snapshotTick || _repairHash != semanticHash)
                return false;

            ClearRepair();
            return true;
        }

        public bool BeginFullRegionSnapshotWait(int3 regionCoord)
        {
            if (_snapshotCatchupActive)
                return false;
            if (_repairPending && !_repairRegion.Equals(regionCoord))
                return false;
            if (_fullSnapshotWaitPending)
                return _fullSnapshotWaitRegion.Equals(regionCoord);

            _fullSnapshotWaitPending = true;
            _fullSnapshotWaitRegion = regionCoord;
            return true;
        }

        public bool CompleteFullRegionSnapshot(
            uint transferId,
            int3 regionCoord,
            uint snapshotTick)
        {
            if (transferId == 0 || !_fullSnapshotWaitPending ||
                !_fullSnapshotWaitRegion.Equals(regionCoord))
                return false;

            if (_repairPending)
            {
                if (!_repairRegion.Equals(regionCoord) || snapshotTick < _repairTick)
                    return false;
                ClearRepair();
            }

            _fullSnapshotWaitPending = false;
            _fullSnapshotWaitRegion = default;
            _snapshotCatchupActive = true;
            _snapshotCatchupTransferId = transferId;
            _snapshotCatchupRegion = regionCoord;
            _snapshotCatchupTick = snapshotTick;
            return true;
        }

        public void ResetAfterAuthoritativeSnapshot()
        {
            _authority.Clear();
            _pendingEvents = 0;
            _pendingBatches = 0;
            _pendingHashes = 0;
            _pendingFences = 0;
            _hasLastReceivedEvent = false;
            _lastReceivedEvent = default;
            _lastBarrierTick = 0;
            ClearRepair();
            _fullSnapshotWaitPending = false;
            _fullSnapshotWaitRegion = default;
            _snapshotCatchupActive = false;
            _snapshotCatchupTransferId = 0;
            _snapshotCatchupRegion = default;
            _snapshotCatchupTick = 0;
        }

        private void ClearRepair()
        {
            _repairPending = false;
            _repairRegion = default;
            _repairTick = 0;
            _repairHash = 0;
        }

        private bool HasRequiredResidency(
            IRegionMutationStore storage,
            AlterationEvent[] events,
            bool excludeRegion,
            int3 excludedRegion)
        {
            for (int i = 0; i < events.Length; i++)
            {
                AlterationEvent evt = events[i];
                bool resident = excludeRegion
                    ? _applier.HasRequiredResidencyExcept(storage, in evt, excludedRegion)
                    : _applier.HasRequiredResidency(storage, in evt);
                if (!resident) return false;
            }
            return true;
        }

        private static int CompareAuthority(in AlterationEvent a, in AlterationEvent b)
        {
            int tick = a.tick.CompareTo(b.tick);
            if (tick != 0) return tick;
            int player = a.playerId.CompareTo(b.playerId);
            return player != 0 ? player : a.sequence.CompareTo(b.sequence);
        }

        private enum PendingKind : byte { Batch = 0, Hash = 1, Fence = 2 }

        private readonly struct PendingAuthority
        {
            public readonly PendingKind Kind;
            public readonly AlterationEvent[] Events;
            public readonly S_RegionHash Hash;
            public readonly S_RegionStateFence Fence;

            private PendingAuthority(
                PendingKind kind,
                AlterationEvent[] events,
                S_RegionHash hash,
                S_RegionStateFence fence)
            {
                Kind = kind;
                Events = events;
                Hash = hash;
                Fence = fence;
            }

            public static PendingAuthority ForBatch(AlterationEvent[] events) =>
                new PendingAuthority(PendingKind.Batch, events, default, default);
            public static PendingAuthority ForHash(S_RegionHash hash) =>
                new PendingAuthority(PendingKind.Hash, null, hash, default);
            public static PendingAuthority ForFence(S_RegionStateFence fence) =>
                new PendingAuthority(PendingKind.Fence, null, default, fence);
        }
    }
}
