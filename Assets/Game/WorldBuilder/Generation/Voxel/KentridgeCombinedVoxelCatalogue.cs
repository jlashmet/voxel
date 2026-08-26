using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using Unity.Collections;
using VoxelEngine.Structures.Api;

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
        /// Convenience path for callers that still have semantic requests. Architecture realization is
        /// deterministic, but higher-level campaign composition should prefer the geometry overload so
        /// the exact hidden spaces used for gameplay selection are also the ones emitted as voxels.
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

        /// <summary>
        /// Emits the exact architecture-realized hidden spaces selected during campaign planning.
        /// The concrete SettlementPlan is required so geometry cannot accidentally be emitted against a
        /// different seed/layout. No WorldBuilder types cross this boundary.
        /// </summary>
        public static FeatureCatalogue Build(
            SettlementPlan plan,
            VoxelWorldGenSettings settings,
            IReadOnlyList<KentridgeHiddenSpaceGeometry> hiddenSpaces,
            Allocator allocator) =>
            KentridgeCombinedVoxelCatalogueCanonical.BuildWithHiddenSpaceGeometry(
                plan,
                settings,
                hiddenSpaces,
                allocator);
    }
}
