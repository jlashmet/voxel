using System;
using Unity.Mathematics;

namespace VoxelEngine.Core.Vegetation
{
    /// <summary>
    /// Semantic species/category identity. Existing values are explicit so generated or serialized
    /// vegetation remains stable as the catalogue grows.
    /// </summary>
    public enum VegetationKind : byte
    {
        Grass = 0,
        Flower = 1,
        Fern = 2,
        Bush = 3,
        Moss = 4,
        Vine = 5,

        Clover = 6,
        Weed = 7,
        Nettle = 8,
        Reed = 9,
        Cattail = 10,
        Mushroom = 11,
        FallenLeaves = 12,
        PineNeedles = 13,
        Ivy = 14,
        Lichen = 15,
        WallFern = 16,
        BerryBush = 17,
        ThornBush = 18,
        HedgeShrub = 19,
        DeadShrub = 20,
        Sapling = 21,
        FloweringShrub = 22,
        LilyPad = 23,
        WaterGrass = 24,
        Algae = 25,
        ShelfFungus = 26,
        FallenLog = 27,
        ExposedRoot = 28,
        HangingMoss = 29,
        TrunkMoss = 30,
        Epiphyte = 31,
        DeadBranch = 32,
        HangingVine = 33,
        ClimbingVine = 34,
        DanglingRoot = 35,
        DeadGrass = 36,
        DeadVine = 37,

        // Fantasy species. These use the same growth-form renderers as mundane vegetation;
        // world generation opts into them through ArcaneSaturation rather than special cases.
        Glowshroom = 38,
        ManaBloom = 39,
        CrystalShrub = 40,
        WispReed = 41,
        MoonFern = 42,
        EmberThorn = 43,
        StarMoss = 44,
        ArcaneVine = 45,
    }

    /// <summary>
    /// Geometry/growth strategy. Many species share one realization algorithm with different
    /// deterministic parameters, which keeps the renderer from becoming species-specific.
    /// </summary>
    public enum VegetationGrowthForm : byte
    {
        Tuft,
        Frond,
        Shrub,
        Creeper,
        Climber,
        Hanger,
        Aquatic,
        Fungus,
        Root,
        Debris,
    }

    [Flags]
    public enum VegetationTraits : ushort
    {
        None = 0,
        Magical = 1 << 0,
        Luminous = 1 << 1,
        Edible = 1 << 2,
        Thorny = 1 << 3,
        Toxic = 1 << 4,
        Dead = 1 << 5,
        Woody = 1 << 6,
        Cuttable = 1 << 7,
    }

    public enum VegetationSurface : byte
    {
        Ground = 0,
        Rock = 1,
        Wood = 2,
        Masonry = 3,
        Water = 4,
    }

    /// <summary>
    /// Authoritative semantic identity for lightweight vegetation. Rendering geometry is derived
    /// from this tuple so vegetation does not require prefab or mesh replication.
    /// </summary>
    public struct VegetationInstance
    {
        public float3 PositionMetres;
        public float3 SurfaceNormal;
        public VegetationKind Kind;
        public uint Seed;
        public float Scale;
    }

    /// <summary>
    /// A world-surface candidate supplied by terrain, structures, or authored content. Placement
    /// remains independent of the source that discovered the surface.
    /// </summary>
    public struct VegetationSurfaceSample
    {
        public float3 PositionMetres;
        public float3 Normal;
        public VegetationSurface Surface;
        public float Moisture;
        public float Shade;

        /// <summary>
        /// 0 = mundane environment, 1 = highly magical/enchanted. Worldgen can derive this from
        /// ley lines, enchanted ruins, cursed ground, magical water, biome fields, or authored POIs.
        /// </summary>
        public float ArcaneSaturation;
    }

    public struct VegetationPlacementSettings
    {
        public uint WorldSeed;
        public float Density;
        public float MinScale;
        public float MaxScale;
        public float MaxGroundSlopeDegrees;
        public float MoistureBias;
        public float ShadeBias;
        public float ArcaneBias;

        public static VegetationPlacementSettings Default(uint worldSeed)
        {
            return new VegetationPlacementSettings
            {
                WorldSeed = worldSeed,
                Density = 0.45f,
                MinScale = 0.8f,
                MaxScale = 1.2f,
                MaxGroundSlopeDegrees = 42f,
                MoistureBias = 0.35f,
                ShadeBias = 0.20f,
                ArcaneBias = 0.35f,
            };
        }
    }

    /// <summary>
    /// Deterministic ecological and rendering descriptor for a vegetation kind.
    /// </summary>
    public struct VegetationProfile
    {
        public VegetationKind Kind;
        public VegetationGrowthForm GrowthForm;
        public VegetationTraits Traits;
        public float GroundWeight;
        public float RockWeight;
        public float WoodWeight;
        public float MasonryWeight;
        public float WaterWeight;
        public float MoistureAffinity;
        public float ShadeAffinity;
        public float ArcaneAffinity;
        public float MinArcaneSaturation;
        public float SlopeTolerance;
    }
}
