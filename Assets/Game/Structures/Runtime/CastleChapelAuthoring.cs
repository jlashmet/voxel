using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned occupied chapel and attached bell/solar tower.</summary>
    public static class CastleChapelAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 keepMin = CastleKeepCoreAuthoring.Minimum(in plan);
            int3 keepSize = CastleKeepCoreAuthoring.Size(in plan);

            int width = math.max(78, keepSize.x / 3);
            int depth = math.max(96, keepSize.z * 3 / 5);
            int height = plan.FloorHeight * 2;
            var min = new int3(
                keepMin.x - width + 4,
                baseY,
                keepMin.z + keepSize.z - depth - 38);
            int centreZ = min.z + depth / 2;

            authoring.Box(
                new int3(min.x - 5, baseY - 12, min.z - 5),
                new int3(width + 10, 16, depth + 10),
                GameMaterialIds.DarkStone);
            authoring.HollowBox(
                min,
                new int3(width, height, depth),
                6,
                GameMaterialIds.Stone,
                false,
                false);
            authoring.FillBulk(
                new int3(min.x + 6, baseY + 1, min.z + 6),
                new int3(width - 12, height - 1, depth - 12),
                GameMaterialIds.Empty);

            authoring.Arch(
                new int3(keepMin.x - 8, baseY + 2, centreZ - 12),
                24,
                36,
                16,
                0,
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(keepMin.x - 12, baseY, centreZ - 8),
                new int3(24, 2, 16),
                GameMaterialIds.Stone);
            authoring.Box(
                new int3(keepMin.x - 12, baseY + 2, centreZ - 8),
                new int3(24, 25, 16),
                GameMaterialIds.Empty);

            authoring.Arch(
                new int3(min.x - 1, baseY + 30, centreZ - 16),
                32,
                34,
                8,
                0,
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(min.x + 2, baseY + 35, centreZ - 10),
                new int3(3, 24, 20),
                GameMaterialIds.LitWindow);
            authoring.Box(
                new int3(min.x + 1, baseY + 35, centreZ - 2),
                new int3(5, 24, 4),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(min.x + 1, baseY + 45, centreZ - 10),
                new int3(5, 4, 20),
                GameMaterialIds.DarkStone);

            for (int side = -1; side <= 1; side += 2)
            {
                int z = centreZ + side * 34;
                authoring.Arch(
                    new int3(min.x + width / 2 - 7, baseY + 20, z - 6),
                    14,
                    38,
                    7,
                    2,
                    GameMaterialIds.Empty);
                authoring.Box(
                    new int3(min.x + width / 2 - 4, baseY + 25, z - 4),
                    new int3(8, 26, 2),
                    GameMaterialIds.LitWindow);
            }

            AuthorSanctuary(authoring, min, centreZ, baseY);

            for (int row = 0; row < 3; row++)
            for (int side = -1; side <= 1; side += 2)
            {
                int x = min.x + 34 + row * 15;
                int z = centreZ + side * 17;
                authoring.Box(
                    new int3(x, baseY + 2, z - 10),
                    new int3(7, 6, 20),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(x + 5, baseY + 7, z - 10),
                    new int3(3, 10, 20),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(x + 1, baseY + 9, z - 8),
                    new int3(4, 2, 16),
                    row == 0 ? GameMaterialIds.Gold : GameMaterialIds.Wood);
            }

            for (int x = min.x + 24; x < min.x + width - 5; x += 24)
            {
                authoring.Box(
                    new int3(x, baseY + 49, min.z + 7),
                    new int3(4, 4, depth - 14),
                    GameMaterialIds.Wood);
                for (int step = 0; step < 12; step++)
                {
                    int braceY = baseY + 50 + step * 2;
                    int southZ = min.z + 8 + step * 3;
                    int northZ = min.z + depth - 12 - step * 3;
                    authoring.Box(
                        new int3(x, braceY, southZ),
                        new int3(4, 3, 5),
                        GameMaterialIds.Wood);
                    authoring.Box(
                        new int3(x, braceY, northZ),
                        new int3(4, 3, 5),
                        GameMaterialIds.Wood);
                }
            }

            int[] chandelierX = { min.x + 30, min.x + 52 };
            for (int i = 0; i < chandelierX.Length; i++)
            {
                int cx = chandelierX[i];
                int fixtureY = baseY + 39 + i * 2;
                authoring.Box(
                    new int3(cx - 1, fixtureY + 3, centreZ - 1),
                    new int3(2, 26 - i * 2, 2),
                    GameMaterialIds.Gold);
                authoring.Box(
                    new int3(cx - 10, fixtureY, centreZ - 1),
                    new int3(20, 3, 2),
                    GameMaterialIds.Gold);
                authoring.Box(
                    new int3(cx - 1, fixtureY, centreZ - 10),
                    new int3(2, 3, 20),
                    GameMaterialIds.Gold);

                int2[] lamps =
                {
                    new(-9, 0), new(8, 0), new(0, -9), new(0, 8),
                };
                for (int lamp = 0; lamp < lamps.Length; lamp++)
                    authoring.Box(
                        new int3(
                            cx + lamps[lamp].x - 1,
                            fixtureY - 3,
                            centreZ + lamps[lamp].y - 1),
                        new int3(3, 5, 3),
                        GameMaterialIds.Glass);
            }

            authoring.Gable(
                new int3(min.x - 4, baseY + height, min.z - 4),
                new int3(width + 8, 42, depth + 8),
                false,
                GameMaterialIds.Slate);

            for (int z = min.z + 10; z < min.z + depth - 8; z += 30)
            {
                authoring.Box(
                    new int3(min.x - 8, baseY, z),
                    new int3(10, 46, 9),
                    GameMaterialIds.DarkStone);
                authoring.Box(
                    new int3(min.x - 5, baseY + 40, z + 1),
                    new int3(7, 25, 7),
                    GameMaterialIds.Stone);
            }

            AuthorBellTower(authoring, in plan, baseY);
        }

        private static void AuthorSanctuary(
            IStructureAuthoringSession authoring,
            int3 min,
            int centreZ,
            int baseY)
        {
            authoring.Box(
                new int3(min.x + 7, baseY + 1, centreZ - 27),
                new int3(21, 2, 54),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(min.x + 9, baseY + 3, centreZ - 24),
                new int3(17, 2, 48),
                GameMaterialIds.Stone);
            authoring.Box(
                new int3(min.x + 19, baseY + 7, centreZ - 21),
                new int3(8, 5, 42),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(min.x + 17, baseY + 5, centreZ - 24),
                new int3(3, 9, 4),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(min.x + 17, baseY + 5, centreZ + 20),
                new int3(3, 9, 4),
                GameMaterialIds.Wood);

            for (int panel = -1; panel <= 1; panel++)
            {
                int panelWidth = panel == 0 ? 15 : 11;
                int panelZ = centreZ + panel * 17 - panelWidth / 2;
                authoring.Box(
                    new int3(min.x + 7, baseY + 12, panelZ),
                    new int3(3, panel == 0 ? 28 : 23, panelWidth),
                    GameMaterialIds.Cloth);
                authoring.Box(
                    new int3(min.x + 6, baseY + 10, panelZ - 2),
                    new int3(2, 3, panelWidth + 4),
                    GameMaterialIds.Gold);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int columnZ = centreZ + side * 25 - 4;
                authoring.Box(
                    new int3(min.x + 6, baseY + 7, columnZ),
                    new int3(8, 36, 8),
                    GameMaterialIds.DarkStone);
                authoring.Box(
                    new int3(min.x + 4, baseY + 40, columnZ - 2),
                    new int3(12, 6, 12),
                    GameMaterialIds.Stone);
            }

            authoring.Box(
                new int3(min.x + 5, baseY + 43, centreZ - 31),
                new int3(11, 6, 62),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(min.x + 11, baseY + 20, centreZ - 2),
                new int3(10, 4, 4),
                GameMaterialIds.Gold);
            authoring.Box(
                new int3(min.x + 14, baseY + 14, centreZ - 2),
                new int3(4, 17, 4),
                GameMaterialIds.Gold);

            for (int candle = -2; candle <= 2; candle++)
            {
                int candleZ = centreZ + candle * 7;
                authoring.Box(
                    new int3(min.x + 20, baseY + 12, candleZ - 1),
                    new int3(2, 5 + (candle & 1), 2),
                    GameMaterialIds.Glass);
                authoring.Box(
                    new int3(min.x + 19, baseY + 11, candleZ - 2),
                    new int3(4, 2, 4),
                    GameMaterialIds.Gold);
            }
        }

        private static void AuthorBellTower(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int baseY)
        {
            const int size = CastleLayout.ChapelBellTowerSize;
            int height = plan.FloorHeight * 4;
            int3 centre = CastleLayout.ChapelBellTowerCentre(in plan);
            var min = new int3(centre.x - size / 2, baseY, centre.z - size / 2);

            authoring.Box(
                new int3(min.x - 5, baseY - 16, min.z - 5),
                new int3(size + 10, 20, size + 10),
                GameMaterialIds.DarkStone);
            authoring.HollowBox(
                min,
                new int3(size, height, size),
                6,
                GameMaterialIds.Stone,
                false,
                false);
            authoring.FillBulk(
                new int3(min.x + 6, baseY + 1, min.z + 6),
                new int3(size - 12, height - 1, size - 12),
                GameMaterialIds.Empty);

            for (int floor = 1; floor < 4; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                authoring.Box(
                    new int3(min.x + 6, floorY, min.z + 6),
                    new int3(size - 12, 3, size - 12),
                    GameMaterialIds.Wood);
            }

            int stairX = min.x + size - 19;
            int stairZ = min.z + size / 2;
            authoring.SpiralStair(
                stairX,
                baseY + 2,
                stairZ,
                CastleLayout.ChapelBellTowerStairRadius,
                height - 4,
                GameMaterialIds.Stone);

            int connectorX = centre.x;
            int keepDepth = plan.KeepHalfZ * 2;
            int chapelDepth = math.max(96, keepDepth * 3 / 5);
            int chapelCentreZ = min.z + 6 - chapelDepth / 2;
            int aisleStartZ = chapelCentreZ - 6;
            authoring.Box(
                new int3(connectorX - 8, baseY, aisleStartZ),
                new int3(16, 2, min.z + 12 - aisleStartZ),
                GameMaterialIds.Stone);
            authoring.Arch(
                new int3(connectorX - 9, baseY + 2, min.z - 9),
                18,
                32,
                18,
                2,
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(connectorX - 7, baseY + 2, aisleStartZ),
                new int3(14, 24, min.z + 12 - aisleStartZ),
                GameMaterialIds.Empty);

            for (int floor = 0; floor < 4; floor++)
            {
                int windowY = baseY + floor * plan.FloorHeight + 12;
                int windowHeight = plan.FloorHeight - 18;

                authoring.Arch(
                    new int3(min.x - 2, windowY, centre.z - 7),
                    14,
                    windowHeight,
                    10,
                    0,
                    GameMaterialIds.Empty);
                authoring.Box(
                    new int3(min.x + 2, windowY + 4, centre.z - 4),
                    new int3(2, windowHeight - 9, 8),
                    GameMaterialIds.LitWindow);
                authoring.Box(
                    new int3(min.x - 4, windowY - 3, centre.z - 11),
                    new int3(5, 3, 22),
                    GameMaterialIds.DarkStone);

                for (int side = -1; side <= 1; side += 2)
                {
                    if (floor == 0 && side < 0) continue;

                    int z = side < 0 ? min.z - 2 : min.z + size - 8;
                    authoring.Arch(
                        new int3(centre.x - 7, windowY, z),
                        14,
                        windowHeight,
                        10,
                        2,
                        GameMaterialIds.Empty);
                    int glassZ = side < 0 ? min.z + 2 : min.z + size - 4;
                    authoring.Box(
                        new int3(centre.x - 4, windowY + 4, glassZ),
                        new int3(8, windowHeight - 9, 2),
                        GameMaterialIds.LitWindow);
                }
            }

            for (int floor = 0; floor < 3; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                authoring.Box(
                    new int3(min.x + 8, floorY + 3, min.z + 9),
                    new int3(10, 24, 18),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(min.x + 19, floorY + 8, min.z + 11),
                    new int3(15, 4, 12),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(min.x + 21, floorY + 3, min.z + 13),
                    new int3(4, 6, 4),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(min.x + 28, floorY + 12, min.z + 14),
                    new int3(3, 7, 3),
                    floor == 2 ? GameMaterialIds.Glass : GameMaterialIds.Gold);
            }

            int bellY = baseY + plan.FloorHeight * 3 + 14;
            authoring.Box(
                new int3(min.x + 9, bellY - 8, centre.z - 2),
                new int3(size - 31, 4, 4),
                GameMaterialIds.Wood);
            for (int i = 0; i < 2; i++)
            {
                int bellX = min.x + 17 + i * 16;
                authoring.Box(
                    new int3(bellX, bellY, centre.z - 5),
                    new int3(9, 10, 10),
                    GameMaterialIds.Gold);
                authoring.Box(
                    new int3(bellX + 3, bellY + 10, centre.z - 2),
                    new int3(3, 10, 3),
                    GameMaterialIds.Wood);
            }

            int topY = baseY + height;
            authoring.Box(
                new int3(min.x - 5, topY, min.z - 5),
                new int3(size + 10, 7, size + 10),
                GameMaterialIds.DarkStone);
            for (int x = min.x - 4; x < min.x + size + 2; x += 18)
            {
                authoring.Box(
                    new int3(x, topY + 7, min.z - 4),
                    new int3(11, 15, 8),
                    GameMaterialIds.Stone);
                authoring.Box(
                    new int3(x, topY + 7, min.z + size - 4),
                    new int3(11, 15, 8),
                    GameMaterialIds.Stone);
            }
            authoring.Gable(
                new int3(min.x + 2, topY + 10, min.z + 2),
                new int3(size - 4, 46, size - 4),
                true,
                GameMaterialIds.Slate);
            authoring.Box(
                new int3(centre.x - 1, topY + 53, centre.z - 1),
                new int3(3, 25, 3),
                GameMaterialIds.Gold);
            authoring.Box(
                new int3(centre.x + 2, topY + 66, centre.z - 1),
                new int3(20, 9, 3),
                GameMaterialIds.Cloth);
        }
    }
}
