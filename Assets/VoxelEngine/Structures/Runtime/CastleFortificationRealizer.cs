using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the defensive shell of a castle: curtain walls, corner towers, and gatehouse.
    /// Courtyard occupation, keep interiors, underground spaces, and landscape dressing remain
    /// separate realization concerns.
    /// </summary>
    internal static class CastleFortificationRealizer
    {
        internal static void CurtainWalls(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.BaileyHalfX, hz = plan.BaileyHalfZ;
            int t = plan.WallThickness;
            int h = plan.WallHeight;

            WallRun(ref brush, in plan, new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                    new int3(1, 0, 0), hx * 2, t, h, true);
            WallRun(ref brush, in plan, new int3(plan.Centre.x - hx, baseY, plan.Centre.z + hz - t),
                    new int3(1, 0, 0), hx * 2, t, h, true);
            WallRun(ref brush, in plan, new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                    new int3(0, 0, 1), hz * 2, t, h, false);
            WallRun(ref brush, in plan, new int3(plan.Centre.x + hx - t, baseY, plan.Centre.z - hz),
                    new int3(0, 0, 1), hz * 2, t, h, false);

            CurtainFacadeDetails(ref brush, in plan, baseY);
        }

        internal static void CornerTowers(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.BaileyHalfX, hz = plan.BaileyHalfZ;

            int3[] corners =
            {
                new(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                new(plan.Centre.x + hx, baseY, plan.Centre.z - hz),
                new(plan.Centre.x - hx, baseY, plan.Centre.z + hz),
                new(plan.Centre.x + hx, baseY, plan.Centre.z + hz),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                int heightVariation = i == 0 ? 58 : i == 1 ? 8 : i == 2 ? 30 : 14;
                int towerHeight = plan.TowerHeight + heightVariation;
                CastleTowerRealizer.Build(ref brush, in plan, corners[i], plan.TowerRadius,
                                          towerHeight, i >= 2);
                if (i < 2)
                    FrontTowerWindows(ref brush, corners[i], plan.TowerRadius,
                                      towerHeight, plan.FloorHeight);
            }
        }

        internal static void Gatehouse(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int r = plan.GateTowerRadius;
            int spacing = 54;

            var left = new int3(plan.Centre.x - spacing, baseY, gateZ);
            var right = new int3(plan.Centre.x + spacing, baseY, gateZ);

            int blockHeight = plan.WallHeight + 22;
            brush.Box(new int3(plan.Centre.x - spacing, baseY, gateZ - plan.WallThickness),
                      new int3(spacing * 2, blockHeight, plan.WallThickness * 2), Mat.Stone);

            int leftHeight = plan.GateTowerHeight + 38;
            int rightHeight = plan.GateTowerHeight + 12;
            CastleTowerRealizer.Build(ref brush, in plan, left, r, leftHeight, false);
            CastleTowerRealizer.Build(ref brush, in plan, right, r, rightHeight, false);
            FrontTowerWindows(ref brush, left, r, leftHeight, plan.FloorHeight);
            FrontTowerWindows(ref brush, right, r, rightHeight, plan.FloorHeight);

            brush.Arch(new int3(plan.Centre.x - 26, baseY, gateZ - plan.WallThickness),
                       52, 74, plan.WallThickness * 2, 2, Mat.Empty);

            brush.Arch(CastleLayout.FrontGateMinimum(in plan), CastleLayout.FrontGateWidth,
                       CastleLayout.FrontGateHeight, CastleLayout.FrontGateDepth, 2, Mat.Wood);
            int3 gateMin = CastleLayout.FrontGateMinimum(in plan);
            for (int band = 0; band < 3; band++)
                brush.Box(new int3(gateMin.x + 2, gateMin.y + 10 + band * 13, gateMin.z),
                          new int3(CastleLayout.FrontGateWidth - 4, 3,
                                   CastleLayout.FrontGateDepth), Mat.DarkStone);
            brush.Box(new int3(plan.Centre.x - 2, gateMin.y + 2, gateMin.z),
                      new int3(4, 44, CastleLayout.FrontGateDepth), Mat.DarkStone);
            for (int side = -1; side <= 1; side += 2)
                brush.Box(new int3(plan.Centre.x + side * 8 - 2, gateMin.y + 23, gateMin.z),
                          new int3(4, 4, 2), Mat.Gold);

            brush.Box(new int3(plan.Centre.x - 28, baseY + 74, gateZ - 4),
                      new int3(56, 6, 8), Mat.Empty);

            for (int i = 0; i < 9; i++)
            {
                int x = plan.Centre.x - 36 + i * 9;
                brush.Box(new int3(x, baseY + plan.WallHeight + 6,
                                   gateZ - plan.WallThickness - 6),
                          new int3(5, 14, 6), Mat.DarkStone);
            }

            brush.Crenellate(
                new int3(plan.Centre.x - spacing, baseY + blockHeight,
                         gateZ - plan.WallThickness),
                new int3(1, 0, 0), spacing * 2, 8, 18, 18, 12, Mat.Stone);

            for (int side = -1; side <= 1; side += 2)
            {
                int bannerX = plan.Centre.x + side * 29;
                brush.Box(new int3(bannerX - 7, baseY + 52,
                                   gateZ - plan.WallThickness - 2),
                          new int3(14, 42, 2), Mat.Cloth);
                brush.Box(new int3(bannerX - 10, baseY + 92,
                                   gateZ - plan.WallThickness - 3),
                          new int3(20, 3, 3), Mat.Gold);
            }

            for (int z = 0; z < 150; z++)
            for (int x = -34; x <= 34; x++)
                brush.FillColumnBulk(plan.Centre.x + x, baseY - 2, baseY - 1,
                                     gateZ - plan.WallThickness - z, Mat.Wood);

            int bridgeNearZ = gateZ - plan.WallThickness - 149;
            int bridgeFarZ = gateZ - plan.WallThickness;
            for (int side = -1; side <= 1; side += 2)
                brush.Box(new int3(plan.Centre.x + side * 25 - 4, baseY - 7, bridgeNearZ),
                          new int3(8, 5, 150), Mat.DarkStone);

            int riverZ = gateZ - plan.WallThickness - 92;
            int riverY = baseY - CastleLayout.LowerRiverDepth;
            int[] pierOffsets = { -27, 0, 27 };
            for (int p = 0; p < pierOffsets.Length; p++)
            for (int side = -1; side <= 1; side += 2)
            {
                int pierZ = riverZ + pierOffsets[p];
                brush.Box(new int3(plan.Centre.x + side * 24 - 6, riverY - 2, pierZ - 6),
                          new int3(12, baseY - riverY - 5, 12), Mat.DarkStone);
                brush.Box(new int3(plan.Centre.x + side * 24 - 9, baseY - 12, pierZ - 8),
                          new int3(18, 6, 16), Mat.Stone);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int railX = plan.Centre.x + side * 32;
                brush.Box(new int3(railX - 2, baseY + 8, bridgeNearZ),
                          new int3(4, 4, 150), Mat.Wood);
                for (int z = bridgeNearZ; z <= bridgeFarZ; z += 24)
                    brush.Box(new int3(railX - 3, baseY - 1, z),
                              new int3(6, 17, 6), Mat.Wood);
            }
            brush.Box(new int3(plan.Centre.x - 42, baseY - 12, bridgeNearZ - 8),
                      new int3(84, 12, 14), Mat.DarkStone);
            brush.Box(new int3(plan.Centre.x - 40, baseY - 5, bridgeFarZ - 5),
                      new int3(80, 7, 12), Mat.Stone);
        }

        private static void CurtainFacadeDetails(ref VoxelBrush brush, in CastlePlan plan,
                                                 int baseY)
        {
            int hx = plan.BaileyHalfX;
            int hz = plan.BaileyHalfZ;
            int gateZ = plan.Centre.z - hz;
            int wallTop = baseY + plan.WallHeight;

            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 3; bay++)
            {
                int x = plan.Centre.x + side * (112 + bay * 58);
                if (math.abs(x - plan.Centre.x) >= hx - plan.TowerRadius) continue;

                brush.Box(new int3(x - 7, baseY, gateZ - 12),
                          new int3(14, 58, 14), Mat.DarkStone);
                brush.Box(new int3(x - 5, baseY + 50, gateZ - 9),
                          new int3(10, plan.WallHeight - 44, 11), Mat.Stone);
                brush.Box(new int3(x - 9, baseY + 52, gateZ - 14),
                          new int3(18, 5, 16), Mat.Stone);

                int panelX = x + side * 26 - 10;
                brush.Arch(new int3(panelX, baseY + 28, gateZ - 2),
                           20, 38, 3, 2, Mat.DarkStone);
                brush.Arch(new int3(panelX + 6, baseY + 39, gateZ - 3),
                           8, 21, 4, 2, Mat.Empty);
            }

            for (int x = plan.Centre.x - hx + plan.TowerRadius;
                 x <= plan.Centre.x + hx - plan.TowerRadius; x += 24)
            {
                if (math.abs(x - plan.Centre.x) < 82) continue;
                brush.Box(new int3(x - 5, wallTop - 8, gateZ - 10),
                          new int3(10, 12, 12), Mat.DarkStone);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int x = plan.Centre.x + side * 132;
                brush.Cylinder(x, wallTop + 1, gateZ + plan.WallThickness / 2,
                               14, 28, Mat.Stone);
                brush.Cylinder(x, wallTop + 25, gateZ + plan.WallThickness / 2,
                               17, 5, Mat.DarkStone, 10);
                brush.Cone(x, wallTop + 29, gateZ + plan.WallThickness / 2,
                           16, 32, Mat.Slate);
                brush.Box(new int3(x, wallTop + 60, gateZ + plan.WallThickness / 2),
                          new int3(2, 15, 2), Mat.Gold);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                const int width = 58;
                int galleryX = plan.Centre.x + side * (hx * 3 / 5) - width / 2;
                int galleryY = wallTop - 34;
                int galleryZ = gateZ - 17;

                brush.Box(new int3(galleryX, galleryY, galleryZ),
                          new int3(width, 4, 20), Mat.Wood);
                for (int post = 4; post < width - 2; post += 16)
                {
                    brush.Box(new int3(galleryX + post, galleryY + 4, galleryZ + 2),
                              new int3(3, 21, 3), Mat.Wood);
                    brush.Box(new int3(galleryX + post, galleryY - 10, galleryZ + 13),
                              new int3(4, 12, 4), Mat.DarkStone);
                }
                brush.Box(new int3(galleryX + 2, galleryY + 13, galleryZ),
                          new int3(width - 4, 3, 3), Mat.Wood);
                brush.Box(new int3(galleryX - 3, galleryY + 24, galleryZ - 2),
                          new int3(width + 6, 3, 24), Mat.Tile);
                brush.Box(new int3(galleryX + 3, galleryY + 7, galleryZ + 3),
                          new int3(4, 7, 4), Mat.LitWindow);
                brush.Box(new int3(galleryX + width - 7, galleryY + 7, galleryZ + 3),
                          new int3(4, 7, 4), Mat.LitWindow);
            }

            for (int side = -1; side <= 1; side += 2)
            for (int z = plan.Centre.z - hz + 76; z < plan.Centre.z + hz - 54; z += 82)
            {
                int x = plan.Centre.x + side * hx;
                int outerX = x + side * 2;
                brush.Box(new int3(outerX + (side < 0 ? -10 : 0), baseY, z - 6),
                          new int3(10, 62, 12), Mat.DarkStone);
                brush.Box(new int3(outerX + (side < 0 ? -7 : 0), baseY + 54, z - 5),
                          new int3(7, plan.WallHeight - 48, 10), Mat.Stone);
            }

            int2[] frontWeathering =
            {
                new(-190, 15), new(-146, 7), new(-94, 24),
                new(103, 10), new(158, 27), new(205, 13),
            };
            for (int i = 0; i < frontWeathering.Length; i++)
            {
                int patchX = plan.Centre.x + frontWeathering[i].x;
                int patchY = baseY + frontWeathering[i].y;
                int width = 13 + (i * 7 % 17);
                int height = 5 + (i * 5 % 13);
                if (math.abs(patchX - plan.Centre.x) > hx - plan.TowerRadius - width) continue;
                brush.Box(new int3(patchX, patchY, gateZ - 2),
                          new int3(width, height, 2), Mat.Moss);
                if ((i & 1) == 0)
                    brush.Box(new int3(patchX + width / 3, baseY + 2, gateZ - 2),
                              new int3(5, frontWeathering[i].y + 4, 2), Mat.Moss);
            }
        }

        private static void WallRun(ref VoxelBrush brush, in CastlePlan plan, int3 start, int3 dir,
                                    int length, int thickness, int height, bool alongX)
        {
            int3 wallSize = alongX
                ? new int3(length, height, thickness)
                : new int3(thickness, height, length);
            brush.FillBulk(start, wallSize, Mat.Stone);

            int3 plinthSize = alongX
                ? new int3(length, 22, thickness)
                : new int3(thickness, 22, length);
            brush.FillBulk(start, plinthSize, Mat.DarkStone);

            int courseY = (int)(height * 0.66f);
            int3 courseMin = start + new int3(0, courseY, 0);
            int3 courseSize = alongX
                ? new int3(length, 2, thickness)
                : new int3(thickness, 2, length);
            brush.FillBulk(courseMin, courseSize, Mat.DarkStone);

            int3 walkMin = start + new int3(0, height, 0);
            int3 walkSize = alongX
                ? new int3(length, 1, thickness)
                : new int3(thickness, 1, length);
            brush.FillBulk(walkMin, walkSize, Mat.Stone);

            for (int i = 40; i < length; i += 90)
            {
                int3 slitMin = start + dir * i + new int3(0, 40, 0);
                int3 slitSize = alongX
                    ? new int3(1, 28, thickness)
                    : new int3(thickness, 28, 1);
                brush.FillBulk(slitMin, slitSize, Mat.Empty);
            }

            int parapetY = start.y + height + 1;
            int merlon = 26, gap = 18;
            for (int i = 0; i < length; i += merlon + gap)
            {
                int3 at = start + dir * i;
                int blockLength = math.min(merlon, length - i);
                int3 blockSize = alongX
                    ? new int3(blockLength, 20, 8)
                    : new int3(8, 20, blockLength);
                brush.FillBulk(new int3(at.x, parapetY, at.z), blockSize, Mat.Stone);
            }

            if (length > 400)
            {
                for (int i = 120; i < length - 120; i += 200)
                {
                    int3 at = start + dir * i;
                    int3 bannerSize = alongX
                        ? new int3(1, 46, 14)
                        : new int3(14, 46, 1);
                    brush.FillBulk(new int3(at.x, start.y + height - 60, at.z),
                                   bannerSize, Mat.Cloth);
                }
            }
        }

        private static void FrontTowerWindows(ref VoxelBrush brush, int3 at, int radius,
                                              int height, int floorHeight)
        {
            const int width = 14;
            const int windowHeight = 24;
            int frontZ = at.z - radius - 2;

            for (int floor = 1; floor * floorHeight + windowHeight + 12 < height; floor++)
            {
                int y = at.y + floor * floorHeight + 9;
                brush.Arch(new int3(at.x - width / 2 - 3, y - 3, frontZ - 3),
                           width + 6, windowHeight + 6, 5, 2, Mat.DarkStone);
                brush.Arch(new int3(at.x - width / 2, y, frontZ - 4),
                           width, windowHeight, 20, 2, Mat.Empty);
                brush.Arch(new int3(at.x - width / 2 + 3, y + 3, frontZ + 2),
                           width - 6, windowHeight - 7, 2, 2, Mat.LitWindow);
                brush.Box(new int3(at.x - 1, y + 4, frontZ + 1),
                          new int3(2, windowHeight - 10, 3), Mat.DarkStone);
                brush.Box(new int3(at.x - width / 2 + 3, y + windowHeight / 2, frontZ + 1),
                          new int3(width - 6, 2, 3), Mat.DarkStone);
                brush.Box(new int3(at.x - width / 2 - 4, y - 4, frontZ - 4),
                          new int3(width + 8, 3, 6), Mat.DarkStone);
            }
        }
    }
}
