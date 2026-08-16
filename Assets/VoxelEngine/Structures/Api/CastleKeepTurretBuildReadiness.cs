namespace VoxelEngine.Structures.Api
{
    public enum CastleKeepTurretBuildReadinessIssue : byte
    {
        None,
        MissingPlan,
        InvalidPlan,
    }

    /// <summary>
    /// Runtime-admission check for planner-owned keep-corner turret variation. Spatial realization
    /// must receive the frozen roof choices rather than deriving them from the seed during voxel
    /// mutation.
    /// </summary>
    public static class CastleKeepTurretBuildReadiness
    {
        public static bool TryValidate(
            in CastleTopologyPlan topology,
            out CastleKeepTurretBuildReadinessIssue issue)
        {
            if (topology.KeepTurrets == null)
            {
                issue = CastleKeepTurretBuildReadinessIssue.MissingPlan;
                return false;
            }

            if (!CastleKeepTurretPlanValidator.TryValidate(topology.KeepTurrets, out _))
            {
                issue = CastleKeepTurretBuildReadinessIssue.InvalidPlan;
                return false;
            }

            issue = CastleKeepTurretBuildReadinessIssue.None;
            return true;
        }
    }
}
