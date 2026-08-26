using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using Game.WorldBuilder.Voxel;
using Unity.Collections;
using VoxelEngine.Composition.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-side composition root for procedural content. WorldBuilder owns town authoring;
    /// this composition root supplies only the voxel material roles used to realize that town.
    /// </summary>
    public static class ShowcaseCatalogue
    {
        public static FeatureCatalogue Build(
            uint seed, in ShowcaseMaterialSet materialRoles, Allocator allocator)
        {
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(
                WorldBuilderTownIds.Kentridge,
                seed);
            var materials = new WorldBuilderVoxelMaterialMap(
                foundationStone: materialRoles.WorldgenFoundation,
                masonry: materialRoles.WorldgenMasonry,
                darkMasonry: materialRoles.WorldgenDarkMasonry,
                timber: materialRoles.WorldgenTimber,
                glass: materialRoles.WorldgenGlass,
                warmWindow: materialRoles.WorldgenWarmWindow,
                roofTile: materialRoles.WorldgenRoofTile,
                slate: materialRoles.WorldgenSlate,
                cloth: materialRoles.WorldgenCloth,
                moss: materialRoles.WorldgenMoss,
                water: materialRoles.WorldgenWater,
                roadSurface: materialRoles.WorldgenRoadSurface);

            // Kentridge is the production mixed-city showcase, not a parallel demo planner. The
            // town is authored once above through WorldBuilder, and the voxel adapter realizes that
            // exact authored plan. The detached detailed-house feature below remains a focused
            // deep-override example composed beside the production town catalogue.
            FeatureCatalogue kentridge =
                WorldBuilderVoxelCatalogue.Build(town, in materials, Allocator.Temp);
            try
            {
                FeatureCatalogue detailedHouse =
                    ShowcaseDetailedHouseCatalogue.Build(seed, in materialRoles, Allocator.Temp);
                try
                {
                    // VoxelEngine.Structures.Api also declares a FeatureCatalogueComposer. The
                    // Runtime one is named explicitly: it copies each source program blob verbatim
                    // and rebases the offset, where the Api one repacks per definition and would
                    // drop or duplicate program bytes that definitions do not own one-to-one.
                    return VoxelEngine.Structures.Runtime.FeatureCatalogueComposer.Combine(
                        in kentridge, in detailedHouse, allocator);
                }
                finally
                {
                    detailedHouse.Dispose();
                }
            }
            finally
            {
                kentridge.Dispose();
            }
        }

        /// <summary>
        /// Compatibility path for the original showcase constructor. The application-owned
        /// constructor immediately disposes this catalogue and rebuilds it from explicit roles.
        /// Keep all new code on the role-based overload above.
        /// </summary>
        [Obsolete("Provide an explicit ShowcaseMaterialSet; material identity is application-owned.")]
        public static FeatureCatalogue Build(uint seed, Allocator allocator)
        {
            const uint structuralMask = (1u << 2) | (1u << 4) | (1u << 6) | (1u << 7)
                                      | (1u << 8) | (1u << 9) | (1u << 12) | (1u << 15);
            var compatibility = new ShowcaseMaterialSet(
                terrainDeep: 5,
                terrainSubsurface: 1,
                terrainLowSurface: 3,
                terrainHighSurface: 10,
                gate: 2,
                referenceArch: 6,
                farStructure: 1,
                worldgenFoundation: 1,
                worldgenMasonry: 1,
                worldgenDarkMasonry: 6,
                worldgenTimber: 2,
                worldgenGlass: 4,
                worldgenWarmWindow: 15,
                worldgenRoofTile: 8,
                worldgenSlate: 7,
                worldgenCloth: 9,
                worldgenMoss: 14,
                worldgenWater: 11,
                worldgenRoadSurface: 13,
                structuralMask: structuralMask);
            return Build(seed, in compatibility, allocator);
        }
    }
}
