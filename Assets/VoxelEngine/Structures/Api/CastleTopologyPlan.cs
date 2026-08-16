namespace VoxelEngine.Structures.Api
{
    public enum CastlePerimeterKind : byte
    {
        Rectangular,
        IrregularQuadrilateral,
        IrregularPolygon,
        Concentric,
    }

    public enum CastleKeepPlacement : byte
    {
        Central,
        Rear,
        HighestGround,
        WallIntegrated,
    }

    public enum CastleWardPattern : byte
    {
        SingleWard,
        InnerAndOuterWards,
    }

    /// <summary>
    /// Planning-only semantic choices for a castle before coordinates or voxel realization are
    /// assigned. Runtime never chooses from this type directly; CastleSpatialPlanner resolves it
    /// into validated spatial geometry before Composition hands the result to realization.
    /// </summary>
    public struct CastleTopologyPlan
    {
        public CastlePerimeterKind Perimeter;
        public CastleKeepPlacement KeepPlacement;
        public CastleWardPattern Wards;
        public int DesiredTowerCount;
        public bool HasPosternGate;
        public CastleSitePlan Site;
        public CastleWallPlan Walls;
        public CastleWallDoorPlan PosternDoor;
        public CastleWallDoorPlan InnerWardDoor;
        public bool HasKeepAnnexPlan;
        public CastleKeepAnnexPlan KeepAnnexes;

        // Keep-turret dimensions are geometric consequences of CastlePlan, but their authored roof
        // variation is frozen here so spatial Runtime never derives keep visual seeds.
        public CastleKeepTurretPlan KeepTurrets;

        // Gatehouse dimensions depend on CastlePlan, so seed-only topology may leave this absent.
        // CastleSpatialPlanner attaches the frozen recipe once dimensional planning is available.
        public bool HasGatehousePlan;
        public CastleGatehousePlan Gatehouse;
    }

    public enum CastleTopologyPlanIssue : byte
    {
        None,
        InvalidPerimeter,
        InvalidKeepPlacement,
        InvalidWardPattern,
        InvalidTowerCount,
        ConcentricRequiresNestedWards,
        InvalidKeepAnnexPlan,
        UnexpectedKeepAnnexPlan,
        InvalidKeepTurretPlan,
        InvalidGatehousePlan,
        InvalidSitePlan,
        InvalidWallPlan,
        InvalidPosternDoorPlan,
        UnexpectedPosternDoorPlan,
        InvalidInnerWardDoorPlan,
        UnexpectedInnerWardDoorPlan,
    }

    /// <summary>
    /// Pure semantic grammar validation performed before any castle coordinates are assigned.
    /// This is the shared contract for generated and caller-supplied topology plans.
    /// </summary>
    public static class CastleTopologyPlanValidator
    {
        public static bool TryValidate(
            in CastleTopologyPlan plan,
            out CastleTopologyPlanIssue issue)
        {
            switch (plan.Perimeter)
            {
                case CastlePerimeterKind.Rectangular:
                case CastlePerimeterKind.IrregularQuadrilateral:
                case CastlePerimeterKind.IrregularPolygon:
                case CastlePerimeterKind.Concentric:
                    break;
                default:
                    issue = CastleTopologyPlanIssue.InvalidPerimeter;
                    return false;
            }

            switch (plan.KeepPlacement)
            {
                case CastleKeepPlacement.Central:
                case CastleKeepPlacement.Rear:
                case CastleKeepPlacement.HighestGround:
                case CastleKeepPlacement.WallIntegrated:
                    break;
                default:
                    issue = CastleTopologyPlanIssue.InvalidKeepPlacement;
                    return false;
            }

            switch (plan.Wards)
            {
                case CastleWardPattern.SingleWard:
                case CastleWardPattern.InnerAndOuterWards:
                    break;
                default:
                    issue = CastleTopologyPlanIssue.InvalidWardPattern;
                    return false;
            }

            int minimumTowerCount = plan.Perimeter switch
            {
                CastlePerimeterKind.IrregularPolygon => 5,
                CastlePerimeterKind.Concentric => 6,
                _ => 4,
            };
            if (plan.DesiredTowerCount < minimumTowerCount || plan.DesiredTowerCount > 8)
            {
                issue = CastleTopologyPlanIssue.InvalidTowerCount;
                return false;
            }

            if (plan.Perimeter == CastlePerimeterKind.Concentric &&
                plan.Wards != CastleWardPattern.InnerAndOuterWards)
            {
                issue = CastleTopologyPlanIssue.ConcentricRequiresNestedWards;
                return false;
            }

            if (!CastleSitePlanValidator.TryValidate(in plan.Site, out _))
            {
                issue = CastleTopologyPlanIssue.InvalidSitePlan;
                return false;
            }

            CastleWallPlan walls = plan.Walls;
            if (!CastleWallPlanValidator.TryValidate(in walls, out _))
            {
                issue = CastleTopologyPlanIssue.InvalidWallPlan;
                return false;
            }

            if (plan.HasPosternGate)
            {
                CastleWallDoorPlan posternDoor = plan.PosternDoor;
                if (!CastleWallDoorPlanValidator.TryValidate(in posternDoor, out _))
                {
                    issue = CastleTopologyPlanIssue.InvalidPosternDoorPlan;
                    return false;
                }
            }
            else if (HasWallDoorRecipe(in plan.PosternDoor))
            {
                issue = CastleTopologyPlanIssue.UnexpectedPosternDoorPlan;
                return false;
            }

            if (plan.Wards == CastleWardPattern.InnerAndOuterWards)
            {
                CastleWallDoorPlan innerWardDoor = plan.InnerWardDoor;
                if (!CastleWallDoorPlanValidator.TryValidate(in innerWardDoor, out _))
                {
                    issue = CastleTopologyPlanIssue.InvalidInnerWardDoorPlan;
                    return false;
                }
            }
            else if (HasWallDoorRecipe(in plan.InnerWardDoor))
            {
                issue = CastleTopologyPlanIssue.UnexpectedInnerWardDoorPlan;
                return false;
            }

            if (plan.HasKeepAnnexPlan)
            {
                CastleKeepAnnexPlan annexes = plan.KeepAnnexes;
                if (!CastleKeepAnnexPlanValidator.TryValidate(in annexes, out _))
                {
                    issue = CastleTopologyPlanIssue.InvalidKeepAnnexPlan;
                    return false;
                }
            }
            else if (plan.KeepAnnexes.HasGreatHallWing ||
                     plan.KeepAnnexes.HasChapelWing ||
                     plan.KeepAnnexes.HasBellTower ||
                     plan.KeepAnnexes.HasRearOriel)
            {
                issue = CastleTopologyPlanIssue.UnexpectedKeepAnnexPlan;
                return false;
            }

            if (plan.KeepTurrets != null &&
                !CastleKeepTurretPlanValidator.TryValidate(plan.KeepTurrets, out _))
            {
                issue = CastleTopologyPlanIssue.InvalidKeepTurretPlan;
                return false;
            }

            if (plan.HasGatehousePlan)
            {
                CastleGatehousePlan gatehouse = plan.Gatehouse;
                if (!CastleGatehousePlanValidator.TryValidate(in gatehouse, out _))
                {
                    issue = CastleTopologyPlanIssue.InvalidGatehousePlan;
                    return false;
                }
            }

            issue = CastleTopologyPlanIssue.None;
            return true;
        }

        private static bool HasWallDoorRecipe(in CastleWallDoorPlan plan) =>
            plan.Width != 0 ||
            plan.Height != 0 ||
            plan.Depth != 0 ||
            plan.OpeningDepthExtra != 0 ||
            plan.LeafWidthReduction != 0 ||
            plan.LeafHeightReduction != 0 ||
            plan.StrapFirstY != 0 ||
            plan.StrapSpacing != 0 ||
            plan.StrapThickness != 0 ||
            plan.StrapDepthExtra != 0;
    }
}
