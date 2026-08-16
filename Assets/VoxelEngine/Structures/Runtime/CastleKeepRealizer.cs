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
        /// <summary>
        /// Executes one migrated keep substage. Returns false once the caller reaches the final
        /// legacy roof/annex substage (substage 6).
        /// </summary>
        internal static bool TryStep(ref VoxelBrush brush, in CastlePlan plan, ref int stage)
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
                    CastleKeepShellRealizer.Build(ref brush, min, size, baseY);
                    break;

                case 1:
                    BuildCornerTurrets(ref brush, in plan, min, size, baseY);
                    break;

                case 2:
                    BuildFloorsAndRooms(ref brush, in plan, min, size, baseY, floors);
                    break;

                case 3:
                    BuildCirculation(ref brush, in plan, min, size, baseY, floors);
                    break;

                case 4:
                    BuildWindows(ref brush, in plan, min, size, baseY, floors);
                    break;

                case 5:
                    BuildFacadeAndOriel(ref brush, in plan, min, size, baseY, floors);
                    break;
            }

            stage++;
            return true;
        }

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

        private static void BuildFloorsAndRooms(ref VoxelBrush brush, in CastlePlan plan,
                                                int3 min, int3 size, int baseY, int floors)
        {
            for (int f = 0; f < floors; f++)
            {
                int y = baseY + f * plan.FloorHeight;
                if (f > 0)
                {
                    brush.Box(new int3(min.x + 8, y, min.z + 8),
                              new int3(size.x - 16, 3, size.z - 16), Mat.Wood);
                }

                CastleRoomFurnisher.Furnish(ref brush, in plan, min, size, y, f);
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

            // Reassert the entrance aisle after furnishing/clutter.
            brush.Box(new int3(entranceX - 9, baseY + 1, min.z + 8),
                      new int3(18, 24, size.z / 2 - 28), Mat.Empty);

            int grandX = plan.Centre.x - 68;
            int grandZ = min.z + 28;
            const int grandWidth = 18;
            const int grandRise = 2;
            const int grandRun = 3;
            int grandSteps = plan.FloorHeight / grandRise;
            brush.Box(new int3(grandX, baseY + 1, grandZ),
                      new int3(grandWidth, plan.FloorHeight + 18,
                               grandSteps * grandRun), Mat.Empty);
            brush.Stairs(new int3(grandX, baseY + 1, grandZ), grandWidth,
                         grandSteps, grandRise, grandRun, 2, Mat.Wood);

            brush.Box(new int3(grandX - 3, baseY + 1, grandZ),
                      new int3(3, 20, 3), Mat.Wood);
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

                    brush.Arch(new int3(x, y, min.z + size.z - 8),
                               16, height, 9, 2, Mat.Empty);
                }
            }
        }

        private static void BuildFacadeAndOriel(ref VoxelBrush brush, in CastlePlan plan,
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

            int2[] keepStains =
            {
                new(-74, 5), new(-35, 14), new(42, 8), new(76, 20),
            };
            for (int i = 0; i < keepStains.Length; i++)
            {
                int stainX = plan.Centre.x + keepStains[i].x;
                int stainHeight = 8 + (i * 6 % 15);
                brush.Box(new int3(stainX, baseY + keepStains[i].y, min.z - 2),
                          new int3(9 + (i & 1) * 6, stainHeight, 2), Mat.Moss);
                brush.Box(new int3(stainX + 3, baseY + 2, min.z - 2),
                          new int3(3, keepStains[i].y + 5, 2), Mat.Moss);
            }

            BuildRearOriel(ref brush, in plan, min, size, baseY);
        }

        private static void BuildRearOriel(ref VoxelBrush brush, in CastlePlan plan,
                                           int3 keepMin, int3 keepSize, int baseY)
        {
            const int width = 44;
            const int depth = 22;
            int minX = plan.Centre.x + 18;
            int wallZ = keepMin.z + keepSize.z;
            int firstFloorY = baseY + plan.FloorHeight * 2;

            for (int x = 3; x < width - 2; x += 12)
            {
                brush.Box(new int3(minX + x, firstFloorY - 13, wallZ + 2),
                          new int3(5, 13, 14), Mat.DarkStone);
            }

            for (int storey = 0; storey < 2; storey++)
            {
                int y = firstFloorY + storey * plan.FloorHeight;
                brush.Box(new int3(minX, y, wallZ - 2),
                          new int3(width, 4, depth), Mat.Wood);
                brush.Box(new int3(minX, y + 4, wallZ + depth - 5),
                          new int3(width, plan.FloorHeight - 7, 4), Mat.Wood);
                brush.Box(new int3(minX, y + 4, wallZ),
                          new int3(4, plan.FloorHeight - 7, depth - 3), Mat.Wood);
                brush.Box(new int3(minX + width - 4, y + 4, wallZ),
                          new int3(4, plan.FloorHeight - 7, depth - 3), Mat.Wood);

                for (int bay = 0; bay < 3; bay++)
                {
                    int bayX = minX + 5 + bay * 13;
                    brush.Box(new int3(bayX, y + 9, wallZ + depth - 4),
                              new int3(9, plan.FloorHeight - 18, 3), Mat.LitWindow);
                }

                brush.Box(new int3(minX + 8, y + 4, wallZ - 8),
                          new int3(width - 16, 25, 12), Mat.Empty);
                brush.Box(new int3(minX + 4, y + 4, wallZ + 4),
                          new int3(width - 8, plan.FloorHeight - 8, depth - 9), Mat.Empty);
            }

            int roofY = firstFloorY + plan.FloorHeight * 2;
            brush.Gable(new int3(minX - 4, roofY, wallZ - 4),
                        new int3(width + 8, 24, depth + 8), true, Mat.Tile);
            brush.Box(new int3(minX - 3, firstFloorY + plan.FloorHeight - 1, wallZ - 1),
                      new int3(width + 6, 3, depth + 1), Mat.DarkStone);
        }
    }
}
