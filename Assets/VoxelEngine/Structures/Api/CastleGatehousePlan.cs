using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Frozen authored recipe for the primary gatehouse and its approach bridge. Coordinates and
    /// orientation still come from the spatial primary gate; this value freezes the dimensional
    /// choices and gate-tower slit patterns that Runtime historically derived during realization.
    /// </summary>
    public struct CastleGatehousePlan
    {
        public int TowerSpacing;
        public int LeftTowerHeight;
        public int RightTowerHeight;
        public CastleTowerSlitPlan LeftTowerSlits;
        public CastleTowerSlitPlan RightTowerSlits;
        public int BlockHeight;
        public int OpeningHeight;

        public int BridgeNearDistance;
        public int BridgeLength;
        public int BridgeWidth;
        public int BridgeDeckYOffset;
        public int BridgeDeckHeight;
        public int BridgeSupportOffset;
        public int BridgeSupportYOffset;
        public int BridgeSupportHeight;
        public int BridgeSupportThickness;
        public int BridgeRailYOffset;
        public int BridgeRailHeight;
        public int BridgeRailThickness;
    }

    public enum CastleGatehousePlanIssue : byte
    {
        None,
        InvalidTowerSpacing,
        InvalidTowerHeight,
        MissingTowerSlitPlan,
        InvalidTowerSlitPlan,
        InvalidMasonry,
        InvalidBridgeSpan,
        InvalidBridgeDeck,
        InvalidBridgeSupports,
        InvalidBridgeRails,
    }

    /// <summary>
    /// Canonical behavior-preserving recipe used by both the planner and the temporary legacy
    /// realization facade. Keeping the formula here prevents Runtime from invoking a planning
    /// entry point while also avoiding a second copy of the historical constants.
    /// </summary>
    public static class CastleGatehouseRecipe
    {
        public static CastleGatehousePlan Historical(in CastlePlan plan)
        {
            var legacyPlacement = new CastleGatePlacementSpec
            {
                EdgeIndex = 0,
                Centre = new int2(0, -plan.BaileyHalfZ),
                Outward = new float2(0f, -1f),
            };
            return Historical(in plan, in legacyPlacement);
        }

        public static CastleGatehousePlan Historical(
            in CastlePlan plan,
            in CastleGatePlacementSpec placement)
        {
            var gatehouse = new CastleGatehousePlan
            {
                TowerSpacing = math.max(
                    54,
                    plan.GateTowerRadius + CastleLayout.FrontGateWidth / 2 + 8),
                LeftTowerHeight = plan.GateTowerHeight + 38,
                RightTowerHeight = plan.GateTowerHeight + 12,
                BlockHeight = plan.WallHeight + 22,
                OpeningHeight = CastleLayout.FrontGateHeight + 14,

                BridgeNearDistance = plan.WallThickness + 4,
                BridgeLength = 150,
                BridgeWidth = 68,
                BridgeDeckYOffset = -2,
                BridgeDeckHeight = 2,
                BridgeSupportOffset = 32,
                BridgeSupportYOffset = -7,
                BridgeSupportHeight = 5,
                BridgeSupportThickness = 8,
                BridgeRailYOffset = 8,
                BridgeRailHeight = 4,
                BridgeRailThickness = 4,
            };

            CastleGateGeometry geometry = CastleGateGeometryResolver.Resolve(in plan, in placement);
            float2 gate = geometry.PerimeterCentre;
            float2 tangent = geometry.Tangent;
            int2 left = Round(gate - tangent * gatehouse.TowerSpacing);
            int2 right = Round(gate + tangent * gatehouse.TowerSpacing);
            gatehouse.LeftTowerSlits = CastleTowerSlitPlanner.Create(
                left, gatehouse.LeftTowerHeight, plan.FloorHeight);
            gatehouse.RightTowerSlits = CastleTowerSlitPlanner.Create(
                right, gatehouse.RightTowerHeight, plan.FloorHeight);

            CastleGatehousePlanValidator.RequireValid(in gatehouse);
            CastleGatehousePlanValidator.RequireTowerDetails(in gatehouse, plan.FloorHeight);
            return gatehouse;
        }

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }

    /// <summary>
    /// Pure planner for the historical gatehouse recipe. Keeping the exact current values makes
    /// the planning migration behavior-preserving while removing those choices from realization.
    /// </summary>
    public static class CastleGatehousePlanner
    {
        public static CastleGatehousePlan Create(in CastlePlan plan) =>
            CastleGatehouseRecipe.Historical(in plan);

        public static CastleGatehousePlan Create(
            in CastlePlan plan,
            in CastleGatePlacementSpec placement) =>
            CastleGatehouseRecipe.Historical(in plan, in placement);
    }

    /// <summary>Structural validation for a frozen primary-gatehouse recipe.</summary>
    public static class CastleGatehousePlanValidator
    {
        public static bool TryValidate(
            in CastleGatehousePlan plan,
            out CastleGatehousePlanIssue issue)
        {
            if (plan.TowerSpacing <= 0)
            {
                issue = CastleGatehousePlanIssue.InvalidTowerSpacing;
                return false;
            }

            if (plan.LeftTowerHeight <= 0 || plan.RightTowerHeight <= 0)
            {
                issue = CastleGatehousePlanIssue.InvalidTowerHeight;
                return false;
            }

            if (plan.BlockHeight <= 0 || plan.OpeningHeight <= 0 ||
                plan.BlockHeight <= plan.OpeningHeight)
            {
                issue = CastleGatehousePlanIssue.InvalidMasonry;
                return false;
            }

            if (plan.BridgeNearDistance <= 0 || plan.BridgeLength <= 0 || plan.BridgeWidth <= 0)
            {
                issue = CastleGatehousePlanIssue.InvalidBridgeSpan;
                return false;
            }

            if (plan.BridgeDeckHeight <= 0)
            {
                issue = CastleGatehousePlanIssue.InvalidBridgeDeck;
                return false;
            }

            if (plan.BridgeSupportOffset <= 0 ||
                plan.BridgeSupportOffset * 2 >= plan.BridgeWidth ||
                plan.BridgeSupportHeight <= 0 || plan.BridgeSupportThickness <= 0)
            {
                issue = CastleGatehousePlanIssue.InvalidBridgeSupports;
                return false;
            }

            if (plan.BridgeRailHeight <= 0 || plan.BridgeRailThickness <= 0)
            {
                issue = CastleGatehousePlanIssue.InvalidBridgeRails;
                return false;
            }

            issue = CastleGatehousePlanIssue.None;
            return true;
        }

        public static bool TryValidateTowerDetails(
            in CastleGatehousePlan plan,
            int floorHeight,
            out CastleGatehousePlanIssue issue)
        {
            if (plan.LeftTowerSlits == null || plan.RightTowerSlits == null)
            {
                issue = CastleGatehousePlanIssue.MissingTowerSlitPlan;
                return false;
            }

            if (!CastleTowerSlitPlanValidator.TryValidate(
                    plan.LeftTowerSlits,
                    plan.LeftTowerHeight,
                    floorHeight,
                    out _) ||
                !CastleTowerSlitPlanValidator.TryValidate(
                    plan.RightTowerSlits,
                    plan.RightTowerHeight,
                    floorHeight,
                    out _))
            {
                issue = CastleGatehousePlanIssue.InvalidTowerSlitPlan;
                return false;
            }

            issue = CastleGatehousePlanIssue.None;
            return true;
        }

        public static void RequireValid(in CastleGatehousePlan plan)
        {
            if (TryValidate(in plan, out CastleGatehousePlanIssue issue))
                return;

            throw new System.InvalidOperationException($"Castle gatehouse plan is invalid: {issue}.");
        }

        public static void RequireTowerDetails(
            in CastleGatehousePlan plan,
            int floorHeight)
        {
            if (TryValidateTowerDetails(in plan, floorHeight, out CastleGatehousePlanIssue issue))
                return;

            throw new System.InvalidOperationException(
                $"Castle gatehouse tower detail plan is invalid: {issue}.");
        }
    }
}
