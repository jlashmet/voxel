using System;
using Unity.Mathematics;

namespace VoxelEngine.Core.AmbientLife
{
    public enum AmbientLifeKind : byte
    {
        Firefly = 0,
        Butterfly = 1,
        Bee = 2,
        Moth = 3,
        Dragonfly = 4,
        Beetle = 5,
        Cricket = 6,
        Frog = 7,
        Songbird = 8,
        Bat = 9,
        SporeMote = 10,

        // Fantasy ambient life. These remain lightweight populations, not full NPCs.
        GlowMoth = 11,
        Wisp = 12,
        Emberfly = 13,
        ManaButterfly = 14,
        SeedLight = 15,
    }

    public enum AmbientMovementForm : byte
    {
        HoverSwarm,
        Flutter,
        Dart,
        Drift,
        GroundScuttle,
        Hop,
        Flock,
        Orbit,
    }

    [Flags]
    public enum AmbientActivity : byte
    {
        None = 0,
        Day = 1 << 0,
        Dusk = 1 << 1,
        Night = 1 << 2,
        All = Day | Dusk | Night,
    }

    [Flags]
    public enum AmbientLifeTraits : ushort
    {
        None = 0,
        Magical = 1 << 0,
        Luminous = 1 << 1,
        Pollinator = 1 << 2,
        Audible = 1 << 3,
        WaterAssociated = 1 << 4,
        Flying = 1 << 5,
    }

    /// <summary>
    /// Habitat summary supplied by world generation or a vegetation adapter. The ambient-life
    /// system intentionally does not depend on vegetation species directly.
    /// </summary>
    public struct AmbientLifeHabitatSample
    {
        public float3 PositionMetres;
        public float RadiusMetres;
        public float Moisture;
        public float Shade;
        public float FlowerDensity;
        public float WaterPresence;
        public float FungusDensity;
        public float DeadwoodDensity;
        public float ArcaneSaturation;
    }

    /// <summary>
    /// Deterministic population seed. Individual visual agents are reconstructed locally from
    /// this value; they do not need to be replicated one by one.
    /// </summary>
    public struct AmbientLifeCluster
    {
        public float3 PositionMetres;
        public AmbientLifeKind Kind;
        public uint Seed;
        public ushort Count;
        public float RadiusMetres;
    }

    public struct AmbientLifePopulationSettings
    {
        public uint WorldSeed;
        public float Density;
        public float MinRadiusMetres;
        public float MaxRadiusMetres;

        public static AmbientLifePopulationSettings Default(uint worldSeed)
        {
            return new AmbientLifePopulationSettings
            {
                WorldSeed = worldSeed,
                Density = 0.55f,
                MinRadiusMetres = 2.5f,
                MaxRadiusMetres = 8f,
            };
        }
    }

    public struct AmbientLifeProfile
    {
        public AmbientLifeKind Kind;
        public AmbientMovementForm Movement;
        public AmbientActivity Activity;
        public AmbientLifeTraits Traits;
        public float BaseWeight;
        public float MoistureAffinity;
        public float ShadeAffinity;
        public float FlowerAffinity;
        public float WaterAffinity;
        public float FungusAffinity;
        public float DeadwoodAffinity;
        public float ArcaneAffinity;
        public float MinArcaneSaturation;
        public ushort MinCount;
        public ushort MaxCount;
    }
}
