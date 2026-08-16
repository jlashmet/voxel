using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Semantic curtain-wall language selected before voxel realization.</summary>
    public enum CastleWallStyle : byte
    {
        Historical,
        Regular,
        Heavy,
        Austere,
        Ceremonial,
    }

    /// <summary>
    /// Frozen authored style for curtain-wall realization. CastlePlan still owns structural wall
    /// height/thickness; this value owns the visual/defensive recipe that used to be hard-coded in
    /// Runtime so realization can apply a completed plan without inventing authored dimensions.
    /// Secondary arched doors use CastleWallDoorPlan rather than duplicating door policy here.
    /// </summary>
    public struct CastleWallPlan
    {
        public CastleWallStyle Style;
        public int PrimaryGateExtraClearWidth;
        public int PrimaryGateMinimumThicknessMultiple;

        public int MaxPlinthHeight;
        public float CourseHeightFraction;
        public int CourseMinimumWallHeight;
        public int CourseThickness;
        public int WallWalkThickness;

        public int ArrowSlitMinimumWallHeight;
        public int ArrowSlitFirstDistance;
        public int ArrowSlitEndInset;
        public int ArrowSlitSpacing;
        public int ArrowSlitYOffset;
        public int ArrowSlitMaxHeight;
        public int ArrowSlitThickness;
        public float ArrowSlitDepthScale;

        public int CrenellationMerlonLength;
        public int CrenellationGapLength;
        public int CrenellationHeight;
        public int CrenellationMinimumThickness;
        public int CrenellationMaximumThickness;
    }

    public enum CastleWallPlanIssue : byte
    {
        None,
        InvalidGateClearance,
        InvalidPlinth,
        InvalidCourse,
        InvalidWallWalk,
        InvalidArrowSlits,
        InvalidCrenellations,
        InvalidStyle,
    }

    /// <summary>
    /// Pure curtain-wall style planner. The parameterless overload preserves the historical
    /// authored recipe for compatibility callers; the seeded overload chooses one coherent
    /// architectural profile on an independent Walls substream for production topology planning.
    /// Individual numeric knobs are not randomized independently: a seed selects a wall language,
    /// and the recipe freezes the dimensions that make that language internally consistent.
    /// </summary>
    public static class CastleWallPlanner
    {
        public static CastleWallPlan Create() => CastleWallRecipe.Historical();

        public static CastleWallPlan Create(uint seed)
        {
            var rng = new Random(CastleSeedPartition.Derive(
                seed, CastleSeedDomain.Walls, 0xA771u));
            int roll = rng.NextInt(0, 100);

            if (roll < 35) return CastleWallRecipe.Regular();
            if (roll < 60) return CastleWallRecipe.Heavy();
            if (roll < 85) return CastleWallRecipe.Austere();
            return CastleWallRecipe.Ceremonial();
        }
    }

    /// <summary>Named authored wall profiles. Historical remains the exact compatibility recipe.</summary>
    public static class CastleWallRecipe
    {
        public static CastleWallPlan Historical() => Require(new CastleWallPlan
        {
            Style = CastleWallStyle.Historical,
            PrimaryGateExtraClearWidth = 12,
            PrimaryGateMinimumThicknessMultiple = 2,

            MaxPlinthHeight = 22,
            CourseHeightFraction = 0.66f,
            CourseMinimumWallHeight = 4,
            CourseThickness = 2,
            WallWalkThickness = 1,

            ArrowSlitMinimumWallHeight = 70,
            ArrowSlitFirstDistance = 40,
            ArrowSlitEndInset = 20,
            ArrowSlitSpacing = 90,
            ArrowSlitYOffset = 40,
            ArrowSlitMaxHeight = 28,
            ArrowSlitThickness = 2,
            ArrowSlitDepthScale = 0.65f,

            CrenellationMerlonLength = 26,
            CrenellationGapLength = 18,
            CrenellationHeight = 20,
            CrenellationMinimumThickness = 2,
            CrenellationMaximumThickness = 8,
        });

        public static CastleWallPlan Regular()
        {
            CastleWallPlan plan = Historical();
            plan.Style = CastleWallStyle.Regular;
            return Require(plan);
        }

        public static CastleWallPlan Heavy()
        {
            CastleWallPlan plan = Historical();
            plan.Style = CastleWallStyle.Heavy;
            plan.PrimaryGateExtraClearWidth = 18;
            plan.MaxPlinthHeight = 30;
            plan.CourseHeightFraction = 0.72f;
            plan.CourseThickness = 3;
            plan.WallWalkThickness = 2;
            plan.ArrowSlitMinimumWallHeight = 82;
            plan.ArrowSlitFirstDistance = 48;
            plan.ArrowSlitEndInset = 26;
            plan.ArrowSlitSpacing = 112;
            plan.ArrowSlitYOffset = 46;
            plan.ArrowSlitDepthScale = 0.75f;
            plan.CrenellationMerlonLength = 32;
            plan.CrenellationGapLength = 16;
            plan.CrenellationHeight = 24;
            plan.CrenellationMinimumThickness = 3;
            plan.CrenellationMaximumThickness = 10;
            return Require(plan);
        }

        public static CastleWallPlan Austere()
        {
            CastleWallPlan plan = Historical();
            plan.Style = CastleWallStyle.Austere;
            plan.PrimaryGateExtraClearWidth = 8;
            plan.MaxPlinthHeight = 18;
            plan.CourseHeightFraction = 0.60f;
            plan.CourseMinimumWallHeight = 6;
            plan.CourseThickness = 1;
            plan.ArrowSlitMinimumWallHeight = 62;
            plan.ArrowSlitFirstDistance = 34;
            plan.ArrowSlitEndInset = 16;
            plan.ArrowSlitSpacing = 76;
            plan.ArrowSlitYOffset = 34;
            plan.ArrowSlitMaxHeight = 24;
            plan.ArrowSlitDepthScale = 0.58f;
            plan.CrenellationMerlonLength = 22;
            plan.CrenellationGapLength = 14;
            plan.CrenellationHeight = 18;
            plan.CrenellationMaximumThickness = 7;
            return Require(plan);
        }

        public static CastleWallPlan Ceremonial()
        {
            CastleWallPlan plan = Historical();
            plan.Style = CastleWallStyle.Ceremonial;
            plan.PrimaryGateExtraClearWidth = 16;
            plan.MaxPlinthHeight = 26;
            plan.CourseHeightFraction = 0.58f;
            plan.CourseThickness = 3;
            plan.WallWalkThickness = 2;
            plan.ArrowSlitMinimumWallHeight = 82;
            plan.ArrowSlitFirstDistance = 56;
            plan.ArrowSlitEndInset = 30;
            plan.ArrowSlitSpacing = 128;
            plan.ArrowSlitYOffset = 52;
            plan.ArrowSlitMaxHeight = 22;
            plan.ArrowSlitDepthScale = 0.70f;
            plan.CrenellationMerlonLength = 30;
            plan.CrenellationGapLength = 22;
            plan.CrenellationHeight = 22;
            plan.CrenellationMaximumThickness = 9;
            return Require(plan);
        }

        private static CastleWallPlan Require(CastleWallPlan plan)
        {
            CastleWallPlanValidator.RequireValid(in plan);
            return plan;
        }
    }

    public static class CastleWallPlanValidator
    {
        public static bool TryValidate(
            in CastleWallPlan plan,
            out CastleWallPlanIssue issue)
        {
            if ((byte)plan.Style > (byte)CastleWallStyle.Ceremonial)
            {
                issue = CastleWallPlanIssue.InvalidStyle;
                return false;
            }

            if (plan.PrimaryGateExtraClearWidth < 0 ||
                plan.PrimaryGateMinimumThicknessMultiple <= 0)
            {
                issue = CastleWallPlanIssue.InvalidGateClearance;
                return false;
            }

            if (plan.MaxPlinthHeight <= 0)
            {
                issue = CastleWallPlanIssue.InvalidPlinth;
                return false;
            }

            if (!math.isfinite(plan.CourseHeightFraction) ||
                plan.CourseHeightFraction <= 0f || plan.CourseHeightFraction >= 1f ||
                plan.CourseMinimumWallHeight <= 0 || plan.CourseThickness <= 0)
            {
                issue = CastleWallPlanIssue.InvalidCourse;
                return false;
            }

            if (plan.WallWalkThickness <= 0)
            {
                issue = CastleWallPlanIssue.InvalidWallWalk;
                return false;
            }

            if (plan.ArrowSlitMinimumWallHeight <= 0 ||
                plan.ArrowSlitFirstDistance <= 0 || plan.ArrowSlitEndInset < 0 ||
                plan.ArrowSlitSpacing <= 0 || plan.ArrowSlitYOffset < 0 ||
                plan.ArrowSlitMaxHeight <= 0 || plan.ArrowSlitThickness <= 0 ||
                !math.isfinite(plan.ArrowSlitDepthScale) || plan.ArrowSlitDepthScale <= 0f)
            {
                issue = CastleWallPlanIssue.InvalidArrowSlits;
                return false;
            }

            if (plan.CrenellationMerlonLength <= 0 || plan.CrenellationGapLength <= 0 ||
                plan.CrenellationHeight <= 0 || plan.CrenellationMinimumThickness <= 0 ||
                plan.CrenellationMaximumThickness < plan.CrenellationMinimumThickness)
            {
                issue = CastleWallPlanIssue.InvalidCrenellations;
                return false;
            }

            issue = CastleWallPlanIssue.None;
            return true;
        }

        public static void RequireValid(in CastleWallPlan plan)
        {
            if (TryValidate(in plan, out CastleWallPlanIssue issue))
                return;

            throw new System.InvalidOperationException($"Castle wall plan is invalid: {issue}.");
        }
    }
}
