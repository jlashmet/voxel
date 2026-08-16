using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleCaveAttachmentIssue : byte
    {
        None = 0,
        MissingDungeonPlan,
        DungeonHasNoCaveThreshold,
        InvalidDungeonPlan,
        InvalidCavePlan,
        CaveEntranceMismatch,
    }

    /// <summary>
    /// Pure structural validation for an optional natural-cave attachment. Partial castle plans
    /// may omit CavePlan entirely; once a cave is attached, however, it must belong to a valid
    /// designed dungeon cave threshold and share that threshold's exact world-space entrance.
    /// Runtime readiness (which may require a cave to exist) remains a separate concern.
    /// </summary>
    public static class CastleCaveAttachmentValidator
    {
        public static bool TryValidate(
            DungeonPlan dungeon,
            CavePlan cave,
            out CastleCaveAttachmentIssue issue)
        {
            if (cave == null)
            {
                issue = CastleCaveAttachmentIssue.None;
                return true;
            }

            if (dungeon == null)
            {
                issue = CastleCaveAttachmentIssue.MissingDungeonPlan;
                return false;
            }

            if (!DungeonPlanValidator.TryValidate(dungeon, out _))
            {
                issue = CastleCaveAttachmentIssue.InvalidDungeonPlan;
                return false;
            }

            if (!dungeon.HasCaveExit)
            {
                issue = CastleCaveAttachmentIssue.DungeonHasNoCaveThreshold;
                return false;
            }

            if (!CavePlanValidator.TryValidate(cave, out _))
            {
                issue = CastleCaveAttachmentIssue.InvalidCavePlan;
                return false;
            }

            DungeonRoomPlan threshold = dungeon.Rooms[dungeon.CaveThresholdRoomId];
            int3 expectedEntrance = new int3(
                threshold.Centre.x,
                threshold.Centre.y - threshold.Size.y / 2,
                threshold.Centre.z);
            if (!cave.Entrance.Equals(expectedEntrance))
            {
                issue = CastleCaveAttachmentIssue.CaveEntranceMismatch;
                return false;
            }

            issue = CastleCaveAttachmentIssue.None;
            return true;
        }
    }
}
