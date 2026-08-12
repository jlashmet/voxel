using System;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Interest;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Small orchestration facade intended to be owned by the authoritative server loop.
    /// The fixed tick remains the clock; gameplay publishes events during the tick and networking
    /// flushes the sealed stream once at the end of the tick.
    /// </summary>
    public sealed class EventDrivenReplicationPipeline
    {
        private readonly AuthoritativeEventStream _events;
        private readonly RegionSubscriptionIndex _subscriptions;
        private readonly ReplicationRouter _router;

        public EventDrivenReplicationPipeline(int initialEventCapacity = 64)
        {
            _events = new AuthoritativeEventStream(initialEventCapacity);
            _subscriptions = new RegionSubscriptionIndex();
            _router = new ReplicationRouter(_subscriptions);
        }

        public uint CurrentTick => _events.Tick;
        public int EventCount => _events.Count;
        public RegionSubscriptionIndex Subscriptions => _subscriptions;

        public void BeginTick(uint tick) => _events.BeginTick(tick);

        public void PublishAlteration(in AlterationEvent evt) => _events.Publish(in evt);

        public int UpdateConnectionPosition(uint connectionId, int3 playerVoxelPosition) =>
            _subscriptions.UpdateForPosition(connectionId, playerVoxelPosition);

        public void SetSubscriptions(uint connectionId, ReadOnlySpan<int3> regions) =>
            _subscriptions.SetSubscriptions(connectionId, regions);

        public void RemoveConnection(uint connectionId) => _subscriptions.RemoveConnection(connectionId);

        /// <summary>Seal the current tick and emit ordered, interest-filtered batches.</summary>
        public void Flush(IAlterationReplicationSink sink)
        {
            _router.RouteTick(_events.SealTick(), sink);
        }
    }
}
