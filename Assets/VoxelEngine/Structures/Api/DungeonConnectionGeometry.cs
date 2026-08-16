using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure geometric contract for designed dungeon connections. Validation and Runtime share this
    /// logic so a graph edge cannot be accepted semantically but realized as disconnected space.
    /// </summary>
    public static class DungeonConnectionGeometry
    {
        public const int StairShaftRadius = 14;
        public const int StairShaftDiameter = StairShaftRadius * 2;

        public static int RoomFloor(in DungeonRoomPlan room) =>
            room.Centre.y - room.Size.y / 2;

        public static bool IsValid(
            in DungeonRoomPlan from,
            in DungeonRoomPlan to,
            DungeonConnectionKind kind)
        {
            switch (kind)
            {
                case DungeonConnectionKind.Stair:
                    return TryStairShaftCentre(in from, in to, out _);
                case DungeonConnectionKind.Corridor:
                case DungeonConnectionKind.SecretPassage:
                    return RoomFloor(in from) == RoomFloor(in to);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Finds a shaft centre contained by both room footprints. The overlap must be wide enough
        /// for the radius-14 carved stair cylinder, otherwise a nominal Stair edge is not physically
        /// buildable without adding another horizontal connection that the plan did not request.
        /// </summary>
        public static bool TryStairShaftCentre(
            in DungeonRoomPlan from,
            in DungeonRoomPlan to,
            out int2 centre)
        {
            centre = default;
            if (RoomFloor(in from) == RoomFloor(in to))
                return false;

            int fromMinX = from.Centre.x - from.Size.x / 2;
            int fromMaxX = fromMinX + from.Size.x;
            int fromMinZ = from.Centre.z - from.Size.z / 2;
            int fromMaxZ = fromMinZ + from.Size.z;
            int toMinX = to.Centre.x - to.Size.x / 2;
            int toMaxX = toMinX + to.Size.x;
            int toMinZ = to.Centre.z - to.Size.z / 2;
            int toMaxZ = toMinZ + to.Size.z;

            int overlapMinX = math.max(fromMinX, toMinX);
            int overlapMaxX = math.min(fromMaxX, toMaxX);
            int overlapMinZ = math.max(fromMinZ, toMinZ);
            int overlapMaxZ = math.min(fromMaxZ, toMaxZ);
            if (overlapMaxX - overlapMinX < StairShaftDiameter ||
                overlapMaxZ - overlapMinZ < StairShaftDiameter)
                return false;

            centre = new int2(
                (overlapMinX + overlapMaxX) / 2,
                (overlapMinZ + overlapMaxZ) / 2);
            return true;
        }
    }
}
