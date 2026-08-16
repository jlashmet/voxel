using System;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using VoxelEngine.Composition.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
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
            return KentridgeCombinedVoxelCatalogue.Build(seed, settings, allocator);
        }

        [Obsolete("Provide an explicit ShowcaseMaterialSet; material identity is application-owned.")]
        public static FeatureCatalogue Build(uint seed, Allocator allocator)
        {
            const uint structuralMask = (1u << 2) | (1u << 4) | (1u << 6) | (1u << 7)
                                      | (1u << 8) | (1u << 9) | (1u << 12) | (1u << 15);
            var roles = new ShowcaseMaterialSet(
                terrainSurface: 10,
                terrainSubsurface: 13,
                terrainDeep: 19,
                worldgenFoundation: 6,
                worldgenMasonry: 19,
                worldgenDarkMasonry: 6,
                worldgenTimber: 2,
                worldgenGlass: 4,
                worldgenWarmWindow: 15,
                worldgenRoofTile: 8,
                worldgenSlate: 7,
                worldgenCloth: 9,
                worldgenMoss: 14,
                worldgenWater: 11,
                worldgenRoadSurface: 18,
                structuralMaterialMask: structuralMask);
            return Build(seed, in roles, allocator);
        }
    }
}
