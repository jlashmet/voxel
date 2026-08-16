namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Frozen authored recipe for a secondary arched door through a curtain wall. Placement comes
    /// from CastleGatePlacementSpec; this plan owns opening/leaf dimensions and iron-strap style.
    /// </summary>
    public struct CastleWallDoorPlan
    {
        public int Width;
        public int Height;
        public int Depth;
        public int OpeningDepthExtra;
        public int LeafWidthReduction;
        public int LeafHeightReduction;
        public int StrapFirstY;
        public int StrapSpacing;
        public int StrapThickness;
        public int StrapDepthExtra;
    }

    public enum CastleWallDoorPlanIssue : byte
    {
        None,
        InvalidOpeningDimensions,
        InvalidOpeningDepth,
        InvalidLeafInset,
        InvalidStrapPattern,
    }

    /// <summary>Pure secondary-door recipes attached before Runtime realization begins.</summary>
    public static class CastleWallDoorPlanner
    {
        public static CastleWallDoorPlan Postern() =>
            CastleWallDoorRecipe.PosternHistorical();

        public static CastleWallDoorPlan InnerWard() =>
            CastleWallDoorRecipe.InnerWardHistorical();
    }

    /// <summary>Behavior-preserving secondary-door recipes shared by planning and compatibility.</summary>
    public static class CastleWallDoorRecipe
    {
        public static CastleWallDoorPlan PosternHistorical() => Historical(
            CastleLayout.PosternGateWidth,
            CastleLayout.PosternGateHeight,
            CastleLayout.PosternGateDepth);

        public static CastleWallDoorPlan InnerWardHistorical() => Historical(
            CastleLayout.FrontGateWidth,
            CastleLayout.FrontGateHeight,
            CastleLayout.FrontGateDepth);

        public static CastleWallDoorPlan Historical(int width, int height, int depth)
        {
            var plan = new CastleWallDoorPlan
            {
                Width = width,
                Height = height,
                Depth = depth,
                OpeningDepthExtra = 4,
                LeafWidthReduction = 4,
                LeafHeightReduction = 4,
                StrapFirstY = 10,
                StrapSpacing = 14,
                StrapThickness = 2,
                StrapDepthExtra = 1,
            };
            CastleWallDoorPlanValidator.RequireValid(in plan);
            return plan;
        }
    }

    public static class CastleWallDoorPlanValidator
    {
        public static bool TryValidate(
            in CastleWallDoorPlan plan,
            out CastleWallDoorPlanIssue issue)
        {
            if (plan.Width <= 4 || plan.Height <= 4 || plan.Depth <= 0)
            {
                issue = CastleWallDoorPlanIssue.InvalidOpeningDimensions;
                return false;
            }

            if (plan.OpeningDepthExtra < 0)
            {
                issue = CastleWallDoorPlanIssue.InvalidOpeningDepth;
                return false;
            }

            if (plan.LeafWidthReduction < 0 || plan.LeafHeightReduction < 0 ||
                plan.LeafWidthReduction >= plan.Width ||
                plan.LeafHeightReduction >= plan.Height)
            {
                issue = CastleWallDoorPlanIssue.InvalidLeafInset;
                return false;
            }

            int leafHeight = plan.Height - plan.LeafHeightReduction;
            if (plan.StrapFirstY < 0 || plan.StrapFirstY >= leafHeight ||
                plan.StrapSpacing <= 0 || plan.StrapThickness <= 0 ||
                plan.StrapThickness > plan.StrapSpacing || plan.StrapDepthExtra < 0)
            {
                issue = CastleWallDoorPlanIssue.InvalidStrapPattern;
                return false;
            }

            issue = CastleWallDoorPlanIssue.None;
            return true;
        }

        public static void RequireValid(in CastleWallDoorPlan plan)
        {
            if (TryValidate(in plan, out CastleWallDoorPlanIssue issue))
                return;

            throw new System.InvalidOperationException(
                $"Castle wall-door plan is invalid: {issue}.");
        }
    }
}
