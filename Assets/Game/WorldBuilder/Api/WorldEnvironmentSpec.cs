using System;

namespace Game.WorldBuilder.Api
{
    [Flags]
    public enum WorldEnvironmentFeature
    {
        None = 0,
        Terrain = 1 << 0,
        Settlement = 1 << 1,
        Fortification = 1 << 2,
        DetailedStructure = 1 << 3,
        Vegetation = 1 << 4,
        AmbientLife = 1 << 5,
        Water = 1 << 6,
        WorldObjects = 1 << 7,
        GalleryDistrict = 1 << 8,
    }

    /// <summary>
    /// Engine-independent request for generated world/environment content.
    /// Scene code selects semantic content and budgets; reusable composition owns how those
    /// requests map to storage, voxel catalogues, structures, vegetation, and other backends.
    /// </summary>
    public readonly struct WorldEnvironmentSpec
    {
        public uint Seed { get; }
        public WorldEnvironmentFeature Features { get; }

        public WorldEnvironmentSpec(uint seed, WorldEnvironmentFeature features)
        {
            if ((features & WorldEnvironmentFeature.Terrain) == 0)
                throw new ArgumentException("Generated environments require terrain.", nameof(features));

            Seed = seed;
            Features = features;
        }

        public bool Includes(WorldEnvironmentFeature feature) =>
            (Features & feature) == feature;

        public WorldEnvironmentSpec With(WorldEnvironmentFeature feature) =>
            new WorldEnvironmentSpec(Seed, Features | feature);

        public WorldEnvironmentSpec Without(WorldEnvironmentFeature feature) =>
            new WorldEnvironmentSpec(Seed, Features & ~feature);
    }

    /// <summary>
    /// Reusable semantic recipes. These describe content intent only; they do not name scenes,
    /// storage implementations, voxel catalogues, or concrete generators.
    /// </summary>
    public static class WorldEnvironmentRecipes
    {
        public static WorldEnvironmentSpec TerrainOnly(uint seed) =>
            new WorldEnvironmentSpec(seed, WorldEnvironmentFeature.Terrain);

        public static WorldEnvironmentSpec DetailedStructure(uint seed) =>
            new WorldEnvironmentSpec(
                seed,
                WorldEnvironmentFeature.Terrain |
                WorldEnvironmentFeature.DetailedStructure);

        public static WorldEnvironmentSpec FortifiedLandmark(uint seed) =>
            new WorldEnvironmentSpec(
                seed,
                WorldEnvironmentFeature.Terrain |
                WorldEnvironmentFeature.Fortification);

        public static WorldEnvironmentSpec SettlementWithFortification(uint seed) =>
            new WorldEnvironmentSpec(
                seed,
                WorldEnvironmentFeature.Terrain |
                WorldEnvironmentFeature.Settlement |
                WorldEnvironmentFeature.Fortification |
                WorldEnvironmentFeature.Vegetation |
                WorldEnvironmentFeature.WorldObjects);

        public static WorldEnvironmentSpec GalleryDistrict(uint seed) =>
            new WorldEnvironmentSpec(
                seed,
                WorldEnvironmentFeature.Terrain |
                WorldEnvironmentFeature.Settlement |
                WorldEnvironmentFeature.Fortification |
                WorldEnvironmentFeature.Vegetation |
                WorldEnvironmentFeature.AmbientLife |
                WorldEnvironmentFeature.Water |
                WorldEnvironmentFeature.WorldObjects |
                WorldEnvironmentFeature.GalleryDistrict);
    }
}
