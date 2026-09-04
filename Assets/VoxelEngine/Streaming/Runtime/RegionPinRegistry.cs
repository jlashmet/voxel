using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Streaming.Runtime
{
    /// <summary>
    /// Process-local ownership registry shared by the public streaming service and eviction policy.
    /// It contains no world state: it only protects Storage-owned resident regions from eviction
    /// while one or more API leases are alive.
    /// </summary>
    internal static class RegionPinRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<int3, int> Counts = new Dictionary<int3, int>();

        public static void Acquire(int3 regionCoord)
        {
            lock (Gate)
            {
                Counts.TryGetValue(regionCoord, out int count);
                Counts[regionCoord] = checked(count + 1);
            }
        }

        public static void Release(int3 regionCoord)
        {
            lock (Gate)
            {
                if (!Counts.TryGetValue(regionCoord, out int count))
                    return;

                if (count <= 1)
                    Counts.Remove(regionCoord);
                else
                    Counts[regionCoord] = count - 1;
            }
        }

        public static bool IsPinned(int3 regionCoord)
        {
            lock (Gate)
                return Counts.TryGetValue(regionCoord, out int count) && count > 0;
        }
    }
}
