using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum DungeonPlanIssue : byte
    {
        None,
        MissingRooms,
        InvalidEntranceRoom,
        MultipleEntranceRooms,
        EntrancePlacementMismatch,
        RoomIdMismatch,
        InvalidRoomSize,
        OverlappingRooms,
        MissingConnections,
        InvalidConnectionEndpoint,
        SelfConnection,
        DuplicateConnection,
        InvalidConnectionKind,
        InvalidConnectionGeometry,
        DisconnectedGraph,
        CaveThresholdMismatch,
    }

    /// <summary>
    /// Pure graph/spatial validation for designed dungeon spaces. It intentionally knows nothing
    /// about voxel materials, cave realization, castles, or terrain.
    /// </summary>
    public static class DungeonPlanValidator
    {
        public static bool TryValidate(DungeonPlan plan, out DungeonPlanIssue issue)
        {
            if (plan == null || plan.Rooms == null || plan.Rooms.Length == 0)
            {
                issue = DungeonPlanIssue.MissingRooms;
                return false;
            }

            DungeonRoomPlan[] rooms = plan.Rooms;
            if (plan.EntranceRoomId < 0 || plan.EntranceRoomId >= rooms.Length ||
                rooms[plan.EntranceRoomId].Purpose != DungeonRoomPurpose.Entrance)
            {
                issue = DungeonPlanIssue.InvalidEntranceRoom;
                return false;
            }

            int entranceCount = 0;
            int caveThresholdCount = 0;
            for (int i = 0; i < rooms.Length; i++)
            {
                DungeonRoomPlan room = rooms[i];
                if (room.Id != i)
                {
                    issue = DungeonPlanIssue.RoomIdMismatch;
                    return false;
                }

                if (math.any(room.Size <= 0))
                {
                    issue = DungeonPlanIssue.InvalidRoomSize;
                    return false;
                }

                if (room.Purpose == DungeonRoomPurpose.Entrance) entranceCount++;
                if (room.Purpose == DungeonRoomPurpose.CaveThreshold) caveThresholdCount++;

                for (int other = 0; other < i; other++)
                {
                    if (!Overlaps(in room, in rooms[other])) continue;
                    issue = DungeonPlanIssue.OverlappingRooms;
                    return false;
                }
            }

            if (entranceCount != 1)
            {
                issue = DungeonPlanIssue.MultipleEntranceRooms;
                return false;
            }

            DungeonRoomPlan entranceRoom = rooms[plan.EntranceRoomId];
            int3 entrancePoint = new int3(
                entranceRoom.Centre.x,
                DungeonConnectionGeometry.RoomFloor(in entranceRoom),
                entranceRoom.Centre.z);
            if (!entrancePoint.Equals(plan.Entrance))
            {
                issue = DungeonPlanIssue.EntrancePlacementMismatch;
                return false;
            }

            bool expectsCave = plan.CaveThresholdRoomId >= 0;
            if ((expectsCave && caveThresholdCount != 1) ||
                (!expectsCave && caveThresholdCount != 0) ||
                (expectsCave &&
                 (plan.CaveThresholdRoomId >= rooms.Length ||
                  rooms[plan.CaveThresholdRoomId].Purpose != DungeonRoomPurpose.CaveThreshold)))
            {
                issue = DungeonPlanIssue.CaveThresholdMismatch;
                return false;
            }

            DungeonConnectionPlan[] connections = plan.Connections;
            if (rooms.Length > 1 && (connections == null || connections.Length == 0))
            {
                issue = DungeonPlanIssue.MissingConnections;
                return false;
            }
            if (connections == null) connections = System.Array.Empty<DungeonConnectionPlan>();

            for (int i = 0; i < connections.Length; i++)
            {
                DungeonConnectionPlan connection = connections[i];
                if (connection.FromRoomId < 0 || connection.FromRoomId >= rooms.Length ||
                    connection.ToRoomId < 0 || connection.ToRoomId >= rooms.Length)
                {
                    issue = DungeonPlanIssue.InvalidConnectionEndpoint;
                    return false;
                }

                if (connection.FromRoomId == connection.ToRoomId)
                {
                    issue = DungeonPlanIssue.SelfConnection;
                    return false;
                }

                if (connection.Kind != DungeonConnectionKind.Stair &&
                    connection.Kind != DungeonConnectionKind.Corridor &&
                    connection.Kind != DungeonConnectionKind.SecretPassage)
                {
                    issue = DungeonPlanIssue.InvalidConnectionKind;
                    return false;
                }

                DungeonRoomPlan fromRoom = rooms[connection.FromRoomId];
                DungeonRoomPlan toRoom = rooms[connection.ToRoomId];
                if (!DungeonConnectionGeometry.IsValid(
                        in fromRoom, in toRoom, connection.Kind))
                {
                    issue = DungeonPlanIssue.InvalidConnectionGeometry;
                    return false;
                }

                int a = math.min(connection.FromRoomId, connection.ToRoomId);
                int b = math.max(connection.FromRoomId, connection.ToRoomId);
                for (int other = 0; other < i; other++)
                {
                    int otherA = math.min(
                        connections[other].FromRoomId, connections[other].ToRoomId);
                    int otherB = math.max(
                        connections[other].FromRoomId, connections[other].ToRoomId);
                    if (a != otherA || b != otherB) continue;
                    issue = DungeonPlanIssue.DuplicateConnection;
                    return false;
                }
            }

            var adjacency = new List<int>[rooms.Length];
            for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>();
            for (int i = 0; i < connections.Length; i++)
            {
                DungeonConnectionPlan connection = connections[i];
                adjacency[connection.FromRoomId].Add(connection.ToRoomId);
                adjacency[connection.ToRoomId].Add(connection.FromRoomId);
            }

            var visited = new bool[rooms.Length];
            var queue = new Queue<int>();
            queue.Enqueue(plan.EntranceRoomId);
            visited[plan.EntranceRoomId] = true;
            int visitedCount = 0;
            while (queue.Count > 0)
            {
                int room = queue.Dequeue();
                visitedCount++;
                List<int> neighbours = adjacency[room];
                for (int i = 0; i < neighbours.Count; i++)
                {
                    int next = neighbours[i];
                    if (visited[next]) continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }

            if (visitedCount != rooms.Length)
            {
                issue = DungeonPlanIssue.DisconnectedGraph;
                return false;
            }

            issue = DungeonPlanIssue.None;
            return true;
        }

        private static bool Overlaps(in DungeonRoomPlan a, in DungeonRoomPlan b)
        {
            int3 delta = math.abs(a.Centre - b.Centre) * 2;
            int3 combined = a.Size + b.Size;
            return delta.x < combined.x && delta.y < combined.y && delta.z < combined.z;
        }
    }
}
