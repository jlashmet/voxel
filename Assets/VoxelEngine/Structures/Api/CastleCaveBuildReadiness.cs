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
        MissingCaveDecorationPlan,
        UnexpectedCaveDecorationPlan,
        InvalidCaveDecorationPlan,
    }

    /// <summary>
    /// Pure admission check for the designed-dungeon to natural-cave handoff. General castle and
    /// dungeon validation remains owned by CastleBuildPreflight; this check exists so Runtime can
    /// require completed natural-space topology and decoration without choosing or repairing it.
    /// </summary>
    public static class CastleCaveBuildReadiness
    {
        public static bool TryValidate(
            CastleSpatialPlan spatial,
            out CastleCaveBuildReadinessIssue issue)
        {
            DungeonPlan dungeon = spatial != null ? spatial.Dungeon : null;
            CavePlan cave = spatial != null ? spatial.Cave : null;
            CastleCaveDecorationPlan decoration = spatial != null ? spatial.CaveDecoration : null;

            if (dungeon == null)
            {
                if (cave != null)
                {
                    issue = CastleCaveBuildReadinessIssue.UnexpectedCavePlan;
                    return false;
                }

                if (decoration != null)
                {
                    issue = CastleCaveBuildReadinessIssue.UnexpectedCaveDecorationPlan;
                    return false;
                }

                issue = CastleCaveBuildReadinessIssue.None;
                return true;
            }

            if (!DungeonPlanValidator.TryValidate(dungeon, out _))
            {
                issue = CastleCaveBuildReadinessIssue.InvalidDungeonPlan;
                return false;
            }

            if (!dungeon.HasCaveExit)
            {
                if (cave != null)
                {
                    issue = CastleCaveBuildReadinessIssue.UnexpectedCavePlan;
                    return false;
                }

                if (decoration != null)
                {
                    issue = CastleCaveBuildReadinessIssue.UnexpectedCaveDecorationPlan;
                    return false;
                }

                issue = CastleCaveBuildReadinessIssue.None;
                return true;
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

            if (decoration == null)
            {
                issue = CastleCaveBuildReadinessIssue.MissingCaveDecorationPlan;
                return false;
            }

            if (!CastleCaveDecorationPlanValidator.TryValidate(
                    cave, decoration, out _))
            {
                issue = CastleCaveBuildReadinessIssue.InvalidCaveDecorationPlan;
                return false;
            }

            issue = CastleCaveBuildReadinessIssue.None;
            return true;
        }
    }
}
