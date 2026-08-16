using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using VoxelEngine.Composition.Api;
using VoxelEngine.Structures.Api;

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

            // Public-space cut/fill rules come first, then buildings. The voxel engine still sees
            // one immutable catalogue, so streaming and renderer code remain unchanged.
            return KentridgeCombinedVoxelCatalogue.Build(seed, settings, allocator);
        }
    }
}
