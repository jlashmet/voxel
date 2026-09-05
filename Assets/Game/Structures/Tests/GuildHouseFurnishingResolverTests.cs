using System;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseFurnishingResolverTests
    {
        [Test]
        public void PaletteCanonicalizesInputAndRejectsRequiredOrUnknownIds()
        {
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 400, 127 },
                    out GuildHouseFurnishingPalette palette),
                Is.True);

            Assert.That(palette.IsSpecified, Is.True);
            Assert.That(palette.Kind, Is.EqualTo(GuildHouseKind.Wizards));
            Assert.That(palette.SelectedOptionalArchetypes, Is.EqualTo(new ushort[] { 127, 400 }));
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 361 },
                    out _),
                Is.False,
                "required fixture must not become user-selectable");
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 999 },
                    out _),
                Is.False);
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 127, 127 },
                    out _),
                Is.False);
        }

        [Test]
        public void SelectedPaletteUsesOnlyProductionRoomArchetypesAndValidSockets()
        {
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards,
                DecorationRegionTheme.Kentridge,
                0x12345678u,
                77u,
                new int3(0, 0, 0),
                128,
                128,
                requestedRooms: 6);
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 127, 400 },
                    out GuildHouseFurnishingPalette palette),
                Is.True);
            Assert.That(
                GuildHouseFurnishingResolver.TryResolvePrototype(
                    in prototype,
                    in palette,
                    out GuildHouseResolvedRoom[] rooms,
                    out GuildHouseUnplacedFurnishing[] unplaced),
                Is.True);
            Assert.That(rooms, Has.Length.EqualTo(prototype.Rooms.Length));

            for (int roomIndex = 0; roomIndex < rooms.Length; roomIndex++)
            {
                GuildHouseResolvedRoom resolved = rooms[roomIndex];
                GuildHouseRoomComposition room = resolved.Room;
                DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in room.Space);

                for (int placementIndex = 0; placementIndex < resolved.Placements.Length; placementIndex++)
                {
                    DecorationPlacement placement = resolved.Placements[placementIndex];
                    ushort stableId = DecorationCanonicalPlacementCatalog.StableIdOfVariant(placement.Variant);
                    Assert.That(stableId, Is.Not.Zero);
                    Assert.That(
                        Contains(room.RequiredArchetypes, stableId) || palette.Contains(stableId),
                        Is.True,
                        $"room={roomIndex} id={stableId}");
                    Assert.That(DecorationCanonicalCatalog.TryGet(stableId, out DecorationCanonicalDescriptor canonical), Is.True);
                    Assert.That(room.Space.Bounds.Contains(in placement.Bounds), Is.True, $"room={roomIndex} id={stableId}");

                    DecorationSocket socket = FindSocket(sockets, placement.SocketId);
                    Assert.That(socket.IsWellFormed, Is.True, $"room={roomIndex} id={stableId}");
                    Assert.That(canonical.AcceptedSockets & socket.Kind, Is.Not.EqualTo(DecorationSocketKind.None));

                    for (int previous = 0; previous < placementIndex; previous++)
                    {
                        DecorationPlacement other = resolved.Placements[previous];
                        Assert.That(
                            placement.Bounds.Expanded(canonical.Clearance).Overlaps(in other.Bounds),
                            Is.False,
                            $"room={roomIndex} ids={stableId}/{DecorationCanonicalPlacementCatalog.StableIdOfVariant(other.Variant)}");
                    }
                }

                for (int requiredIndex = 0; requiredIndex < room.RequiredArchetypes.Length; requiredIndex++)
                    Assert.That(ContainsPlacement(resolved.Placements, room.RequiredArchetypes[requiredIndex]), Is.True);
            }

            for (int i = 0; i < unplaced.Length; i++)
            {
                Assert.That(unplaced[i].IsWellFormed, Is.True);
                Assert.That(palette.Contains(unplaced[i].StableId), Is.True);
            }
        }

        [Test]
        public void SameHousePaletteAndSeedProduceIdenticalSemanticSignature()
        {
            GuildHousePrototype first = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards,
                DecorationRegionTheme.Kentridge,
                424242u,
                91u,
                new int3(12, 0, -8),
                128,
                128,
                requestedRooms: 6);
            GuildHousePrototype second = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards,
                DecorationRegionTheme.Kentridge,
                424242u,
                91u,
                new int3(12, 0, -8),
                128,
                128,
                requestedRooms: 6);
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 400, 127, 233 },
                    out GuildHouseFurnishingPalette firstPalette),
                Is.True);
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 233, 127, 400 },
                    out GuildHouseFurnishingPalette secondPalette),
                Is.True);

            Assert.That(GuildHouseFurnishingResolver.TryResolvePrototype(
                in first, in firstPalette, out GuildHouseResolvedRoom[] firstRooms, out GuildHouseUnplacedFurnishing[] firstUnplaced), Is.True);
            Assert.That(GuildHouseFurnishingResolver.TryResolvePrototype(
                in second, in secondPalette, out GuildHouseResolvedRoom[] secondRooms, out GuildHouseUnplacedFurnishing[] secondUnplaced), Is.True);

            AssertSamePrototype(first, second);
            AssertSameRooms(firstRooms, secondRooms);
            Assert.That(secondUnplaced.Length, Is.EqualTo(firstUnplaced.Length));
            for (int i = 0; i < firstUnplaced.Length; i++)
            {
                Assert.That(secondUnplaced[i].StableId, Is.EqualTo(firstUnplaced[i].StableId));
                Assert.That(secondUnplaced[i].Reason, Is.EqualTo(firstUnplaced[i].Reason));
            }
        }

        [Test]
        public void DifferentSeedCanChangeProductionRoomLayout()
        {
            GuildHousePrototype baseline = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards,
                DecorationRegionTheme.Kentridge,
                1u,
                123u,
                int3.zero,
                72,
                72,
                requestedRooms: 6);

            bool changed = false;
            for (uint seed = 2; seed <= 64 && !changed; seed++)
            {
                GuildHousePrototype candidate = GuildHousePrototypeComposition.Build(
                    GuildHouseKind.Wizards,
                    DecorationRegionTheme.Kentridge,
                    seed,
                    123u,
                    int3.zero,
                    72,
                    72,
                    requestedRooms: 6);
                changed = !SameSpatialSignature(baseline, candidate);
            }

            Assert.That(changed, Is.True, "production planner should expose seed-driven room/layout variation");
        }

        [Test]
        public void SelectedPropReportsRoomUnavailableWhenItsOptionalRoomWasNotGenerated()
        {
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Bards,
                    new ushort[] { 260 },
                    out GuildHouseFurnishingPalette palette),
                Is.True);

            bool proved = false;
            for (uint seed = 1; seed <= 64 && !proved; seed++)
            {
                GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                    GuildHouseKind.Bards,
                    DecorationRegionTheme.Kentridge,
                    seed,
                    201u,
                    int3.zero,
                    128,
                    128,
                    requestedRooms: 4);
                Assert.That(GuildHouseFurnishingResolver.TryResolvePrototype(
                    in prototype,
                    in palette,
                    out _,
                    out GuildHouseUnplacedFurnishing[] unplaced), Is.True);
                if (unplaced.Length == 1 && unplaced[0].Reason == GuildHouseUnplacedReason.RoomUnavailable)
                    proved = true;
            }

            Assert.That(proved, Is.True);
        }

        [Test]
        public void SocketScarcityReportsNoValidPlacementInsteadOfFallbackCoordinates()
        {
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 127 },
                    out GuildHouseFurnishingPalette palette),
                Is.True);

            var space = new DecorationSpace
            {
                SpaceId = 1u,
                Kind = DecorationSpaceKind.Study,
                Bounds = new DecorationBounds
                {
                    Min = int3.zero,
                    MaxExclusive = new int3(2, 2, 2),
                },
            };
            var context = new DecorationContext
            {
                WorldSeed = 11u,
                StructureId = 12u,
                SpaceId = 1u,
                StructureKind = DecorationStructureKind.House,
                SpaceKind = DecorationSpaceKind.Study,
                Wealth = DecorationWealthTier.Comfortable,
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior,
            };
            var spatialRoom = new GuildHouseSpatialRoom(
                default,
                0,
                0,
                int3.zero,
                new int3(2, 2, 2));
            var room = new GuildHouseRoomComposition(
                spatialRoom,
                space,
                context,
                Array.Empty<ushort>(),
                new ushort[] { 127 });
            var spatialPlan = new GuildHouseSpatialPlan(
                GuildHouseKind.Wizards,
                GuildHouseShellStyle.Tower,
                int3.zero,
                64,
                64,
                30,
                1,
                new[] { spatialRoom });
            var prototype = new GuildHousePrototype(
                spatialPlan,
                DecorationRegionTheme.Kentridge,
                new[] { room });

            Assert.That(GuildHouseFurnishingResolver.TryResolvePrototype(
                in prototype,
                in palette,
                out GuildHouseResolvedRoom[] rooms,
                out GuildHouseUnplacedFurnishing[] unplaced), Is.True);
            Assert.That(rooms[0].Placements, Is.Empty);
            Assert.That(unplaced, Has.Length.EqualTo(1));
            Assert.That(unplaced[0].StableId, Is.EqualTo(127));
            Assert.That(unplaced[0].Reason, Is.EqualTo(GuildHouseUnplacedReason.NoValidPlacement));
        }

        private static DecorationSocket FindSocket(DecorationSocket[] sockets, uint socketId)
        {
            for (int i = 0; i < sockets.Length; i++)
                if (sockets[i].SocketId == socketId)
                    return sockets[i];
            return default;
        }

        private static bool Contains(ushort[] values, ushort value)
        {
            for (int i = 0; i < values.Length; i++)
                if (values[i] == value)
                    return true;
            return false;
        }

        private static bool ContainsPlacement(DecorationPlacement[] placements, ushort stableId)
        {
            for (int i = 0; i < placements.Length; i++)
                if (DecorationCanonicalPlacementCatalog.StableIdOfVariant(placements[i].Variant) == stableId)
                    return true;
            return false;
        }

        private static void AssertSamePrototype(in GuildHousePrototype first, in GuildHousePrototype second)
        {
            Assert.That(second.SpatialPlan.ShellStyle, Is.EqualTo(first.SpatialPlan.ShellStyle));
            Assert.That(second.SpatialPlan.Rooms.Length, Is.EqualTo(first.SpatialPlan.Rooms.Length));
            for (int i = 0; i < first.SpatialPlan.Rooms.Length; i++)
            {
                GuildHouseSpatialRoom a = first.SpatialPlan.Rooms[i];
                GuildHouseSpatialRoom b = second.SpatialPlan.Rooms[i];
                Assert.That(b.Node.Room.Role, Is.EqualTo(a.Node.Room.Role));
                Assert.That(b.FloorIndex, Is.EqualTo(a.FloorIndex));
                Assert.That(b.CellIndex, Is.EqualTo(a.CellIndex));
                Assert.That(b.Min, Is.EqualTo(a.Min));
                Assert.That(b.Size, Is.EqualTo(a.Size));
            }
        }

        private static void AssertSameRooms(GuildHouseResolvedRoom[] first, GuildHouseResolvedRoom[] second)
        {
            Assert.That(second.Length, Is.EqualTo(first.Length));
            for (int roomIndex = 0; roomIndex < first.Length; roomIndex++)
            {
                DecorationPlacement[] a = first[roomIndex].Placements;
                DecorationPlacement[] b = second[roomIndex].Placements;
                Assert.That(b.Length, Is.EqualTo(a.Length));
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.That(b[i].Variant, Is.EqualTo(a[i].Variant));
                    Assert.That(b[i].SlotId, Is.EqualTo(a[i].SlotId));
                    Assert.That(b[i].SocketId, Is.EqualTo(a[i].SocketId));
                    Assert.That(b[i].Bounds.Min, Is.EqualTo(a[i].Bounds.Min));
                    Assert.That(b[i].Bounds.MaxExclusive, Is.EqualTo(a[i].Bounds.MaxExclusive));
                    Assert.That(b[i].Facing, Is.EqualTo(a[i].Facing));
                }
            }
        }

        private static bool SameSpatialSignature(in GuildHousePrototype first, in GuildHousePrototype second)
        {
            if (first.SpatialPlan.Rooms.Length != second.SpatialPlan.Rooms.Length)
                return false;
            for (int i = 0; i < first.SpatialPlan.Rooms.Length; i++)
            {
                GuildHouseSpatialRoom a = first.SpatialPlan.Rooms[i];
                GuildHouseSpatialRoom b = second.SpatialPlan.Rooms[i];
                if (a.Node.Room.Role != b.Node.Room.Role ||
                    a.FloorIndex != b.FloorIndex ||
                    a.CellIndex != b.CellIndex ||
                    !math.all(a.Min == b.Min) ||
                    !math.all(a.Size == b.Size))
                    return false;
            }
            return true;
        }
    }
}
