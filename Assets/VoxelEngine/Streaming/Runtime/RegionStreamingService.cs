using System;
using VoxelEngine.Storage.Api;
using VoxelEngine.Streaming.Api;

namespace VoxelEngine.Streaming.Runtime
{
    /// <summary>Runtime Streaming implementation that hides Storage residency behind Streaming.Api.</summary>
    public sealed class RegionStreamingService : IRegionStreaming
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
    }
}
