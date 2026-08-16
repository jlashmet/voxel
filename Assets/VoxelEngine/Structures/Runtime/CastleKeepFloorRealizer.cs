using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes keep floor slabs and dispatches each floor to the existing room-furnishing recipes.
    /// It accepts either the compatibility floor-index contract or planner-owned semantic purposes;
    /// individual furniture geometry remains owned by CastleRoomFurnisher.
    /// </summary>
    internal static class CastleKeepFloorRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int baseY,
            int floors,
            CastleKeepFloorPlan[] roomPlans)
        {
            for (int f = 0; f < floors; f++)
            {
                int y = baseY + f * plan.FloorHeight;
                if (f > 0)
                {
                    brush.Box(new int3(min.x + 8, y, min.z + 8),
                              new int3(size.x - 16, 3, size.z - 16), Mat.Wood);
                }

                if (roomPlans == null)
                {
                    CastleRoomFurnisher.Furnish(ref brush, in plan, min, size, y, f);
                    continue;
                }

                CastleKeepFloorPlan roomPlan = roomPlans[f];
                int furnishingRecipe = FurnishingRecipe(in roomPlan, f);
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
