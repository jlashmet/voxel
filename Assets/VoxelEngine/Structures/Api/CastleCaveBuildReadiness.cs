using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleCaveBuildReadinessIssue : byte
    {
        None = 0,
        MissingCavePlan,
        UnexpectedCavePlan,
        InvalidCavePlan,
        CaveEntranceMismatch,
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

            if (!CavePlanValidator.TryValidate(cave, out _))
            {
                issue = CastleCaveBuildReadinessIssue.InvalidCavePlan;
                return false;
            }

            DungeonRoomPlan threshold = dungeon.Rooms[dungeon.CaveThresholdRoomId];
            int3 expectedEntrance = new int3(
                threshold.Centre.x,
                threshold.Centre.y - threshold.Size.y / 2,
                threshold.Centre.z);
            if (!cave.Entrance.Equals(expectedEntrance))
            {
                issue = CastleCaveBuildReadinessIssue.CaveEntranceMismatch;
                return false;
            }

            issue = CastleCaveBuildReadinessIssue.None;
            return true;
        }
    }
}
