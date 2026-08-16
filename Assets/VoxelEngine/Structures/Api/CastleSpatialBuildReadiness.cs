namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Canonical runtime-admission contract for a completed spatial castle plan. General spatial
    /// validation deliberately permits intermediate planning snapshots; this stricter check proves
    /// that every planner-owned sub-plan required by Runtime is present, valid, and attached to the
    /// same semantic castle before voxel mutation begins.
    /// </summary>
    public static class CastleSpatialBuildReadiness
    {
        public static bool TryValidate(
            in CastlePlan plan,
            CastleSpatialPlan spatial,
            out CastleSpatialBuildReadinessIssue issue)
        {
            if (spatial == null)
            {
                issue = CastleSpatialBuildReadinessIssue.MissingSpatialPlan;
                return false;
            }

            if (spatial.KeepRequiresTerrainResolution)
            {
                issue = CastleSpatialBuildReadinessIssue.KeepRequiresTerrainResolution;
                return false;
            }

            CastleTopologyPlan topology = spatial.Topology;
            if (!topology.HasGatehousePlan)
            {
                issue = CastleSpatialBuildReadinessIssue.MissingGatehousePlan;
                return false;
            }

            CastleGatehousePlan gatehouse = topology.Gatehouse;
            if (!CastleGatehousePlanValidator.TryValidate(in gatehouse, out _) ||
                !CastleGatehousePlanValidator.TryValidateTowerDetails(
                    in gatehouse, plan.FloorHeight, out _))
            {
                issue = CastleSpatialBuildReadinessIssue.InvalidGatehousePlan;
                return false;
            }

            if (topology.KeepTurrets == null)
            {
                issue = CastleSpatialBuildReadinessIssue.MissingKeepTurretPlan;
                return false;
            }

            if (!CastleKeepTurretPlanValidator.TryValidate(
                    topology.KeepTurrets, out _))
            {
                issue = CastleSpatialBuildReadinessIssue.InvalidKeepTurretPlan;
                return false;
            }

            CastleSpatialProjection keepProjection = CastleSpatialProjection.Create(in plan, spatial);
            CastlePlan keepPlan = keepProjection.KeepPlan;
            if (!CastleKeepTurretPlanValidator.TryValidateSlits(
                    in keepPlan, topology.KeepTurrets, out _))
            {
                issue = CastleSpatialBuildReadinessIssue.InvalidKeepTurretPlan;
                return false;
            }

            if (!CastleTowerSlitPlanCompletion.TryValidate(
                    in plan,
                    spatial,
                    out CastleTowerSlitBuildReadinessIssue towerSlitIssue))
            {
                issue = towerSlitIssue == CastleTowerSlitBuildReadinessIssue.InvalidSlitPlan
                    ? CastleSpatialBuildReadinessIssue.InvalidTowerSlitPlan
                    : CastleSpatialBuildReadinessIssue.MissingTowerSlitPlan;
                return false;
            }

            CastleKeepFloorPlan[] floors = spatial.KeepFloors;
            if (!CastleKeepFloorPlanValidator.TryValidate(
                    in plan, floors, out CastleKeepFloorPlanIssue floorIssue))
            {
                issue = floorIssue == CastleKeepFloorPlanIssue.MissingFloors ||
                        floorIssue == CastleKeepFloorPlanIssue.FloorCountMismatch
                    ? CastleSpatialBuildReadinessIssue.MissingKeepFloorPlan
                    : CastleSpatialBuildReadinessIssue.InvalidKeepFloorPlan;
                return false;
            }

            CastleKeepCirculationPlan circulation = spatial.KeepCirculation;
            if (!CastleKeepCirculationPlanValidator.TryValidate(
                    in plan, in circulation, out _))
            {
                issue = CastleSpatialBuildReadinessIssue.InvalidKeepCirculationPlan;
                return false;
            }

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleKeepFace expectedEntranceFace = CastleKeepFacadePlanner.FacingPrimaryGate(
                spatial.KeepCentre, in primaryGate);
            if (circulation.EntranceFace != expectedEntranceFace)
            {
                issue = CastleSpatialBuildReadinessIssue.InvalidKeepCirculationPlan;
                return false;
            }

            CastleKeepWindowSpec[] windows = spatial.KeepWindows;
            if (windows == null || windows.Length == 0)
            {
                issue = CastleSpatialBuildReadinessIssue.MissingKeepWindowPlan;
                return false;
            }

            if (!CastleKeepWindowPlanValidator.TryValidate(
                    in plan, windows, circulation.EntranceFace, out _))
            {
                issue = CastleSpatialBuildReadinessIssue.InvalidKeepWindowPlan;
                return false;
            }

            if (!CastleKeepAnnexBuildReadiness.TryValidate(
                    in topology,
                    out CastleKeepAnnexBuildReadinessIssue annexIssue))
            {
                issue = annexIssue == CastleKeepAnnexBuildReadinessIssue.MissingPlan
                    ? CastleSpatialBuildReadinessIssue.MissingKeepAnnexPlan
                    : CastleSpatialBuildReadinessIssue.InvalidKeepAnnexPlan;
                return false;
            }

            if (!CastleLandscapeBuildReadiness.TryValidate(
                    spatial, out CastleLandscapeBuildReadinessIssue landscapeIssue))
            {
                issue = landscapeIssue == CastleLandscapeBuildReadinessIssue.MissingLandscapePlan
                    ? CastleSpatialBuildReadinessIssue.MissingLandscapePlan
                    : CastleSpatialBuildReadinessIssue.InvalidLandscapePlan;
                return false;
            }

            DungeonPlan dungeon = spatial.Dungeon;
            if (dungeon == null)
            {
                issue = CastleSpatialBuildReadinessIssue.MissingDungeonPlan;
                return false;
            }

            if (!DungeonPlanValidator.TryValidate(dungeon, out _))
            {
                issue = CastleSpatialBuildReadinessIssue.InvalidDungeonPlan;
                return false;
            }

            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
            if (!dungeon.Entrance.Equals(projection.TrapdoorCentre))
            {
                issue = CastleSpatialBuildReadinessIssue.DungeonEntranceMismatch;
                return false;
            }

            if (!CastleCaveBuildReadiness.TryValidate(
                    spatial, out CastleCaveBuildReadinessIssue caveIssue))
            {
                issue = MapCaveReadiness(caveIssue);
                return false;
            }

            issue = CastleSpatialBuildReadinessIssue.None;
            return true;
        }

        private static CastleSpatialBuildReadinessIssue MapCaveReadiness(
            CastleCaveBuildReadinessIssue issue)
        {
            switch (issue)
            {
                case CastleCaveBuildReadinessIssue.MissingCavePlan:
                    return CastleSpatialBuildReadinessIssue.MissingCavePlan;
                case CastleCaveBuildReadinessIssue.UnexpectedCavePlan:
                    return CastleSpatialBuildReadinessIssue.UnexpectedCavePlan;
                case CastleCaveBuildReadinessIssue.InvalidCavePlan:
                    return CastleSpatialBuildReadinessIssue.InvalidCavePlan;
                case CastleCaveBuildReadinessIssue.CaveEntranceMismatch:
                    return CastleSpatialBuildReadinessIssue.CaveEntranceMismatch;
                case CastleCaveBuildReadinessIssue.InvalidDungeonPlan:
                    return CastleSpatialBuildReadinessIssue.InvalidDungeonPlan;
                case CastleCaveBuildReadinessIssue.MissingCaveDecorationPlan:
                    return CastleSpatialBuildReadinessIssue.MissingCaveDecorationPlan;
                case CastleCaveBuildReadinessIssue.UnexpectedCaveDecorationPlan:
                    return CastleSpatialBuildReadinessIssue.UnexpectedCaveDecorationPlan;
                case CastleCaveBuildReadinessIssue.InvalidCaveDecorationPlan:
                    return CastleSpatialBuildReadinessIssue.InvalidCaveDecorationPlan;
                default:
                    return CastleSpatialBuildReadinessIssue.None;
            }
        }
    }
}
