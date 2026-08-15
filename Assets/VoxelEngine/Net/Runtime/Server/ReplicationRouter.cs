using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Runtime.Interest;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Net.Runtime.Server
{
    public interface IAlterationReplicationSink
    {
        void SendBatch(uint connectionId, int3 encodingRegion, uint tick, ReadOnlySpan<AlterationEvent> events);
    }

    /// <summary>
    /// Interest-routes sealed authoritative events without changing their global order.
    /// Cross-region fan-out uses the exact canonical effect bounds whenever the shape is known.
    /// </summary>
    public sealed class ReplicationRouter
    {
        private readonly RegionSubscriptionIndex _subscriptions;
        private readonly Dictionary<uint, List<RoutedAlteration>> _routesByConnection =
            new Dictionary<uint, List<RoutedAlteration>>();
        private readonly Stack<List<RoutedAlteration>> _routeListPool = new Stack<List<RoutedAlteration>>();
        private readonly HashSet<int3> _impactedRegions = new HashSet<int3>();
        private readonly HashSet<uint> _recipientScratch = new HashSet<uint>();

        public ReplicationRouter(RegionSubscriptionIndex subscriptions)
        {
            _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));
        }

        public void RouteTick(IReadOnlyList<AlterationEvent> events, IAlterationReplicationSink sink)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            ResetRoutes();

            for (int i = 0; i < events.Count; i++)
            {
                AlterationEvent evt = events[i];
                int3 encodingRegion = SimulationInterest.WorldVoxelToRegion(evt.origin);

                CollectImpactedRegions(in evt, _impactedRegions);
                _recipientScratch.Clear();
                foreach (int3 impactedRegion in _impactedRegions)
                    _subscriptions.AddSubscribers(impactedRegion, _recipientScratch);

                foreach (uint connectionId in _recipientScratch)
                    GetRoute(connectionId).Add(new RoutedAlteration(encodingRegion, evt));
            }

            Span<AlterationEvent> batchScratch = stackalloc AlterationEvent[S_AlterationEventBatch.MaxEventsPerBatch];

            foreach (var pair in _routesByConnection)
            {
                uint connectionId = pair.Key;
                List<RoutedAlteration> route = pair.Value;
                int index = 0;

                while (index < route.Count)
                {
                    RoutedAlteration first = route[index];
                    int count = 0;

                    while (index + count < route.Count && count < S_AlterationEventBatch.MaxEventsPerBatch)
                    {
                        RoutedAlteration next = route[index + count];
                        if (next.Event.tick != first.Event.tick || !math.all(next.EncodingRegion == first.EncodingRegion))
                            break;

                        batchScratch[count] = next.Event;
                        count++;
                    }

                    sink.SendBatch(
                        connectionId,
                        first.EncodingRegion,
                        first.Event.tick,
                        batchScratch.Slice(0, count));
                    index += count;
                }
            }
        }

        private List<RoutedAlteration> GetRoute(uint connectionId)
        {
            if (_routesByConnection.TryGetValue(connectionId, out var route))
                return route;

            route = _routeListPool.Count > 0 ? _routeListPool.Pop() : new List<RoutedAlteration>(32);
            _routesByConnection.Add(connectionId, route);
            return route;
        }

        private void ResetRoutes()
        {
            foreach (var pair in _routesByConnection)
            {
                pair.Value.Clear();
                _routeListPool.Push(pair.Value);
            }
            _routesByConnection.Clear();
        }

        private static void CollectImpactedRegions(in AlterationEvent evt, HashSet<int3> destination)
        {
            destination.Clear();

            int3 minVoxel;
            int3 maxVoxel;
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                {
                    int radiusVoxels = evt.Radius() * VoxelReadGrid.BlockEdge;
                    int3 padding = new int3(radiusVoxels);
                    minVoxel = evt.origin - padding;
                    maxVoxel = evt.origin + padding;
                    break;
                }

                case AlterationEvent.KindBrush when BrushShapeCodec.Validate(evt.shapeKind, evt.shapeData):
                    BrushShapeCodec.GetCubeVoxelBounds(
                        evt.origin,
                        evt.BrushExtents(),
                        out minVoxel,
                        out maxVoxel);
                    break;

                case AlterationEvent.KindRawBatch:
                default:
                {
                    int3 padding = new int3(1 << VoxelGrid.RegionVoxelEdgeLog2);
                    minVoxel = evt.origin - padding;
                    maxVoxel = evt.origin + padding;
                    break;
                }
            }

            int3 minRegion = SimulationInterest.WorldVoxelToRegion(minVoxel);
            int3 maxRegion = SimulationInterest.WorldVoxelToRegion(maxVoxel);

            for (int x = minRegion.x; x <= maxRegion.x; x++)
            for (int y = minRegion.y; y <= maxRegion.y; y++)
            for (int z = minRegion.z; z <= maxRegion.z; z++)
                destination.Add(new int3(x, y, z));
        }

        private readonly struct RoutedAlteration
        {
            public readonly int3 EncodingRegion;
            public readonly AlterationEvent Event;

            public RoutedAlteration(int3 encodingRegion, AlterationEvent evt)
            {
                EncodingRegion = encodingRegion;
                Event = evt;
            }
        }
    }
}
