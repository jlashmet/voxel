using System;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum GuildHouseDecorationSource : byte
    {
        None = 0,
        Content = 1,
        Expansion200 = 2,
        Expansion260 = 3,
        Expansion300 = 4,
        Expansion320 = 5,
        Expansion340 = 6,
        Expansion360 = 7,
        Expansion380 = 8,
    }

    public readonly struct GuildHouseResolvedRoom
    {
        public readonly GuildHouseRoomComposition Room;
        public readonly GuildHouseDecorationSource Source;
        public readonly DecorationPlacement[] Placements;

        public GuildHouseResolvedRoom(
            GuildHouseRoomComposition room,
            GuildHouseDecorationSource source,
            DecorationPlacement[] placements)
        {
            Room = room;
            Source = source;
            Placements = placements ?? Array.Empty<DecorationPlacement>();
        }
    }

    /// <summary>
    /// Reuses existing semantic room scenes for guild-house interiors. Guild identity chooses proven
    /// scene resolvers; signature guild props can later layer on top without duplicating core content.
    /// </summary>
    public static class GuildHouseRoomDecorationResolver
    {
        public static bool TryResolvePrototype(
            in GuildHousePrototype prototype,
            out GuildHouseResolvedRoom[] rooms)
        {
            rooms = Array.Empty<GuildHouseResolvedRoom>();
            if (!prototype.IsWellFormed)
                return false;

            var result = new GuildHouseResolvedRoom[prototype.Rooms.Length];
            for (int i = 0; i < prototype.Rooms.Length; i++)
            {
                if (!TryResolveRoom(prototype.SpatialPlan.Kind, prototype.Region,
                        in prototype.Rooms[i], out GuildHouseResolvedRoom resolved))
                    return false;
                result[i] = resolved;
            }
            rooms = result;
            return true;
        }

        public static bool TryResolveRoom(
            GuildHouseKind guild,
            DecorationRegionTheme region,
            in GuildHouseRoomComposition room,
            out GuildHouseResolvedRoom resolved)
        {
            resolved = default;
            GuildHouseDecorationSource source;
            DecorationPlacement[] placements;
            bool ok;

            switch (guild)
            {
                case GuildHouseKind.Wizards: ok = TryResolveWizard(region, in room, out source, out placements); break;
                case GuildHouseKind.Druids: ok = TryResolveDruid(region, in room, out source, out placements); break;
                case GuildHouseKind.Adventurers: ok = TryResolveAdventurer(region, in room, out source, out placements); break;
                case GuildHouseKind.Knights: ok = TryResolveKnight(region, in room, out source, out placements); break;
                case GuildHouseKind.Assassins: ok = TryResolveAssassin(region, in room, out source, out placements); break;
                case GuildHouseKind.Thieves: ok = TryResolveThief(region, in room, out source, out placements); break;
                case GuildHouseKind.Clerics: ok = TryResolveCleric(region, in room, out source, out placements); break;
                case GuildHouseKind.Rangers: ok = TryResolveRanger(region, in room, out source, out placements); break;
                case GuildHouseKind.Bards: ok = TryResolveBard(region, in room, out source, out placements); break;
                case GuildHouseKind.Alchemists: ok = TryResolveAlchemist(region, in room, out source, out placements); break;
                default: return false;
            }

            if (!ok || placements == null || placements.Length == 0)
                return false;
            resolved = new GuildHouseResolvedRoom(room, source, placements);
            return true;
        }

        private static bool TryResolveWizard(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.Library: return R380(DecorationExpansion380SceneKind.WizardLibrary, region, in room, out source, out placements);
                case GuildHouseRoomRole.Workshop: return R260(DecorationExpansion260SceneKind.EnchantersWorkshop, in room, out source, out placements);
                case GuildHouseRoomRole.RitualRoom: return R200(DecorationExpansion200SceneKind.RitualChamber, in room, out source, out placements);
                case GuildHouseRoomRole.TrainingRoom: return R380(DecorationExpansion380SceneKind.SpellClassroom, region, in room, out source, out placements);
                case GuildHouseRoomRole.HiddenRoom:
                case GuildHouseRoomRole.Vault: return R380(DecorationExpansion380SceneKind.ForbiddenArchive, region, in room, out source, out placements);
                case GuildHouseRoomRole.GuildmasterOffice: return R260(DecorationExpansion260SceneKind.ArcaneGallery, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static bool TryResolveDruid(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.Garden: return R320(DecorationExpansion320SceneKind.EnchantedGrove, region, in room, out source, out placements);
                case GuildHouseRoomRole.Shrine:
                case GuildHouseRoomRole.RitualRoom: return R320(DecorationExpansion320SceneKind.DruidShrine, region, in room, out source, out placements);
                case GuildHouseRoomRole.CommonHall:
                case GuildHouseRoomRole.HiddenRoom: return R320(DecorationExpansion320SceneKind.FairyClearing, region, in room, out source, out placements);
                case GuildHouseRoomRole.Workshop: return R200(DecorationExpansion200SceneKind.AlchemyLab, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static bool TryResolveAdventurer(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.ContractHall:
                case GuildHouseRoomRole.TrophyHall: return R300(DecorationExpansion300SceneKind.AdventurerGuildHall, in room, out source, out placements);
                case GuildHouseRoomRole.CommonHall:
                case GuildHouseRoomRole.Kitchen: return RC(DecorationContentSceneKind.TavernBar, in room, out source, out placements);
                case GuildHouseRoomRole.Vault: return R340(DecorationExpansion340SceneKind.TreasureVault, in room, out source, out placements);
                case GuildHouseRoomRole.Dormitory: return R260(DecorationExpansion260SceneKind.PrivateChamber, in room, out source, out placements);
                case GuildHouseRoomRole.Stable: return RC(DecorationContentSceneKind.Stable, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static bool TryResolveKnight(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.CommonHall: return R260(DecorationExpansion260SceneKind.NobleSalon, in room, out source, out placements);
                case GuildHouseRoomRole.TrainingRoom: return R260(DecorationExpansion260SceneKind.ArmoryShop, in room, out source, out placements);
                case GuildHouseRoomRole.Shrine: return R360(DecorationExpansion360SceneKind.VillageShrine, region, in room, out source, out placements);
                case GuildHouseRoomRole.TrophyHall: return R300(DecorationExpansion300SceneKind.AdventurerGuildHall, in room, out source, out placements);
                case GuildHouseRoomRole.GuildmasterOffice: return R260(DecorationExpansion260SceneKind.NobleSalon, in room, out source, out placements);
                case GuildHouseRoomRole.Stable: return RC(DecorationContentSceneKind.Stable, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static bool TryResolveAssassin(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.ContractHall: return R300(DecorationExpansion300SceneKind.AdventurerGuildHall, in room, out source, out placements);
                case GuildHouseRoomRole.Workshop: return R200(DecorationExpansion200SceneKind.AlchemyLab, in room, out source, out placements);
                case GuildHouseRoomRole.TrainingRoom: return R260(DecorationExpansion260SceneKind.ArmoryShop, in room, out source, out placements);
                case GuildHouseRoomRole.HiddenRoom:
                case GuildHouseRoomRole.Vault: return R340(DecorationExpansion340SceneKind.TreasureVault, in room, out source, out placements);
                case GuildHouseRoomRole.Infirmary: return R360(DecorationExpansion360SceneKind.VillageShrine, region, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static bool TryResolveThief(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.CommonHall: return RC(DecorationContentSceneKind.TavernBar, in room, out source, out placements);
                case GuildHouseRoomRole.ContractHall: return R300(DecorationExpansion300SceneKind.AdventurerGuildHall, in room, out source, out placements);
                case GuildHouseRoomRole.HiddenRoom:
                case GuildHouseRoomRole.Vault: return R340(DecorationExpansion340SceneKind.TreasureVault, in room, out source, out placements);
                case GuildHouseRoomRole.Workshop: return RC(DecorationContentSceneKind.Smithy, in room, out source, out placements);
                case GuildHouseRoomRole.Dormitory: return R260(DecorationExpansion260SceneKind.PrivateChamber, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static bool TryResolveCleric(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.Shrine:
                case GuildHouseRoomRole.CommonHall: return R360(DecorationExpansion360SceneKind.GrandTemple, region, in room, out source, out placements);
                case GuildHouseRoomRole.Infirmary: return R360(DecorationExpansion360SceneKind.VillageShrine, region, in room, out source, out placements);
                case GuildHouseRoomRole.Library: return R380(DecorationExpansion380SceneKind.WizardLibrary, region, in room, out source, out placements);
                case GuildHouseRoomRole.Vault: return R360(DecorationExpansion360SceneKind.SacredCrypt, region, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static bool TryResolveRanger(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.CommonHall: return RC(DecorationContentSceneKind.TavernBar, in room, out source, out placements);
                case GuildHouseRoomRole.Workshop: return R300(DecorationExpansion300SceneKind.CaravanStaging, in room, out source, out placements);
                case GuildHouseRoomRole.TrophyHall: return R300(DecorationExpansion300SceneKind.AdventurerGuildHall, in room, out source, out placements);
                case GuildHouseRoomRole.Stable: return RC(DecorationContentSceneKind.Stable, in room, out source, out placements);
                case GuildHouseRoomRole.Dormitory: return R260(DecorationExpansion260SceneKind.PrivateChamber, in room, out source, out placements);
                case GuildHouseRoomRole.Garden: return R320(DecorationExpansion320SceneKind.EnchantedGrove, region, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static bool TryResolveBard(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.PerformanceHall: return R260(DecorationExpansion260SceneKind.MusicRoom, in room, out source, out placements);
                case GuildHouseRoomRole.CommonHall: return RC(DecorationContentSceneKind.TavernBar, in room, out source, out placements);
                case GuildHouseRoomRole.Library: return R380(DecorationExpansion380SceneKind.WizardLibrary, region, in room, out source, out placements);
                case GuildHouseRoomRole.GuildmasterOffice: return R260(DecorationExpansion260SceneKind.NobleSalon, in room, out source, out placements);
                case GuildHouseRoomRole.Dormitory: return R260(DecorationExpansion260SceneKind.PrivateChamber, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static bool TryResolveAlchemist(DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        {
            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.Workshop:
                case GuildHouseRoomRole.CommonHall: return R200(DecorationExpansion200SceneKind.AlchemyLab, in room, out source, out placements);
                case GuildHouseRoomRole.Library: return R380(DecorationExpansion380SceneKind.WizardLibrary, region, in room, out source, out placements);
                case GuildHouseRoomRole.Vault: return R340(DecorationExpansion340SceneKind.TreasureVault, in room, out source, out placements);
                case GuildHouseRoomRole.GuildmasterOffice: return R260(DecorationExpansion260SceneKind.ArcaneGallery, in room, out source, out placements);
                case GuildHouseRoomRole.HiddenRoom: return R200(DecorationExpansion200SceneKind.RitualChamber, in room, out source, out placements);
                default: return Fail(out source, out placements);
            }
        }

        private static DecorationExclusion[] E() => Array.Empty<DecorationExclusion>();

        private static bool RC(DecorationContentSceneKind kind, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        { source = GuildHouseDecorationSource.Content; return DecorationContentSceneResolver.TryResolve(kind, in room.Space, in room.Context, E(), out placements); }

        private static bool R200(DecorationExpansion200SceneKind kind, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        { source = GuildHouseDecorationSource.Expansion200; return DecorationExpansion200SceneResolver.TryResolve(kind, in room.Space, in room.Context, E(), out placements); }

        private static bool R260(DecorationExpansion260SceneKind kind, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        { source = GuildHouseDecorationSource.Expansion260; return DecorationExpansion260SceneResolver.TryResolve(kind, in room.Space, in room.Context, E(), out placements); }

        private static bool R300(DecorationExpansion300SceneKind kind, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        { source = GuildHouseDecorationSource.Expansion300; return DecorationExpansion300SceneResolver.TryResolve(kind, in room.Space, in room.Context, E(), out placements); }

        private static bool R320(DecorationExpansion320SceneKind kind, DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        { source = GuildHouseDecorationSource.Expansion320; return DecorationExpansion320SceneResolver.TryResolve(kind, region, in room.Space, in room.Context, E(), out placements); }

        private static bool R340(DecorationExpansion340SceneKind kind, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        { source = GuildHouseDecorationSource.Expansion340; return DecorationExpansion340SceneResolver.TryResolve(kind, in room.Space, in room.Context, E(), out placements); }

        private static bool R360(DecorationExpansion360SceneKind kind, DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        { source = GuildHouseDecorationSource.Expansion360; return DecorationExpansion360SceneResolver.TryResolve(kind, region, in room.Space, in room.Context, E(), out placements); }

        private static bool R380(DecorationExpansion380SceneKind kind, DecorationRegionTheme region, in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        { source = GuildHouseDecorationSource.Expansion380; return DecorationExpansion380SceneResolver.TryResolve(kind, region, in room.Space, in room.Context, E(), out placements); }

        private static bool Fail(out GuildHouseDecorationSource source, out DecorationPlacement[] placements)
        { source = GuildHouseDecorationSource.None; placements = Array.Empty<DecorationPlacement>(); return false; }
    }
}
