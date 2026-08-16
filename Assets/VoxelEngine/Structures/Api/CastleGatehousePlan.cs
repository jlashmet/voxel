using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Frozen authored recipe for the primary gatehouse and its approach bridge. Coordinates and
    /// orientation still come from the spatial primary gate; this value freezes the dimensional
    /// choices that Runtime historically derived while realizing the gatehouse.
    /// </summary>
    public struct CastleGatehousePlan
    {
        public int TowerSpacing;
        public int LeftTowerHeight;
        public int RightTowerHeight;
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

            CastleGatehousePlanValidator.RequireValid(in gatehouse);
            return gatehouse;
        }
    }

    /// <summary>
    /// Pure planner for the historical gatehouse recipe. Keeping the exact current values makes
    /// the planning migration behavior-preserving while removing those choices from realization.
    /// </summary>
    public static class CastleGatehousePlanner
    {
        public static CastleGatehousePlan Create(in CastlePlan plan) =>
            CastleGatehouseRecipe.Historical(in plan);
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

        public static void RequireValid(in CastleGatehousePlan plan)
        {
            if (TryValidate(in plan, out CastleGatehousePlanIssue issue))
                return;

            throw new System.InvalidOperationException($"Castle gatehouse plan is invalid: {issue}.");
        }
    }
}
