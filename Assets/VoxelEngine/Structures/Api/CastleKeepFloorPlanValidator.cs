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
        MissingAccentPlan,
        InvalidAccentPlan,
    }

    /// <summary>
    /// Structural contract for supported keep-floor semantics. Runtime may rely on this validator
    /// instead of inferring meaning from physical floor index: anchor floors are fixed, while
    /// intermediate upper floors may use either supported upper-room recipe.
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

                if (!PurposeAllowed(floor, floors.Length, planned.Purpose))
                {
                    issue = CastleKeepFloorPlanIssue.PurposeMismatch;
                    return false;
                }

                bool expectedPartition =
                    planned.Purpose == CastleKeepFloorPurpose.LibraryAndStores;
                if (planned.HasPartition != expectedPartition)
                {
                    issue = CastleKeepFloorPlanIssue.PartitionMismatch;
                    return false;
                }

                if (planned.Accents == null)
                {
                    issue = CastleKeepFloorPlanIssue.MissingAccentPlan;
                    return false;
                }

                if (!CastleRoomAccentPlanValidator.TryValidate(
                        in plan, planned.Accents, out _))
                {
                    issue = CastleKeepFloorPlanIssue.InvalidAccentPlan;
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

        private static bool PurposeAllowed(
            int floor,
            int floorCount,
            CastleKeepFloorPurpose purpose)
        {
            if (floor == 0)
                return purpose == CastleKeepFloorPurpose.GreatHall;

            if (floor == 1)
                return purpose == CastleKeepFloorPurpose.Bedchamber;

            // Keeps with at least three floors retain a guaranteed library/storey at the top.
            if (floor == floorCount - 1)
                return purpose == CastleKeepFloorPurpose.LibraryAndStores;

            return purpose == CastleKeepFloorPurpose.Bedchamber ||
                   purpose == CastleKeepFloorPurpose.LibraryAndStores;
        }
    }
}
