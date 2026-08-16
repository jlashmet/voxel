using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

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

        // Secondary wall-door dimensions are part of the frozen wall recipe. Placement still comes
        // from CastleSpatialPlan; Runtime only realizes these already-authored dimensions.
        public int PosternDoorWidth;
        public int PosternDoorHeight;
        public int PosternDoorDepth;
        public int InnerGateDoorWidth;
        public int InnerGateDoorHeight;
        public int InnerGateDoorDepth;

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
        InvalidSecondaryDoor,
        InvalidPlinth,
        InvalidCourse,
        InvalidWallWalk,
        InvalidArrowSlits,
        InvalidCrenellations,
    }

    /// <summary>
    /// Pure curtain-wall style planner. The parameterless overload preserves the historical
    /// authored recipe for compatibility callers; the seeded overload freezes visual/defensive
    /// variation on an independent Walls substream for production topology planning.
    /// </summary>
    public static class CastleWallPlanner
    {
        public static CastleWallPlan Create() => CastleWallRecipe.Historical();

        public static CastleWallPlan Create(uint seed)
        {
            CastleWallPlan plan = CastleWallRecipe.Historical();
            var rng = new Random(CastleSeedPartition.Derive(
                seed, CastleSeedDomain.Walls, 0xA771u));

            // Structural wall height/thickness and door clearances remain stable compatibility
            // dimensions. Vary only the authored defensive profile so a seed changes how the same
            // perimeter is expressed rather than changing its access topology.
            plan.MaxPlinthHeight = rng.NextInt(16, 29);
            plan.CourseHeightFraction = rng.NextFloat(0.58f, 0.74f);
            plan.CourseThickness = rng.NextInt(2, 5);
            plan.WallWalkThickness = rng.NextInt(1, 4);

            plan.ArrowSlitFirstDistance = rng.NextInt(32, 51);
            plan.ArrowSlitEndInset = rng.NextInt(16, 31);
            plan.ArrowSlitSpacing = rng.NextInt(72, 113);
            plan.ArrowSlitYOffset = rng.NextInt(34, 49);
            plan.ArrowSlitMaxHeight = rng.NextInt(20, 33);
            plan.ArrowSlitDepthScale = rng.NextFloat(0.55f, 0.76f);

            plan.CrenellationMerlonLength = rng.NextInt(22, 33);
            plan.CrenellationGapLength = rng.NextInt(14, 25);
            plan.CrenellationHeight = rng.NextInt(16, 25);
            plan.CrenellationMaximumThickness = rng.NextInt(6, 11);

            CastleWallPlanValidator.RequireValid(in plan);
            return plan;
        }
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

                PosternDoorWidth = CastleLayout.PosternGateWidth,
                PosternDoorHeight = CastleLayout.PosternGateHeight,
                PosternDoorDepth = CastleLayout.PosternGateDepth,
                InnerGateDoorWidth = CastleLayout.FrontGateWidth,
                InnerGateDoorHeight = CastleLayout.FrontGateHeight,
                InnerGateDoorDepth = CastleLayout.FrontGateDepth,

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

            if (!ValidDoor(plan.PosternDoorWidth, plan.PosternDoorHeight, plan.PosternDoorDepth) ||
                !ValidDoor(plan.InnerGateDoorWidth, plan.InnerGateDoorHeight, plan.InnerGateDoorDepth))
            {
                issue = CastleWallPlanIssue.InvalidSecondaryDoor;
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

        private static bool ValidDoor(int width, int height, int depth) =>
            width > 4 && height > 4 && depth > 0;
    }
}
