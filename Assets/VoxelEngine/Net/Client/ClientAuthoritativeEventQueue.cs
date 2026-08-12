using System;
using System.Collections.Generic;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Ordered client-side queue for authoritative EVENT packets.
    ///
    /// A batch at the head remains pending until every region its semantic events may touch is
    /// resident. Later events never leapfrog it, preserving server arbitration order across region
    /// streaming. Rejections are notifications and do not enter the world-mutation queue.
    /// </summary>
    public sealed class ClientAuthoritativeEventQueue
    {
        public const int DefaultMaxPendingEvents = 4096;

        private readonly Queue<PendingBatch> _batches = new Queue<PendingBatch>(64);
        private readonly int _maxPendingEvents;
        private int _pendingEvents;
        private bool _hasLastReceived;
        private AlterationEvent _lastReceived;

        public ClientAuthoritativeEventQueue(int maxPendingEvents = DefaultMaxPendingEvents)
        {
            if (maxPendingEvents <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPendingEvents));
            _maxPendingEvents = maxPendingEvents;
        }

        public int PendingBatchCount => _batches.Count;
        public int PendingEventCount => _pendingEvents;

        public bool TryEnqueueEventPacket(
            ReadOnlySpan<byte> packet,
            IClientEventNotificationSink notifications = null)
        {
            if (!ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset))
                return false;

            if (kind == ProtocolMessageKind.S_AlterationRejected)
            {
                if (!AlterationRejectedPacket.TryDecode(packet, out S_AlterationRejected rejection))
                    return false;

                notifications?.OnAlterationRejected(in rejection);
                return true;
            }

            if (kind != ProtocolMessageKind.S_AlterationEventBatch)
                return false;

            ReadOnlySpan<byte> payload = packet.Slice(payloadOffset);
            if (!S_AlterationEventBatch.TryDecodeHeader(payload, out var header) ||
                header.count <= 0 ||
                _pendingEvents + header.count > _maxPendingEvents)
            {
                return false;
            }

            var decoded = new AlterationEvent[header.count];
            AlterationEvent prior = _lastReceived;
            bool hasPrior = _hasLastReceived;

            for (int i = 0; i < header.count; i++)
            {
                if (!S_AlterationEventBatch.TryDecodeEvent(payload, in header, i, out AlterationEvent evt) ||
                    !DeterministicAlterationApplier.Supports(in evt) ||
                    (hasPrior && CompareAuthority(in evt, in prior) < 0))
                {
                    return false;
                }

                decoded[i] = evt;
                prior = evt;
                hasPrior = true;
            }

            _batches.Enqueue(new PendingBatch(decoded));
            _pendingEvents += decoded.Length;
            _lastReceived = prior;
            _hasLastReceived = hasPrior;
            return true;
        }

        /// <summary>
        /// Apply as many leading batches as current region residency permits. A missing region in
        /// the first batch stops the drain without consuming that batch or any later authority.
        /// </summary>
        public int DrainReady(ref RegionTable table, ref BrickPool pool, out int appliedEvents)
        {
            appliedEvents = 0;
            int appliedBatches = 0;

            while (_batches.Count > 0)
            {
                PendingBatch batch = _batches.Peek();
                if (!HasRequiredResidency(ref table, batch.Events))
                    break;

                for (int i = 0; i < batch.Events.Length; i++)
                {
                    AlterationEvent evt = batch.Events[i];
                    DeterministicAlterationApplier.TryApply(
                        ref table,
                        ref pool,
                        in evt,
                        out var affectedBricks);

                    if (affectedBricks.IsCreated)
                        affectedBricks.Dispose();
                    appliedEvents++;
                }

                _batches.Dequeue();
                _pendingEvents -= batch.Events.Length;
                appliedBatches++;
            }

            return appliedBatches;
        }

        /// <summary>
        /// Clear pending authority and ordering history after a state-based reconnect/full snapshot.
        /// Do not call during ordinary region streaming; deferred events must survive that.
        /// </summary>
        public void ResetAfterAuthoritativeSnapshot()
        {
            _batches.Clear();
            _pendingEvents = 0;
            _hasLastReceived = false;
            _lastReceived = default;
        }

        private static bool HasRequiredResidency(ref RegionTable table, AlterationEvent[] events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                AlterationEvent evt = events[i];
                if (!DeterministicAlterationApplier.HasRequiredResidency(ref table, in evt))
                    return false;
            }

            return true;
        }

        private static int CompareAuthority(in AlterationEvent a, in AlterationEvent b)
        {
            int tick = a.tick.CompareTo(b.tick);
            if (tick != 0) return tick;

            int player = a.playerId.CompareTo(b.playerId);
            if (player != 0) return player;

            return a.sequence.CompareTo(b.sequence);
        }

        private readonly struct PendingBatch
        {
            public readonly AlterationEvent[] Events;
            public PendingBatch(AlterationEvent[] events) => Events = events;
        }
    }
}
