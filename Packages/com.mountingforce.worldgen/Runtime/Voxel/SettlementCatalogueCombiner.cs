using System;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Joins the voxel catalogues of several settlements into the single catalogue a world takes.
    ///
    /// A world is configured with one <see cref="FeatureCatalogue"/>, so a slice containing two
    /// towns has to hand over one. Each town is generated independently — different plan, different
    /// theme, different part of the map — and this concatenates the results, rebasing every internal
    /// offset exactly as the multi-stage pass already does when it merges its own catalogues.
    /// </summary>
    public static class SettlementCatalogueCombiner
    {
        public static FeatureCatalogue Combine(Allocator allocator, params FeatureCatalogue[] catalogues)
        {
            if (catalogues == null) throw new ArgumentNullException(nameof(catalogues));
            if (catalogues.Length == 0)
                throw new ArgumentException("At least one catalogue is required.", nameof(catalogues));

            return KentridgeCombinedVoxelCatalogueCanonical.CombineCatalogues(catalogues, allocator);
        }
    }
}
