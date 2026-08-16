using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Castle-specific semantic dressing for a reusable DungeonPlan. DungeonRealizer owns room
    /// shells and circulation; this component only translates room purpose into authored content.
    /// Natural cave geometry remains owned by CastleCaveRealizer.
    /// </summary>
    internal static class CastleDungeonFurnisher
    {
        internal static void Build(ref VoxelBrush brush, DungeonPlan plan)
        {
            if (!DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue))
                throw new InvalidOperationException($"Cannot furnish invalid dungeon plan: {issue}.");

            BuildEntranceHatch(ref brush, plan.Entrance);

            DungeonRoomPlan[] rooms = plan.Rooms;
            for (int i = 0; i < rooms.Length; i++)
            {
                switch (rooms[i].Purpose)
                {
                    case DungeonRoomPurpose.Archive:
                        FurnishArchive(ref brush, in rooms[i]);
                        break;
                    case DungeonRoomPurpose.GreatHall:
                        FurnishGreatHall(ref brush, in rooms[i]);
                        break;
                    case DungeonRoomPurpose.Puzzle:
                        FurnishPuzzle(ref brush, in rooms[i]);
                        break;
                    case DungeonRoomPurpose.Treasury:
                        FurnishTreasury(ref brush, in rooms[i]);
                        break;
                }
            }
        }

        private static void BuildEntranceHatch(ref VoxelBrush brush, int3 trapdoor)
        {
            int half = CastleLayout.TrapdoorHalfSize;
            brush.Box(
                new int3(trapdoor.x - half, trapdoor.y, trapdoor.z - half),
                new int3(half * 2, 2, half * 2),
                Mat.Wood);
            brush.Box(
                new int3(trapdoor.x - half, trapdoor.y + 2, trapdoor.z - half),
                new int3(3, 2, half * 2),
                Mat.Gold);
            brush.Box(
                new int3(trapdoor.x + half - 3, trapdoor.y + 2, trapdoor.z - half),
                new int3(3, 2, half * 2),
                Mat.Gold);
        }

        private static void FurnishArchive(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floorY = min.y;
            int leftX = min.x + 14;
            int rightX = min.x + room.Size.x - 26;
            int firstZ = min.z + 18;
            int lastZ = min.z + room.Size.z - 30;

            for (int z = firstZ; z < lastZ; z += 30)
            {
                brush.Box(new int3(leftX, floorY, z), new int3(12, 28, 20), Mat.Wood);
                brush.Box(new int3(rightX, floorY, z), new int3(12, 28, 20), Mat.Wood);

                for (int shelf = 0; shelf < 3; shelf++)
                for (int book = 0; book < 5; book++)
                {
                    int bookZ = z + 2 + book * 3;
                    int bookY = floorY + 5 + shelf * 8;
                    int bookHeight = 4 + ((book + shelf * 2 + z) & 3);
                    byte material = ((book + shelf) & 2) == 0 ? Mat.Cloth : Mat.Gold;
                    brush.Box(new int3(leftX + 11, bookY, bookZ),
                              new int3(3, bookHeight, 2), material);
                    brush.Box(new int3(rightX - 2, bookY, bookZ),
                              new int3(3, bookHeight, 2), material);
                }
            }

            for (int z = min.z + 18; z < min.z + room.Size.z - 20; z += 38)
            {
                brush.Box(
                    new int3(min.x + 10, floorY + room.Size.y - 6, z),
                    new int3(math.max(4, room.Size.x - 20), 4, 4),
                    Mat.Wood);
            }

            brush.Box(
                new int3(room.Centre.x - 12, floorY, min.z + 18),
                new int3(24, 1, math.max(8, room.Size.z - 42)),
                Mat.Cloth);

            int deskX = room.Centre.x - math.min(55, math.max(18, room.Size.x / 4));
            int deskZ = room.Centre.z;
            brush.Box(new int3(deskX - 18, floorY + 8, deskZ - 10),
                      new int3(36, 3, 20), Mat.Wood);
            for (int folio = 0; folio < 3; folio++)
            {
                brush.Box(
                    new int3(deskX - 10 + folio * 8, floorY + 11 + folio, deskZ - 4),
                    new int3(7, 1, 10),
                    folio == 1 ? Mat.Gold : Mat.Cloth);
            }

            int lampOffset = math.min(55, math.max(20, room.Size.x / 4));
            for (int side = -1; side <= 1; side += 2)
            {
                int lampX = room.Centre.x + side * lampOffset;
                brush.Box(new int3(lampX - 2, floorY + 17, room.Centre.z - 2),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(lampX - 3, floorY + 14, room.Centre.z - 1),
                          new int3(6, 3, 3), Mat.Gold);
            }
        }

        private static void FurnishGreatHall(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floorY = min.y;
            int usableX = math.max(1, room.Size.x - 100);
            int usableZ = math.max(1, room.Size.z - 110);

            for (int ix = 0; ix < 3; ix++)
            for (int iz = 0; iz < 2; iz++)
            {
                int x = min.x + 50 + (usableX * ix) / 2;
                int z = min.z + 55 + usableZ * iz;
                int columnHeight = math.max(18, room.Size.y);
                brush.Cylinder(x, floorY, z, 12, columnHeight, Mat.Stone);
                brush.Cylinder(x, floorY + columnHeight - 4, z, 15, 4, Mat.DarkStone);
                brush.Box(new int3(x - 2, floorY + columnHeight / 2, z - 14),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(x - 2, floorY + columnHeight / 2 - 3, z - 13),
                          new int3(4, 3, 3), Mat.Gold);
            }

            int daisZ = min.z + 18;
            brush.Box(new int3(room.Centre.x - 34, floorY, daisZ),
                      new int3(68, 5, 26), Mat.DarkStone);
            brush.Box(new int3(room.Centre.x - 12, floorY + 5, daisZ + 6),
                      new int3(24, 9, 14), Mat.Stone);
            brush.Box(new int3(room.Centre.x - 4, floorY + 14, daisZ + 10),
                      new int3(8, 12, 6), Mat.Gold);

            int benchOffset = math.min(54, math.max(26, room.Size.x / 4));
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
            {
                int z = min.z + 76 + row * math.max(20, (room.Size.z - 105) / 3);
                if (z + 8 >= min.z + room.Size.z) break;
                brush.Box(new int3(room.Centre.x + side * benchOffset - 20, floorY + 1, z),
                          new int3(40, 5, 8), row == 1 ? Mat.DarkStone : Mat.Wood);
            }
        }

        private static void FurnishPuzzle(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floorY = min.y;
            int halfX = room.Size.x / 2;
            int halfZ = room.Size.z / 2;

            brush.Box(new int3(min.x + 8, floorY + 1, room.Centre.z - 2),
                      new int3(math.max(4, room.Size.x - 16), 1, 4), Mat.Slate);
            brush.Box(new int3(room.Centre.x - 2, floorY + 1, min.z + 8),
                      new int3(4, 1, math.max(4, room.Size.z - 16)), Mat.Slate);

            int runeX = math.max(16, halfX / 2);
            int runeZ = math.max(18, halfZ / 2);
            int2[] offsets =
            {
                new(-runeX, -runeZ), new(runeX, -runeZ),
                new(-runeX, runeZ), new(runeX, runeZ),
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                int x = room.Centre.x + offsets[i].x;
                int z = room.Centre.z + offsets[i].y;
                brush.Box(new int3(x - 8, floorY + 2, z - 8),
                          new int3(16, 8, 16), Mat.Stone);
                brush.Disc(x, floorY + 10, z, 6, Mat.DarkStone);
                brush.Cone(x, floorY + 11, z, 3 + (i & 1),
                           8 + i * 2, i % 2 == 0 ? Mat.Glass : Mat.Gold);
            }

            brush.Box(new int3(room.Centre.x - 14, floorY + 2, room.Centre.z - 14),
                      new int3(28, 3, 28), Mat.Slate);
            brush.Disc(room.Centre.x, floorY + 5, room.Centre.z, 8, Mat.DarkStone);
            brush.Cone(room.Centre.x, floorY + 6, room.Centre.z, 4, 10, Mat.Glass);

            for (int x = min.x + 15; x < min.x + room.Size.x - 8; x += 25)
            {
                brush.Box(new int3(x, floorY + room.Size.y - 8, min.z + 5),
                          new int3(4, 4, math.max(4, room.Size.z - 10)), Mat.Wood);
            }
        }

        private static void FurnishTreasury(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floorY = min.y;

            for (int x = min.x + 12; x < min.x + room.Size.x - 6; x += 24)
            {
                brush.Box(new int3(x, floorY + room.Size.y - 10, min.z + 5),
                          new int3(5, 4, math.max(4, room.Size.z - 10)), Mat.Wood);
            }

            int wallZOffset = math.max(18, room.Size.z / 2 - 7);
            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 3; bay++)
            {
                int x = min.x + 18 + bay * math.max(22, (room.Size.x - 36) / 3);
                if (x + 10 >= min.x + room.Size.x) break;
                int z = room.Centre.z + side * wallZOffset;
                brush.Box(new int3(x - 9, floorY + 2, z - 5),
                          new int3(18, 23, 10), Mat.Wood);
                brush.Box(new int3(x - 10, floorY + 9, z - 6),
                          new int3(20, 2, 12), Mat.Gold);
                brush.Box(new int3(x - 10, floorY + 18, z - 6),
                          new int3(20, 2, 12), Mat.Gold);
            }

            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
            {
                int x = min.x + 24 + row * math.max(20, (room.Size.x - 48) / 3);
                int z = room.Centre.z + side * math.max(16, room.Size.z / 3);
                brush.Box(new int3(x - 8, floorY + 2, z - 7),
                          new int3(16, 10, 14), Mat.Wood);
                brush.Box(new int3(x - 9, floorY + 10, z - 8),
                          new int3(18, 3, 16), Mat.Gold);
            }

            brush.Box(new int3(min.x + 18, floorY + 1, room.Centre.z - 8),
                      new int3(math.max(16, room.Size.x - 36), 1, 16), Mat.Cloth);
            brush.Box(new int3(min.x + 15, floorY + 2, min.z + 12),
                      new int3(math.max(16, room.Size.x - 30), 5, 12), Mat.Gold);
            for (int pile = 0; pile < 5; pile++)
            {
                int x = min.x + 18 + pile * math.max(12, (room.Size.x - 36) / 5);
                if (x >= min.x + room.Size.x - 8) break;
                int z = min.z + 21 + (pile & 1) * 7;
                brush.Cone(x, floorY + 7, z, 5, 7 + (pile % 3) * 3, Mat.Gold);
            }
        }

        private static int3 RoomMin(in DungeonRoomPlan room) =>
            room.Centre - room.Size / 2;
    }
}
