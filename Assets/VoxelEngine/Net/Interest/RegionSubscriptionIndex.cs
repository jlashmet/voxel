using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Net.Interest
{
    /// <summary>
    /// Persistent bidirectional subscription index for simulation regions.
    ///
    /// Forward mapping makes connection cleanup cheap; inverse mapping makes event fan-out
    /// proportional to interested clients instead of all clients. UpdateForPosition applies
    /// common load/unload hysteresis and deliberately has no device-tier parameter.
    /// </summary>
    public sealed class RegionSubscriptionIndex
    {
        private readonly Dictionary<uint, HashSet<int3>> _regionsByConnection = new Dictionary<uint, HashSet<int3>>();
        private readonly Dictionary<int3, HashSet<uint>> _connectionsByRegion = new Dictionary<int3, HashSet<uint>>();
        private readonly List<int3> _loadScratch = new List<int3>(1024);
        private readonly List<int3> _removeScratch = new List<int3>(128);

        /// <summary>Refresh one connection's simulation subscriptions from its authoritative position.</summary>
        public int UpdateForPosition(uint connectionId, int3 playerVoxelPosition)
        {
            if (!_regionsByConnection.TryGetValue(connectionId, out var current))
            {
                current = new HashSet<int3>();
                _regionsByConnection.Add(connectionId, current);
            }

            SimulationInterest.CollectLoadRegions(playerVoxelPosition, _loadScratch);
            for (int i = 0; i < _loadScratch.Count; i++)
            {
                int3 region = _loadScratch[i];
                if (current.Add(region))
                    AddInverse(connectionId, region);
            }

            _removeScratch.Clear();
            foreach (int3 region in current)
            {
                if (!SimulationInterest.IsWithinUnloadRadius(playerVoxelPosition, region))
                    _removeScratch.Add(region);
            }

            for (int i = 0; i < _removeScratch.Count; i++)
            {
                int3 region = _removeScratch[i];
                current.Remove(region);
                RemoveInverse(connectionId, region);
            }

            return current.Count;
        }

        /// <summary>
        /// Replace a connection's subscriptions explicitly. Used for join/bootstrap and tests;
        /// normal live movement should use UpdateForPosition so hysteresis is preserved.
        /// </summary>
        public void SetSubscriptions(uint connectionId, ReadOnlySpan<int3> regions)
        {
            RemoveConnection(connectionId);

            var current = new HashSet<int3>();
            _regionsByConnection.Add(connectionId, current);
            for (int i = 0; i < regions.Length; i++)
            {
                int3 region = regions[i];
                if (current.Add(region))
                    AddInverse(connectionId, region);
            }
        }

        /// <summary>Add all subscribers for a region into destination without clearing it.</summary>
        public void AddSubscribers(int3 regionCoord, HashSet<uint> destination)
        {
            if (!_connectionsByRegion.TryGetValue(regionCoord, out var subscribers))
                return;

            foreach (uint connectionId in subscribers)
                destination.Add(connectionId);
        }

        public bool IsSubscribed(uint connectionId, int3 regionCoord)
        {
            return _regionsByConnection.TryGetValue(connectionId, out var regions) && regions.Contains(regionCoord);
        }

        public int CountForConnection(uint connectionId)
        {
            return _regionsByConnection.TryGetValue(connectionId, out var regions) ? regions.Count : 0;
        }

        public int SubscriberCount(int3 regionCoord)
        {
            return _connectionsByRegion.TryGetValue(regionCoord, out var subscribers) ? subscribers.Count : 0;
        }

        public void RemoveConnection(uint connectionId)
        {
            if (!_regionsByConnection.TryGetValue(connectionId, out var regions))
                return;

            foreach (int3 region in regions)
                RemoveInverse(connectionId, region);

            _regionsByConnection.Remove(connectionId);
        }

        private void AddInverse(uint connectionId, int3 regionCoord)
        {
            if (!_connectionsByRegion.TryGetValue(regionCoord, out var subscribers))
            {
                subscribers = new HashSet<uint>();
                _connectionsByRegion.Add(regionCoord, subscribers);
            }

            subscribers.Add(connectionId);
        }

        private void RemoveInverse(uint connectionId, int3 regionCoord)
        {
            if (!_connectionsByRegion.TryGetValue(regionCoord, out var subscribers))
                return;

            subscribers.Remove(connectionId);
            if (subscribers.Count == 0)
                _connectionsByRegion.Remove(regionCoord);
        }
    }
}
