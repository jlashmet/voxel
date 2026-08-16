using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Generic voxel realization of a designed DungeonPlan. It owns room shells and circulation
    /// only; semantic furnishing and natural cave geometry are separate downstream components.
    /// </summary>
    public static class DungeonRealizer
    {
        public static void Build(ref VoxelBrush brush, DungeonPlan plan) =>
            Build(ref brush, plan, Mat.DarkStone, Mat.Stone);

        public static void Build(
            ref VoxelBrush brush,
            DungeonPlan plan,
            byte floorMaterial,
            byte stairMaterial)
        {
            if (!DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue))
                throw new InvalidOperationException($"Invalid dungeon plan: {issue}.");

            DungeonRoomPlan[] rooms = plan.Rooms;
            for (int i = 0; i < rooms.Length; i++)
                CarveRoom(ref brush, in rooms[i], floorMaterial);

            DungeonConnectionPlan[] connections = plan.Connections;
            for (int i = 0; i < connections.Length; i++)
            {
                DungeonConnectionPlan connection = connections[i];
                DungeonRoomPlan from = rooms[connection.FromRoomId];
                DungeonRoomPlan to = rooms[connection.ToRoomId];

                switch (connection.Kind)
                {
                    case DungeonConnectionKind.Stair:
                        CarveStair(ref brush, in from, in to, floorMaterial, stairMaterial);
                        break;
                    case DungeonConnectionKind.SecretPassage:
                        CarvePassage(ref brush, in from, in to, 28, 32, floorMaterial);
                        break;
                    default:
                        CarvePassage(ref brush, in from, in to, 20, 30, floorMaterial);
                        break;
                }
            }
        }

        private static void CarveRoom(
            ref VoxelBrush brush,
            in DungeonRoomPlan room,
            byte floorMaterial)
        {
            int3 min = RoomMin(in room);
            brush.FillBulk(min, room.Size, Mat.Empty);
            brush.Box(
                new int3(min.x, min.y - 2, min.z),
                new int3(room.Size.x, 2, room.Size.z),
                floorMaterial);
        }

        private static void CarveStair(
            ref VoxelBrush brush,
            in DungeonRoomPlan from,
            in DungeonRoomPlan to,
            byte floorMaterial,
            byte stairMaterial)
        {
            int fromFloor = RoomMin(in from).y;
            int toFloor = RoomMin(in to).y;
            int lowY = math.min(fromFloor, toFloor);
            int highY = math.max(fromFloor, toFloor);
            int height = highY - lowY;
            if (height <= 0)
            {
                CarvePassage(ref brush, in from, in to, 20, 30, floorMaterial);
                return;
            }

            int3 lower = fromFloor <= toFloor ? from.Centre : to.Centre;
            int x = lower.x;
            int z = lower.z;
            brush.Cylinder(x, lowY, z, 14, height + 2, Mat.Empty);
            brush.SpiralStair(x, lowY, z, 11, height, stairMaterial);
        }

        private static void CarvePassage(
            ref VoxelBrush brush,
            in DungeonRoomPlan from,
            in DungeonRoomPlan to,
            int width,
            int height,
            byte floorMaterial)
        {
            int fromFloor = RoomMin(in from).y;
            int toFloor = RoomMin(in to).y;
            int floorY = math.min(fromFloor, toFloor);
            int2 a = new int2(from.Centre.x, from.Centre.z);
            int2 b = new int2(to.Centre.x, to.Centre.z);
            int2 corner = new int2(b.x, a.y);

            CarveHorizontalLeg(ref brush, a, corner, floorY, width, height, floorMaterial);
            CarveHorizontalLeg(ref brush, corner, b, floorY, width, height, floorMaterial);
        }

        private static void CarveHorizontalLeg(
            ref VoxelBrush brush,
            int2 a,
            int2 b,
            int floorY,
            int width,
            int height,
            byte floorMaterial)
        {
            int half = width / 2;
            if (a.y == b.y)
            {
                int minX = math.min(a.x, b.x);
                int length = math.abs(b.x - a.x) + 1;
                brush.FillBulk(
                    new int3(minX, floorY, a.y - half),
                    new int3(length, height, width),
                    Mat.Empty);
                brush.Box(
                    new int3(minX, floorY - 2, a.y - half),
                    new int3(length, 2, width),
                    floorMaterial);
                return;
            }

            if (a.x == b.x)
            {
                int minZ = math.min(a.y, b.y);
                int length = math.abs(b.y - a.y) + 1;
                brush.FillBulk(
                    new int3(a.x - half, floorY, minZ),
                    new int3(width, height, length),
                    Mat.Empty);
                brush.Box(
                    new int3(a.x - half, floorY - 2, minZ),
                    new int3(width, 2, length),
                    floorMaterial);
            }
        }

        private static int3 RoomMin(in DungeonRoomPlan room) =>
            room.Centre - room.Size / 2;
    }
}
