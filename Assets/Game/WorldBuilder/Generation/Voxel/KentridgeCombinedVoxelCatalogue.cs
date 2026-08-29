using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    public static class KentridgeCombinedVoxelCatalogue
    {
        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            FeatureCatalogue local = KentridgeCombinedVoxelCatalogueCanonical.Build(
                seed, settings, allocator);
            Int2 origin = settings.Settlement != null
                ? settings.Settlement.CentreDm
                : KentridgeDefinition.TownCentreDm;
            return AddMacroWorld(local, seed, settings, origin, allocator);
        }

        /// <summary>
        /// Convenience path for callers that still have semantic requests. Architecture realization is
        /// deterministic, but higher-level campaign composition should prefer the geometry overload so
        /// the exact hidden spaces used for gameplay selection are also the ones emitted as voxels.
        /// </summary>
        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            IReadOnlyList<SiteHiddenSpaceRequest> hiddenSpaces,
            Allocator allocator)
        {
            FeatureCatalogue local = KentridgeCombinedVoxelCatalogueCanonical.BuildWithHiddenSpaces(
                seed,
                settings,
                hiddenSpaces,
                allocator);
            Int2 origin = settings.Settlement != null
                ? settings.Settlement.CentreDm
                : KentridgeDefinition.TownCentreDm;
            return AddMacroWorld(local, seed, settings, origin, allocator);
        }

        /// <summary>
        /// Emits the exact architecture-realized hidden spaces selected during campaign planning.
        /// The concrete SettlementPlan is required so geometry cannot accidentally be emitted against a
        /// different seed/layout. The shared macro layout is composed at this backend boundary too, so
        /// every full Kentridge world catalogue carries the same source-backed surrounding world.
        /// </summary>
        public static FeatureCatalogue Build(
            SettlementPlan plan,
            VoxelWorldGenSettings settings,
            IReadOnlyList<KentridgeHiddenSpaceGeometry> hiddenSpaces,
            Allocator allocator)
        {
            FeatureCatalogue local = KentridgeCombinedVoxelCatalogueCanonical.BuildWithHiddenSpaceGeometry(
                plan,
                settings,
                hiddenSpaces,
                allocator);
            return AddMacroWorld(local, plan.Seed, settings, plan.CentreDm, allocator);
        }

        private static FeatureCatalogue AddMacroWorld(
            FeatureCatalogue local,
            uint seed,
            VoxelWorldGenSettings settings,
            Int2 rootCentreDm,
            Allocator allocator)
        {
            FeatureCatalogue macro = default;
            try
            {
                TopDownWorldLayout layout = KentridgeTopDownWorldLayout.Build(seed);
                macro = TopDownWorldVoxelCatalogue.Build(
                    layout,
                    rootCentreDm,
                    KentridgeTopDownWorldLayout.CellSizeDm,
                    settings,
                    allocator);
                return SettlementCatalogueCombiner.Combine(allocator, local, macro);
            }
            finally
            {
                if (local.IsCreated) local.Dispose();
                if (macro.IsCreated) macro.Dispose();
            }
        }
    }
}
