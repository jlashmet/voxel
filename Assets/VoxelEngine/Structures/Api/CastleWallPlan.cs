using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Frozen authored style for curtain-wall realization. CastlePlan still owns structural wall
    /// height/thickness; this value owns the visual/defensive recipe that used to be hard-coded in
    /// Runtime so realization can apply a completed plan without inventing authored dimensions.
    /// </summary>
    public struct CastleWallPlan
    {
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
    }

    /// <summary>
    /// Pure planner for the historical curtain-wall style. Keeping these values together makes
    /// future wall-style variation a planner concern while preserving the current voxel recipe.
    /// </summary>
    public static class CastleWallPlanner
    {
        public static CastleWallPlan Create() => CastleWallRecipe.Historical();
    }

    /// <summary>Compatibility recipe with the exact pre-planning curtain-wall authored values.</summary>
    public static class CastleWallRecipe
    {
        public static CastleWallPlan Historical()
        {
            var plan = new CastleWallPlan
            {
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
            };

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
