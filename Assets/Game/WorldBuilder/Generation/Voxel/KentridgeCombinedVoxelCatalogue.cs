using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
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
            Allocator allocator)
        {
            FeatureCatalogue local = KentridgeCombinedVoxelCatalogueCanonical.Build(
                seed, settings, allocator);
            return AddSelectedMacroWorld(local, seed, settings, allocator);
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
            return AddSelectedMacroWorld(local, seed, settings, allocator);
        }

        /// <summary>
        /// Emits the exact architecture-realized hidden spaces selected during campaign planning.
        /// The concrete SettlementPlan is required so geometry cannot accidentally be emitted against a
        /// different seed/layout. A macro world is composed only when the game/scene WorldBuilder path
        /// explicitly selected one for this seed; ordinary Kentridge catalogues retain their old cost.
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
            return AddSelectedMacroWorld(local, plan.Seed, settings, allocator);
        }

        private static FeatureCatalogue AddSelectedMacroWorld(
            FeatureCatalogue local,
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            if (!TopDownWorldLayoutSelection.TryConsume(seed, out TopDownWorldBuildSelection selection))
                return local;

            FeatureCatalogue macro = default;
            FeatureCatalogue waterBodies = default;
            try
            {
                TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
                var root = new Int2(selection.RootXdm, selection.RootZdm);
                TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                    selection.Layout,
                    intent,
                    root,
                    selection.CellSizeDm,
                    settings);
                macro = TopDownWorldPhysicalVoxelCatalogue.Build(
                    selection.Layout,
                    intent,
                    root,
                    selection.CellSizeDm,
                    settings,
                    allocator);
                waterBodies = TopDownWorldWaterBodyVoxelCatalogue.Build(
                    physical,
                    seed,
                    settings,
                    allocator);
                return waterBodies.IsCreated
                    ? SettlementCatalogueCombiner.Combine(allocator, local, macro, waterBodies)
                    : SettlementCatalogueCombiner.Combine(allocator, local, macro);
            }
            finally
            {
                if (local.IsCreated) local.Dispose();
                if (macro.IsCreated) macro.Dispose();
                if (waterBodies.IsCreated) waterBodies.Dispose();
            }
        }
    }
}
