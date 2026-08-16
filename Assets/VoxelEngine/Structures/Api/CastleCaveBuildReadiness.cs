namespace VoxelEngine.Structures.Api
{
    public enum CastleCaveBuildReadinessIssue : byte
    {
        None = 0,
        MissingCavePlan,
        UnexpectedCavePlan,
        InvalidCavePlan,
        CaveEntranceMismatch,
        InvalidDungeonPlan,
    }

    /// <summary>
    /// Pure admission check for the designed-dungeon to natural-cave handoff. General castle and
    /// dungeon validation remains owned by CastleBuildPreflight; this check exists so Runtime can
    /// require a completed natural-space plan without choosing or repairing cave topology itself.
    /// </summary>
    public static class CastleCaveBuildReadiness
    {
        public static bool TryValidate(
            CastleSpatialPlan spatial,
            out CastleCaveBuildReadinessIssue issue)
        {
            DungeonPlan dungeon = spatial != null ? spatial.Dungeon : null;
            CavePlan cave = spatial != null ? spatial.Cave : null;

            if (dungeon == null)
            {
                issue = cave == null
                    ? CastleCaveBuildReadinessIssue.None
                    : CastleCaveBuildReadinessIssue.UnexpectedCavePlan;
                return cave == null;
            }

            if (!DungeonPlanValidator.TryValidate(dungeon, out _))
            {
                issue = CastleCaveBuildReadinessIssue.InvalidDungeonPlan;
                return false;
            }

            if (!dungeon.HasCaveExit)
            {
                issue = cave == null
                    ? CastleCaveBuildReadinessIssue.None
                    : CastleCaveBuildReadinessIssue.UnexpectedCavePlan;
                return cave == null;
            }

            if (cave == null)
            {
                issue = CastleCaveBuildReadinessIssue.MissingCavePlan;
                return false;
            }

            if (!CastleCaveAttachmentValidator.TryValidate(
                    dungeon, cave, out CastleCaveAttachmentIssue attachmentIssue))
            {
                switch (attachmentIssue)
                {
                    case CastleCaveAttachmentIssue.InvalidDungeonPlan:
                    case CastleCaveAttachmentIssue.MissingDungeonPlan:
                        issue = CastleCaveBuildReadinessIssue.InvalidDungeonPlan;
                        return false;
                    case CastleCaveAttachmentIssue.CaveEntranceMismatch:
                        issue = CastleCaveBuildReadinessIssue.CaveEntranceMismatch;
                        return false;
                    case CastleCaveAttachmentIssue.DungeonHasNoCaveThreshold:
                        issue = CastleCaveBuildReadinessIssue.UnexpectedCavePlan;
                        return false;
                    default:
                        issue = CastleCaveBuildReadinessIssue.InvalidCavePlan;
                        return false;
                }
            }

            issue = CastleCaveBuildReadinessIssue.None;
            return true;
        }
    }
}
