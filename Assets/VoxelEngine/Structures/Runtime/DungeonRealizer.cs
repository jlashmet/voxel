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
                        CarveStair(ref brush, in from, in to, stairMaterial);
                        break;
                    case DungeonConnectionKind.SecretPassage:
                    case DungeonConnectionKind.Corridor:
                        CarvePassage(
                            ref brush,
                            in from,
                            in to,
                            DungeonConnectionGeometry.PassageWidth(connection.Kind),
                            DungeonConnectionGeometry.PassageHeight(connection.Kind),
                            floorMaterial);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported dungeon connection kind {connection.Kind}.");
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
                new int3(min.x, min.y - DungeonConnectionGeometry.FloorThickness, min.z),
                new int3(room.Size.x, DungeonConnectionGeometry.FloorThickness, room.Size.z),
                floorMaterial);
        }

        private static void CarveStair(
            ref VoxelBrush brush,
            in DungeonRoomPlan from,
            in DungeonRoomPlan to,
            byte stairMaterial)
        {
            if (!DungeonConnectionGeometry.TryStairShaftCentre(
                    in from, in to, out int2 shaftCentre))
            {
                throw new InvalidOperationException(
                    "Validated dungeon Stair connection has no buildable shared shaft footprint.");
            }

            int fromFloor = DungeonConnectionGeometry.RoomFloor(in from);
            int toFloor = DungeonConnectionGeometry.RoomFloor(in to);
            int lowY = math.min(fromFloor, toFloor);
            int highY = math.max(fromFloor, toFloor);
            int height = highY - lowY;

            brush.Cylinder(
                shaftCentre.x,
                lowY,
                shaftCentre.y,
                DungeonConnectionGeometry.StairShaftRadius,
                height + 2,
                Mat.Empty);
            brush.SpiralStair(
                shaftCentre.x,
                lowY,
                shaftCentre.y,
                11,
                height,
                stairMaterial);
        }

        private static void CarvePassage(
            ref VoxelBrush brush,
            in DungeonRoomPlan from,
            in DungeonRoomPlan to,
            int width,
            int height,
            byte floorMaterial)
        {
            int floorY = DungeonConnectionGeometry.RoomFloor(in from);
            int2 a = new int2(from.Centre.x, from.Centre.z);
            int2 b = new int2(to.Centre.x, to.Centre.z);
            int2 corner = DungeonConnectionGeometry.PassageCorner(in from, in to);

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
                    new int3(
                        minX,
                        floorY - DungeonConnectionGeometry.FloorThickness,
                        a.y - half),
                    new int3(length, DungeonConnectionGeometry.FloorThickness, width),
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
                    new int3(
                        a.x - half,
                        floorY - DungeonConnectionGeometry.FloorThickness,
                        minZ),
                    new int3(width, DungeonConnectionGeometry.FloorThickness, length),
                    floorMaterial);
            }
        }

        private static int3 RoomMin(in DungeonRoomPlan room) =>
            room.Centre - room.Size / 2;
    }
}
