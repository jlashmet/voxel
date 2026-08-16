using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Pure structural validation result for a planned castle.</summary>
    public enum CastlePlanIssue : byte
    {
        None = 0,
        InvalidSite,
        InvalidBailey,
        InvalidWalls,
        InvalidTowers,
        InvalidKeep,
        KeepOutsideBailey,
        KeepFloorStackMismatch,
        PlateauDoesNotContainBailey,
    }

    /// <summary>
    /// Validates relationships that must hold before any voxel writes are attempted.
    /// Runtime-specific concerns such as mutation capacity and write budgets stay with the
    /// realization layer.
    /// </summary>
    public static class CastlePlanValidator
    {
        public static bool TryValidate(in CastlePlan plan, out CastlePlanIssue issue)
        {
            if (plan.PlateauRadius <= 0 || plan.PlateauHeight <= 0 || plan.CliffDrop <= 0)
            {
                issue = CastlePlanIssue.InvalidSite;
                return false;
            }

            if (plan.BaileyHalfX <= 0 || plan.BaileyHalfZ <= 0)
            {
                issue = CastlePlanIssue.InvalidBailey;
                return false;
            }

            if (plan.WallHeight <= 0 || plan.WallThickness <= 0)
            {
                issue = CastlePlanIssue.InvalidWalls;
                return false;
            }

            if (plan.TowerRadius <= 0 || plan.TowerHeight <= 0 ||
                plan.GateTowerRadius <= 0 || plan.GateTowerHeight <= 0)
            {
                issue = CastlePlanIssue.InvalidTowers;
                return false;
            }

            if (plan.KeepHalfX <= 0 || plan.KeepHalfZ <= 0 || plan.KeepHeight <= 0 ||
                plan.FloorHeight <= 0 || plan.Floors <= 0)
            {
                issue = CastlePlanIssue.InvalidKeep;
                return false;
            }

            if (plan.KeepHalfX + plan.WallThickness >= plan.BaileyHalfX ||
                plan.KeepHalfZ + plan.WallThickness >= plan.BaileyHalfZ)
            {
                issue = CastlePlanIssue.KeepOutsideBailey;
                return false;
            }

            if (plan.KeepHeight != plan.Floors * plan.FloorHeight)
            {
                issue = CastlePlanIssue.KeepFloorStackMismatch;
                return false;
            }

            int baileyCornerRadius = (int)math.ceil(
                math.sqrt(plan.BaileyHalfX * plan.BaileyHalfX +
                          plan.BaileyHalfZ * plan.BaileyHalfZ));
            if (plan.PlateauRadius <= baileyCornerRadius)
            {
                issue = CastlePlanIssue.PlateauDoesNotContainBailey;
                return false;
            }

            issue = CastlePlanIssue.None;
            return true;
        }
    }
}
