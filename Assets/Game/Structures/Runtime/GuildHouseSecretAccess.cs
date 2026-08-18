using System;
using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public readonly struct GuildHouseSecretPortal
    {
        public readonly int RoomIndex;
        public readonly int3 Min;
        public readonly int3 Size;
        public readonly int3 Facing;
        public readonly bool Arcane;

        public GuildHouseSecretPortal(int roomIndex, int3 min, int3 size, int3 facing, bool arcane)
        {
            RoomIndex = roomIndex;
            Min = min;
            Size = size;
            Facing = facing;
            Arcane = arcane;
        }

        public bool IsWellFormed => RoomIndex >= 0 && math.all(Size > 0) &&
            math.abs(Facing.x) + math.abs(Facing.z) == 1;
    }

    public static class GuildHouseSecretAccessPlanner
    {
        public static GuildHouseSecretPortal[] Plan(in GuildHouseSpatialPlan plan)
        {
            if (!plan.IsWellFormed || plan.Rooms == null)
                return Array.Empty<GuildHouseSecretPortal>();

            int count = 0;
            for (int i = 0; i < plan.Rooms.Length; i++)
                if (plan.Rooms[i].Node.HiddenAccess) count++;

            var result = new GuildHouseSecretPortal[count];
            int output = 0;
            for (int i = 0; i < plan.Rooms.Length; i++)
            {
                GuildHouseSpatialRoom room = plan.Rooms[i];
                if (!room.Node.HiddenAccess) continue;

                // Grid shells expose their central corridor on the room's X-facing inner edge.
                // Lodge hidden rooms use the same deterministic convention until organic corridors
                // receive a richer polygonal planner.
                bool leftSide = room.CellIndex % 2 == 0;
                int doorWidth = math.min(8, math.max(5, room.Size.z / 4));
                int doorHeight = math.min(16, math.max(10, room.Size.y - 5));
                int centerZ = room.Min.z + room.Size.z / 2;
                int z = centerZ - doorWidth / 2;
                int x = leftSide ? room.MaxExclusive.x - 2 : room.Min.x;
                int3 facing = leftSide ? new int3(1, 0, 0) : new int3(-1, 0, 0);
                bool arcane = plan.Kind == GuildHouseKind.Wizards;

                result[output++] = new GuildHouseSecretPortal(
                    i,
                    new int3(x, room.Min.y + 1, z),
                    new int3(2, doorHeight, doorWidth),
                    facing,
                    arcane);
            }
            return result;
        }
    }

    /// <summary>
    /// Authors a corridor-facing concealed partition. The panel uses the surrounding wall material,
    /// so it reads as hidden by default; Wizard forbidden archives receive a small emissive rune clue.
    /// </summary>
    public static class GuildHouseSecretAccessAuthoring
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            in GuildHouseSpatialPlan plan,
            DecorationRegionTheme region)
        {
            if (authoring == null || !plan.IsWellFormed) return;
            GuildHouseSecretPortal[] portals = GuildHouseSecretAccessPlanner.Plan(in plan);
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            byte wall = profile.IsWellFormed ? profile.PrimaryMaterial : GameMaterialIds.Wood;
            byte accent = profile.IsWellFormed ? profile.MagicMaterial : GameMaterialIds.LitWindow;

            for (int i = 0; i < portals.Length; i++)
            {
                GuildHouseSecretPortal portal = portals[i];
                if (!portal.IsWellFormed) continue;
                GuildHouseSpatialRoom room = plan.Rooms[portal.RoomIndex];
                int wallX = portal.Min.x;
                int wallY = room.Min.y;
                int wallZ = room.Min.z;
                int wallHeight = room.Size.y;

                // Author the hidden room's corridor-facing partition around the disguised panel.
                int before = portal.Min.z - wallZ;
                int afterStart = portal.Min.z + portal.Size.z;
                int after = room.MaxExclusive.z - afterStart;
                if (before > 0)
                    authoring.Box(new int3(wallX, wallY, wallZ), new int3(2, wallHeight, before), wall);
                if (after > 0)
                    authoring.Box(new int3(wallX, wallY, afterStart), new int3(2, wallHeight, after), wall);
                int lintelY = portal.Min.y + portal.Size.y;
                int lintelHeight = room.MaxExclusive.y - lintelY;
                if (lintelHeight > 0)
                    authoring.Box(new int3(wallX, lintelY, portal.Min.z),
                        new int3(2, lintelHeight, portal.Size.z), wall);

                // Closed concealed panel. Gameplay can later promote/open it using the portal metadata.
                authoring.Box(portal.Min, portal.Size, wall);
                if (portal.Arcane)
                {
                    int runeY = portal.Min.y + portal.Size.y / 2;
                    int runeZ = portal.Min.z + portal.Size.z / 2;
                    authoring.Box(new int3(wallX - portal.Facing.x, runeY, runeZ),
                        new int3(1, 3, 1), accent);
                }
            }
        }
    }
}
