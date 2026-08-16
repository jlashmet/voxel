using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the keep's final skyline and attached occupied wings.
    /// Separated from the core keep shell/circulation so future planners can choose annexes
    /// independently rather than baking them into one monolithic keep recipe.
    /// </summary>
    internal static class CastleKeepAnnexRealizer
    {
        internal static void Build(ref VoxelBrush brush, in CastlePlan plan)
        {
            int3 min = CastleSpatialProjection.KeepMinimum(in plan);
            int baseY = min.y;
            int hx = plan.KeepHalfX;
            int hz = plan.KeepHalfZ;
            var size = new int3(hx * 2, plan.KeepHeight, hz * 2);
            int topY = baseY + plan.Floors * plan.FloorHeight;

            brush.Box(new int3(min.x - 5, topY, min.z - 5),
                      new int3(size.x + 10, 6, size.z + 10), Mat.DarkStone);

            for (int i = 0; i < size.x + 10; i += 44)
            {
                brush.Box(new int3(min.x - 5 + i, topY + 6, min.z - 5),
                          new int3(24, 20, 7), Mat.Stone);
                brush.Box(new int3(min.x - 5 + i, topY + 6, min.z + size.z + 3),
                          new int3(24, 20, 7), Mat.Stone);
            }

            brush.Gable(new int3(min.x, topY + 8, min.z),
                        new int3(size.x, 70, size.z), true, Mat.Tile);

            BuildRooflineDetails(ref brush, in plan, min, size, topY);
            BuildGreatHallWing(ref brush, in plan, min, size, baseY);
            BuildChapelWing(ref brush, in plan, min, size, baseY);
        }

        internal static void BuildPlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleKeepAnnexPlan annexes)
        {
            CastleKeepAnnexPlanValidator.RequireValid(in annexes);

            int3 min = CastleSpatialProjection.KeepMinimum(in plan);
            int baseY = min.y;
            int hx = plan.KeepHalfX;
            int hz = plan.KeepHalfZ;
            var size = new int3(hx * 2, plan.KeepHeight, hz * 2);
            int topY = baseY + plan.Floors * plan.FloorHeight;

            brush.Box(new int3(min.x - 5, topY, min.z - 5),
                      new int3(size.x + 10, 6, size.z + 10), Mat.DarkStone);

            for (int i = 0; i < size.x + 10; i += 44)
            {
                brush.Box(new int3(min.x - 5 + i, topY + 6, min.z - 5),
                          new int3(24, 20, 7), Mat.Stone);
                brush.Box(new int3(min.x - 5 + i, topY + 6, min.z + size.z + 3),
                          new int3(24, 20, 7), Mat.Stone);
            }

            brush.Gable(new int3(min.x, topY + 8, min.z),
                        new int3(size.x, 70, size.z), true, Mat.Tile);

            BuildRooflineDetails(ref brush, in plan, min, size, topY);

            if (annexes.HasGreatHallWing)
                BuildGreatHallWing(ref brush, in plan, min, size, baseY);
            if (annexes.HasChapelWing)
                BuildChapelWing(
                    ref brush, in plan, min, size, baseY, annexes.HasBellTower);
        }

        private static void BuildRooflineDetails(ref VoxelBrush brush, in CastlePlan plan,
                                                 int3 min, int3 size, int topY)
        {
            int roofFrontZ = min.z - 2;

            for (int side = -1; side <= 1; side += 2)
            {
                int dormerX = plan.Centre.x + side * 52;
                brush.Box(new int3(dormerX - 12, topY + 25, roofFrontZ),
                          new int3(24, 25, 18), Mat.Stone);
                brush.Arch(new int3(dormerX - 6, topY + 32, roofFrontZ - 1),
                           12, 16, 4, 2, Mat.Empty);
                brush.Box(new int3(dormerX - 3, topY + 35, roofFrontZ),
                          new int3(6, 10, 2), Mat.LitWindow);
                brush.Gable(new int3(dormerX - 15, topY + 49, roofFrontZ - 4),
                            new int3(30, 20, 25), true, Mat.Slate);
            }

            int lanternX = plan.Centre.x + size.x / 7;
            int lanternZ = min.z + size.z / 2;
            int lanternY = topY + 63;
            const int half = 24;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                brush.Box(new int3(lanternX + sx * half - 5, lanternY,
                                   lanternZ + sz * half - 5),
                          new int3(10, 48, 10), Mat.Stone);
            }

            brush.Box(new int3(lanternX - half - 5, lanternY,
                               lanternZ - half - 5),
                      new int3(half * 2 + 10, 48, 8), Mat.Stone);
            brush.Box(new int3(lanternX - half - 5, lanternY,
                               lanternZ + half - 3),
                      new int3(half * 2 + 10, 48, 8), Mat.Stone);
            brush.Box(new int3(lanternX - half - 5, lanternY,
                               lanternZ - half + 3),
                      new int3(8, 48, half * 2 - 6), Mat.Stone);
            brush.Box(new int3(lanternX + half - 3, lanternY,
                               lanternZ - half + 3),
                      new int3(8, 48, half * 2 - 6), Mat.Stone);
            brush.Arch(new int3(lanternX - 13, lanternY + 7,
                                lanternZ - half - 6), 26, 34, 10, 2, Mat.Empty);
            brush.Arch(new int3(lanternX - 13, lanternY + 7,
                                lanternZ + half - 4), 26, 34, 10, 2, Mat.Empty);
            brush.Arch(new int3(lanternX - half - 6, lanternY + 7,
                                lanternZ - 13), 26, 34, 10, 0, Mat.Empty);
            brush.Arch(new int3(lanternX + half - 4, lanternY + 7,
                                lanternZ - 13), 26, 34, 10, 0, Mat.Empty);

            brush.Box(new int3(lanternX - half - 5, lanternY + 40,
                               lanternZ - half - 5),
                      new int3(half * 2 + 10, 9, 10), Mat.DarkStone);
            brush.Box(new int3(lanternX - half - 5, lanternY + 40,
                               lanternZ + half - 5),
                      new int3(half * 2 + 10, 9, 10), Mat.DarkStone);
            brush.Box(new int3(lanternX - half - 5, lanternY + 40,
                               lanternZ - half + 5),
                      new int3(10, 9, half * 2 - 10), Mat.DarkStone);
            brush.Box(new int3(lanternX + half - 5, lanternY + 40,
                               lanternZ - half + 5),
                      new int3(10, 9, half * 2 - 10), Mat.DarkStone);

            brush.Box(new int3(lanternX - half - 8, lanternY + 49,
                               lanternZ - half - 8),
                      new int3(half * 2 + 16, 7, half * 2 + 16), Mat.DarkStone);
            for (int x = -half - 7; x <= half - 5; x += 18)
            {
                brush.Box(new int3(lanternX + x, lanternY + 56,
                                   lanternZ - half - 7), new int3(11, 15, 8), Mat.Stone);
                brush.Box(new int3(lanternX + x, lanternY + 56,
                                   lanternZ + half - 1), new int3(11, 15, 8), Mat.Stone);
            }
            for (int z = -half + 8; z <= half - 10; z += 18)
            {
                brush.Box(new int3(lanternX - half - 7, lanternY + 56,
                                   lanternZ + z), new int3(8, 15, 11), Mat.Stone);
                brush.Box(new int3(lanternX + half - 1, lanternY + 56,
                                   lanternZ + z), new int3(8, 15, 11), Mat.Stone);
            }
            brush.Box(new int3(lanternX - 1, lanternY + 70, lanternZ - 1),
                      new int3(3, 30, 3), Mat.Gold);
            brush.Box(new int3(lanternX + 2, lanternY + 86, lanternZ - 1),
                      new int3(24, 11, 3), Mat.Cloth);
        }

        private static void BuildChapelWing(ref VoxelBrush brush, in CastlePlan plan,
                                            int3 keepMin, int3 keepSize, int baseY,
                                            bool buildBellTower = true)
        {
            int width = math.max(78, keepSize.x / 3);
            int depth = math.max(96, keepSize.z * 3 / 5);
            int height = plan.FloorHeight * 2;
            var min = new int3(keepMin.x - width + 4, baseY,
                               keepMin.z + keepSize.z - depth - 38);
            int centreZ = min.z + depth / 2;

            brush.Box(new int3(min.x - 5, baseY - 12, min.z - 5),
                      new int3(width + 10, 16, depth + 10), Mat.DarkStone);
            brush.HollowBox(min, new int3(width, height, depth), 6,
                            Mat.Stone, false, false);
            brush.FillBulk(new int3(min.x + 6, baseY + 1, min.z + 6),
                           new int3(width - 12, height - 1, depth - 12), Mat.Empty);

            brush.Arch(new int3(keepMin.x - 8, baseY + 2, centreZ - 12),
                       24, 36, 16, 0, Mat.Empty);
            brush.Box(new int3(keepMin.x - 12, baseY, centreZ - 8),
                      new int3(24, 2, 16), Mat.Stone);
            brush.Box(new int3(keepMin.x - 12, baseY + 2, centreZ - 8),
                      new int3(24, 25, 16), Mat.Empty);

            brush.Arch(new int3(min.x - 1, baseY + 30, centreZ - 16),
                       32, 34, 8, 0, Mat.Empty);
            brush.Box(new int3(min.x + 2, baseY + 35, centreZ - 10),
                      new int3(3, 24, 20), Mat.LitWindow);
            brush.Box(new int3(min.x + 1, baseY + 35, centreZ - 2),
                      new int3(5, 24, 4), Mat.DarkStone);
            brush.Box(new int3(min.x + 1, baseY + 45, centreZ - 10),
                      new int3(5, 4, 20), Mat.DarkStone);
            for (int side = -1; side <= 1; side += 2)
            {
                int z = centreZ + side * 34;
                brush.Arch(new int3(min.x + width / 2 - 7, baseY + 20, z - 6),
                           14, 38, 7, 2, Mat.Empty);
                brush.Box(new int3(min.x + width / 2 - 4, baseY + 25, z - 4),
                          new int3(8, 26, 2), Mat.LitWindow);
            }

            brush.Box(new int3(min.x + 7, baseY + 1, centreZ - 27),
                      new int3(21, 2, 54), Mat.DarkStone);
            brush.Box(new int3(min.x + 9, baseY + 3, centreZ - 24),
                      new int3(17, 2, 48), Mat.Stone);
            brush.Box(new int3(min.x + 19, baseY + 7, centreZ - 21),
                      new int3(8, 5, 42), Mat.Wood);
            brush.Box(new int3(min.x + 17, baseY + 5, centreZ - 24),
                      new int3(3, 9, 4), Mat.Wood);
            brush.Box(new int3(min.x + 17, baseY + 5, centreZ + 20),
                      new int3(3, 9, 4), Mat.Wood);

            for (int panel = -1; panel <= 1; panel++)
            {
                int panelWidth = panel == 0 ? 15 : 11;
                int panelZ = centreZ + panel * 17 - panelWidth / 2;
                brush.Box(new int3(min.x + 7, baseY + 12, panelZ),
                          new int3(3, panel == 0 ? 28 : 23, panelWidth), Mat.Cloth);
                brush.Box(new int3(min.x + 6, baseY + 10, panelZ - 2),
                          new int3(2, 3, panelWidth + 4), Mat.Gold);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                int columnZ = centreZ + side * 25 - 4;
                brush.Box(new int3(min.x + 6, baseY + 7, columnZ),
                          new int3(8, 36, 8), Mat.DarkStone);
                brush.Box(new int3(min.x + 4, baseY + 40, columnZ - 2),
                          new int3(12, 6, 12), Mat.Stone);
            }
            brush.Box(new int3(min.x + 5, baseY + 43, centreZ - 31),
                      new int3(11, 6, 62), Mat.DarkStone);
            brush.Box(new int3(min.x + 11, baseY + 20, centreZ - 2),
                      new int3(10, 4, 4), Mat.Gold);
            brush.Box(new int3(min.x + 14, baseY + 14, centreZ - 2),
                      new int3(4, 17, 4), Mat.Gold);

            for (int candle = -2; candle <= 2; candle++)
            {
                int candleZ = centreZ + candle * 7;
                brush.Box(new int3(min.x + 20, baseY + 12, candleZ - 1),
                          new int3(2, 5 + (candle & 1), 2), Mat.Glass);
                brush.Box(new int3(min.x + 19, baseY + 11, candleZ - 2),
                          new int3(4, 2, 4), Mat.Gold);
            }

            for (int row = 0; row < 3; row++)
            for (int side = -1; side <= 1; side += 2)
            {
                int x = min.x + 34 + row * 15;
                int z = centreZ + side * 17;
                brush.Box(new int3(x, baseY + 2, z - 10),
                          new int3(7, 6, 20), Mat.Wood);
                brush.Box(new int3(x + 5, baseY + 7, z - 10),
                          new int3(3, 10, 20), Mat.Wood);
                brush.Box(new int3(x + 1, baseY + 9, z - 8),
                          new int3(4, 2, 16), row == 0 ? Mat.Gold : Mat.Wood);
            }

            for (int x = min.x + 24; x < min.x + width - 5; x += 24)
            {
                brush.Box(new int3(x, baseY + 49, min.z + 7),
                          new int3(4, 4, depth - 14), Mat.Wood);
                for (int step = 0; step < 12; step++)
                {
                    int braceY = baseY + 50 + step * 2;
                    int southZ = min.z + 8 + step * 3;
                    int northZ = min.z + depth - 12 - step * 3;
                    brush.Box(new int3(x, braceY, southZ),
                              new int3(4, 3, 5), Mat.Wood);
                    brush.Box(new int3(x, braceY, northZ),
                              new int3(4, 3, 5), Mat.Wood);
                }
            }

            int[] chandelierX = { min.x + 30, min.x + 52 };
            for (int i = 0; i < chandelierX.Length; i++)
            {
                int cx = chandelierX[i];
                int fixtureY = baseY + 39 + i * 2;
                brush.Box(new int3(cx - 1, fixtureY + 3, centreZ - 1),
                          new int3(2, 26 - i * 2, 2), Mat.Gold);
                brush.Box(new int3(cx - 10, fixtureY, centreZ - 1),
                          new int3(20, 3, 2), Mat.Gold);
                brush.Box(new int3(cx - 1, fixtureY, centreZ - 10),
                          new int3(2, 3, 20), Mat.Gold);
                int2[] lamps = { new(-9, 0), new(8, 0), new(0, -9), new(0, 8) };
                for (int lamp = 0; lamp < lamps.Length; lamp++)
                {
                    brush.Box(new int3(cx + lamps[lamp].x - 1, fixtureY - 3,
                                       centreZ + lamps[lamp].y - 1),
                              new int3(3, 5, 3), Mat.Glass);
                }
            }

            brush.Gable(new int3(min.x - 4, baseY + height, min.z - 4),
                        new int3(width + 8, 42, depth + 8), false, Mat.Slate);

            for (int z = min.z + 10; z < min.z + depth - 8; z += 30)
            {
                brush.Box(new int3(min.x - 8, baseY, z),
                          new int3(10, 46, 9), Mat.DarkStone);
                brush.Box(new int3(min.x - 5, baseY + 40, z + 1),
                          new int3(7, 25, 7), Mat.Stone);
            }

            if (buildBellTower)
                BuildChapelBellTower(ref brush, in plan, baseY);
        }

        private static void BuildChapelBellTower(ref VoxelBrush brush, in CastlePlan plan, int baseY)
        {
            const int size = CastleLayout.ChapelBellTowerSize;
            int height = plan.FloorHeight * 4;
            int3 centre = CastleLayout.ChapelBellTowerCentre(in plan);
            var min = new int3(centre.x - size / 2, baseY, centre.z - size / 2);

            brush.Box(new int3(min.x - 5, baseY - 16, min.z - 5),
                      new int3(size + 10, 20, size + 10), Mat.DarkStone);
            brush.HollowBox(min, new int3(size, height, size), 6,
                            Mat.Stone, false, false);
            brush.FillBulk(new int3(min.x + 6, baseY + 1, min.z + 6),
                           new int3(size - 12, height - 1, size - 12), Mat.Empty);

            for (int floor = 1; floor < 4; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                brush.Box(new int3(min.x + 6, floorY, min.z + 6),
                          new int3(size - 12, 3, size - 12), Mat.Wood);
            }

            int stairX = min.x + size - 19;
            int stairZ = min.z + size / 2;
            brush.SpiralStair(stairX, baseY + 2, stairZ,
                              CastleLayout.ChapelBellTowerStairRadius, height - 4, Mat.Stone);

            int connectorX = centre.x;
            int keepDepth = plan.KeepHalfZ * 2;
            int chapelDepth = math.max(96, keepDepth * 3 / 5);
            int chapelCentreZ = min.z + 6 - chapelDepth / 2;
            int aisleStartZ = chapelCentreZ - 6;
            brush.Box(new int3(connectorX - 8, baseY, aisleStartZ),
                      new int3(16, 2, min.z + 12 - aisleStartZ), Mat.Stone);
            brush.Arch(new int3(connectorX - 9, baseY + 2, min.z - 9),
                       18, 32, 18, 2, Mat.Empty);
            brush.Box(new int3(connectorX - 7, baseY + 2, aisleStartZ),
                      new int3(14, 24, min.z + 12 - aisleStartZ), Mat.Empty);

            for (int floor = 0; floor < 4; floor++)
            {
                int windowY = baseY + floor * plan.FloorHeight + 12;
                int windowHeight = plan.FloorHeight - 18;

                brush.Arch(new int3(min.x - 2, windowY, centre.z - 7),
                           14, windowHeight, 10, 0, Mat.Empty);
                brush.Box(new int3(min.x + 2, windowY + 4, centre.z - 4),
                          new int3(2, windowHeight - 9, 8), Mat.LitWindow);
                brush.Box(new int3(min.x - 4, windowY - 3, centre.z - 11),
                          new int3(5, 3, 22), Mat.DarkStone);

                for (int side = -1; side <= 1; side += 2)
                {
                    if (floor == 0 && side < 0) continue;

                    int z = side < 0 ? min.z - 2 : min.z + size - 8;
                    brush.Arch(new int3(centre.x - 7, windowY, z),
                               14, windowHeight, 10, 2, Mat.Empty);
                    int glassZ = side < 0 ? min.z + 2 : min.z + size - 4;
                    brush.Box(new int3(centre.x - 4, windowY + 4, glassZ),
                              new int3(8, windowHeight - 9, 2), Mat.LitWindow);
                }
            }

            for (int floor = 0; floor < 3; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                brush.Box(new int3(min.x + 8, floorY + 3, min.z + 9),
                          new int3(10, 24, 18), Mat.Wood);
                brush.Box(new int3(min.x + 19, floorY + 8, min.z + 11),
                          new int3(15, 4, 12), Mat.Wood);
                brush.Box(new int3(min.x + 21, floorY + 3, min.z + 13),
                          new int3(4, 6, 4), Mat.Wood);
                brush.Box(new int3(min.x + 28, floorY + 12, min.z + 14),
                          new int3(3, 7, 3), floor == 2 ? Mat.Glass : Mat.Gold);
            }

            int bellY = baseY + plan.FloorHeight * 3 + 14;
            brush.Box(new int3(min.x + 9, bellY - 8, centre.z - 2),
                      new int3(size - 31, 4, 4), Mat.Wood);
            for (int i = 0; i < 2; i++)
            {
                int bellX = min.x + 17 + i * 16;
                brush.Box(new int3(bellX, bellY, centre.z - 5),
                          new int3(9, 10, 10), Mat.Gold);
                brush.Box(new int3(bellX + 3, bellY + 10, centre.z - 2),
                          new int3(3, 10, 3), Mat.Wood);
            }

            int topY = baseY + height;
            brush.Box(new int3(min.x - 5, topY, min.z - 5),
                      new int3(size + 10, 7, size + 10), Mat.DarkStone);
            for (int x = min.x - 4; x < min.x + size + 2; x += 18)
            {
                brush.Box(new int3(x, topY + 7, min.z - 4),
                          new int3(11, 15, 8), Mat.Stone);
                brush.Box(new int3(x, topY + 7, min.z + size - 4),
                          new int3(11, 15, 8), Mat.Stone);
            }
            brush.Gable(new int3(min.x + 2, topY + 10, min.z + 2),
                        new int3(size - 4, 46, size - 4), true, Mat.Slate);
            brush.Box(new int3(centre.x - 1, topY + 53, centre.z - 1),
                      new int3(3, 25, 3), Mat.Gold);
            brush.Box(new int3(centre.x + 2, topY + 66, centre.z - 1),
                      new int3(20, 9, 3), Mat.Cloth);
        }

        private static void BuildGreatHallWing(ref VoxelBrush brush, in CastlePlan plan,
                                               int3 keepMin, int3 keepSize, int baseY)
        {
            int wingHeight = plan.FloorHeight * 2;
            int wingWidth = math.max(96, keepSize.x * 2 / 5);
            int wingDepth = math.max(80, keepSize.z - 72);
            var wingMin = new int3(keepMin.x + keepSize.x - 4, baseY,
                                   keepMin.z + 24);

            brush.Box(new int3(wingMin.x - 4, baseY - 12, wingMin.z - 4),
                      new int3(wingWidth + 8, 16, wingDepth + 8), Mat.DarkStone);
            brush.HollowBox(wingMin, new int3(wingWidth, wingHeight, wingDepth),
                            6, Mat.Stone, false, false);
            brush.FillBulk(new int3(wingMin.x + 6, baseY + 1, wingMin.z + 6),
                           new int3(wingWidth - 12, wingHeight - 1, wingDepth - 12),
                           Mat.Empty);
            brush.Box(new int3(wingMin.x + 6, baseY + plan.FloorHeight, wingMin.z + 6),
                      new int3(wingWidth - 12, 3, wingDepth - 12), Mat.Wood);

            int hallCentreZ = wingMin.z + wingDepth / 2;
            for (int side = -1; side <= 1; side += 2)
            {
                int tableZ = hallCentreZ + side * 25;
                brush.Box(new int3(wingMin.x + 22, baseY + 7, tableZ - 5),
                          new int3(wingWidth - 46, 4, 10), Mat.Wood);
                brush.Box(new int3(wingMin.x + 27, baseY + 2, tableZ - 3),
                          new int3(4, 6, 6), Mat.Wood);
                brush.Box(new int3(wingMin.x + wingWidth - 31, baseY + 2, tableZ - 3),
                          new int3(4, 6, 6), Mat.Wood);
                brush.Box(new int3(wingMin.x + 20, baseY + 2, tableZ + side * 9 - 2),
                          new int3(wingWidth - 42, 4, 4), Mat.Wood);
            }
            brush.Box(new int3(wingMin.x + wingWidth - 20, baseY + 2, hallCentreZ - 17),
                      new int3(8, 4, 34), Mat.DarkStone);
            brush.Box(new int3(wingMin.x + wingWidth - 17, baseY + 6, hallCentreZ - 8),
                      new int3(5, 14, 16), Mat.Wood);
            brush.Box(new int3(wingMin.x + wingWidth - 16, baseY + 12, hallCentreZ - 6),
                      new int3(4, 8, 12), Mat.Cloth);

            int upperY = baseY + plan.FloorHeight;
            for (int z = wingMin.z + 12; z < wingMin.z + wingDepth - 18; z += 28)
            {
                brush.Box(new int3(wingMin.x + wingWidth - 18, upperY + 3, z),
                          new int3(10, 28, 18), Mat.Wood);
                for (int shelf = 0; shelf < 3; shelf++)
                {
                    brush.Box(new int3(wingMin.x + wingWidth - 19, upperY + 9 + shelf * 8, z - 1),
                              new int3(12, 2, 20), shelf == 1 ? Mat.Gold : Mat.Wood);
                }
            }
            brush.Box(new int3(wingMin.x + 28, upperY + 8, hallCentreZ - 12),
                      new int3(34, 4, 24), Mat.Wood);
            brush.Box(new int3(wingMin.x + 32, upperY + 3, hallCentreZ - 8),
                      new int3(5, 6, 5), Mat.Wood);
            brush.Box(new int3(wingMin.x + 53, upperY + 3, hallCentreZ + 3),
                      new int3(5, 6, 5), Mat.Wood);

            for (int floor = 0; floor < 2; floor++)
            for (int side = -1; side <= 1; side += 2)
            {
                int lampY = baseY + floor * plan.FloorHeight + 17;
                int lampZ = hallCentreZ + side * (wingDepth / 2 - 13);
                brush.Box(new int3(wingMin.x + wingWidth / 2 - 2, lampY, lampZ - 2),
                          new int3(4, 7, 4), Mat.Glass);
                brush.Box(new int3(wingMin.x + wingWidth / 2 - 3, lampY - 3, lampZ - 1),
                          new int3(6, 3, 3), Mat.Gold);
            }

            for (int i = 0; i < 2; i++)
            {
                int z = wingMin.z + 14 + i * (wingDepth - 28);
                brush.Arch(new int3(wingMin.x + wingWidth - 7, baseY + 12, z),
                           16, 28, 8, 0, Mat.Empty);
                brush.Box(new int3(wingMin.x + wingWidth - 5, baseY + 16, z + 3),
                          new int3(2, 18, 10), Mat.LitWindow);
            }

            brush.Arch(new int3(wingMin.x - 8, baseY + 2,
                                wingMin.z + wingDepth / 2 - 10),
                       20, 32, 16, 0, Mat.Empty);
            brush.Arch(new int3(wingMin.x - 8, baseY + plan.FloorHeight + 2,
                                wingMin.z + wingDepth / 2 - 10),
                       20, 30, 16, 0, Mat.Empty);

            int connectorZ = wingMin.z + wingDepth / 2;
            for (int floor = 0; floor < 2; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                brush.Box(new int3(keepMin.x + keepSize.x - 12, floorY, connectorZ - 7),
                          new int3(24, floor == 0 ? 2 : 3, 14),
                          floor == 0 ? Mat.Stone : Mat.Wood);

                int footY = floorY + (floor == 0 ? 2 : 3);
                brush.Box(new int3(keepMin.x + keepSize.x - 12, footY, connectorZ - 7),
                          new int3(24, 24, 14), Mat.Empty);
            }

            brush.Gable(new int3(wingMin.x - 4, baseY + wingHeight, wingMin.z - 4),
                        new int3(wingWidth + 8, 34, wingDepth + 8), true, Mat.Tile);

            int balconyY = baseY + plan.FloorHeight + 4;
            int balconyZ = wingMin.z + wingDepth / 2 - 25;
            brush.Box(new int3(wingMin.x + wingWidth - 2, balconyY, balconyZ),
                      new int3(18, 4, 50), Mat.Wood);
            brush.Box(new int3(wingMin.x + wingWidth + 12, balconyY + 4, balconyZ),
                      new int3(3, 18, 3), Mat.Wood);
            brush.Box(new int3(wingMin.x + wingWidth + 12, balconyY + 4, balconyZ + 47),
                      new int3(3, 18, 3), Mat.Wood);
            brush.Box(new int3(wingMin.x + wingWidth + 12, balconyY + 18, balconyZ),
                      new int3(3, 3, 50), Mat.Wood);
        }
    }
}
