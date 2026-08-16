using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Castle-specific semantic dressing for a generic designed DungeonPlan. Room identity comes
    /// from planning; this component adds only castle flavor and never chooses topology or room
    /// placement. Natural cave dressing remains owned by CastleCaveRealizer.
    /// </summary>
    internal static class CastleDungeonFurnisher
    {
        internal static void Furnish(ref VoxelBrush brush, DungeonPlan plan)
        {
            if (plan == null || plan.Rooms == null) return;

            for (int i = 0; i < plan.Rooms.Length; i++)
            {
                DungeonRoomPlan room = plan.Rooms[i];
                switch (room.Purpose)
                {
                    case DungeonRoomPurpose.Archive:
                        FurnishArchive(ref brush, in room);
                        break;
                    case DungeonRoomPurpose.GreatHall:
                        FurnishGreatHall(ref brush, in room);
                        break;
                    case DungeonRoomPurpose.Puzzle:
                        FurnishPuzzle(ref brush, in room);
                        break;
                    case DungeonRoomPurpose.Treasury:
                        FurnishTreasury(ref brush, in room);
                        break;
                }
            }
        }

        private static void FurnishArchive(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floorY = min.y;
            int shelfHeight = math.max(12, math.min(28, room.Size.y - 8));
            int shelfDepth = math.min(12, math.max(6, room.Size.x / 18));
            int firstZ = min.z + 12;
            int lastZ = min.z + room.Size.z - 24;

            for (int z = firstZ; z <= lastZ; z += 30)
            {
                brush.Box(new int3(min.x + 8, floorY, z),
                          new int3(shelfDepth, shelfHeight, 20), Mat.Wood);
                brush.Box(new int3(min.x + room.Size.x - 8 - shelfDepth, floorY, z),
                          new int3(shelfDepth, shelfHeight, 20), Mat.Wood);

                for (int shelf = 0; shelf < 3; shelf++)
                for (int book = 0; book < 5; book++)
                {
                    int bookZ = z + 2 + book * 3;
                    int bookY = floorY + 5 + shelf * 8;
                    if (bookY + 7 >= floorY + room.Size.y) continue;
                    int bookHeight = 4 + ((book + shelf + room.Id) & 3);
                    byte material = ((book + shelf) & 2) == 0 ? Mat.Cloth : Mat.Gold;
                    brush.Box(new int3(min.x + 8 + shelfDepth, bookY, bookZ),
                              new int3(3, bookHeight, 2), material);
                    brush.Box(new int3(min.x + room.Size.x - 11 - shelfDepth,
                                       bookY, bookZ),
                              new int3(3, bookHeight, 2), material);
                }
            }

            for (int z = min.z + 16; z < min.z + room.Size.z - 16; z += 38)
            {
                brush.Box(new int3(min.x + 8, floorY + room.Size.y - 6, z),
                          new int3(room.Size.x - 16, 4, 4), Mat.Wood);
            }

            int deskX = room.Centre.x - math.min(55, room.Size.x / 4);
            int deskZ = room.Centre.z;
            brush.Box(new int3(deskX - 18, floorY + 8, deskZ - 10),
                      new int3(36, 3, 20), Mat.Wood);
            brush.Box(new int3(deskX - 14, floorY + 1, deskZ - 7),
                      new int3(5, 7, 5), Mat.Wood);
            brush.Box(new int3(deskX + 9, floorY + 1, deskZ + 2),
                      new int3(5, 7, 5), Mat.Wood);
            for (int folio = 0; folio < 3; folio++)
            {
                brush.Box(new int3(deskX - 10 + folio * 8,
                                   floorY + 11 + folio, deskZ - 4),
                          new int3(7, 1, 10), folio == 1 ? Mat.Gold : Mat.Cloth);
            }

            int runnerHalf = math.min(12, room.Size.x / 10);
            brush.Box(new int3(room.Centre.x - runnerHalf, floorY + 1, min.z + 10),
                      new int3(runnerHalf * 2, 1, math.max(1, room.Size.z - 20)), Mat.Cloth);
        }

        private static void FurnishGreatHall(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floorY = min.y;
            int columnsX = room.Size.x >= 180 ? 3 : 2;
            int rowsZ = room.Size.z >= 120 ? 2 : 1;

            for (int xIndex = 0; xIndex < columnsX; xIndex++)
            for (int zIndex = 0; zIndex < rowsZ; zIndex++)
            {
                int x = min.x + (xIndex + 1) * room.Size.x / (columnsX + 1);
                int z = min.z + (zIndex + 1) * room.Size.z / (rowsZ + 1);
                int radius = math.min(10, math.max(6, room.Size.y / 5));
                int height = math.max(16, room.Size.y - 4);
                brush.Cylinder(x, floorY, z, radius, height, Mat.Stone);
                brush.Cylinder(x, floorY + height - 4, z, radius + 3, 4, Mat.DarkStone);
            }

            int daisWidth = math.min(68, room.Size.x - 24);
            int daisDepth = math.min(26, room.Size.z / 4);
            int daisZ = min.z + 16;
            brush.Box(new int3(room.Centre.x - daisWidth / 2, floorY,
                               daisZ),
                      new int3(daisWidth, 5, daisDepth), Mat.DarkStone);
            brush.Box(new int3(room.Centre.x - 12, floorY + 5,
                               daisZ + math.max(3, daisDepth / 4)),
                      new int3(24, 9, math.max(8, daisDepth / 2)), Mat.Stone);
            brush.Box(new int3(room.Centre.x - 4, floorY + 14,
                               daisZ + math.max(5, daisDepth / 3)),
                      new int3(8, 12, 6), Mat.Gold);

            int benchZStart = room.Centre.z - math.min(30, room.Size.z / 5);
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
            {
                int width = math.min(40, room.Size.x / 5);
                int x = room.Centre.x + side * math.min(54, room.Size.x / 4) - width / 2;
                int z = benchZStart + row * 28;
                if (z + 8 >= min.z + room.Size.z - 6) continue;
                brush.Box(new int3(x, floorY + 1, z),
                          new int3(width, 5, 8), row == 1 ? Mat.DarkStone : Mat.Wood);
            }
        }

        private static void FurnishPuzzle(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floorY = min.y;
            int cx = room.Centre.x;
            int cz = room.Centre.z;

            int crossHalfX = math.max(12, room.Size.x / 2 - 8);
            int crossHalfZ = math.max(12, room.Size.z / 2 - 8);
            brush.Box(new int3(cx - crossHalfX, floorY + 1, cz - 2),
                      new int3(crossHalfX * 2, 1, 4), Mat.Slate);
            brush.Box(new int3(cx - 2, floorY + 1, cz - crossHalfZ),
                      new int3(4, 1, crossHalfZ * 2), Mat.Slate);

            int offsetX = math.max(16, math.min(26, room.Size.x / 4));
            int offsetZ = math.max(18, math.min(30, room.Size.z / 4));
            int2[] runeOffsets =
            {
                new(-offsetX, -offsetZ), new(offsetX, -offsetZ),
                new(-offsetX, offsetZ), new(offsetX, offsetZ),
            };
            for (int i = 0; i < runeOffsets.Length; i++)
            {
                int x = cx + runeOffsets[i].x;
                int z = cz + runeOffsets[i].y;
                brush.Box(new int3(x - 8, floorY + 2, z - 8),
                          new int3(16, 8, 16), Mat.Stone);
                brush.Disc(x, floorY + 10, z, 6, Mat.DarkStone);
                brush.Cone(x, floorY + 11, z, 3 + (i & 1),
                           8 + i * 2, i % 2 == 0 ? Mat.Glass : Mat.Gold);
            }

            brush.Box(new int3(cx - 14, floorY + 2, cz - 14),
                      new int3(28, 3, 28), Mat.Slate);
            brush.Disc(cx, floorY + 5, cz, 8, Mat.DarkStone);
            brush.Cone(cx, floorY + 6, cz, 4, 10, Mat.Glass);
            brush.Cone(cx - 6, floorY + 6, cz + 4, 2, 7, Mat.Gold);
        }

        private static void FurnishTreasury(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floorY = min.y;
            int bayCount = math.max(2, math.min(3, room.Size.x / 30));

            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < bayCount; bay++)
            {
                int x = min.x + (bay + 1) * room.Size.x / (bayCount + 1);
                int z = room.Centre.z + side * math.min(45, room.Size.z / 2 - 10);
                brush.Box(new int3(x - 9, floorY + 2, z - 5),
                          new int3(18, 23, 10), Mat.Wood);
                brush.Box(new int3(x - 10, floorY + 9, z - 6),
                          new int3(20, 2, 12), Mat.Gold);
                brush.Box(new int3(x - 10, floorY + 18, z - 6),
                          new int3(20, 2, 12), Mat.Gold);
            }

            int carpetWidth = math.max(16, math.min(62, room.Size.x - 24));
            brush.Box(new int3(room.Centre.x - carpetWidth / 2, floorY + 1,
                               room.Centre.z - 8),
                      new int3(carpetWidth, 1, 16), Mat.Cloth);

            int pileCount = math.max(3, math.min(5, room.Size.x / 18));
            int startX = room.Centre.x - (pileCount - 1) * 8;
            int pileZ = min.z + math.min(24, room.Size.z / 4);
            for (int pile = 0; pile < pileCount; pile++)
            {
                int x = startX + pile * 16;
                int z = pileZ + (pile & 1) * 7;
                brush.Cone(x, floorY + 2, z, 5, 7 + (pile % 3) * 3, Mat.Gold);
            }
        }

        private static int3 RoomMin(in DungeonRoomPlan room) =>
            room.Centre - room.Size / 2;
    }
}
