using Unity.Mathematics;

namespace VoxelEngine.Streaming.Api
{
    /// <summary>Stable logical request for asynchronous region residency/loading.</summary>
    public readonly struct RegionLoadRequest
    {
        public readonly int3 RegionCoord;
        public readonly uint TerrainSeed;
        public readonly byte RequestedMipLevel;

        public RegionLoadRequest(int3 regionCoord, uint terrainSeed, byte requestedMipLevel = 0)
        {
            RegionCoord = regionCoord;
            TerrainSeed = terrainSeed;
            RequestedMipLevel = requestedMipLevel;
        }
    }
}
