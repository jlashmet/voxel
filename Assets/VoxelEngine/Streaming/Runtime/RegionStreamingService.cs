using System;
using System.Threading;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Streaming.Api;

namespace VoxelEngine.Streaming.Runtime
{
    /// <summary>Runtime Streaming implementation that hides Storage residency behind Streaming.Api.</summary>
    public sealed class RegionStreamingService : IRegionStreaming, IRegionResidencyPins
    {
        private readonly IRegionResidencyStore _storage;

        public RegionStreamingService(IRegionResidencyStore storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void QueueLoad(in RegionLoadRequest request)
        {
            RegionLoader.QueueLoad(request.RegionCoord, request.TerrainSeed, request.RequestedMipLevel);
        }

        public int PublishLoaded(float mainThreadBudgetMs)
        {
            return RegionLoader.PublishLoaded(_storage, mainThreadBudgetMs);
        }

        public bool IsResident(int3 regionCoord) => _storage.IsRegionResident(regionCoord);

        public bool Evict(int3 regionCoord) =>
            !RegionPinRegistry.IsPinned(regionCoord) && _storage.EvictRegion(regionCoord);

        public IRegionResidencyLease AcquireResidency(in RegionLoadRequest request)
        {
            RegionPinRegistry.Acquire(request.RegionCoord);
            try
            {
                if (!IsResident(request.RegionCoord))
                    QueueLoad(request);
                return new Lease(this, request.RegionCoord);
            }
            catch
            {
                RegionPinRegistry.Release(request.RegionCoord);
                throw;
            }
        }

        private sealed class Lease : IRegionResidencyLease
        {
            private RegionStreamingService _owner;

            public Lease(RegionStreamingService owner, int3 regionCoord)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                RegionCoord = regionCoord;
            }

            public int3 RegionCoord { get; }
            public bool IsReady => _owner != null && _owner.IsResident(RegionCoord);

            public void Dispose()
            {
                RegionStreamingService owner = Interlocked.Exchange(ref _owner, null);
                if (owner != null)
                    RegionPinRegistry.Release(RegionCoord);
            }
        }
    }
}
