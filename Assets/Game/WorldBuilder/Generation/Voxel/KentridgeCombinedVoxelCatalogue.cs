using System.Collections.Generic;
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
            try
            {
                // The detailed Kentridge catalogue already owns the selected root settlement. The
                // macro root marker is only coarse instrumentation; composing it here repaints a
                // 120m terrain-grounded square across the authored town and can become visible in
                // otherwise-unowned seams between roads and plot surfaces. Keep all macro routes and
                // non-root destinations, but leave the root's visible surface to its detailed pass.
                macro = TopDownWorldVoxelCatalogue.Build(
                    selection.Layout,
                    new Int2(selection.RootXdm, selection.RootZdm),
                    selection.CellSizeDm,
                    settings,
                    allocator,
                    selection.Layout.RootId);
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
