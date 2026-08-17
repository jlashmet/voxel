using System;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum GuildHouseDecorationSource : byte
    {
        None = 0,
        Expansion200 = 1,
        Expansion260 = 2,
        Expansion320 = 3,
        Expansion380 = 4,
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
    /// Reuses existing semantic room scenes for guild-house interiors. This is deliberately a
    /// composition layer: guild identity chooses which proven scene resolver furnishes each room.
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
            DecorationPlacement[] placements;
            GuildHouseDecorationSource source;

            if (guild == GuildHouseKind.Wizards)
            {
                if (!TryResolveWizard(region, in room, out source, out placements))
                    return false;
            }
            else if (guild == GuildHouseKind.Druids)
            {
                if (!TryResolveDruid(region, in room, out source, out placements))
                    return false;
            }
            else
            {
                // Other guild programs already exist; their room-to-scene dispatch is the next
                // incremental layer. Do not silently invent generic furniture here.
                return false;
            }

            resolved = new GuildHouseResolvedRoom(room, source, placements);
            return placements != null && placements.Length > 0;
        }

        private static bool TryResolveWizard(
            DecorationRegionTheme region,
            in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source,
            out DecorationPlacement[] placements)
        {
            source = GuildHouseDecorationSource.None;
            placements = Array.Empty<DecorationPlacement>();
            DecorationExclusion[] exclusions = Array.Empty<DecorationExclusion>();

            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.Library:
                    source = GuildHouseDecorationSource.Expansion380;
                    return DecorationExpansion380SceneResolver.TryResolve(
                        DecorationExpansion380SceneKind.WizardLibrary, region,
                        in room.Space, in room.Context, exclusions, out placements);
                case GuildHouseRoomRole.Workshop:
                    source = GuildHouseDecorationSource.Expansion260;
                    return DecorationExpansion260SceneResolver.TryResolve(
                        DecorationExpansion260SceneKind.EnchantersWorkshop,
                        in room.Space, in room.Context, exclusions, out placements);
                case GuildHouseRoomRole.RitualRoom:
                    source = GuildHouseDecorationSource.Expansion200;
                    return DecorationExpansion200SceneResolver.TryResolve(
                        DecorationExpansion200SceneKind.RitualChamber,
                        in room.Space, in room.Context, exclusions, out placements);
                case GuildHouseRoomRole.TrainingRoom:
                    source = GuildHouseDecorationSource.Expansion380;
                    return DecorationExpansion380SceneResolver.TryResolve(
                        DecorationExpansion380SceneKind.SpellClassroom, region,
                        in room.Space, in room.Context, exclusions, out placements);
                case GuildHouseRoomRole.HiddenRoom:
                case GuildHouseRoomRole.Vault:
                    source = GuildHouseDecorationSource.Expansion380;
                    return DecorationExpansion380SceneResolver.TryResolve(
                        DecorationExpansion380SceneKind.ForbiddenArchive, region,
                        in room.Space, in room.Context, exclusions, out placements);
                case GuildHouseRoomRole.GuildmasterOffice:
                    source = GuildHouseDecorationSource.Expansion260;
                    return DecorationExpansion260SceneResolver.TryResolve(
                        DecorationExpansion260SceneKind.ArcaneGallery,
                        in room.Space, in room.Context, exclusions, out placements);
                default:
                    return false;
            }
        }

        private static bool TryResolveDruid(
            DecorationRegionTheme region,
            in GuildHouseRoomComposition room,
            out GuildHouseDecorationSource source,
            out DecorationPlacement[] placements)
        {
            source = GuildHouseDecorationSource.None;
            placements = Array.Empty<DecorationPlacement>();
            DecorationExclusion[] exclusions = Array.Empty<DecorationExclusion>();

            switch (room.SpatialRoom.Node.Room.Role)
            {
                case GuildHouseRoomRole.Garden:
                    source = GuildHouseDecorationSource.Expansion320;
                    return DecorationExpansion320SceneResolver.TryResolve(
                        DecorationExpansion320SceneKind.EnchantedGrove, region,
                        in room.Space, in room.Context, exclusions, out placements);
                case GuildHouseRoomRole.Shrine:
                case GuildHouseRoomRole.RitualRoom:
                    source = GuildHouseDecorationSource.Expansion320;
                    return DecorationExpansion320SceneResolver.TryResolve(
                        DecorationExpansion320SceneKind.DruidShrine, region,
                        in room.Space, in room.Context, exclusions, out placements);
                case GuildHouseRoomRole.CommonHall:
                case GuildHouseRoomRole.HiddenRoom:
                    source = GuildHouseDecorationSource.Expansion320;
                    return DecorationExpansion320SceneResolver.TryResolve(
                        DecorationExpansion320SceneKind.FairyClearing, region,
                        in room.Space, in room.Context, exclusions, out placements);
                case GuildHouseRoomRole.Workshop:
                    source = GuildHouseDecorationSource.Expansion200;
                    return DecorationExpansion200SceneResolver.TryResolve(
                        DecorationExpansion200SceneKind.AlchemyLab,
                        in room.Space, in room.Context, exclusions, out placements);
                default:
                    return false;
            }
        }
    }
}
