using System.Collections.Generic;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    public static class KentridgeCombinedVoxelCatalogue
    {
        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator) =>
            KentridgeCombinedVoxelCatalogueCanonical.Build(seed, settings, allocator);

        /// <summary>
        /// Opt-in generation path used after a higher-level site solver has requested physical hidden
        /// spaces. Existing callers remain unchanged and generate the exact legacy combined catalogue.
        /// </summary>
        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            IReadOnlyList<SiteHiddenSpaceRequest> hiddenSpaces,
            Allocator allocator) =>
            KentridgeCombinedVoxelCatalogueCanonical.BuildWithHiddenSpaces(
                seed,
                settings,
                hiddenSpaces,
                allocator);
    }
}
