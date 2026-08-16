namespace VoxelEngine.Structures.Api
{
    public enum CastleKeepAnnexBuildReadinessIssue : byte
    {
        None = 0,
        MissingPlan,
        InvalidPlan,
    }

    /// <summary>
    /// Runtime-independent trust-boundary check for the semantic keep-annex recipe carried by a
    /// castle topology plan. Runtime may realize this data, but it must never invent it.
    /// </summary>
    public static class CastleKeepAnnexBuildReadiness
    {
        public static bool TryValidate(
            in CastleTopologyPlan topology,
            out CastleKeepAnnexBuildReadinessIssue issue)
        {
            if (!topology.HasKeepAnnexPlan)
            {
                issue = CastleKeepAnnexBuildReadinessIssue.MissingPlan;
                return false;
            }

            CastleKeepAnnexPlan annexes = topology.KeepAnnexes;
            if (!CastleKeepAnnexPlanValidator.TryValidate(in annexes, out _))
            {
                issue = CastleKeepAnnexBuildReadinessIssue.InvalidPlan;
                return false;
            }

            issue = CastleKeepAnnexBuildReadinessIssue.None;
            return true;
        }
    }
}
