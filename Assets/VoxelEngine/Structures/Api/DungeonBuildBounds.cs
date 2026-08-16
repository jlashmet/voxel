using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Exact world-voxel envelope of a validated designed DungeonPlan.</summary>
    public readonly struct DungeonBuildBounds
    {
        public readonly int3 Min;
        public readonly int3 MaxExclusive;

        internal DungeonBuildBounds(int3 min, int3 maxExclusive)
        {
            Min = min;
            MaxExclusive = maxExclusive;
        }

        public bool Contains(int3 voxel) =>
            math.all(voxel >= Min) && math.all(voxel < MaxExclusive);
    }

    /// <summary>
    /// Pure bounds resolver for designed dungeon realization. It deliberately mirrors the shared
    /// DungeonConnectionGeometry contract so streaming/admission bounds cannot drift from the
    /// corridors and stair shafts that Runtime carves.
    /// </summary>
    public static class DungeonBuildBoundsResolver
    {
        public static DungeonBuildBounds Resolve(DungeonPlan plan)
        {
            if (!DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue))
                throw new InvalidOperationException($"Dungeon bounds require a valid plan: {issue}.");

            bool hasBounds = false;
            int3 min = default;
            int3 maxExclusive = default;

            DungeonRoomPlan[] rooms = plan.Rooms;
            for (int i = 0; i < rooms.Length; i++)
                IncludeRoom(in rooms[i], ref hasBounds, ref min, ref maxExclusive);

            DungeonConnectionPlan[] connections = plan.Connections;
            for (int i = 0; i < connections.Length; i++)
            {
                DungeonConnectionPlan connection = connections[i];
                DungeonRoomPlan from = rooms[connection.FromRoomId];
                DungeonRoomPlan to = rooms[connection.ToRoomId];

                switch (connection.Kind)
                {
                    case DungeonConnectionKind.Stair:
                        IncludeStair(in from, in to, ref hasBounds, ref min, ref maxExclusive);
                        break;
                    case DungeonConnectionKind.Corridor:
                    case DungeonConnectionKind.SecretPassage:
                        IncludePassage(
                            in from,
                            in to,
                            connection.Kind,
                            ref hasBounds,
                            ref min,
                            ref maxExclusive);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported dungeon connection kind {connection.Kind}.");
                }
            }

            return new DungeonBuildBounds(min, maxExclusive);
        }

        private static void IncludeRoom(
            in DungeonRoomPlan room,
            ref bool hasBounds,
            ref int3 min,
            ref int3 maxExclusive)
        {
            int3 roomMin = room.Centre - room.Size / 2;
            IncludeBox(
                roomMin - new int3(0, DungeonConnectionGeometry.FloorThickness, 0),
                roomMin + room.Size,
                ref hasBounds,
                ref min,
                ref maxExclusive);
        }

        private static void IncludePassage(
            in DungeonRoomPlan from,
            in DungeonRoomPlan to,
            DungeonConnectionKind kind,
            ref bool hasBounds,
            ref int3 min,
            ref int3 maxExclusive)
        {
            int width = DungeonConnectionGeometry.PassageWidth(kind);
            int height = DungeonConnectionGeometry.PassageHeight(kind);
            int floorY = DungeonConnectionGeometry.RoomFloor(in from);
            int2 a = new int2(from.Centre.x, from.Centre.z);
            int2 b = new int2(to.Centre.x, to.Centre.z);
            int2 corner = DungeonConnectionGeometry.PassageCorner(in from, in to);

            IncludeHorizontalLeg(
                a, corner, floorY, width, height,
                ref hasBounds, ref min, ref maxExclusive);
            IncludeHorizontalLeg(
                corner, b, floorY, width, height,
                ref hasBounds, ref min, ref maxExclusive);
        }

        private static void IncludeHorizontalLeg(
            int2 a,
            int2 b,
            int floorY,
            int width,
            int height,
            ref bool hasBounds,
            ref int3 min,
            ref int3 maxExclusive)
        {
            int half = width / 2;
            int bottomY = floorY - DungeonConnectionGeometry.FloorThickness;
            int topYExclusive = floorY + height;

            if (a.y == b.y)
            {
                int minX = math.min(a.x, b.x);
                int maxXExclusive = math.max(a.x, b.x) + 1;
                IncludeBox(
                    new int3(minX, bottomY, a.y - half),
                    new int3(maxXExclusive, topYExclusive, a.y - half + width),
                    ref hasBounds,
                    ref min,
                    ref maxExclusive);
                return;
            }

            if (a.x == b.x)
            {
                int minZ = math.min(a.y, b.y);
                int maxZExclusive = math.max(a.y, b.y) + 1;
                IncludeBox(
                    new int3(a.x - half, bottomY, minZ),
                    new int3(a.x - half + width, topYExclusive, maxZExclusive),
                    ref hasBounds,
                    ref min,
                    ref maxExclusive);
                return;
            }

            throw new InvalidOperationException("Dungeon passage leg must be axis aligned.");
        }

        private static void IncludeStair(
            in DungeonRoomPlan from,
            in DungeonRoomPlan to,
            ref bool hasBounds,
            ref int3 min,
            ref int3 maxExclusive)
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
            int radius = DungeonConnectionGeometry.StairShaftRadius;

            // Runtime carves a radius-14 cylinder with height+2 and builds a smaller spiral stair
            // inside it. +2 therefore covers the cylinder's authored top cap conservatively.
            IncludeBox(
                new int3(shaftCentre.x - radius, lowY, shaftCentre.y - radius),
                new int3(shaftCentre.x + radius + 1, highY + 2, shaftCentre.y + radius + 1),
                ref hasBounds,
                ref min,
                ref maxExclusive);
        }

        private static void IncludeBox(
            int3 boxMin,
            int3 boxMaxExclusive,
            ref bool hasBounds,
            ref int3 min,
            ref int3 maxExclusive)
        {
            if (!hasBounds)
            {
                min = boxMin;
                maxExclusive = boxMaxExclusive;
                hasBounds = true;
                return;
            }

            min = math.min(min, boxMin);
            maxExclusive = math.max(maxExclusive, boxMaxExclusive);
        }
    }
}
