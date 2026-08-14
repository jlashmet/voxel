using Unity.Mathematics;

namespace VoxelEngine.Core.Vegetation
{
    public enum VegetationKind : byte
    {
        Grass,
        Flower,
        Fern,
        Bush,
        Moss,
        Vine,
    }

    public enum VegetationSurface : byte
    {
        Ground,
        Rock,
        Wood,
        Masonry,
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
            };
        }
    }

    /// <summary>
    /// Deterministic profile describing where a vegetation category prefers to live.
    /// </summary>
    public struct VegetationProfile
    {
        public VegetationKind Kind;
        public float GroundWeight;
        public float RockWeight;
        public float WoodWeight;
        public float MasonryWeight;
        public float MoistureAffinity;
        public float ShadeAffinity;
        public float SlopeTolerance;
    }

    public static class VegetationProfiles
    {
        public static VegetationProfile Get(VegetationKind kind)
        {
            switch (kind)
            {
                case VegetationKind.Flower:
                    return new VegetationProfile
                    {
                        Kind = kind, GroundWeight = 1f, RockWeight = 0.08f,
                        MoistureAffinity = 0.35f, ShadeAffinity = -0.20f, SlopeTolerance = 0.70f,
                    };
                case VegetationKind.Fern:
                    return new VegetationProfile
                    {
                        Kind = kind, GroundWeight = 1f, RockWeight = 0.15f,
                        MoistureAffinity = 0.80f, ShadeAffinity = 0.75f, SlopeTolerance = 0.80f,
                    };
                case VegetationKind.Bush:
                    return new VegetationProfile
                    {
                        Kind = kind, GroundWeight = 1f, RockWeight = 0.05f,
                        MoistureAffinity = 0.25f, ShadeAffinity = 0.05f, SlopeTolerance = 0.55f,
                    };
                case VegetationKind.Moss:
                    return new VegetationProfile
                    {
                        Kind = kind, GroundWeight = 0.45f, RockWeight = 1f, WoodWeight = 0.80f,
                        MasonryWeight = 1f, MoistureAffinity = 1f, ShadeAffinity = 0.90f,
                        SlopeTolerance = 1f,
                    };
                case VegetationKind.Vine:
                    return new VegetationProfile
                    {
                        Kind = kind, RockWeight = 0.55f, WoodWeight = 1f, MasonryWeight = 1f,
                        MoistureAffinity = 0.70f, ShadeAffinity = 0.45f, SlopeTolerance = 1f,
                    };
                case VegetationKind.Grass:
                default:
                    return new VegetationProfile
                    {
                        Kind = VegetationKind.Grass, GroundWeight = 1f, RockWeight = 0.04f,
                        MoistureAffinity = 0.20f, ShadeAffinity = -0.05f, SlopeTolerance = 0.85f,
                    };
            }
        }

        public static float SurfaceWeight(in VegetationProfile profile, VegetationSurface surface)
        {
            switch (surface)
            {
                case VegetationSurface.Rock: return profile.RockWeight;
                case VegetationSurface.Wood: return profile.WoodWeight;
                case VegetationSurface.Masonry: return profile.MasonryWeight;
                case VegetationSurface.Ground:
                default: return profile.GroundWeight;
            }
        }
    }
}
