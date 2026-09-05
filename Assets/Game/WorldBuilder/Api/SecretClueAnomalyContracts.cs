using System;

namespace Game.WorldBuilder.Api
{
    public enum SecretClueMotifFamily
    {
        StructuralFracture = 0,
        MaterialSeam = 1,
        SurfaceWear = 2,
        MechanicalTrace = 3,
        DebrisAlignment = 4,
        VegetationDiscontinuity = 5,
        ErosionTrail = 6,
        SightlineGap = 7,
        DisturbedGround = 8
    }

    public enum SecretClueContrastAxis
    {
        Material = 0,
        Silhouette = 1,
        Density = 2,
        Alignment = 3,
        Repetition = 4,
        NegativeSpace = 5
    }

    public enum SecretClueActionIntent
    {
        Investigate = 0,
        BreakBarrier = 1,
        OperateMechanism = 2,
        TraverseTerrain = 3
    }

    /// <summary>
    /// Small, feature-owned summary of what is locally normal around a semantic clue anchor.
    /// Values are percentages so realization policy can choose a controlled deviation from local
    /// normality without depending on prefab names, scene coordinates, or renderer-specific data.
    /// </summary>
    public readonly struct SecretClueLocalContext
    {
        public int VegetationDensityPercent { get; }
        public int SurfaceUniformityPercent { get; }
        public int StructuralRegularityPercent { get; }
        public int OcclusionPercent { get; }
        public int RecentDisturbancePercent { get; }

        public SecretClueLocalContext(
            int vegetationDensityPercent,
            int surfaceUniformityPercent,
            int structuralRegularityPercent,
            int occlusionPercent,
            int recentDisturbancePercent)
        {
            VegetationDensityPercent = Percent(vegetationDensityPercent, nameof(vegetationDensityPercent));
            SurfaceUniformityPercent = Percent(surfaceUniformityPercent, nameof(surfaceUniformityPercent));
            StructuralRegularityPercent = Percent(structuralRegularityPercent, nameof(structuralRegularityPercent));
            OcclusionPercent = Percent(occlusionPercent, nameof(occlusionPercent));
            RecentDisturbancePercent = Percent(recentDisturbancePercent, nameof(recentDisturbancePercent));
        }

        private static int Percent(int value, string name)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    /// <summary>
    /// Deterministic semantic presentation decision. Feature-specific realizers translate this into
    /// geometry/material/vegetation/audio while route interaction and discovery state remain owned by
    /// their canonical runtime systems.
    /// </summary>
    public readonly struct SecretClueAnomalyPlan
    {
        public SecretClueMotifFamily Motif { get; }
        public SecretClueContrastAxis PrimaryContrast { get; }
        public SecretClueContrastAxis SecondaryContrast { get; }
        public SecretClueActionIntent ActionIntent { get; }
        public int StrengthPercent { get; }

        public SecretClueAnomalyPlan(
            SecretClueMotifFamily motif,
            SecretClueContrastAxis primaryContrast,
            SecretClueContrastAxis secondaryContrast,
            SecretClueActionIntent actionIntent,
            int strengthPercent)
        {
            if (strengthPercent < 1 || strengthPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(strengthPercent));
            Motif = motif;
            PrimaryContrast = primaryContrast;
            SecondaryContrast = secondaryContrast;
            ActionIntent = actionIntent;
            StrengthPercent = strengthPercent;
        }
    }
}
