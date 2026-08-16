using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleTowerPlacementBuildReadinessIssue : byte
    {
        None,
        MissingOuterTowerSlits,
        InvalidOuterTowerSlits,
        MissingInnerTowerSlits,
        InvalidInnerTowerSlits,
    }

    /// <summary>
    /// Runtime-admission checks for authored tower details that are not part of placement geometry.
    /// Spatial validation proves where towers stand; this proves every planned tower can be
    /// realized without Runtime falling back to seeded slit generation or throwing mid-build.
    /// </summary>
    public static class CastleTowerPlacementBuildReadiness
    {
        public static bool TryValidate(
            in CastlePlan plan,
            CastleSpatialPlan spatial,
            out CastleTowerPlacementBuildReadinessIssue issue)
        {
            if (spatial == null)
            {
                issue = CastleTowerPlacementBuildReadinessIssue.MissingOuterTowerSlits;
                return false;
            }

            CastleTowerPlacementSpec[] outer = spatial.Towers;
            if (outer == null)
            {
                issue = CastleTowerPlacementBuildReadinessIssue.MissingOuterTowerSlits;
                return false;
            }

            for (int i = 0; i < outer.Length; i++)
            {
                CastleTowerPlacementSpec tower = outer[i];
                if (tower.Slits == null)
                {
                    issue = CastleTowerPlacementBuildReadinessIssue.MissingOuterTowerSlits;
                    return false;
                }

                int realizedHeight = plan.TowerHeight + math.max(0, tower.HeightVariation);
                if (!CastleTowerSlitPlanValidator.TryValidate(
                        tower.Slits, realizedHeight, plan.FloorHeight, out _))
                {
                    issue = CastleTowerPlacementBuildReadinessIssue.InvalidOuterTowerSlits;
                    return false;
                }
            }

            CastleTowerPlacementSpec[] inner = spatial.InnerTowers;
            if (inner == null)
            {
                issue = CastleTowerPlacementBuildReadinessIssue.MissingInnerTowerSlits;
                return false;
            }

            int innerBaseHeight = CastleInnerWardTowerPlanner.Height(in plan);
            for (int i = 0; i < inner.Length; i++)
            {
                CastleTowerPlacementSpec tower = inner[i];
                if (tower.Slits == null)
                {
                    issue = CastleTowerPlacementBuildReadinessIssue.MissingInnerTowerSlits;
                    return false;
                }

                int realizedHeight = innerBaseHeight + math.max(0, tower.HeightVariation);
                if (!CastleTowerSlitPlanValidator.TryValidate(
                        tower.Slits, realizedHeight, plan.FloorHeight, out _))
                {
                    issue = CastleTowerPlacementBuildReadinessIssue.InvalidInnerTowerSlits;
                    return false;
                }
            }

            issue = CastleTowerPlacementBuildReadinessIssue.None;
            return true;
        }
    }
}
