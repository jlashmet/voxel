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
    /// Showcase-side composition root for procedural content. WorldBuilder owns town and landmark
    /// authoring; this composition root supplies only scene parameters and voxel material roles.
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

            FeatureCatalogue kentridge =
                WorldBuilderVoxelCatalogue.Build(town, in materials, Allocator.Temp);
            try
            {
                FeatureCatalogue detailedHouse =
                    ShowcaseDetailedHouseCatalogue.Build(seed, in materialRoles, Allocator.Temp);
                try
                {
                    MountainLandmarkSpec mountainSpec =
                        ShowcaseMountainDragonLayout.CreateLandmark(seed);
                    FeatureCatalogue mountain = WorldBuilderMountainLandmarkCatalogue.Build(
                        in mountainSpec,
                        mountainMaterial: materialRoles.WorldgenFoundation,
                        pathMaterial: materialRoles.WorldgenRoadSurface,
                        placeholderMaterial: materialRoles.WorldgenDarkMasonry,
                        allocator: Allocator.Temp);
                    try
                    {
                        FeatureCatalogue townAndHouse =
                            VoxelEngine.Structures.Runtime.FeatureCatalogueComposer.Combine(
                                in kentridge, in detailedHouse, Allocator.Temp);
                        try
                        {
                            return VoxelEngine.Structures.Runtime.FeatureCatalogueComposer.Combine(
                                in townAndHouse, in mountain, allocator);
                        }
                        finally
                        {
                            townAndHouse.Dispose();
                        }
                    }
                    finally
                    {
                        mountain.Dispose();
                    }
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
