using System;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Produces semantic castle topology choices from independent named seed streams. This planner
    /// deliberately stops before spatial placement; a later layout solver can turn these choices
    /// into walls, towers, gates, wards, and building coordinates without changing their identity.
    /// </summary>
    public static class CastleLayoutPlanner
    {
        public static CastleTopologyPlan Create(uint seed)
        {
            var layoutRng = new Random(CastleSeedPartition.Derive(seed, CastleSeedDomain.Layout));
            var wallRng = new Random(CastleSeedPartition.Derive(seed, CastleSeedDomain.Walls));
            var keepRng = new Random(CastleSeedPartition.Derive(seed, CastleSeedDomain.Keep));

            CastlePerimeterKind perimeter = ChoosePerimeter(ref layoutRng);
            CastleWardPattern wards = perimeter == CastlePerimeterKind.Concentric
                ? CastleWardPattern.InnerAndOuterWards
                : (layoutRng.NextInt(0, 100) < 30
                    ? CastleWardPattern.InnerAndOuterWards
                    : CastleWardPattern.SingleWard);
            CastleKeepAnnexPlan annexes = CastleKeepAnnexPlanner.Create(seed);

            var plan = new CastleTopologyPlan
            {
                Perimeter = perimeter,
                KeepPlacement = ChooseKeepPlacement(ref keepRng),
                Wards = wards,
                DesiredTowerCount = ChooseTowerCount(perimeter, ref wallRng),
                HasPosternGate = wallRng.NextInt(0, 100) < 25,
                Site = CastleSitePlanner.Create(seed),
                Walls = CastleWallPlanner.Create(),
                HasKeepAnnexPlan = true,
                KeepAnnexes = annexes,
                KeepTurrets = CastleKeepTurretPlanner.Create(seed),
            };

            if (!CastleTopologyPlanValidator.TryValidate(
                    in plan, out CastleTopologyPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle topology planning produced an invalid plan: {issue}.");
            }

            return plan;
        }

        private static CastlePerimeterKind ChoosePerimeter(ref Random rng)
        {
            int roll = rng.NextInt(0, 100);
            if (roll < 25) return CastlePerimeterKind.Rectangular;
            if (roll < 50) return CastlePerimeterKind.IrregularQuadrilateral;
            if (roll < 85) return CastlePerimeterKind.IrregularPolygon;
            return CastlePerimeterKind.Concentric;
        }

        private static CastleKeepPlacement ChooseKeepPlacement(ref Random rng)
        {
            int roll = rng.NextInt(0, 100);
            if (roll < 35) return CastleKeepPlacement.Central;
            if (roll < 70) return CastleKeepPlacement.Rear;
            if (roll < 90) return CastleKeepPlacement.HighestGround;
            return CastleKeepPlacement.WallIntegrated;
        }

        private static int ChooseTowerCount(CastlePerimeterKind perimeter, ref Random rng)
        {
            switch (perimeter)
            {
                case CastlePerimeterKind.Rectangular:
                case CastlePerimeterKind.IrregularQuadrilateral:
                    return rng.NextInt(4, 7);
                case CastlePerimeterKind.IrregularPolygon:
                    return rng.NextInt(5, 9);
                case CastlePerimeterKind.Concentric:
                    return rng.NextInt(6, 9);
                default:
                    return 4;
            }
        }
    }
}
