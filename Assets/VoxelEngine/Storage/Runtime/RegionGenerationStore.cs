using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>Current Core implementation of the bulk region-generation write boundary.</summary>
    public sealed class RegionGenerationStore : IRegionGenerationStore
    {
        private RegionTable _table;

        public RegionGenerationStore(in RegionTable table) => _table = table;

        public void Refresh(in RegionTable table) => _table = table;

        public RegionGenerationWriteView AcquireRegion(int3 regionCoord)
        {
            Region region = _table.LoadRegion(regionCoord);
            return new RegionGenerationWriteView(
                region.Coord,
                region.BrickRefs.Reinterpret<int>(),
                region.OccupiedBlockWords,
                region.FullySolidBlockWords);
        }
    }
}
