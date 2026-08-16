using System;

namespace VoxelEngine.Structures.Api
{
    public enum CastleKeepFloorPlanIssue : byte
    {
        None = 0,
        MissingFloors,
        FloorCountMismatch,
        FloorIndexMismatch,
        PurposeMismatch,
        PartitionMismatch,
    }

    /// <summary>
    /// Structural contract for the currently supported keep-floor recipe. The planner owns the
    /// semantic purpose of each floor; runtime may rely on this validator instead of inferring
    /// meaning from a floor-number switch.
    /// </summary>
    public static class CastleKeepFloorPlanValidator
    {
        public static bool TryValidate(
            in CastlePlan plan,
            CastleKeepFloorPlan[] floors,
            out CastleKeepFloorPlanIssue issue)
        {
            if (floors == null || floors.Length == 0)
            {
                issue = CastleKeepFloorPlanIssue.MissingFloors;
                return false;
            }

            if (floors.Length != plan.Floors)
            {
                issue = CastleKeepFloorPlanIssue.FloorCountMismatch;
                return false;
            }

            for (int floor = 0; floor < floors.Length; floor++)
            {
                CastleKeepFloorPlan planned = floors[floor];
                if (planned.FloorIndex != floor)
                {
                    issue = CastleKeepFloorPlanIssue.FloorIndexMismatch;
                    return false;
                }

                CastleKeepFloorPurpose expectedPurpose = floor == 0
                    ? CastleKeepFloorPurpose.GreatHall
                    : floor == 1
                        ? CastleKeepFloorPurpose.Bedchamber
                        : CastleKeepFloorPurpose.LibraryAndStores;
                if (planned.Purpose != expectedPurpose)
                {
                    issue = CastleKeepFloorPlanIssue.PurposeMismatch;
                    return false;
                }

                bool expectedPartition = floor >= 2;
                if (planned.HasPartition != expectedPartition)
                {
                    issue = CastleKeepFloorPlanIssue.PartitionMismatch;
                    return false;
                }
            }

            issue = CastleKeepFloorPlanIssue.None;
            return true;
        }

        public static void RequireValid(in CastlePlan plan, CastleKeepFloorPlan[] floors)
        {
            if (TryValidate(in plan, floors, out CastleKeepFloorPlanIssue issue))
                return;

            throw new InvalidOperationException(
                $"Castle keep floor plan is invalid: {issue}.");
        }
    }
}
