using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum DungeonRoomPurpose : byte
    {
        Entrance,
        Archive,
        GreatHall,
        Puzzle,
        Treasury,
        CaveThreshold,
    }

    public enum DungeonConnectionKind : byte
    {
        Stair,
        Corridor,
        SecretPassage,
    }

    public struct DungeonRoomPlan
    {
        public int Id;
        public DungeonRoomPurpose Purpose;
        public int3 Centre;
        public int3 Size;
    }

    public struct DungeonConnectionPlan
    {
        public int FromRoomId;
        public int ToRoomId;
        public DungeonConnectionKind Kind;
    }

    /// <summary>
    /// Scale and feature constraints supplied by the owning structure. Coordinates are expressed
    /// in the caller's coordinate space; the dungeon planner has no castle, terrain, or voxel
    /// storage dependency.
    /// </summary>
    public struct DungeonPlanningConstraints
    {
        public int3 Entrance;
        public int UpperLevelDrop;
        public int MainLevelDrop;
        public int RoomHeight;
        public int MainHallHalfX;
        public int MainHallHalfZ;
        public int SideRoomOffset;
        public int SideRoomHalfX;
        public int SideRoomHalfZ;
        public int CavePassageLength;
        public bool IncludeArchive;
        public bool IncludePuzzle;
        public bool IncludeTreasury;
        public bool IncludeCaveExit;
    }

    /// <summary>
    /// Pure designed-space dungeon graph. Natural cave layout deliberately remains outside this
    /// plan; CaveThreshold identifies only the designed-to-natural handoff point.
    /// </summary>
    public sealed class DungeonPlan
    {
        public uint Seed { get; }
        public int3 Entrance { get; }
        public DungeonRoomPlan[] Rooms { get; }
        public DungeonConnectionPlan[] Connections { get; }
        public int EntranceRoomId { get; }
        public int CaveThresholdRoomId { get; }
        public bool HasCaveExit => CaveThresholdRoomId >= 0;

        internal DungeonPlan(
            uint seed,
            int3 entrance,
            DungeonRoomPlan[] rooms,
            DungeonConnectionPlan[] connections,
            int entranceRoomId,
            int caveThresholdRoomId)
        {
            Seed = seed;
            Entrance = entrance;
            Rooms = rooms;
            Connections = connections;
            EntranceRoomId = entranceRoomId;
            CaveThresholdRoomId = caveThresholdRoomId;
        }
    }
}
