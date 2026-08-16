using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Furnishes semantic floors inside the current keep recipe. Fixed furniture is deterministic
    /// geometry; variable accents come from planning on the spatial path and retain the historical
    /// RNG loop only on the dimension-only compatibility path.
    /// </summary>
    internal static class CastleRoomFurnisher
    {
        internal static void Furnish(ref VoxelBrush brush, in CastlePlan plan,
                                     int3 min, int3 size, int y, int floor)
        {
            FurnishFixed(ref brush, in plan, min, size, y, floor);

            var rng = new Random(plan.Seed ^ (uint)(floor * 7919 + 13));
            FurnishLegacyAccents(ref brush, min, size, y, ref rng);
        }

        internal static void FurnishPlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int y,
            int furnishingRecipe,
            CastleRoomAccentPlan accents)
        {
            if (accents == null)
                throw new InvalidOperationException(
                    "Planned keep furnishing requires planner-owned room accents.");

            FurnishFixed(ref brush, in plan, min, size, y, furnishingRecipe);
            FurnishPlannedAccents(ref brush, min, y, accents);
        }

        private static void FurnishFixed(ref VoxelBrush brush, in CastlePlan plan,
                                         int3 min, int3 size, int y, int floor)
        {
            const int inner = 8;

            // Ground floor is one hall; upper floors are divided.
            if (floor >= 2)
            {
                int split = min.z + size.z / 2;
                brush.Box(new int3(min.x + inner, y, split),
                          new int3(size.x - inner * 2, plan.FloorHeight - 4, 1), Mat.Stone);

                int doorX = min.x + size.x / 2;
                // Preserve the three-voxel timber floor under the opening.
                brush.Box(new int3(doorX - 9, y + 3, split), new int3(18, 27, 1), Mat.Empty);
            }

            int cx = min.x + size.x / 2;
            int cz = min.z + size.z / 2;

            switch (floor)
            {
                case 0: // great hall
                    for (int beamZ = min.z + 22; beamZ < min.z + size.z - 18; beamZ += 34)
                        brush.Box(new int3(min.x + 9, y + plan.FloorHeight - 8, beamZ),
                                  new int3(size.x - 18, 5, 5), Mat.Wood);

                    brush.Box(new int3(cx - 42, y + 8, cz - 9), new int3(84, 3, 18), Mat.Wood);
                    for (int sideX = -1; sideX <= 1; sideX += 2)
                    for (int sideZ = -1; sideZ <= 1; sideZ += 2)
                        brush.Box(new int3(cx + sideX * 34 - 3, y + 1, cz + sideZ * 6 - 3),
                                  new int3(6, 7, 6), Mat.Wood);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        int benchZ = cz + side * 18;
                        brush.Box(new int3(cx - 44, y + 5, benchZ - 3),
                                  new int3(88, 3, 6), Mat.Wood);
                        for (int leg = -1; leg <= 1; leg += 2)
                            brush.Box(new int3(cx + leg * 34 - 2, y + 1, benchZ - 2),
                                      new int3(4, 4, 4), Mat.Wood);
                    }

                    brush.Box(new int3(min.x + inner, y + 1, cz - 24),
                              new int3(10, 40, 48), Mat.DarkStone);
                    brush.Box(new int3(min.x + inner + 2, y + 3, cz - 14),
                              new int3(6, 16, 28), Mat.Empty);
                    brush.Box(new int3(min.x + inner + 5, y + 3, cz - 10),
                              new int3(4, 6, 20), Mat.Gold);

                    brush.Box(new int3(cx + 57, y + 1, cz - 18),
                              new int3(14, 4, 36), Mat.DarkStone);
                    brush.Box(new int3(cx + 60, y + 5, cz - 9),
                              new int3(8, 11, 18), Mat.Wood);
                    brush.Box(new int3(cx + 62, y + 10, cz - 7),
                              new int3(5, 10, 14), Mat.Cloth);

                    brush.Box(new int3(cx - 1, y + 33, cz - 1), new int3(2, 9, 2), Mat.Gold);
                    brush.Box(new int3(cx - 13, y + 30, cz - 1), new int3(26, 3, 2), Mat.Wood);
                    brush.Box(new int3(cx - 1, y + 30, cz - 13), new int3(2, 3, 26), Mat.Wood);
                    int2[] candleOffsets = { new(-12, 0), new(12, 0), new(0, -12), new(0, 12) };
                    foreach (int2 candle in candleOffsets)
                    {
                        brush.Box(new int3(cx + candle.x - 2, y + 27, cz + candle.y - 2),
                                  new int3(4, 6, 4), Mat.Glass);
                        brush.Box(new int3(cx + candle.x - 1, y + 26, cz + candle.y - 1),
                                  new int3(2, 2, 2), Mat.Gold);
                    }

                    for (int side = -1; side <= 1; side += 2)
                    {
                        int hangingX = cx + side * 48;
                        brush.Box(new int3(hangingX - 10, y + 13,
                                           min.z + size.z - inner - 1),
                                  new int3(20, 25, 2), Mat.Cloth);
                        brush.Box(new int3(hangingX - 13, y + 36,
                                           min.z + size.z - inner - 2),
                                  new int3(26, 3, 3), Mat.Gold);
                    }
                    for (int setting = -3; setting <= 3; setting++)
                    {
                        int settingX = cx + setting * 11;
                        brush.Disc(settingX, y + 11, cz, 3, Mat.Gold);
                        if ((setting & 1) == 0)
                            brush.Box(new int3(settingX - 1, y + 12, cz - 1),
                                      new int3(2, 5, 2), Mat.Glass);
                    }

                    for (int side = -1; side <= 1; side += 2)
                    {
                        int lampZ = cz + side * 38;
                        brush.Box(new int3(min.x + inner + 10, y + 16, lampZ - 2),
                                  new int3(4, 8, 4), Mat.Glass);
                        brush.Box(new int3(min.x + inner + 8, y + 14, lampZ - 1),
                                  new int3(3, 3, 3), Mat.Gold);
                    }
                    break;

                case 1: // bedchamber
                    int bedX = cx + 24;
                    int bedZ = cz - 23;

                    for (int beamZ = min.z + 22; beamZ < min.z + size.z - 18; beamZ += 36)
                        brush.Box(new int3(min.x + 9, y + plan.FloorHeight - 7, beamZ),
                                  new int3(size.x - 18, 4, 4), Mat.Wood);

                    for (int bxSide = 0; bxSide <= 1; bxSide++)
                    for (int bzSide = 0; bzSide <= 1; bzSide++)
                        brush.Box(new int3(bedX + bxSide * 22, y + 3, bedZ + bzSide * 39),
                                  new int3(4, 7, 4), Mat.Wood);
                    brush.Box(new int3(bedX, y + 8, bedZ), new int3(26, 3, 43), Mat.Wood);
                    brush.Box(new int3(bedX + 2, y + 11, bedZ + 2), new int3(22, 4, 39), Mat.Cloth);
                    brush.Box(new int3(bedX, y + 3, bedZ + 39), new int3(26, 22, 4), Mat.Wood);
                    brush.Box(new int3(bedX + 3, y + 16, bedZ + 40), new int3(20, 7, 2), Mat.Cloth);

                    brush.Box(new int3(bedX, y + 11, bedZ), new int3(3, 19, 3), Mat.Wood);
                    brush.Box(new int3(bedX + 23, y + 11, bedZ), new int3(3, 19, 3), Mat.Wood);
                    brush.Box(new int3(bedX, y + 11, bedZ + 39), new int3(3, 19, 3), Mat.Wood);
                    brush.Box(new int3(bedX + 23, y + 11, bedZ + 39), new int3(3, 19, 3), Mat.Wood);
                    brush.Box(new int3(bedX, y + 27, bedZ), new int3(26, 3, 5), Mat.Cloth);
                    brush.Box(new int3(bedX, y + 27, bedZ + 37), new int3(26, 3, 5), Mat.Cloth);
                    brush.Box(new int3(bedX, y + 28, bedZ + 3), new int3(3, 2, 34), Mat.Wood);
                    brush.Box(new int3(bedX + 23, y + 28, bedZ + 3), new int3(3, 2, 34), Mat.Wood);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        int tableX = side < 0 ? bedX - 14 : bedX + 31;
                        brush.Box(new int3(tableX, y + 3, bedZ + 4),
                                  new int3(9, 8, 11), Mat.Wood);
                        brush.Box(new int3(tableX + 3, y + 11, bedZ + 7),
                                  new int3(3, 6, 4), Mat.Glass);
                        brush.Box(new int3(tableX + 4, y + 10, bedZ + 8),
                                  new int3(2, 2, 2), Mat.Gold);
                    }

                    brush.Box(new int3(cx - 42, y + 3, cz + 24), new int3(22, 11, 15), Mat.Wood);
                    brush.Box(new int3(cx - 43, y + 13, cz + 23), new int3(24, 3, 17), Mat.Gold);
                    brush.Box(new int3(min.x + size.x - inner - 26, y + 3, min.z + inner + 12),
                              new int3(18, 28, 22), Mat.Wood);
                    brush.Box(new int3(cx - 32, y + 3, cz - 26),
                              new int3(48, 1, 52), Mat.Cloth);

                    brush.Box(new int3(min.x + inner, y + 3, cz + 25),
                              new int3(9, 28, 36), Mat.DarkStone);
                    brush.Arch(new int3(min.x + inner + 1, y + 5, cz + 33),
                               20, 17, 8, 0, Mat.Empty);
                    brush.Box(new int3(min.x + inner + 4, y + 5, cz + 37),
                              new int3(4, 7, 12), Mat.Gold);
                    brush.Box(new int3(min.x + inner - 2, y + 29, cz + 22),
                              new int3(13, 4, 42), Mat.Wood);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int hangingZ = cz + side * 48;
                        brush.Box(new int3(min.x + size.x - inner - 2, y + 15,
                                           hangingZ - 10),
                                  new int3(2, 24, 20), Mat.Cloth);
                        brush.Box(new int3(min.x + size.x - inner - 3, y + 37,
                                           hangingZ - 13),
                                  new int3(3, 3, 26), Mat.Gold);
                    }

                    for (int side = -1; side <= 1; side += 2)
                    {
                        int chairZ = cz + 25 + side * 18;
                        int chairX = min.x + inner + 31;
                        brush.Box(new int3(chairX, y + 4, chairZ - 5),
                                  new int3(10, 4, 10), Mat.Wood);
                        brush.Box(new int3(chairX, y + 8, chairZ - 5),
                                  new int3(4, 13, 10), Mat.Wood);
                        brush.Box(new int3(chairX + 2, y + 8, chairZ - 4),
                                  new int3(7, 3, 8), Mat.Cloth);
                    }
                    brush.Cylinder(min.x + inner + 48, y + 3, cz + 25, 7, 7, Mat.Wood);
                    brush.Disc(min.x + inner + 48, y + 10, cz + 25, 9, Mat.Gold);

                    int bedLampX = cx - 18;
                    brush.Box(new int3(bedLampX - 1, y + 32, cz - 1),
                              new int3(2, 10, 2), Mat.Gold);
                    brush.Box(new int3(bedLampX - 12, y + 30, cz - 1),
                              new int3(24, 2, 2), Mat.Wood);
                    brush.Box(new int3(bedLampX - 1, y + 30, cz - 10),
                              new int3(2, 2, 20), Mat.Wood);
                    int2[] bedroomCandles =
                    {
                        new(-10, 0), new(10, 0), new(-5, -8), new(5, -8),
                        new(-5, 8), new(5, 8),
                    };
                    foreach (int2 candle in bedroomCandles)
                        brush.Box(new int3(bedLampX + candle.x - 2, y + 27,
                                           cz + candle.y - 2),
                                  new int3(4, 6, 4), Mat.Glass);
                    break;

                default: // library / stores
                    for (int i = 0; i < 4; i++)
                    {
                        int shelfZ = min.z + inner + 10 + i * 34;
                        brush.Box(new int3(min.x + inner + 4, y + 3, shelfZ),
                                  new int3(14, 34, 24), Mat.Wood);
                        brush.Box(new int3(min.x + size.x - inner - 18, y + 3, shelfZ),
                                  new int3(14, 34, 24), Mat.Wood);
                        for (int shelf = 0; shelf < 3; shelf++)
                        {
                            for (int book = 0; book < 6; book++)
                            {
                                byte books = (book + i + shelf) % 3 == 0 ? Mat.Gold : Mat.Cloth;
                                int bookHeight = 4 + ((book * 3 + i + shelf) % 4);
                                int bookZ = shelfZ + 2 + book * 3;
                                brush.Box(new int3(min.x + inner + 17,
                                                   y + 8 + shelf * 9, bookZ),
                                          new int3(3, bookHeight, 2), books);
                                brush.Box(new int3(min.x + size.x - inner - 20,
                                                   y + 8 + shelf * 9, bookZ),
                                          new int3(3, bookHeight, 2), books);
                            }
                        }
                    }

                    for (int side = -1; side <= 1; side += 2)
                    {
                        int deskZ = cz + side * 43;
                        brush.Box(new int3(cx - 22, y + 10, deskZ - 10),
                                  new int3(44, 3, 20), Mat.Wood);
                        brush.Box(new int3(cx - 18, y + 3, deskZ - 7),
                                  new int3(5, 7, 5), Mat.Wood);
                        brush.Box(new int3(cx + 13, y + 3, deskZ + 2),
                                  new int3(5, 7, 5), Mat.Wood);
                        brush.Box(new int3(cx - 2, y + 13, deskZ - 1),
                                  new int3(4, 3, 3), Mat.Glass);

                        for (int book = 0; book < 3; book++)
                            brush.Box(new int3(cx - 15 + book * 8, y + 13 + book,
                                               deskZ + 4),
                                      new int3(7, 1, 9),
                                      (book & 1) == 0 ? Mat.Cloth : Mat.Gold);

                        brush.Box(new int3(cx + 25, y + 4, deskZ - 4),
                                  new int3(9, 4, 9), Mat.Wood);
                        brush.Box(new int3(cx + 31, y + 8, deskZ - 4),
                                  new int3(3, 12, 9), Mat.Wood);
                    }

                    brush.Box(new int3(cx - 42, y + 3, cz - 31),
                              new int3(84, 1, 62), Mat.Cloth);
                    for (int beamZ = min.z + 24; beamZ < min.z + size.z - 20; beamZ += 38)
                        brush.Box(new int3(min.x + 9, y + plan.FloorHeight - 7, beamZ),
                                  new int3(size.x - 18, 4, 4), Mat.Wood);

                    for (int roomSide = -1; roomSide <= 1; roomSide += 2)
                    {
                        int lampZ = cz + roomSide * 42;
                        brush.Box(new int3(cx - 1, y + 31, lampZ - 1),
                                  new int3(2, 10, 2), Mat.Gold);
                        brush.Box(new int3(cx - 10, y + 29, lampZ - 1),
                                  new int3(20, 2, 2), Mat.Wood);
                        brush.Box(new int3(cx - 1, y + 29, lampZ - 10),
                                  new int3(2, 2, 20), Mat.Wood);
                        int2[] libraryCandles =
                        {
                            new(-9, 0), new(9, 0), new(0, -9), new(0, 9),
                        };
                        foreach (int2 candle in libraryCandles)
                            brush.Box(new int3(cx + candle.x - 2, y + 25,
                                               lampZ + candle.y - 2),
                                      new int3(4, 6, 4), Mat.Glass);

                        brush.Box(new int3(min.x + inner + 17, y + 18, lampZ - 2),
                                  new int3(4, 7, 4), Mat.Glass);
                        brush.Box(new int3(min.x + size.x - inner - 21, y + 18, lampZ - 2),
                                  new int3(4, 7, 4), Mat.Glass);
                    }
                    break;
            }
        }

        private static void FurnishLegacyAccents(
            ref VoxelBrush brush,
            int3 min,
            int3 size,
            int y,
            ref Random rng)
        {
            const int inner = 8;
            // Keep the legacy number and order of random draws stable for compatibility builds.
            for (int i = 0; i < rng.NextInt(2, 5); i++)
            {
                bool leftWall = rng.NextBool();
                int px = leftWall ? min.x + inner + 22 : min.x + size.x - inner - 30;
                int pz = rng.NextInt(min.z + inner + 8, min.z + size.z - inner - 12);
                int radius = rng.NextInt(4, 7);
                int height = rng.NextInt(8, 14);
                BuildAccent(ref brush, px, pz, y, radius, height);
            }
        }

        private static void FurnishPlannedAccents(
            ref VoxelBrush brush,
            int3 min,
            int y,
            CastleRoomAccentPlan accents)
        {
            for (int i = 0; i < accents.Count; i++)
            {
                CastleRoomAccentSpec accent = accents.AccentAt(i);
                BuildAccent(
                    ref brush,
                    min.x + accent.LocalX,
                    min.z + accent.LocalZ,
                    y,
                    accent.Radius,
                    accent.Height);
            }
        }

        private static void BuildAccent(
            ref VoxelBrush brush,
            int px,
            int pz,
            int y,
            int radius,
            int height)
        {
            brush.Cylinder(px, y + 3, pz, radius, height, Mat.Wood);
            brush.Box(new int3(px - radius, y + 7, pz - radius - 1),
                      new int3(radius * 2, 2, radius * 2 + 2), Mat.Gold);
        }
    }
}
