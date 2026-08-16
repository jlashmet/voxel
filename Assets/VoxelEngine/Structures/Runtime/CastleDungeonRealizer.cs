using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the designed underground spaces beneath the castle: cellar, ruined hall,
    /// side chambers, and secret passage. Natural cave geometry is delegated separately.
    /// </summary>
    internal static class CastleDungeonRealizer
    {
        internal static void Build(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int cellarY = baseY - 46;
            int dungeonY = cellarY - 120;

            int hx = plan.KeepHalfX;
            int hz = plan.KeepHalfZ;
            var keepMin = new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz + 60);

            brush.FillBulk(new int3(keepMin.x + 10, cellarY, keepMin.z + 10),
                           new int3(hx * 2 - 20, 40, hz * 2 - 20), Mat.Empty);
            brush.Box(new int3(keepMin.x + 8, cellarY - 4, keepMin.z + 8),
                      new int3(hx * 2 - 16, 4, hz * 2 - 16), Mat.DarkStone);

            for (int z = keepMin.z + 18; z < keepMin.z + hz * 2 - 30; z += 30)
            {
                brush.Box(new int3(keepMin.x + 14, cellarY, z),
                          new int3(12, 28, 20), Mat.Wood);
                brush.Box(new int3(keepMin.x + hx * 2 - 26, cellarY, z),
                          new int3(12, 28, 20), Mat.Wood);

                for (int shelf = 0; shelf < 3; shelf++)
                for (int book = 0; book < 5; book++)
                {
                    int bookZ = z + 2 + book * 3;
                    int bookY = cellarY + 5 + shelf * 8;
                    int bookHeight = 4 + ((book + shelf * 2 + z) & 3);
                    byte bookMaterial = ((book + shelf) & 2) == 0 ? Mat.Cloth : Mat.Gold;
                    brush.Box(new int3(keepMin.x + 25, bookY, bookZ),
                              new int3(3, bookHeight, 2), bookMaterial);
                    brush.Box(new int3(keepMin.x + hx * 2 - 28, bookY, bookZ),
                              new int3(3, bookHeight, 2), bookMaterial);
                }
            }

            for (int beamZ = keepMin.z + 18; beamZ < keepMin.z + hz * 2 - 20; beamZ += 38)
            {
                brush.Box(new int3(keepMin.x + 10, cellarY + 34, beamZ),
                          new int3(hx * 2 - 20, 4, 4), Mat.Wood);
            }
            brush.Box(new int3(plan.Centre.x - 12, cellarY, keepMin.z + 18),
                      new int3(24, 1, hz * 2 - 42), Mat.Cloth);

            int archiveDeskX = plan.Centre.x - 55;
            int archiveDeskZ = keepMin.z + hz;
            brush.Box(new int3(archiveDeskX - 18, cellarY + 8, archiveDeskZ - 10),
                      new int3(36, 3, 20), Mat.Wood);
            brush.Box(new int3(archiveDeskX - 14, cellarY + 1, archiveDeskZ - 7),
                      new int3(5, 7, 5), Mat.Wood);
            brush.Box(new int3(archiveDeskX + 9, cellarY + 1, archiveDeskZ + 2),
                      new int3(5, 7, 5), Mat.Wood);
            for (int folio = 0; folio < 3; folio++)
            {
                brush.Box(new int3(archiveDeskX - 10 + folio * 8,
                                   cellarY + 11 + folio, archiveDeskZ - 4),
                          new int3(7, 1, 10), folio == 1 ? Mat.Gold : Mat.Cloth);
            }
            brush.Box(new int3(archiveDeskX + 23, cellarY + 4, archiveDeskZ - 5),
                      new int3(9, 4, 10), Mat.Wood);
            brush.Box(new int3(archiveDeskX + 29, cellarY + 8, archiveDeskZ - 5),
                      new int3(3, 12, 10), Mat.Wood);

            for (int side = -1; side <= 1; side += 2)
            {
                int lampX = plan.Centre.x + side * 55;
                brush.Box(new int3(lampX - 2, cellarY + 17, keepMin.z + hz - 2),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(lampX - 3, cellarY + 14, keepMin.z + hz - 1),
                          new int3(6, 3, 3), Mat.Gold);
            }
            brush.Box(new int3(keepMin.x + 38, cellarY + 1, keepMin.z + 24),
                      new int3(28, 10, 18), Mat.Wood);
            brush.Box(new int3(keepMin.x + 42, cellarY + 11, keepMin.z + 28),
                      new int3(20, 4, 10), Mat.Gold);
            for (int i = 0; i < 4; i++)
            {
                int bx = keepMin.x + hx * 2 - 42 - (i & 1) * 18;
                int bz = keepMin.z + 24 + (i >> 1) * 22;
                brush.Cylinder(bx, cellarY, bz, 6, 12, Mat.Wood);
                brush.Box(new int3(bx - 5, cellarY + 5, bz - 7),
                          new int3(10, 2, 14), Mat.Gold);
            }

            int3 trapdoor = CastleLayout.TrapdoorCentre(in plan);
            int tx = trapdoor.x;
            int tz = trapdoor.z;
            brush.Box(new int3(tx - 10, cellarY + 40, tz - 10),
                      new int3(20, 8, 20), Mat.Empty);
            brush.SpiralStair(tx, cellarY, tz, 9, 46, Mat.Stone);

            brush.Box(new int3(tx - CastleLayout.TrapdoorHalfSize, baseY,
                               tz - CastleLayout.TrapdoorHalfSize),
                      new int3(CastleLayout.TrapdoorHalfSize * 2, 2,
                               CastleLayout.TrapdoorHalfSize * 2), Mat.Wood);
            brush.Box(new int3(tx - CastleLayout.TrapdoorHalfSize, baseY + 2,
                               tz - CastleLayout.TrapdoorHalfSize),
                      new int3(3, 2, CastleLayout.TrapdoorHalfSize * 2), Mat.Gold);
            brush.Box(new int3(tx + CastleLayout.TrapdoorHalfSize - 3, baseY + 2,
                               tz - CastleLayout.TrapdoorHalfSize),
                      new int3(3, 2, CastleLayout.TrapdoorHalfSize * 2), Mat.Gold);

            brush.Cylinder(tx, dungeonY, tz, 16, cellarY - dungeonY, Mat.Empty);
            brush.SpiralStair(tx, dungeonY, tz, 13, cellarY - dungeonY, Mat.Stone);

            var hallMin = new int3(tx - 130, dungeonY, tz - 90);
            brush.FillBulk(hallMin, new int3(260, 46, 180), Mat.Empty);
            brush.Box(new int3(hallMin.x - 6, dungeonY - 5, hallMin.z - 6),
                      new int3(272, 5, 192), Mat.DarkStone);

            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 2; j++)
            {
                int px = hallMin.x + 50 + i * 80;
                int pz = hallMin.z + 55 + j * 70;
                brush.Cylinder(px, dungeonY, pz, 12, 46, Mat.Stone);
                brush.Cylinder(px, dungeonY + 42, pz, 15, 4, Mat.DarkStone);
                brush.Box(new int3(px - 2, dungeonY + 23, pz - 14),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(px - 2, dungeonY + 20, pz - 13),
                          new int3(4, 3, 3), Mat.Gold);
            }

            brush.Box(new int3(tx - 34, dungeonY, hallMin.z + 18),
                      new int3(68, 5, 26), Mat.DarkStone);
            brush.Box(new int3(tx - 12, dungeonY + 5, hallMin.z + 24),
                      new int3(24, 9, 14), Mat.Stone);
            brush.Box(new int3(tx - 4, dungeonY + 14, hallMin.z + 28),
                      new int3(8, 12, 6), Mat.Gold);
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
            {
                brush.Box(new int3(tx + side * 54 - 20, dungeonY + 1,
                                   hallMin.z + 76 + row * 28),
                          new int3(40, 5, 8), row == 1 ? Mat.DarkStone : Mat.Wood);
            }

            BuildSideChambers(ref brush, tx, tz, dungeonY);

            int passZ = hallMin.z - 1;
            for (int i = 0; i < 320; i++)
            {
                int z = passZ - i;
                int y = dungeonY + (int)math.round(math.sin(i * 0.02f) * 8f);
                for (int x = tx - 14; x < tx + 14; x++)
                    brush.FillColumnBulk(x, y, y + 32, z, Mat.Empty);
                brush.Box(new int3(tx - 16, y - 2, z),
                          new int3(32, 2, 1), Mat.DarkStone);
            }

            CastleCaveRealizer.Build(ref brush, in plan, new int3(tx, dungeonY, passZ - 320));
        }

        private static void BuildSideChambers(ref VoxelBrush brush, int tx, int trapZ, int dungeonY)
        {
            const int corridorHalf = 10;
            const int corridorHeight = 30;

            int puzzleMinX = tx + 176;
            int puzzleMinZ = trapZ - 58;
            brush.Box(new int3(tx + 118, dungeonY + 2, trapZ - corridorHalf),
                      new int3(70, corridorHeight, corridorHalf * 2), Mat.Empty);
            brush.Box(new int3(tx + 118, dungeonY, trapZ - corridorHalf),
                      new int3(70, 2, corridorHalf * 2), Mat.DarkStone);
            brush.FillBulk(new int3(puzzleMinX, dungeonY + 2, puzzleMinZ),
                           new int3(100, 38, 116), Mat.Empty);
            brush.Box(new int3(puzzleMinX, dungeonY, puzzleMinZ),
                      new int3(100, 2, 116), Mat.DarkStone);

            brush.Box(new int3(puzzleMinX + 8, dungeonY + 1, trapZ - 2),
                      new int3(84, 1, 4), Mat.Slate);
            brush.Box(new int3(puzzleMinX + 48, dungeonY + 1, puzzleMinZ + 8),
                      new int3(4, 1, 100), Mat.Slate);
            for (int ring = 0; ring < 3; ring++)
            {
                int inset = 18 + ring * 10;
                brush.Box(new int3(puzzleMinX + inset, dungeonY + 1, puzzleMinZ + 15),
                          new int3(2, 1, 86), ring == 1 ? Mat.Gold : Mat.Cloth);
                brush.Box(new int3(puzzleMinX + 98 - inset, dungeonY + 1, puzzleMinZ + 15),
                          new int3(2, 1, 86), ring == 1 ? Mat.Gold : Mat.Cloth);
            }

            int puzzleCx = puzzleMinX + 50;
            int puzzleCz = trapZ;
            int2[] runeOffsets = { new(-26, -30), new(26, -30), new(-26, 30), new(26, 30) };
            for (int i = 0; i < runeOffsets.Length; i++)
            {
                int px = puzzleCx + runeOffsets[i].x;
                int pz = puzzleCz + runeOffsets[i].y;
                brush.Box(new int3(px - 8, dungeonY + 2, pz - 8),
                          new int3(16, 8, 16), Mat.Stone);
                brush.Disc(px, dungeonY + 10, pz, 6, Mat.DarkStone);
                brush.Cone(px, dungeonY + 11, pz, 3 + (i & 1),
                           8 + i * 2, i % 2 == 0 ? Mat.Glass : Mat.Gold);
                brush.Cone(px + (i < 2 ? 5 : -5), dungeonY + 11, pz + 4,
                           2, 6 + (i & 1) * 2, Mat.Glass);
            }
            brush.Box(new int3(puzzleCx - 14, dungeonY + 2, puzzleCz - 14),
                      new int3(28, 3, 28), Mat.Slate);
            brush.Disc(puzzleCx, dungeonY + 5, puzzleCz, 8, Mat.DarkStone);
            brush.Cone(puzzleCx, dungeonY + 6, puzzleCz, 4, 10, Mat.Glass);
            brush.Cone(puzzleCx - 6, dungeonY + 6, puzzleCz + 4, 2, 7, Mat.Gold);

            int shrineX = puzzleMinX + 91;
            brush.Box(new int3(shrineX - 5, dungeonY + 2, puzzleCz - 28),
                      new int3(7, 30, 7), Mat.Stone);
            brush.Box(new int3(shrineX - 5, dungeonY + 2, puzzleCz + 21),
                      new int3(7, 30, 7), Mat.Stone);
            brush.Box(new int3(shrineX - 6, dungeonY + 28, puzzleCz - 28),
                      new int3(8, 6, 56), Mat.DarkStone);
            brush.Box(new int3(shrineX - 10, dungeonY + 3, puzzleCz - 12),
                      new int3(10, 6, 24), Mat.DarkStone);
            brush.Cone(shrineX - 7, dungeonY + 9, puzzleCz, 4, 16, Mat.Glass);

            for (int arch = 0; arch < 2; arch++)
            {
                int z = puzzleMinZ + 16 + arch * 84;
                brush.Cylinder(puzzleMinX + 12, dungeonY + 2, z, 7, 31, Mat.Stone);
                brush.Cylinder(puzzleMinX + 88, dungeonY + 2, z, 7, 31, Mat.Stone);
            }
            for (int x = puzzleMinX + 15; x < puzzleMinX + 92; x += 25)
            {
                brush.Box(new int3(x, dungeonY + 32, puzzleMinZ + 5),
                          new int3(4, 4, 106), Mat.Wood);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                brush.Box(new int3(puzzleMinX + 48, dungeonY + 18,
                                   trapZ + side * 49 - 2),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(puzzleMinX + 48, dungeonY + 15,
                                   trapZ + side * 49 - 1),
                          new int3(6, 3, 3), Mat.Gold);
            }

            int treasuryMinX = tx - 276;
            int treasuryMinZ = trapZ - 52;
            brush.Box(new int3(tx - 188, dungeonY + 2, trapZ - corridorHalf),
                      new int3(70, corridorHeight, corridorHalf * 2), Mat.Empty);
            brush.Box(new int3(tx - 188, dungeonY, trapZ - corridorHalf),
                      new int3(70, 2, corridorHalf * 2), Mat.DarkStone);
            brush.FillBulk(new int3(treasuryMinX, dungeonY + 2, treasuryMinZ),
                           new int3(100, 36, 104), Mat.Empty);
            brush.Box(new int3(treasuryMinX, dungeonY, treasuryMinZ),
                      new int3(100, 2, 104), Mat.DarkStone);

            for (int x = treasuryMinX + 12; x < treasuryMinX + 94; x += 24)
            {
                brush.Box(new int3(x, dungeonY + 30, treasuryMinZ + 5),
                          new int3(5, 4, 94), Mat.Wood);
            }
            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 3; bay++)
            {
                int x = treasuryMinX + 18 + bay * 30;
                int z = trapZ + side * 45;
                brush.Box(new int3(x - 9, dungeonY + 2, z - 5),
                          new int3(18, 23, 10), Mat.Wood);
                brush.Box(new int3(x - 10, dungeonY + 9, z - 6),
                          new int3(20, 2, 12), Mat.Gold);
                brush.Box(new int3(x - 10, dungeonY + 18, z - 6),
                          new int3(20, 2, 12), Mat.Gold);
            }

            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
            {
                int x = treasuryMinX + 24 + row * 27;
                int z = trapZ + side * 34;
                brush.Box(new int3(x - 8, dungeonY + 2, z - 7),
                          new int3(16, 10, 14), Mat.Wood);
                brush.Box(new int3(x - 9, dungeonY + 10, z - 8),
                          new int3(18, 3, 16), Mat.Gold);
            }
            brush.Box(new int3(treasuryMinX + 18, dungeonY + 1, trapZ - 8),
                      new int3(62, 1, 16), Mat.Cloth);
            brush.Box(new int3(treasuryMinX + 15, dungeonY + 2, treasuryMinZ + 12),
                      new int3(70, 5, 12), Mat.Gold);
            for (int pile = 0; pile < 5; pile++)
            {
                int px = treasuryMinX + 18 + pile * 16;
                int pz = treasuryMinZ + 21 + (pile & 1) * 7;
                brush.Cone(px, dungeonY + 7, pz, 5, 7 + (pile % 3) * 3, Mat.Gold);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                brush.Box(new int3(treasuryMinX + 48, dungeonY + 17,
                                   trapZ + side * 45 - 2),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(treasuryMinX + 48, dungeonY + 14,
                                   trapZ + side * 45 - 1),
                          new int3(6, 3, 3), Mat.Gold);
            }
        }
    }
}
