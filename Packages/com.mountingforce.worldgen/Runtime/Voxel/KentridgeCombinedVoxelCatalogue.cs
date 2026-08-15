using Unity.Collections;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    public static class KentridgeCombinedVoxelCatalogue
    {
        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator) =>
            KentridgeCombinedVoxelCatalogueCanonical.Build(seed, settings, allocator);
    }
}
