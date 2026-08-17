using System;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using VoxelEngine.Composition.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-side composition root for procedural content. Worldgen receives semantic roles,
    /// but the material indices occupying those roles are supplied by the application.
    /// </summary>
    public static class ShowcaseCatalogue
    {
        public static FeatureCatalogue Build(
            uint seed, in ShowcaseMaterialSet materialRoles, Allocator allocator)
        {
            var materials = new VoxelMaterialMap(
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

            var settings = new VoxelWorldGenSettings(
                voxelsPerDecimetre: 1,
                materials: materials);

            // Keep worldgen and the detailed shared-house example as separate authoring sources,
            // then freeze them into the one immutable catalogue consumed by streaming/rendering.
            FeatureCatalogue kentridge =
                KentridgeCombinedVoxelCatalogue.Build(seed, settings, Allocator.Temp);
            try
            {
                FeatureCatalogue detailedHouse =
                    ShowcaseDetailedHouseCatalogue.Build(seed, in materialRoles, Allocator.Temp);
                try
                {
                    return FeatureCatalogueComposer.Combine(
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
