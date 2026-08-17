using System;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// High-level fantasy guild identities. These are building/program identities, not decoration
    /// archetype IDs: the same stable decoration library is deliberately reused across guilds.
    /// </summary>
    public enum GuildHouseKind : byte
    {
        Adventurers = 1,
        Wizards = 2,
        Knights = 3,
        Assassins = 4,
        Druids = 5,
        Thieves = 6,
        Clerics = 7,
        Rangers = 8,
        Bards = 9,
        Alchemists = 10,
    }

    public enum GuildHouseRoomRole : byte
    {
        EntryHall = 1,
        CommonHall = 2,
        GuildmasterOffice = 3,
        ContractHall = 4,
        Library = 5,
        Workshop = 6,
        TrainingRoom = 7,
        Shrine = 8,
        Vault = 9,
        Dormitory = 10,
        Kitchen = 11,
        HiddenRoom = 12,
        RitualRoom = 13,
        Garden = 14,
        TrophyHall = 15,
        PerformanceHall = 16,
        Infirmary = 17,
        Stable = 18,
    }

    [Flags]
    public enum GuildHouseTrait : ushort
    {
        None = 0,
        PublicFacing = 1 << 0,
        Secretive = 1 << 1,
        Magical = 1 << 2,
        Sacred = 1 << 3,
        Martial = 1 << 4,
        Organic = 1 << 5,
        Scholarly = 1 << 6,
        Mercantile = 1 << 7,
        Rustic = 1 << 8,
        Noble = 1 << 9,
    }

    public readonly struct GuildHouseRoomProgram
    {
        public readonly GuildHouseRoomRole Role;
        public readonly bool Required;
        public readonly byte Weight;
        public readonly ushort[] RequiredArchetypes;
        public readonly ushort[] OptionalArchetypes;

        public GuildHouseRoomProgram(
            GuildHouseRoomRole role,
            bool required,
            byte weight,
            ushort[] requiredArchetypes,
            ushort[] optionalArchetypes)
        {
            Role = role;
            Required = required;
            Weight = weight;
            RequiredArchetypes = requiredArchetypes ?? Array.Empty<ushort>();
            OptionalArchetypes = optionalArchetypes ?? Array.Empty<ushort>();
        }
    }

    public readonly struct GuildHouseProgram
    {
        public readonly GuildHouseKind Kind;
        public readonly GuildHouseTrait Traits;
        public readonly byte MinimumRooms;
        public readonly byte PreferredRooms;
        public readonly GuildHouseRoomProgram[] Rooms;

        public GuildHouseProgram(
            GuildHouseKind kind,
            GuildHouseTrait traits,
            byte minimumRooms,
            byte preferredRooms,
            GuildHouseRoomProgram[] rooms)
        {
            Kind = kind;
            Traits = traits;
            MinimumRooms = minimumRooms;
            PreferredRooms = preferredRooms;
            Rooms = rooms ?? Array.Empty<GuildHouseRoomProgram>();
        }
    }

    /// <summary>
    /// Building-scale composition profiles for fantasy guild houses. Archetype numbers refer to the
    /// canonical WorldBuilder decoration manifest. The shell planner can choose rooms from this
    /// program; each room then resolves through the existing decoration/socket pipeline.
    /// </summary>
    public static class GuildHouseProgramCatalog
    {
        public static GuildHouseProgram Get(GuildHouseKind kind)
        {
            switch (kind)
            {
                case GuildHouseKind.Wizards: return Wizards();
                case GuildHouseKind.Knights: return Knights();
                case GuildHouseKind.Assassins: return Assassins();
                case GuildHouseKind.Druids: return Druids();
                case GuildHouseKind.Thieves: return Thieves();
                case GuildHouseKind.Clerics: return Clerics();
                case GuildHouseKind.Rangers: return Rangers();
                case GuildHouseKind.Bards: return Bards();
                case GuildHouseKind.Alchemists: return Alchemists();
                default: return Adventurers();
            }
        }

        private static GuildHouseProgram Adventurers() => Program(
            GuildHouseKind.Adventurers,
            GuildHouseTrait.PublicFacing | GuildHouseTrait.Mercantile | GuildHouseTrait.Rustic,
            5, 8,
            Room(GuildHouseRoomRole.ContractHall, true, 255, A(281, 283, 284), A(282, 290, 291, 292, 296)),
            Room(GuildHouseRoomRole.CommonHall, true, 240, A(7, 12), A(8, 10, 41, 253)),
            Room(GuildHouseRoomRole.TrophyHall, true, 220, A(290), A(257, 267, 226, 227)),
            Room(GuildHouseRoomRole.Vault, true, 200, A(293), A(204, 132, 340)),
            Room(GuildHouseRoomRole.Dormitory, false, 180, A(287), A(241, 47, 289)),
            Room(GuildHouseRoomRole.Stable, false, 150, A(25, 28), A(27, 298, 29)),
            Room(GuildHouseRoomRole.Kitchen, false, 140, A(85), A(89, 91, 101)));

        private static GuildHouseProgram Wizards() => Program(
            GuildHouseKind.Wizards,
            GuildHouseTrait.Magical | GuildHouseTrait.Scholarly | GuildHouseTrait.Noble,
            6, 10,
            Room(GuildHouseRoomRole.Library, true, 255, A(361, 362, 376), A(127, 128, 372, 373, 378)),
            Room(GuildHouseRoomRole.Workshop, true, 250, A(221, 222), A(223, 224, 225, 232, 240)),
            Room(GuildHouseRoomRole.RitualRoom, true, 235, A(135, 136), A(142, 233, 236, 237, 360)),
            Room(GuildHouseRoomRole.TrainingRoom, true, 220, A(363, 365), A(366, 367, 379)),
            Room(GuildHouseRoomRole.GuildmasterOffice, true, 210, A(380), A(124, 126, 233, 251)),
            Room(GuildHouseRoomRole.Vault, false, 170, A(377), A(225, 228, 293, 400)),
            Room(GuildHouseRoomRole.HiddenRoom, false, 120, A(372), A(399, 400, 138)));

        private static GuildHouseProgram Knights() => Program(
            GuildHouseKind.Knights,
            GuildHouseTrait.Martial | GuildHouseTrait.Noble | GuildHouseTrait.PublicFacing,
            5, 8,
            Room(GuildHouseRoomRole.CommonHall, true, 255, A(12, 208), A(207, 227, 355, 257)),
            Room(GuildHouseRoomRole.TrainingRoom, true, 250, A(207, 208), A(226, 227, 367)),
            Room(GuildHouseRoomRole.Shrine, true, 190, A(341, 343), A(345, 347, 355, 358)),
            Room(GuildHouseRoomRole.TrophyHall, true, 210, A(257), A(226, 227, 267, 290)),
            Room(GuildHouseRoomRole.GuildmasterOffice, false, 170, A(246), A(251, 252, 259)),
            Room(GuildHouseRoomRole.Stable, false, 160, A(25, 27), A(28, 29, 298)));

        private static GuildHouseProgram Assassins() => Program(
            GuildHouseKind.Assassins,
            GuildHouseTrait.Secretive | GuildHouseTrait.Magical | GuildHouseTrait.Mercantile,
            5, 8,
            Room(GuildHouseRoomRole.HiddenRoom, true, 255, A(291, 204), A(389, 238, 386)),
            Room(GuildHouseRoomRole.Workshop, true, 240, A(115, 118), A(119, 131, 132, 139)),
            Room(GuildHouseRoomRole.TrainingRoom, true, 230, A(366), A(207, 129, 389)),
            Room(GuildHouseRoomRole.ContractHall, true, 220, A(282, 291), A(281, 296)),
            Room(GuildHouseRoomRole.Vault, false, 170, A(204), A(132, 293, 399)),
            Room(GuildHouseRoomRole.Infirmary, false, 130, A(131), A(119, 123, 137)));

        private static GuildHouseProgram Druids() => Program(
            GuildHouseKind.Druids,
            GuildHouseTrait.Organic | GuildHouseTrait.Magical | GuildHouseTrait.Sacred | GuildHouseTrait.Rustic,
            5, 9,
            Room(GuildHouseRoomRole.Garden, true, 255, A(303, 305, 318), A(301, 304, 309, 313, 319)),
            Room(GuildHouseRoomRole.Shrine, true, 250, A(316, 317), A(310, 311, 312, 315)),
            Room(GuildHouseRoomRole.RitualRoom, true, 220, A(307, 311), A(135, 142, 360)),
            Room(GuildHouseRoomRole.Workshop, true, 200, A(120, 123), A(96, 97, 119, 131)),
            Room(GuildHouseRoomRole.CommonHall, false, 170, A(302), A(306, 309, 318)),
            Room(GuildHouseRoomRole.HiddenRoom, false, 100, A(315), A(320, 238)));

        private static GuildHouseProgram Thieves() => Program(
            GuildHouseKind.Thieves,
            GuildHouseTrait.Secretive | GuildHouseTrait.Mercantile | GuildHouseTrait.Rustic,
            5, 8,
            Room(GuildHouseRoomRole.CommonHall, true, 240, A(12, 7), A(8, 204, 297)),
            Room(GuildHouseRoomRole.ContractHall, true, 235, A(282, 292), A(281, 291)),
            Room(GuildHouseRoomRole.HiddenRoom, true, 255, A(204, 293), A(132, 389, 399)),
            Room(GuildHouseRoomRole.Workshop, true, 190, A(47), A(48, 49, 84, 129)),
            Room(GuildHouseRoomRole.Vault, false, 180, A(293), A(204, 259, 340)),
            Room(GuildHouseRoomRole.Dormitory, false, 120, A(287), A(241, 244)));

        private static GuildHouseProgram Clerics() => Program(
            GuildHouseKind.Clerics,
            GuildHouseTrait.Sacred | GuildHouseTrait.PublicFacing | GuildHouseTrait.Scholarly,
            5, 8,
            Room(GuildHouseRoomRole.Shrine, true, 255, A(341, 346, 350), A(342, 345, 349, 352, 358, 360)),
            Room(GuildHouseRoomRole.Infirmary, true, 220, A(123, 131), A(119, 137, 154)),
            Room(GuildHouseRoomRole.Library, true, 200, A(351, 376), A(127, 128, 373)),
            Room(GuildHouseRoomRole.CommonHall, true, 190, A(343), A(344, 355, 359)),
            Room(GuildHouseRoomRole.Vault, false, 140, A(348), A(349, 168, 400)));

        private static GuildHouseProgram Rangers() => Program(
            GuildHouseKind.Rangers,
            GuildHouseTrait.Organic | GuildHouseTrait.Rustic | GuildHouseTrait.PublicFacing,
            4, 7,
            Room(GuildHouseRoomRole.CommonHall, true, 240, A(12, 199), A(7, 97, 298)),
            Room(GuildHouseRoomRole.Workshop, true, 220, A(27, 288), A(289, 298, 159)),
            Room(GuildHouseRoomRole.TrophyHall, true, 200, A(267), A(266, 275, 290)),
            Room(GuildHouseRoomRole.Stable, true, 190, A(25, 28), A(27, 29, 298)),
            Room(GuildHouseRoomRole.Dormitory, false, 150, A(287), A(47, 289)),
            Room(GuildHouseRoomRole.Garden, false, 100, A(318), A(303, 305)));

        private static GuildHouseProgram Bards() => Program(
            GuildHouseKind.Bards,
            GuildHouseTrait.PublicFacing | GuildHouseTrait.Noble | GuildHouseTrait.Mercantile,
            4, 7,
            Room(GuildHouseRoomRole.PerformanceHall, true, 255, A(253, 254), A(255, 256, 252)),
            Room(GuildHouseRoomRole.CommonHall, true, 230, A(7, 12), A(8, 10, 249)),
            Room(GuildHouseRoomRole.Library, true, 170, A(295, 376), A(127, 128, 374)),
            Room(GuildHouseRoomRole.GuildmasterOffice, false, 150, A(246), A(251, 259, 260)),
            Room(GuildHouseRoomRole.Dormitory, false, 120, A(287), A(241, 248)));

        private static GuildHouseProgram Alchemists() => Program(
            GuildHouseKind.Alchemists,
            GuildHouseTrait.Magical | GuildHouseTrait.Scholarly | GuildHouseTrait.Mercantile,
            5, 8,
            Room(GuildHouseRoomRole.Workshop, true, 255, A(115, 116, 133), A(117, 118, 119, 131, 139)),
            Room(GuildHouseRoomRole.Library, true, 190, A(120, 127), A(128, 139, 374)),
            Room(GuildHouseRoomRole.Vault, true, 170, A(132), A(121, 142, 225)),
            Room(GuildHouseRoomRole.CommonHall, true, 150, A(85), A(89, 113, 131)),
            Room(GuildHouseRoomRole.GuildmasterOffice, false, 140, A(246), A(123, 124, 233)),
            Room(GuildHouseRoomRole.HiddenRoom, false, 90, A(135), A(136, 138, 399)));

        private static GuildHouseProgram Program(
            GuildHouseKind kind,
            GuildHouseTrait traits,
            byte minimumRooms,
            byte preferredRooms,
            params GuildHouseRoomProgram[] rooms)
            => new GuildHouseProgram(kind, traits, minimumRooms, preferredRooms, rooms);

        private static GuildHouseRoomProgram Room(
            GuildHouseRoomRole role,
            bool required,
            byte weight,
            ushort[] required,
            ushort[] optional)
            => new GuildHouseRoomProgram(role, required, weight, required, optional);

        private static ushort[] A(params ushort[] ids) => ids;
    }
}
