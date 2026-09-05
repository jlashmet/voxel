using System;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Palette-aware guild-house furnishing path. It consumes the archetypes carried by the generated
    /// production rooms and delegates every actual placement to the shared socket/clearance resolver.
    /// </summary>
    public static class GuildHouseFurnishingResolver
    {
        private const uint RequiredSlotBase = 0x00100000u;
        private const uint OptionalSlotBase = 0x00200000u;
        private const uint SceneDiscriminator = 0x484F5553u; // HOUS

        public static bool TryResolvePrototype(
            in GuildHousePrototype prototype,
            in GuildHouseFurnishingPalette palette,
            out GuildHouseResolvedRoom[] rooms,
            out GuildHouseUnplacedFurnishing[] unplaced)
        {
            rooms = Array.Empty<GuildHouseResolvedRoom>();
            unplaced = Array.Empty<GuildHouseUnplacedFurnishing>();
            if (!prototype.IsWellFormed)
                return false;

            if (!palette.IsSpecified)
                return GuildHouseRoomDecorationResolver.TryResolvePrototype(in prototype, out rooms);
            if (palette.Kind != prototype.SpatialPlan.Kind)
                return false;

            ushort[] selected = palette.SelectedOptionalArchetypes;
            var seenInGeneratedRoom = new bool[selected.Length];
            var placedSelected = new bool[selected.Length];
            var result = new GuildHouseResolvedRoom[prototype.Rooms.Length];

            for (int roomIndex = 0; roomIndex < prototype.Rooms.Length; roomIndex++)
            {
                GuildHouseRoomComposition room = prototype.Rooms[roomIndex];
                DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in room.Space);
                var occupied = new DecorationPlacement[
                    room.RequiredArchetypes.Length + room.OptionalArchetypes.Length];
                int occupiedCount = 0;
                uint sceneId = DecorationSeed.Derive(room.Space.SpaceId, SceneDiscriminator);

                for (int i = 0; i < room.RequiredArchetypes.Length; i++)
                {
                    ushort stableId = room.RequiredArchetypes[i];
                    MarkSeen(selected, stableId, seenInGeneratedRoom);
                    if (!TryPlaceCanonical(
                            in room,
                            sceneId,
                            RequiredSlotBase + stableId,
                            stableId,
                            sockets,
                            occupied,
                            occupiedCount,
                            out DecorationPlacement placement))
                        return false;

                    occupied[occupiedCount++] = placement;
                    MarkPlaced(selected, stableId, placedSelected);
                }

                for (int i = 0; i < room.OptionalArchetypes.Length; i++)
                {
                    ushort stableId = room.OptionalArchetypes[i];
                    int selectedIndex = IndexOf(selected, stableId);
                    if (selectedIndex < 0)
                        continue;

                    seenInGeneratedRoom[selectedIndex] = true;
                    if (placedSelected[selectedIndex])
                        continue;

                    if (!TryPlaceCanonical(
                            in room,
                            sceneId,
                            OptionalSlotBase + stableId,
                            stableId,
                            sockets,
                            occupied,
                            occupiedCount,
                            out DecorationPlacement placement))
                        continue;

                    occupied[occupiedCount++] = placement;
                    placedSelected[selectedIndex] = true;
                }

                var placements = new DecorationPlacement[occupiedCount];
                Array.Copy(occupied, placements, occupiedCount);
                result[roomIndex] = new GuildHouseResolvedRoom(
                    room,
                    GuildHouseDecorationSource.None,
                    placements);
            }

            var tmpUnplaced = new GuildHouseUnplacedFurnishing[selected.Length];
            int unplacedCount = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (placedSelected[i])
                    continue;
                tmpUnplaced[unplacedCount++] = new GuildHouseUnplacedFurnishing(
                    selected[i],
                    seenInGeneratedRoom[i]
                        ? GuildHouseUnplacedReason.NoValidPlacement
                        : GuildHouseUnplacedReason.RoomUnavailable);
            }

            var finalUnplaced = new GuildHouseUnplacedFurnishing[unplacedCount];
            Array.Copy(tmpUnplaced, finalUnplaced, unplacedCount);
            rooms = result;
            unplaced = finalUnplaced;
            return true;
        }

        private static bool TryPlaceCanonical(
            in GuildHouseRoomComposition room,
            uint sceneId,
            uint slotId,
            ushort stableId,
            DecorationSocket[] sockets,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            placement = default;
            if (!DecorationCanonicalPlacementCatalog.TryDescribe(
                    in room.Context,
                    sceneId,
                    slotId,
                    stableId,
                    out DecorationPropDescriptor descriptor))
                return false;

            if (descriptor.MountMode == DecorationMountMode.AnchorRelative)
            {
                // The rectangular guild-room analyzer owns structural floor/wall/ceiling sockets.
                // Anchor-relative recipes that explicitly accept Floor may legally fall back to that
                // production socket; anchor-only recipes remain unplaced rather than receiving a
                // fabricated anchor or coordinate.
                if ((descriptor.AcceptedSockets & DecorationSocketKind.Floor) == 0)
                    return false;
                descriptor.AcceptedSockets = DecorationSocketKind.Floor;
                descriptor.MountMode = DecorationMountMode.Floor;
            }

            return DecorationPlacementResolver.TryPlace(
                in room.Space,
                in room.Context,
                sceneId,
                slotId,
                in descriptor,
                sockets,
                Array.Empty<DecorationExclusion>(),
                occupied,
                occupiedCount,
                out placement);
        }

        private static void MarkSeen(ushort[] selected, ushort stableId, bool[] seen)
        {
            int index = IndexOf(selected, stableId);
            if (index >= 0)
                seen[index] = true;
        }

        private static void MarkPlaced(ushort[] selected, ushort stableId, bool[] placed)
        {
            int index = IndexOf(selected, stableId);
            if (index >= 0)
                placed[index] = true;
        }

        private static int IndexOf(ushort[] values, ushort value)
        {
            for (int i = 0; i < values.Length; i++)
                if (values[i] == value)
                    return i;
            return -1;
        }
    }
}
