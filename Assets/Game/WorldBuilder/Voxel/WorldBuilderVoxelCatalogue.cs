using System;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Game-facing material roles for voxel realization. Backend worldgen material types remain
    /// behind the WorldBuilder adapter so presentation code depends only on WorldBuilder.
    /// </summary>
    public readonly struct WorldBuilderVoxelMaterialMap
    {
        public readonly byte FoundationStone;
        public readonly byte Masonry;
        public readonly byte DarkMasonry;
        public readonly byte Timber;
        public readonly byte Glass;
        public readonly byte WarmWindow;
        public readonly byte RoofTile;
        public readonly byte Slate;
        public readonly byte Cloth;
        public readonly byte Moss;
        public readonly byte Water;
        public readonly byte RoadSurface;

        public WorldBuilderVoxelMaterialMap(
            byte foundationStone,
            byte masonry,
            byte darkMasonry,
            byte timber,
            byte glass,
            byte warmWindow,
            byte roofTile,
            byte slate,
            byte cloth,
            byte moss,
            byte water,
            byte roadSurface)
        {
            FoundationStone = foundationStone;
            Masonry = masonry;
            DarkMasonry = darkMasonry;
            Timber = timber;
            Glass = glass;
            WarmWindow = warmWindow;
            RoofTile = roofTile;
            Slate = slate;
            Cloth = cloth;
            Moss = moss;
            Water = water;
            RoadSurface = roadSurface;
        }
    }

    /// <summary>
    /// Canonical voxel realization boundary for an authored WorldBuilder town.
    /// The exact backend plan produced by WorldBuilder is bound into the voxel settings, which
    /// prevents the backend's compatibility fallback from authoring Kentridge a second time.
    /// </summary>
    public static class WorldBuilderVoxelCatalogue
    {
        public static FeatureCatalogue Build(
            AuthoredTownPlan town,
            in WorldBuilderVoxelMaterialMap materials,
            Allocator allocator)
        {
            if (town == null)
                throw new ArgumentNullException(nameof(town));
            if (!string.Equals(town.SettlementId, WorldBuilderTownIds.Kentridge, StringComparison.Ordinal))
                throw new ArgumentOutOfRangeException(
                    nameof(town),
                    town.SettlementId,
                    "The voxel adapter has no registered realization backend for this authored town.");
            if (!(town.BackendPlan is SettlementPlan settlement))
                throw new InvalidOperationException(
                    "The authored town does not carry the expected settlement realization.");

            var backendMaterials = new VoxelMaterialMap(
                foundationStone: materials.FoundationStone,
                masonry: materials.Masonry,
                darkMasonry: materials.DarkMasonry,
                timber: materials.Timber,
                glass: materials.Glass,
                warmWindow: materials.WarmWindow,
                roofTile: materials.RoofTile,
                slate: materials.Slate,
                cloth: materials.Cloth,
                moss: materials.Moss,
                water: materials.Water,
                roadSurface: materials.RoadSurface);
            var settings = new VoxelWorldGenSettings(
                voxelsPerDecimetre: 1,
                materials: backendMaterials,
                settlement: settlement);

            return KentridgeCombinedVoxelCatalogue.Build(town.Seed, settings, allocator);
        }
    }
}
