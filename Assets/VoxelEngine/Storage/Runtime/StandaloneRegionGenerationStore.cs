using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Test/offline support for parity harnesses that intentionally own one standalone Region
    /// buffer rather than a RegionTable. Terrain still sees only IRegionGenerationStore; this
    /// adapter stays with the physical Storage implementation and moves with it to Storage.Runtime.
    /// </summary>
    public sealed class StandaloneRegionGenerationStore : IRegionGenerationStore
    {
        private readonly int3 _regionCoord;
        private RegionGenerationWriteView _view;

        public StandaloneRegionGenerationStore(in Region region)
        {
            _regionCoord = region.Coord;
            _view = new RegionGenerationWriteView(
                region.Coord,
                region.BrickRefs.Reinterpret<int>(),
                region.OccupiedBlockWords,
                region.FullySolidBlockWords);
        }

        public RegionGenerationWriteView AcquireRegion(int3 regionCoord)
        {
            if (!regionCoord.Equals(_regionCoord))
                throw new ArgumentOutOfRangeException(
                    nameof(regionCoord), regionCoord,
                    $"Standalone generation store owns only region {_regionCoord}.");
            return _view;
        }
    }
}
