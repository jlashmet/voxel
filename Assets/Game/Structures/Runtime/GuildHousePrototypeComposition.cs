using System;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public readonly struct GuildHouseRoomComposition
    {
        public readonly GuildHouseSpatialRoom SpatialRoom;
        public readonly DecorationSpace Space;
        public readonly DecorationContext Context;
        public readonly ushort[] RequiredArchetypes;
        public readonly ushort[] OptionalArchetypes;

        public GuildHouseRoomComposition(
            GuildHouseSpatialRoom spatialRoom,
            DecorationSpace space,
            DecorationContext context,
            ushort[] requiredArchetypes,
            ushort[] optionalArchetypes)
        {
            SpatialRoom = spatialRoom;
            Space = space;
            Context = context;
            RequiredArchetypes = requiredArchetypes ?? Array.Empty<ushort>();
            OptionalArchetypes = optionalArchetypes ?? Array.Empty<ushort>();
        }
    }

    public readonly struct GuildHousePrototype
    {
        public readonly GuildHouseSpatialPlan SpatialPlan;
        public readonly DecorationRegionTheme Region;
        public readonly GuildHouseRoomComposition[] Rooms;

        public GuildHousePrototype(
            GuildHouseSpatialPlan spatialPlan,
            DecorationRegionTheme region,
            GuildHouseRoomComposition[] rooms)
        {
            SpatialPlan = spatialPlan;
            Region = region;
            Rooms = rooms ?? Array.Empty<GuildHouseRoomComposition>();
        }

        public bool IsWellFormed => SpatialPlan.IsWellFormed && Rooms != null && Rooms.Length > 0;
    }

    /// <summary>
    /// Bridges building-scale guild rooms into the existing decoration system. Room identity is fixed
    /// before region presentation: the same Wizard Library remains semantically the same room in
    /// Kentridge or Moordell, while style/wealth/material presentation can differ by region.
    /// </summary>
    public static class GuildHousePrototypeComposition
    {
        public static GuildHousePrototype Build(
            GuildHouseKind kind,
            DecorationRegionTheme region,
            uint worldSeed,
            uint structureId,
            int3 origin,
            int width,
            int depth,
            int requestedRooms = 0)
        {
            GuildHouseSpatialPlan spatial = GuildHouseSpatialPlanner.Plan(
                kind, worldSeed ^ structureId, origin, width, depth, requestedRooms);
            var rooms = new GuildHouseRoomComposition[spatial.Rooms.Length];

            for (int i = 0; i < spatial.Rooms.Length; i++)
            {
                GuildHouseSpatialRoom room = spatial.Rooms[i];
                uint spaceId = structureId * 257u + (uint)(i + 1);
                DecorationSpaceKind spaceKind = SpaceKind(room.Node.Room.Role);
                var space = new DecorationSpace
                {
                    SpaceId = spaceId,
                    Kind = spaceKind,
                    Bounds = new DecorationBounds
                    {
                        Min = room.Min,
                        MaxExclusive = room.MaxExclusive,
                    },
                };

                var context = new DecorationContext
                {
                    WorldSeed = worldSeed,
                    StructureId = structureId,
                    SpaceId = spaceId,
                    StructureKind = DecorationStructureKind.House,
                    SpaceKind = spaceKind,
                    Wealth = DecorationWealthTier.Comfortable,
                    Condition = DecorationConditionTier.Maintained,
                    Environment = room.Node.Room.Role == GuildHouseRoomRole.Garden
                        ? DecorationEnvironmentTags.Exterior
                        : DecorationEnvironmentTags.Interior,
                };
                context = DecorationRegionProfiles.ApplyDefaults(
                    in context, region, localStyleVariation: (uint)(i + 1));

                rooms[i] = new GuildHouseRoomComposition(
                    room,
                    space,
                    context,
                    room.Node.Room.RequiredArchetypes,
                    room.Node.Room.OptionalArchetypes);
            }

            return new GuildHousePrototype(spatial, region, rooms);
        }

        private static DecorationSpaceKind SpaceKind(GuildHouseRoomRole role)
        {
            switch (role)
            {
                case GuildHouseRoomRole.Dormitory: return DecorationSpaceKind.Bedroom;
                case GuildHouseRoomRole.Shrine:
                case GuildHouseRoomRole.RitualRoom: return DecorationSpaceKind.Shrine;
                case GuildHouseRoomRole.Library:
                case GuildHouseRoomRole.GuildmasterOffice:
                case GuildHouseRoomRole.ContractHall: return DecorationSpaceKind.Study;
                case GuildHouseRoomRole.Vault: return DecorationSpaceKind.Storage;
                case GuildHouseRoomRole.Stable: return DecorationSpaceKind.Storage;
                default: return DecorationSpaceKind.Study;
            }
        }
    }
}
