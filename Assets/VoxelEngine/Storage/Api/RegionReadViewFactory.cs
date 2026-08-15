using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Construction boundary for Storage providers that publish borrowed zero-copy region views.
    /// Consumers receive only <see cref="RegionReadView"/> and cannot access the backing arrays.
    /// The descriptor encoding is owned by Storage.Api; no BrickRef/Region/pool owner type crosses
    /// the subsystem boundary.
    /// </summary>
    public static class RegionReadViewFactory
    {
        public static RegionReadView CreateBorrowed(
            int3 regionCoord,
            ulong version,
            NativeArray<int> blockDescriptors,
            NativeArray<ulong> hardSurfaceWords,
            NativeArray<ulong> occupancyMips,
            NativeArray<byte> materialMips,
            int mipLevelCount,
            NativeArray<byte> mixedVoxels,
            NativeArray<ushort> mixedSurfaceSemantics,
            NativeArray<byte> mixedBoundarySamples,
            NativeArray<ulong> mixedOccupancy)
        {
            return new RegionReadView(
                regionCoord,
                version,
                blockDescriptors,
                hardSurfaceWords,
                occupancyMips,
                materialMips,
                mipLevelCount,
                mixedVoxels,
                mixedSurfaceSemantics,
                mixedBoundarySamples,
                mixedOccupancy);
        }
    }
}
