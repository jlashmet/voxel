using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Purpose-driven furnishing for a designed DungeonPlan. Room placement and graph topology are
    /// already fixed; this component adds reusable authored detail without knowing which larger
    /// structure owns the dungeon.
    /// </summary>
    public static class DungeonRoomFurnisher
    {
        public static void FurnishAll(ref VoxelBrush brush, DungeonPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue))
                throw new InvalidOperationException($"Cannot furnish invalid dungeon plan: {issue}.");

            for (int i = 0; i < plan.Rooms.Length; i++)
            {
                DungeonRoomPlan room = plan.Rooms[i];
                Furnish(ref brush, in room);
            }
        }

        private static void Furnish(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            switch (room.Purpose)
            {
                case DungeonRoomPurpose.Archive:
                    Archive(ref brush, in room);
                    break;
                case DungeonRoomPurpose.GreatHall:
                    GreatHall(ref brush, in room);
                    break;
                case DungeonRoomPurpose.Puzzle:
                    Puzzle(ref brush, in room);
                    break;
                case DungeonRoomPurpose.Treasury:
                    Treasury(ref brush, in room);
                    break;
                case DungeonRoomPurpose.CaveThreshold:
                    CaveThreshold(ref brush, in room);
                    break;
            }
        }

        private static void Archive(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int3 max = min + room.Size;
            int floor = min.y;
            int shelfDepth = math.min(12, math.max(6, room.Size.x / 16));
            int shelfHeight = math.min(28, room.Size.y - 6);

            for (int z = min.z + 14; z < max.z - 18; z += 30)
            {
                brush.Box(new int3(min.x + 8, floor + 1, z),
                          new int3(shelfDepth, shelfHeight, 18), Mat.Wood);
                brush.Box(new int3(max.x - 8 - shelfDepth, floor + 1, z),
                          new int3(shelfDepth, shelfHeight, 18), Mat.Wood);

                for (int shelf = 0; shelf < 3; shelf++)
                {
                    int y = floor + 5 + shelf * 8;
                    brush.Box(new int3(min.x + 8 + shelfDepth, y, z + 2),
                              new int3(3, 5 + ((z + shelf) & 3), 12),
                              (shelf & 1) == 0 ? Mat.Cloth : Mat.Gold);
                    brush.Box(new int3(max.x - 11 - shelfDepth, y, z + 2),
                              new int3(3, 5 + ((z + shelf + 1) & 3), 12),
                              (shelf & 1) == 0 ? Mat.Gold : Mat.Cloth);
                }
            }

            int deskWidth = math.min(42, room.Size.x / 3);
            brush.Box(
                new int3(room.Centre.x - deskWidth / 2, floor + 7, room.Centre.z - 10),
                new int3(deskWidth, 3, 20),
                Mat.Wood);
        }

        private static void GreatHall(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floor = min.y;
            int xInset = math.max(24, room.Size.x / 5);
            int zInset = math.max(24, room.Size.z / 4);
            int columnHeight = math.max(20, room.Size.y - 4);

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                int x = room.Centre.x + sx * xInset;
                int z = room.Centre.z + sz * zInset;
                brush.Cylinder(x, floor + 1, z, 8, columnHeight, Mat.Stone);
                brush.Cylinder(x, floor + columnHeight - 3, z, 11, 4, Mat.DarkStone);
            }

            int daisZ = room.Centre.z - room.Size.z / 2 + 28;
            brush.Box(new int3(room.Centre.x - 34, floor + 1, daisZ),
                      new int3(68, 5, 24), Mat.DarkStone);
            brush.Box(new int3(room.Centre.x - 12, floor + 6, daisZ + 6),
                      new int3(24, 10, 12), Mat.Stone);
            brush.Box(new int3(room.Centre.x - 4, floor + 16, daisZ + 9),
                      new int3(8, 12, 6), Mat.Gold);
        }

        private static void Puzzle(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floor = min.y;
            int ox = math.max(14, room.Size.x / 4);
            int oz = math.max(14, room.Size.z / 4);
            int2[] offsets =
            {
                new int2(-ox, -oz), new int2(ox, -oz),
                new int2(-ox, oz), new int2(ox, oz),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int x = room.Centre.x + offsets[i].x;
                int z = room.Centre.z + offsets[i].y;
                brush.Box(new int3(x - 7, floor + 1, z - 7),
                          new int3(14, 7, 14), Mat.Stone);
                brush.Disc(x, floor + 8, z, 5, Mat.DarkStone);
                brush.Cone(x, floor + 9, z, 3, 8 + i * 2,
                           (i & 1) == 0 ? Mat.Glass : Mat.Gold);
            }

            brush.Disc(room.Centre.x, floor + 2, room.Centre.z, 9, Mat.Slate);
            brush.Cone(room.Centre.x, floor + 3, room.Centre.z, 4, 10, Mat.Glass);
        }

        private static void Treasury(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int3 max = min + room.Size;
            int floor = min.y;

            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 3; bay++)
            {
                int x = min.x + room.Size.x * (bay + 1) / 4;
                int z = room.Centre.z + side * math.max(14, room.Size.z / 3);
                brush.Box(new int3(x - 8, floor + 1, z - 5),
                          new int3(16, 20, 10), Mat.Wood);
                brush.Box(new int3(x - 9, floor + 9, z - 6),
                          new int3(18, 2, 12), Mat.Gold);
            }

            int pileZ = min.z + math.min(24, room.Size.z / 4);
            for (int pile = 0; pile < 5; pile++)
            {
                int x = min.x + 18 + pile * math.max(10, (room.Size.x - 36) / 5);
                if (x >= max.x - 8) break;
                brush.Cone(x, floor + 1, pileZ + (pile & 1) * 7,
                           5, 7 + (pile % 3) * 3, Mat.Gold);
            }
        }

        private static void CaveThreshold(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = RoomMin(in room);
            int floor = min.y;
            int halfX = math.max(5, room.Size.x / 2 - 4);
            brush.Box(new int3(room.Centre.x - halfX, floor + 1, room.Centre.z - 2),
                      new int3(halfX * 2, 3, 4), Mat.DarkStone);
        }

        private static int3 RoomMin(in DungeonRoomPlan room) =>
            room.Centre - room.Size / 2;
    }
}
