using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the reusable designed-space shell of a <see cref="DungeonPlan"/>. Room purpose is
    /// deliberately ignored here: this component owns only traversable room volumes, floors, and
    /// planned connections. Furnishing and natural cave morphology remain separate downstream work.
    /// </summary>
    public static class DungeonPlanShellRealizer
    {
        private const int FloorThickness = 3;
        private const int CorridorHalfWidth = 10;
        private const int CorridorHeight = 30;

        public static void Build(ref VoxelBrush brush, DungeonPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot realize invalid dungeon plan: {issue}.");
            }

            DungeonRoomPlan[] rooms = plan.Rooms;
            for (int i = 0; i < rooms.Length; i++)
                CarveRoom(ref brush, in rooms[i]);

            DungeonConnectionPlan[] connections = plan.Connections;
            for (int i = 0; i < connections.Length; i++)
            {
                DungeonConnectionPlan connection = connections[i];
                DungeonRoomPlan from = rooms[connection.FromRoomId];
                DungeonRoomPlan to = rooms[connection.ToRoomId];

                switch (connection.Kind)
                {
                    case DungeonConnectionKind.Stair:
                        CarveStairConnection(ref brush, in from, in to);
                        break;

                    case DungeonConnectionKind.Corridor:
                    case DungeonConnectionKind.SecretPassage:
                        CarveHorizontalConnection(ref brush, in from, in to);
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unsupported dungeon connection kind {connection.Kind}.");
                }
            }
        }

        private static void CarveRoom(ref VoxelBrush brush, in DungeonRoomPlan room)
        {
            int3 min = room.Centre - room.Size / 2;
            int3 max = min + room.Size;
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
            {
                brush.FillColumnBulk(x, min.y, max.y, z, Mat.Empty);
                brush.FillColumnBulk(
                    x,
                    min.y - FloorThickness,
                    min.y,
                    z,
                    Mat.DarkStone);
            }
        }

        private static void CarveStairConnection(
            ref VoxelBrush brush,
            in DungeonRoomPlan from,
            in DungeonRoomPlan to)
        {
            int bottomY = math.min(
                from.Centre.y - from.Size.y / 2,
                to.Centre.y - to.Size.y / 2);
            int topY = math.max(
                from.Centre.y + from.Size.y / 2,
                to.Centre.y + to.Size.y / 2);
            int height = math.max(1, topY - bottomY);
            int x = (from.Centre.x + to.Centre.x) / 2;
            int z = (from.Centre.z + to.Centre.z) / 2;

            const int shaftRadius = 14;
            int radiusSq = shaftRadius * shaftRadius;
            for (int dz = -shaftRadius; dz <= shaftRadius; dz++)
            for (int dx = -shaftRadius; dx <= shaftRadius; dx++)
            {
                if (dx * dx + dz * dz > radiusSq) continue;
                brush.FillColumnBulk(x + dx, bottomY, topY, z + dz, Mat.Empty);
            }

            brush.SpiralStair(x, bottomY, z, shaftRadius - 2, height, Mat.Stone);
        }

        private static void CarveHorizontalConnection(
            ref VoxelBrush brush,
            in DungeonRoomPlan from,
            in DungeonRoomPlan to)
        {
            int2 start = new int2(from.Centre.x, from.Centre.z);
            int2 end = new int2(to.Centre.x, to.Centre.z);
            int dx = end.x - start.x;
            int dz = end.y - start.y;
            int steps = math.max(math.abs(dx), math.abs(dz));
            if (steps == 0) return;

            float2 direction = math.normalize(new float2(dx, dz));
            float2 normal = new float2(-direction.y, direction.x);
            int floorY = math.min(
                from.Centre.y - from.Size.y / 2,
                to.Centre.y - to.Size.y / 2);

            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                float2 centre = math.lerp(
                    new float2(start.x, start.y),
                    new float2(end.x, end.y),
                    t);

                for (int across = -CorridorHalfWidth; across <= CorridorHalfWidth; across++)
                {
                    float2 sample = centre + normal * across;
                    int x = (int)math.round(sample.x);
                    int z = (int)math.round(sample.y);
                    brush.FillColumnBulk(
                        x,
                        floorY,
                        floorY + CorridorHeight,
                        z,
                        Mat.Empty);
                    brush.FillColumnBulk(
                        x,
                        floorY - FloorThickness,
                        floorY,
                        z,
                        Mat.DarkStone);
                }
            }
        }
    }
}
