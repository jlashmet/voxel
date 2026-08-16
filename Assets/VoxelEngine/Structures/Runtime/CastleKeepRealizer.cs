using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Incrementally realizes the occupied keep from a precomputed castle plan.
    ///
    /// The migrated substages deliberately preserve the legacy order and geometry. The final
    /// roof/annex substage remains on the migration fallback until its larger helper graph is
    /// extracted as one unit.
    /// </summary>
    internal static class CastleKeepRealizer
    {
        /// <summary>Compatibility path for castles without explicit keep-floor semantics.</summary>
        internal static bool TryStep(ref VoxelBrush brush, in CastlePlan plan, ref int stage) =>
            TryStepCore(ref brush, in plan, null, ref stage);

        /// <summary>
        /// Spatial path: realizes supplied semantic floor purposes without choosing them from the
        /// physical floor index. Existing furnishing recipes remain behavior-compatible.
        /// </summary>
        internal static bool TryStep(
            ref VoxelBrush brush,
            in CastlePlan plan,
            CastleKeepFloorPlan[] roomPlans,
            ref int stage)
        {
            if (roomPlans == null || roomPlans.Length != plan.Floors)
                throw new InvalidOperationException("Castle keep realization requires one planned room per floor.");
            return TryStepCore(ref brush, in plan, roomPlans, ref stage);
        }

        private static bool TryStepCore(
            ref VoxelBrush brush,
            in CastlePlan plan,
            CastleKeepFloorPlan[] roomPlans,
            ref int stage)
        {
            if (stage < 0 || stage >= 6) return false;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.KeepHalfX;
            int hz = plan.KeepHalfZ;
            var min = new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz + 60);
            var size = new int3(hx * 2, plan.KeepHeight, hz * 2);
            int floors = plan.Floors;

            switch (stage)
            {
                case 0:
                    BuildShell(ref brush, min, size, baseY);
                    break;

                case 1:
                    BuildCornerTurrets(ref brush, in plan, min, size, baseY);
                    break;

                case 2:
                    BuildFloorsAndRooms(ref brush, in plan, min, size, baseY, floors, roomPlans);
                    break;

                case 3:
                    BuildCirculation(ref brush, in plan, min, size, baseY, floors);
                    break;

                case 4:
                    BuildWindows(ref brush, in plan, min, size, baseY, floors);
                    break;

                case 5:
                    BuildFacade(ref brush, in plan, min, size, baseY, floors);
                    if (roomPlans == null)
                        CastleRearOrielRealizer.Build(ref brush, in plan);
                    break;
            }

            stage++;
            return true;
        }

        private static void BuildShell(ref VoxelBrush brush, int3 min, int3 size, int baseY) =>
            CastleKeepShellRealizer.Build(ref brush, min, size, baseY);

        private static void BuildCornerTurrets(ref VoxelBrush brush, in CastlePlan plan,
                                               int3 min, int3 size, int baseY)
        {
            for (int i = 0; i < 4; i++)
            {
                int cx = min.x + (i % 2 == 0 ? 0 : size.x);
                int cz = min.z + (i < 2 ? 0 : size.z);
                CastleTowerRealizer.Build(ref brush, in plan, new int3(cx, baseY, cz), 26,
                                          plan.KeepHeight + 30, true);
            }
        }

        private static void BuildFloorsAndRooms(
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

        private static void BuildCirculation(ref VoxelBrush brush, in CastlePlan plan,
                                             int3 min, int3 size, int baseY, int floors)
        {
            int entranceX = plan.Centre.x;
            brush.Arch(new int3(entranceX - 15, baseY + 1, min.z - 1),
                       30, 34, 10, 2, Mat.Empty);
            brush.Box(new int3(entranceX - 15, baseY + 2, min.z + 9),
                      new int3(4, 29, 3), Mat.Wood);
            brush.Box(new int3(entranceX + 11, baseY + 2, min.z + 9),
                      new int3(4, 29, 3), Mat.Wood);
            brush.Box(new int3(entranceX - 9, baseY + 1, min.z + 8),
                      new int3(18, 24, size.z / 2 - 28), Mat.Empty);

            int grandX = plan.Centre.x - 68;
            int grandZ = min.z + 28;
            const int grandWidth = 18;
            const int grandRise = 2;
            const int grandRun = 3;
            int grandSteps = plan.FloorHeight / grandRise;
            brush.Box(new int3(grandX, baseY + 1, grandZ),
                      new int3(grandWidth, plan.FloorHeight + 18, grandSteps * grandRun), Mat.Empty);
            brush.Stairs(new int3(grandX, baseY + 1, grandZ), grandWidth,
                         grandSteps, grandRise, grandRun, 2, Mat.Wood);
            brush.Box(new int3(grandX - 3, baseY + 1, grandZ), new int3(3, 20, 3), Mat.Wood);
            brush.Box(new int3(grandX + grandWidth, baseY + 1, grandZ),
                      new int3(3, 20, 3), Mat.Wood);

            int stairX = min.x + 34;
            int stairZ = min.z + 34;
            const int stairRadius = 22;
            brush.SpiralStair(stairX, baseY + 2, stairZ, stairRadius,
                              floors * plan.FloorHeight, Mat.Stone);
        }

        private static void BuildWindows(ref VoxelBrush brush, in CastlePlan plan,
                                         int3 min, int3 size, int baseY, int floors)
        {
            for (int f = 0; f < floors; f++)
            {
                int y = baseY + f * plan.FloorHeight + 12;
                int height = f == 1 ? plan.FloorHeight - 14 : plan.FloorHeight - 18;
                for (int i = 0; i < 3; i++)
                {
                    int x = min.x + size.x / 4 + i * size.x / 4 - 8;
                    bool mainEntrance = f == 0 && i == 1;
                    if (!mainEntrance)
                    {
                        brush.Arch(new int3(x, y, min.z), 16, height, 9, 2, Mat.Empty);
                        brush.Box(new int3(x + 3, y + 4, min.z + 2),
                                  new int3(10, height - 10, 2), Mat.LitWindow);
                        brush.Box(new int3(x + 7, y + 5, min.z + 1),
                                  new int3(2, height - 12, 3), Mat.DarkStone);
                        brush.Box(new int3(x + 3, y + height / 2, min.z + 1),
                                  new int3(10, 2, 3), Mat.DarkStone);
                    }
                    brush.Arch(new int3(x, y, min.z + size.z - 8), 16, height, 9, 2, Mat.Empty);
                }
            }
        }

        private static void BuildFacade(ref VoxelBrush brush, in CastlePlan plan,
                                        int3 min, int3 size, int baseY, int floors)
        {
            for (int f = 1; f < floors; f++)
            {
                int courseY = baseY + f * plan.FloorHeight - 3;
                brush.Box(new int3(min.x - 3, courseY, min.z - 3),
                          new int3(size.x + 6, 3, 4), Mat.DarkStone);
                brush.Box(new int3(min.x - 3, courseY, min.z + size.z - 1),
                          new int3(size.x + 6, 3, 4), Mat.DarkStone);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int bannerX = plan.Centre.x + side * 52;
                brush.Box(new int3(bannerX - 7, baseY + plan.FloorHeight * 2 + 8, min.z - 3),
                          new int3(14, 54, 3), Mat.Cloth);
                brush.Box(new int3(bannerX - 10, baseY + plan.FloorHeight * 2 + 59, min.z - 4),
                          new int3(20, 3, 4), Mat.Gold);
            }

            int2[] keepStains = { new(-74, 5), new(-35, 14), new(42, 8), new(76, 20) };
            for (int i = 0; i < keepStains.Length; i++)
            {
                int stainX = plan.Centre.x + keepStains[i].x;
                int stainHeight = 8 + (i * 6 % 15);
                brush.Box(new int3(stainX, baseY + keepStains[i].y, min.z - 2),
                          new int3(9 + (i & 1) * 6, stainHeight, 2), Mat.Moss);
                brush.Box(new int3(stainX + 3, baseY + 2, min.z - 2),
                          new int3(3, keepStains[i].y + 5, 2), Mat.Moss);
            }
        }
    }
}
