using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes keep floor slabs and dispatches each floor to the existing room-furnishing recipes.
    /// Compatibility and planned semantics have explicit entry points; individual furniture
    /// geometry remains owned by CastleRoomFurnisher.
    /// </summary>
    internal static class CastleKeepFloorRealizer
    {
        internal static void BuildCompatibility(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int baseY,
            int floors)
        {
            for (int floor = 0; floor < floors; floor++)
            {
                int y = baseY + floor * plan.FloorHeight;
                BuildFloorSlab(ref brush, min, size, y, floor);
                CastleRoomFurnisher.Furnish(
                    ref brush, in plan, min, size, y, floor);
            }
        }

        /// <summary>Realizes planner-owned semantic floor purposes. Null is never a mode switch.</summary>
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int baseY,
            int floors,
            CastleKeepFloorPlan[] roomPlans)
        {
            if (roomPlans == null || roomPlans.Length != floors)
            {
                throw new InvalidOperationException(
                    "Planned keep floor realization requires one semantic room plan per floor.");
            }

            for (int floor = 0; floor < floors; floor++)
            {
                int y = baseY + floor * plan.FloorHeight;
                BuildFloorSlab(ref brush, min, size, y, floor);

                CastleKeepFloorPlan roomPlan = roomPlans[floor];
                int furnishingRecipe = FurnishingRecipe(in roomPlan, floor);
                CastleRoomFurnisher.FurnishPlanned(
                    ref brush,
                    in plan,
                    min,
                    size,
                    y,
                    furnishingRecipe,
                    roomPlan.Accents);
            }
        }

        private static void BuildFloorSlab(
            ref VoxelBrush brush,
            int3 min,
            int3 size,
            int y,
            int floor)
        {
            if (floor <= 0) return;
            brush.Box(
                new int3(min.x + 8, y, min.z + 8),
                new int3(size.x - 16, 3, size.z - 16),
                Mat.Wood);
        }

        private static int FurnishingRecipe(in CastleKeepFloorPlan roomPlan, int expectedFloor)
        {
            if (roomPlan.FloorIndex != expectedFloor)
                throw new InvalidOperationException("Castle keep floor plans must be ordered by floor index.");

            switch (roomPlan.Purpose)
            {
                case CastleKeepFloorPurpose.GreatHall:
                    if (roomPlan.HasPartition)
                        throw new InvalidOperationException("Great-hall floor cannot use the partitioned recipe.");
                    return 0;
                case CastleKeepFloorPurpose.Bedchamber:
                    if (roomPlan.HasPartition)
                        throw new InvalidOperationException("Bedchamber floor cannot use the partitioned recipe.");
                    return 1;
                case CastleKeepFloorPurpose.LibraryAndStores:
                    if (!roomPlan.HasPartition)
                        throw new InvalidOperationException("Library/store floor requires the partitioned recipe.");
                    return 2;
                default:
                    throw new InvalidOperationException($"Unsupported keep-floor purpose: {roomPlan.Purpose}.");
            }
        }
    }
}
