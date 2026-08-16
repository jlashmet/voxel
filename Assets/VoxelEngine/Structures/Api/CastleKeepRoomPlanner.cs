using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Semantic purpose of one authored keep floor before furnishing is realized.</summary>
    public enum CastleKeepFloorPurpose : byte
    {
        GreatHall,
        Bedchamber,
        LibraryAndStores,
    }

    /// <summary>
    /// Pure room semantics for one keep floor. Geometry and furniture remain downstream concerns.
    /// </summary>
    public readonly struct CastleKeepFloorPlan
    {
        public readonly int FloorIndex;
        public readonly CastleKeepFloorPurpose Purpose;
        public readonly bool HasPartition;

        internal CastleKeepFloorPlan(
            int floorIndex,
            CastleKeepFloorPurpose purpose,
            bool hasPartition)
        {
            FloorIndex = floorIndex;
            Purpose = purpose;
            HasPartition = hasPartition;
        }
    }

    /// <summary>
    /// Converts the keep's floor stack into explicit semantic room purposes. This intentionally
    /// preserves the current furnishing recipe: ground floor great hall, first-floor bedchamber,
    /// and partitioned library/store floors above. Runtime should realize this plan, not infer
    /// purpose from a floor-number switch.
    /// </summary>
    public static class CastleKeepRoomPlanner
    {
        public static CastleKeepFloorPlan[] Create(in CastlePlan plan)
        {
            if (plan.Floors <= 0)
                throw new ArgumentOutOfRangeException(nameof(plan.Floors));

            var floors = new CastleKeepFloorPlan[plan.Floors];
            for (int floor = 0; floor < floors.Length; floor++)
            {
                CastleKeepFloorPurpose purpose;
                bool partition;
                if (floor == 0)
                {
                    purpose = CastleKeepFloorPurpose.GreatHall;
                    partition = false;
                }
                else if (floor == 1)
                {
                    purpose = CastleKeepFloorPurpose.Bedchamber;
                    partition = false;
                }
                else
                {
                    purpose = CastleKeepFloorPurpose.LibraryAndStores;
                    partition = true;
                }

                floors[floor] = new CastleKeepFloorPlan(floor, purpose, partition);
            }

            return floors;
        }
    }
}
