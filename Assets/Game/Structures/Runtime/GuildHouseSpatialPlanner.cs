using System;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum GuildHouseShellStyle : byte
    {
        Hall = 1,
        Tower = 2,
        Lodge = 3,
        HiddenDen = 4,
        ChapelHouse = 5,
    }

    public readonly struct GuildHouseSpatialRoom
    {
        public readonly GuildHouseRoomNode Node;
        public readonly int FloorIndex;
        public readonly int CellIndex;
        public readonly int3 Min;
        public readonly int3 Size;

        public GuildHouseSpatialRoom(
            GuildHouseRoomNode node,
            int floorIndex,
            int cellIndex,
            int3 min,
            int3 size)
        {
            Node = node;
            FloorIndex = floorIndex;
            CellIndex = cellIndex;
            Min = min;
            Size = size;
        }

        public int3 MaxExclusive => Min + Size;
    }

    public readonly struct GuildHouseSpatialPlan
    {
        public readonly GuildHouseKind Kind;
        public readonly GuildHouseShellStyle ShellStyle;
        public readonly int3 Origin;
        public readonly int Width;
        public readonly int Depth;
        public readonly int FloorHeight;
        public readonly int FloorCount;
        public readonly GuildHouseSpatialRoom[] Rooms;

        public GuildHouseSpatialPlan(
            GuildHouseKind kind,
            GuildHouseShellStyle shellStyle,
            int3 origin,
            int width,
            int depth,
            int floorHeight,
            int floorCount,
            GuildHouseSpatialRoom[] rooms)
        {
            Kind = kind;
            ShellStyle = shellStyle;
            Origin = origin;
            Width = width;
            Depth = depth;
            FloorHeight = floorHeight;
            FloorCount = floorCount;
            Rooms = rooms ?? Array.Empty<GuildHouseSpatialRoom>();
        }

        public bool IsWellFormed => Width >= 48 && Depth >= 48 && FloorHeight >= 24 &&
            FloorCount >= 1 && Rooms != null && Rooms.Length > 0;
    }

    /// <summary>
    /// Deterministically maps the semantic guild topology into concrete rectangular room blocks.
    /// The initial allocator intentionally uses a simple four-cell-per-floor grammar: a central
    /// circulation cross plus four furnishable quadrants. This is easy to validate and can later
    /// be replaced by richer polygonal shells without changing guild room identity.
    /// </summary>
    public static class GuildHouseSpatialPlanner
    {
        private const int Wall = 2;
        private const int Corridor = 6;

        public static GuildHouseSpatialPlan Plan(
            GuildHouseKind kind,
            uint seed,
            int3 origin,
            int width,
            int depth,
            int requestedRooms = 0)
        {
            var program = GuildHouseProgramCatalog.Get(kind);
            int roomCapacity = requestedRooms > 0 ? requestedRooms : program.PreferredRooms;
            roomCapacity = math.max(program.MinimumRooms, roomCapacity);
            var selected = GuildHouseRoomSelector.Select(program, seed, roomCapacity);
            var topology = GuildHouseTopologyPlanner.Plan(program, selected);

            GuildHouseShellStyle shell = ShellFor(kind);
            int floorHeight = shell == GuildHouseShellStyle.Tower ? 34 : 30;
            int cellsPerFloor = shell == GuildHouseShellStyle.Lodge ? 6 : 4;
            int floorCount = math.max(1, (topology.Length + cellsPerFloor - 1) / cellsPerFloor);
            if (shell == GuildHouseShellStyle.Tower)
                floorCount = math.max(2, floorCount);

            width = math.max(width, shell == GuildHouseShellStyle.Lodge ? 84 : 64);
            depth = math.max(depth, shell == GuildHouseShellStyle.Lodge ? 72 : 64);

            var rooms = shell == GuildHouseShellStyle.Lodge
                ? AllocateLodge(topology, origin, width, depth, floorHeight)
                : AllocateGrid(topology, origin, width, depth, floorHeight);

            return new GuildHouseSpatialPlan(kind, shell, origin, width, depth,
                floorHeight, floorCount, rooms);
        }

        private static GuildHouseSpatialRoom[] AllocateGrid(
            GuildHouseRoomNode[] topology,
            int3 origin,
            int width,
            int depth,
            int floorHeight)
        {
            var result = new GuildHouseSpatialRoom[topology.Length];
            int halfX = width / 2;
            int halfZ = depth / 2;
            int leftWidth = halfX - Corridor / 2 - Wall * 2;
            int rightWidth = width - halfX - Corridor / 2 - Wall * 2;
            int frontDepth = halfZ - Corridor / 2 - Wall * 2;
            int backDepth = depth - halfZ - Corridor / 2 - Wall * 2;

            for (int i = 0; i < topology.Length; i++)
            {
                int floor = i / 4;
                int cell = i & 3;
                bool right = (cell & 1) != 0;
                bool back = (cell & 2) != 0;
                int x = right ? origin.x + halfX + Corridor / 2 : origin.x + Wall;
                int z = back ? origin.z + halfZ + Corridor / 2 : origin.z + Wall;
                int sx = right ? rightWidth : leftWidth;
                int sz = back ? backDepth : frontDepth;
                int y = origin.y + floor * floorHeight + 1;
                result[i] = new GuildHouseSpatialRoom(topology[i], floor, cell,
                    new int3(x, y, z), new int3(sx, floorHeight - 3, sz));
            }

            return result;
        }

        private static GuildHouseSpatialRoom[] AllocateLodge(
            GuildHouseRoomNode[] topology,
            int3 origin,
            int width,
            int depth,
            int floorHeight)
        {
            // Organic lodge grammar: six broad cells around a central open garden/circulation spine.
            var result = new GuildHouseSpatialRoom[topology.Length];
            int thirdX = width / 3;
            int halfZ = depth / 2;
            for (int i = 0; i < topology.Length; i++)
            {
                int floor = i / 6;
                int cell = i % 6;
                int column = cell % 3;
                bool back = cell >= 3;
                int x = origin.x + Wall + column * thirdX;
                int z = origin.z + Wall + (back ? halfZ + Corridor / 2 : 0);
                int sx = thirdX - Wall * 2;
                int sz = halfZ - Corridor / 2 - Wall * 2;
                int y = origin.y + floor * floorHeight + 1;
                result[i] = new GuildHouseSpatialRoom(topology[i], floor, cell,
                    new int3(x, y, z), new int3(sx, floorHeight - 3, sz));
            }
            return result;
        }

        private static GuildHouseShellStyle ShellFor(GuildHouseKind kind)
        {
            switch (kind)
            {
                case GuildHouseKind.Wizards: return GuildHouseShellStyle.Tower;
                case GuildHouseKind.Druids: return GuildHouseShellStyle.Lodge;
                case GuildHouseKind.Assassins:
                case GuildHouseKind.Thieves: return GuildHouseShellStyle.HiddenDen;
                case GuildHouseKind.Clerics: return GuildHouseShellStyle.ChapelHouse;
                default: return GuildHouseShellStyle.Hall;
            }
        }
    }
}
