using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Relative primary-gatehouse workload model. Historical gatehouse construction retains the
    /// previous fixed two-tower preflight charge; completed seeded recipes scale that baseline by
    /// the masonry, tower-height and bridge work they actually ask Runtime to realize.
    /// </summary>
    internal static class CastleGatehouseBuildEstimate
    {
        internal static double EstimateEquivalentWrites(
            in CastlePlan plan,
            in CastleTopologyPlan topology)
        {
            double baseline = HistoricalBaseline(in plan);
            if (!topology.HasGatehousePlan)
                return baseline;

            CastleGatehousePlan planned = topology.Gatehouse;
            if (!CastleGatehousePlanValidator.TryValidate(in planned, out _) ||
                !CastleGatehousePlanValidator.TryValidateTowerDetails(
                    in planned, plan.FloorHeight, out _))
                return baseline;

            CastleGatehousePlan historical = CastleGatehouseRecipe.Historical(in plan);
            CastleWallPlan historicalWalls = CastleWallRecipe.Historical();
            CastleWallPlan plannedWalls = topology.Walls;
            if (!CastleWallPlanValidator.TryValidate(in plannedWalls, out _))
                plannedWalls = historicalWalls;

            double historicalUnits = RecipeUnits(
                in plan, in historical, in historicalWalls);
            if (historicalUnits <= 0.0)
                return baseline;

            double plannedUnits = RecipeUnits(in plan, in planned, in plannedWalls);
            return baseline * math.max(0.0, plannedUnits / historicalUnits);
        }

        private static double HistoricalBaseline(in CastlePlan plan) =>
            2.0 * math.PI_DBL * plan.GateTowerRadius * plan.GateTowerRadius * 30.0;

        private static double RecipeUnits(
            in CastlePlan plan,
            in CastleGatehousePlan gatehouse,
            in CastleWallPlan walls)
        {
            int radius = math.max(0, plan.GateTowerRadius);
            double towerArea = math.PI_DBL * radius * radius;
            double units = towerArea *
                (math.max(0, gatehouse.LeftTowerHeight)
                 + math.max(0, gatehouse.RightTowerHeight));

            int masonryHeight = math.max(0, gatehouse.BlockHeight - gatehouse.OpeningHeight);
            int masonryThickness = math.max(0, plan.WallThickness * 2);
            double span = math.max(0, gatehouse.TowerSpacing * 2);
            units += span * masonryHeight * masonryThickness;

            units += gatehouse.BridgeLength * (double)gatehouse.BridgeWidth
                   * gatehouse.BridgeDeckHeight;
            units += 2.0 * gatehouse.BridgeLength * gatehouse.BridgeSupportThickness
                   * gatehouse.BridgeSupportHeight;
            units += 2.0 * gatehouse.BridgeLength * gatehouse.BridgeRailThickness
                   * gatehouse.BridgeRailHeight;

            double period = walls.CrenellationMerlonLength + walls.CrenellationGapLength;
            if (period > 0.0 && span > 0.0)
            {
                int crenellationThickness = math.clamp(
                    masonryThickness,
                    walls.CrenellationMinimumThickness,
                    walls.CrenellationMaximumThickness);
                double merlonLength = 0.0;
                for (double distance = 0.0; distance < span; distance += period)
                {
                    merlonLength += math.min(
                        (double)walls.CrenellationMerlonLength,
                        span - distance);
                }

                units += merlonLength * walls.CrenellationHeight * crenellationThickness;
            }

            return units;
        }
    }
}
