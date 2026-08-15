using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-side composition root for procedural content.
    ///
    /// Kentridge itself lives in the MountingForce world-generation package. This class only maps
    /// the showcase palette to semantic material roles and hands the resulting catalogue to the
    /// existing voxel feature pipeline. Neither worldgen package references the showcase or renderer.
    /// </summary>
    public static class ShowcaseCatalogue
    {
        public static FeatureCatalogue Build(uint seed, Allocator allocator)
        {
            var materials = new VoxelMaterialMap(
                foundationStone: ShowcaseWorld.MatStone,
                masonry: ShowcaseWorld.MatStone,
                darkMasonry: 6,
                timber: ShowcaseWorld.MatWood,
                glass: ShowcaseWorld.MatGlass,
                warmWindow: 15,
                roofTile: 8,
                slate: 7,
                cloth: 9,
                moss: 14,
                water: 11,
                roadSurface: 13);

            var settings = new VoxelWorldGenSettings(
                voxelsPerDecimetre: 1,
                materials: materials);

            // Public-space cut/fill rules come first, then buildings. The voxel engine still sees
            // one immutable catalogue, so streaming and renderer code remain unchanged.
            return KentridgeCombinedVoxelCatalogue.Build(seed, settings, allocator);
        }
    }
}
