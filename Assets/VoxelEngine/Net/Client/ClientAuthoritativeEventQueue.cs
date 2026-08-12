using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    public interface IRegionHashMismatchSink
    {
        void OnRegionHashMismatch(in C_RegionHashMismatch mismatch);
    }

    /// <summary>
    /// Ordered client-side queue for authoritative EVENT packets. Mutation batches and region hash
    /// checkpoints share one FIFO. A verified local mismatch pauses the queue exactly after the hash
    /// barrier; later authority remains buffered until a semantic snapshot for that checkpoint is
    /// applied, preventing duplicate application of post-checkpoint events.
    /// </summary>
    public sealed class ClientAuthoritativeEventQueue
    {
        public const int DefaultMaxPendingEvents = 4096;

        private readonly Queue<PendingAuthority> _authority = new Queue<PendingAuthority>(64);
        private readonly int _maxPendingEvents;
        private int _pendingEvents;
        private int _pendingBatches;
        private int _pendingHashes;
        private bool _hasLastReceivedEvent;
        private AlterationEvent _lastReceivedEvent;
        private uint _lastBarrierTick;
        private bool _repairPending;
        private int3 _repairRegion;
        private uint _repairTick;
        private uint _repairHash;

        public ClientAuthoritativeEventQueue(int maxPendingEvents = DefaultMaxPendingEvents)
        {
            if (maxPendingEvents <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPendingEvents));
            _maxPendingEvents = maxPendingEvents;
        }

        public int PendingBatchCount => _pendingBatches;
        public int PendingEventCount => _pendingEvents;
        public int PendingHashCount => _pendingHashes;
        public int PendingAuthorityCount => _authority.Count;
        public bool RepairPending => _repairPending;
        public int3 RepairRegion => _repairRegion;
        public uint RepairTick => _repairTick;
        public uint RepairHash => _repairHash;

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
                    !DeterministicAlterationApplier.Supports(in evt) || evt.tick < _lastBarrierTick ||
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

            if (_repairPending)
                return 0;

            while (_authority.Count > 0)
            {
                PendingAuthority item = _authority.Peek();
                if (item.Kind == PendingKind.Hash)
                {
                    S_RegionHash checkpoint = item.Hash;
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
                if (!HasRequiredResidency(ref table, events))
                    break;

                for (int i = 0; i < events.Length; i++)
                {
                    AlterationEvent evt = events[i];
                    DeterministicAlterationApplier.TryApply(
                        ref table,
                        ref pool,
                        in evt,
                        out var affectedBricks);
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

        /// <summary>Resume only when repair metadata exactly matches the paused hash barrier.</summary>
        public bool CompleteRepair(int3 regionCoord, uint snapshotTick, uint semanticHash)
        {
            if (!_repairPending || !_repairRegion.Equals(regionCoord) ||
                _repairTick != snapshotTick || _repairHash != semanticHash)
                return false;

            _repairPending = false;
            _repairRegion = default;
            _repairTick = 0;
            _repairHash = 0;
            return true;
        }

        public void ResetAfterAuthoritativeSnapshot()
        {
            _authority.Clear();
            _pendingEvents = 0;
            _pendingBatches = 0;
            _pendingHashes = 0;
            _hasLastReceivedEvent = false;
            _lastReceivedEvent = default;
            _lastBarrierTick = 0;
            _repairPending = false;
            _repairRegion = default;
            _repairTick = 0;
            _repairHash = 0;
        }

        private static bool HasRequiredResidency(ref RegionTable table, AlterationEvent[] events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                AlterationEvent evt = events[i];
                if (!DeterministicAlterationApplier.HasRequiredResidency(ref table, in evt)) return false;
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

        private enum PendingKind : byte { Batch = 0, Hash = 1 }

        private readonly struct PendingAuthority
        {
            public readonly PendingKind Kind;
            public readonly AlterationEvent[] Events;
            public readonly S_RegionHash Hash;

            private PendingAuthority(PendingKind kind, AlterationEvent[] events, S_RegionHash hash)
            {
                Kind = kind;
                Events = events;
                Hash = hash;
            }

            public static PendingAuthority ForBatch(AlterationEvent[] events) =>
                new PendingAuthority(PendingKind.Batch, events, default);
            public static PendingAuthority ForHash(S_RegionHash hash) =>
                new PendingAuthority(PendingKind.Hash, null, hash);
        }
    }
}
