using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Compatibility facade for older perimeter callers. Production spatial walls are realized by
    /// CastlePlannedPerimeterRealizer from frozen CastleWallPlan data; this type retains historical
    /// wall/tower/gatehouse entry points without making the planned path depend on their recipes.
    /// </summary>
    public static class CastlePerimeterRealizer
    {
        public static void Walls(ref VoxelBrush brush, in CastlePlan plan, int2[] localVertices)
        {
            CastleWallPlan walls = CastleWallRecipe.Historical();
            CastlePlannedPerimeterRealizer.Walls(
                ref brush, in plan, localVertices, in walls);
        }

        public static void Walls(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localVertices,
            int gateEdgeIndex,
            int2 localGateCentre,
            int gateClearWidth)
        {
            CastleWallPlan walls = CastleWallRecipe.Historical();
            CastlePlannedPerimeterRealizer.WallsWithExplicitGateClearance(
                ref brush,
                in plan,
                localVertices,
                gateEdgeIndex,
                localGateCentre,
                gateClearWidth,
                in walls);
        }

        /// <summary>Compatibility wrapper for a caller that already carries frozen wall style.</summary>
        public static void Walls(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localVertices,
            in CastleWallPlan walls) =>
            CastlePlannedPerimeterRealizer.Walls(
                ref brush, in plan, localVertices, in walls);

        /// <summary>Compatibility wrapper for a planned gate opening and frozen wall style.</summary>
        public static void Walls(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localVertices,
            int gateEdgeIndex,
            int2 localGateCentre,
            in CastleWallPlan walls) =>
            CastlePlannedPerimeterRealizer.Walls(
                ref brush,
                in plan,
                localVertices,
                gateEdgeIndex,
                localGateCentre,
                in walls);

        /// <summary>
        /// Historical tower-variation wrapper retained for compatibility callers. Production
        /// spatial builds consume frozen CastleTowerPlacementSpec variation through
        /// CastlePlannedTowerRealizer instead.
        /// </summary>
        public static void Towers(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localCentres,
            int cornerCount)
        {
            if (localCentres == null)
                return;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int corners = math.clamp(cornerCount, 0, localCentres.Length);
            for (int i = 0; i < localCentres.Length; i++)
            {
                int2 world = ToWorld(in plan, localCentres[i]);
                CastleTowerVariation variation = CastleTowerVariationRecipe.Historical(
                    plan.Seed, i, i < corners);
                int height = plan.TowerHeight + variation.HeightVariation;

                CastleTowerRealizer.Build(
                    ref brush,
                    in plan,
                    new int3(world.x, baseY, world.y),
                    plan.TowerRadius,
                    height,
                    variation.HasRoof);
            }
        }

        /// <summary>
        /// Historical gatehouse wrapper retained for compatibility callers. Production spatial
        /// builds carry a frozen CastleGatehousePlan and call CastlePlannedGatehouseRealizer.
        /// </summary>
        public static void Gatehouse(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2 localGateCentre,
            float2 outward)
        {
            var placement = new CastleGatePlacementSpec
            {
                EdgeIndex = -1,
                Centre = localGateCentre,
                Outward = outward,
            };
            CastleGatehousePlan gatehouse = CastleGatehouseRecipe.Historical(
                in plan, in placement);
            CastlePlannedGatehouseRealizer.Build(
                ref brush, in plan, in placement, in gatehouse);
        }

        private static int2 ToWorld(in CastlePlan plan, int2 local) =>
            new int2(plan.Centre.x + local.x, plan.Centre.z + local.y);
    }
}
