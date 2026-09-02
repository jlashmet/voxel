using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Amortizes water/cascade classification for newly discovered surface bricks without delaying
    /// authoritative water mutations. Solid discovery can publish a large batch at once, but water
    /// classification re-reads material data for every brick; draining a small FIFO keeps that
    /// secondary presentation scan from becoming a main-thread hitch.
    /// </summary>
    internal sealed class WaterSurfaceDiscoveryAdmission
    {
        internal const int SurfaceDiscoveryBricksPerPrepare = 32;
        internal const int DeadlineCheckStride = 4;

        private readonly Queue<int3> _pending = new(SurfaceDiscoveryBricksPerPrepare * 4);
        private readonly HashSet<int3> _queued = new();

        public int PendingCount => _pending.Count;

        public void EnqueueAndStep(CpuWaterSurfaceChunkCache water,
                                   IRegionReadSource storage,
                                   IReadOnlyList<int3> discoveredSurfaceBricks,
                                   double deadlineSeconds)
        {
            if (water == null || storage == null) return;

            if (discoveredSurfaceBricks != null)
            {
                for (int i = 0; i < discoveredSurfaceBricks.Count; i++)
                {
                    int3 worldBrick = discoveredSurfaceBricks[i];
                    if (_queued.Add(worldBrick))
                        _pending.Enqueue(worldBrick);
                }
            }

            int processed = 0;
            RegionReadView cachedRegion = default;
            while (processed < SurfaceDiscoveryBricksPerPrepare && _pending.Count > 0)
            {
                // A four-brick progress floor prevents a sub-millisecond budget from starving
                // water discovery on coarse timers. Beyond that floor, every individual block is
                // a deadline boundary; the former 32-block synchronous batch produced 29 ms
                // presentation hitches while the solid GPU path itself stayed below 3 ms.
                if (processed >= DeadlineCheckStride
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                    break;
                int3 worldBrick = _pending.Dequeue();
                _queued.Remove(worldBrick);
                water.InvalidateSurfaceBrick(storage, worldBrick, ref cachedRegion);
                processed++;
            }
        }
    }
}
