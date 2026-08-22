using System.Collections.Generic;
using Unity.Mathematics;
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

        private readonly Queue<int3> _pending = new(SurfaceDiscoveryBricksPerPrepare * 4);
        private readonly HashSet<int3> _queued = new();
        private readonly List<int3> _batch = new(SurfaceDiscoveryBricksPerPrepare);

        public int PendingCount => _pending.Count;

        public void EnqueueAndStep(CpuWaterSurfaceChunkCache water,
                                   IRegionReadSource storage,
                                   IReadOnlyList<int3> discoveredSurfaceBricks)
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

            _batch.Clear();
            while (_batch.Count < SurfaceDiscoveryBricksPerPrepare && _pending.Count > 0)
            {
                int3 worldBrick = _pending.Dequeue();
                _queued.Remove(worldBrick);
                _batch.Add(worldBrick);
            }

            if (_batch.Count > 0)
                water.InvalidateSurfaceBricks(storage, _batch);
        }
    }
}
