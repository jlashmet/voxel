using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Why a reusable structure component configuration was rejected.</summary>
    public enum StructureComponentValidationIssue : byte
    {
        None = 0,
        InvalidDimension = 1,
        ImpossibleOpeningSpacing = 2,
        UnsupportedRoofCombination = 3,
        BoundsOverflow = 4,
        PrimitiveBudgetOverflow = 5,
    }

    /// <summary>
    /// Cross-archetype validation for constraints that must mean the same thing everywhere.
    /// Archetype-specific policy remains outside this type; these checks protect universal bounds,
    /// spacing, roof, and primitive-budget invariants before configs compile to shape programs.
    /// </summary>
    public static class StructureComponentValidation
    {
        public static StructureComponentValidationIssue Opening(
            in OpeningConfig config,
            int wallRunLength)
        {
            if (config.Width <= 0 || config.Height <= 0 || wallRunLength <= 0 ||
                config.BottomOffset < 0 || config.StartMargin < 0 || config.EndMargin < 0 ||
                config.FrameThickness < 0 || config.LintelThickness < 0 ||
                config.WidthVariation < 0 || config.HeightVariation < 0 || config.Spacing < 0)
                return StructureComponentValidationIssue.InvalidDimension;

            long widestOpening = (long)config.Width + config.WidthVariation;
            long requiredWidth = (long)config.StartMargin + widestOpening + config.EndMargin;
            if (requiredWidth > wallRunLength)
                return StructureComponentValidationIssue.ImpossibleOpeningSpacing;

            // Zero means a single explicitly positioned opening. Repeated openings must advance
            // at least far enough to avoid deterministic overlap at maximum configured width.
            if (config.Spacing > 0 && config.Spacing < widestOpening)
                return StructureComponentValidationIssue.ImpossibleOpeningSpacing;

            return StructureComponentValidationIssue.None;
        }

        public static StructureComponentValidationIssue Roof(in RoofConfig config)
        {
            if (config.Thickness <= 0 || config.EaveOverhang < 0 || config.ParapetHeight < 0 ||
                config.PitchRise < 0 || config.PitchRun < 0)
                return StructureComponentValidationIssue.InvalidDimension;

            if (config.RidgeAxis != RoofAxis.X && config.RidgeAxis != RoofAxis.Z)
                return StructureComponentValidationIssue.UnsupportedRoofCombination;

            switch (config.Style)
            {
                case RoofStyle.Flat:
                    return config.PitchRise == 0 && config.PitchRun == 0
                        ? StructureComponentValidationIssue.None
                        : StructureComponentValidationIssue.UnsupportedRoofCombination;

                case RoofStyle.Shed:
                case RoofStyle.Gable:
                case RoofStyle.Hip:
                    return config.PitchRise > 0 && config.PitchRun > 0 && config.ParapetHeight == 0
                        ? StructureComponentValidationIssue.None
                        : StructureComponentValidationIssue.UnsupportedRoofCombination;

                default:
                    return StructureComponentValidationIssue.UnsupportedRoofCombination;
            }
        }

        public static StructureComponentValidationIssue VolumeWithinBounds(
            in StructureGenerationBounds bounds,
            int3 min,
            int3 maxExclusive)
        {
            long sizeX = (long)maxExclusive.x - min.x;
            long sizeY = (long)maxExclusive.y - min.y;
            long sizeZ = (long)maxExclusive.z - min.z;
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
                return StructureComponentValidationIssue.InvalidDimension;

            // Calculate in 64-bit space so extreme authored coordinates cannot wrap an int size and
            // accidentally appear to fit. Oversized volumes are outside the declared bounded model.
            if (sizeX > int.MaxValue || sizeY > int.MaxValue || sizeZ > int.MaxValue)
                return StructureComponentValidationIssue.BoundsOverflow;

            return bounds.ContainsVolume(min, maxExclusive)
                ? StructureComponentValidationIssue.None
                : StructureComponentValidationIssue.BoundsOverflow;
        }

        public static StructureComponentValidationIssue PrimitiveBudget(
            int emittedPrimitiveCount,
            int declaredMaxPrimitives)
        {
            if (emittedPrimitiveCount < 0 || declaredMaxPrimitives <= 0)
                return StructureComponentValidationIssue.InvalidDimension;

            if (declaredMaxPrimitives > FeatureBudget.MaxPrimitivesPerInstance ||
                emittedPrimitiveCount > declaredMaxPrimitives ||
                emittedPrimitiveCount > FeatureBudget.MaxPrimitivesPerInstance)
                return StructureComponentValidationIssue.PrimitiveBudgetOverflow;

            return StructureComponentValidationIssue.None;
        }
    }
}
