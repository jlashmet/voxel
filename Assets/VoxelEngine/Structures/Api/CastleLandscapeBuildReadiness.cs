namespace VoxelEngine.Structures.Api
{
    public enum CastleLandscapeBuildReadinessIssue : byte
    {
        None = 0,
        MissingLandscapePlan,
        InvalidLandscapePlan,
    }

    /// <summary>
    /// Runtime-admission readiness for planner-owned stage-8 landscape dressing. Structural castle
    /// validation intentionally permits an unattached landscape during intermediate planning; this
    /// helper is the stricter boundary used once a castle is expected to be fully runtime-ready.
    /// </summary>
    public static class CastleLandscapeBuildReadiness
    {
        public static bool TryValidate(
            CastleSpatialPlan spatial,
            out CastleLandscapeBuildReadinessIssue issue)
        {
            CastleLandscapePlan landscape = spatial?.Landscape;
            if (landscape == null)
            {
                issue = CastleLandscapeBuildReadinessIssue.MissingLandscapePlan;
                return false;
            }

            if (!CastleLandscapePlanValidator.TryValidate(landscape, out _))
            {
                issue = CastleLandscapeBuildReadinessIssue.InvalidLandscapePlan;
                return false;
            }

            issue = CastleLandscapeBuildReadinessIssue.None;
            return true;
        }
    }
}
