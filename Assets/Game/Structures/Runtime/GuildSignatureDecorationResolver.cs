using System;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public readonly struct GuildSignatureResolvedRoom
    {
        public readonly int RoomIndex;
        public readonly DecorationPlacement[] Placements;
        public GuildSignatureResolvedRoom(int roomIndex, DecorationPlacement[] placements)
        { RoomIndex = roomIndex; Placements = placements ?? Array.Empty<DecorationPlacement>(); }
    }

    /// <summary>
    /// Adds a sparse guild-specific identity layer over the ordinary room scene. Signature content is
    /// additive and never replaces the base semantic room composition.
    /// </summary>
    public static class GuildSignatureDecorationResolver
    {
        public static GuildSignatureResolvedRoom[] Resolve(in GuildHousePrototype prototype, GuildHouseResolvedRoom[] baseRooms)
        {
            if (!prototype.IsWellFormed || baseRooms == null || baseRooms.Length != prototype.Rooms.Length)
                return Array.Empty<GuildSignatureResolvedRoom>();

            var tmp = new GuildSignatureResolvedRoom[prototype.Rooms.Length];
            int output = 0;
            for (int i = 0; i < prototype.Rooms.Length; i++)
            {
                GuildSignatureKind[] kinds = KindsFor(prototype.SpatialPlan.Kind, prototype.Rooms[i].SpatialRoom.Node.Room.Role);
                if (kinds.Length == 0) continue;
                DecorationPlacement[] placements = ResolveRoom(in prototype.Rooms[i], baseRooms[i].Placements, kinds,
                    prototype.SpatialPlan.Kind, i);
                if (placements.Length == 0) continue;
                tmp[output++] = new GuildSignatureResolvedRoom(i, placements);
            }

            var result = new GuildSignatureResolvedRoom[output];
            Array.Copy(tmp, result, output);
            return result;
        }

        private static DecorationPlacement[] ResolveRoom(in GuildHouseRoomComposition room,
            DecorationPlacement[] occupiedBase, GuildSignatureKind[] kinds, GuildHouseKind guild, int roomIndex)
        {
            uint sceneId = 0x47480000u | ((uint)guild << 8) | (uint)(roomIndex + 1); // GH..
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in room.Space);
            var occupied = new DecorationPlacement[(occupiedBase?.Length ?? 0) + kinds.Length];
            int occupiedCount = 0;
            if (occupiedBase != null)
            {
                Array.Copy(occupiedBase, occupied, occupiedBase.Length);
                occupiedCount = occupiedBase.Length;
            }
            var signatures = new DecorationPlacement[kinds.Length];
            int count = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                uint slot = (uint)(i + 1);
                DecorationPropDescriptor descriptor = GuildSignatureDecorationCatalog.Describe(
                    in room.Context, sceneId, slot, kinds[i]);
                if (!descriptor.IsWellFormed) continue;
                if (!DecorationPlacementResolver.TryPlace(in room.Space, in room.Context, sceneId, slot,
                        in descriptor, sockets, Array.Empty<DecorationExclusion>(), occupied, occupiedCount,
                        out DecorationPlacement p))
                    continue;
                signatures[count++] = p;
                occupied[occupiedCount++] = p;
            }
            var result = new DecorationPlacement[count];
            Array.Copy(signatures, result, count);
            return result;
        }

        private static GuildSignatureKind[] KindsFor(GuildHouseKind guild, GuildHouseRoomRole role)
        {
            switch (guild)
            {
                case GuildHouseKind.Adventurers:
                    if (role == GuildHouseRoomRole.ContractHall) return A(GuildSignatureKind.AdventurerPartyTable, GuildSignatureKind.MembershipRosterBoard);
                    if (role == GuildHouseRoomRole.TrophyHall) return A(GuildSignatureKind.TrophyMonsterMount, GuildSignatureKind.GuildDonationChest);
                    break;
                case GuildHouseKind.Wizards:
                    if (role == GuildHouseRoomRole.GuildmasterOffice) return A(GuildSignatureKind.GuildmasterDesk, GuildSignatureKind.WizardGuildSeal, GuildSignatureKind.SpellRankBoard);
                    if (role == GuildHouseRoomRole.Workshop) return A(GuildSignatureKind.FamiliarFeedingStation, GuildSignatureKind.InitiationPedestal);
                    break;
                case GuildHouseKind.Knights:
                    if (role == GuildHouseRoomRole.Shrine) return A(GuildSignatureKind.OathStone, GuildSignatureKind.KnightOathBanner);
                    if (role == GuildHouseRoomRole.TrainingRoom) return A(GuildSignatureKind.ArmorMaintenanceRack, GuildSignatureKind.TournamentShieldWall);
                    break;
                case GuildHouseKind.Assassins:
                    if (role == GuildHouseRoomRole.ContractHall) return A(GuildSignatureKind.CodedContractBoard, GuildSignatureKind.AssassinTargetSilhouette);
                    if (role == GuildHouseRoomRole.Workshop) return A(GuildSignatureKind.PoisonLockCabinet, GuildSignatureKind.ConcealedWeaponPanel);
                    break;
                case GuildHouseKind.Druids:
                    if (role == GuildHouseRoomRole.Shrine) return A(GuildSignatureKind.DruidSeedShrine, GuildSignatureKind.AnimalTotemPole);
                    if (role == GuildHouseRoomRole.CommonHall || role == GuildHouseRoomRole.Garden) return A(GuildSignatureKind.LivingRootSeat, GuildSignatureKind.HerbDryingTree);
                    break;
                case GuildHouseKind.Thieves:
                    if (role == GuildHouseRoomRole.Workshop) return A(GuildSignatureKind.LockPracticeBoard, GuildSignatureKind.StolenGoodsSortingTable);
                    if (role == GuildHouseRoomRole.HiddenRoom) return A(GuildSignatureKind.ConcealedFloorCache);
                    break;
                case GuildHouseKind.Clerics:
                    if (role == GuildHouseRoomRole.Infirmary) return A(GuildSignatureKind.HealerCot, GuildSignatureKind.MedicineScreen, GuildSignatureKind.BlessingTable);
                    break;
                case GuildHouseKind.Rangers:
                    if (role == GuildHouseRoomRole.Workshop) return A(GuildSignatureKind.RangerBowyerStation, GuildSignatureKind.FletchingBench, GuildSignatureKind.HuntingMapWall);
                    break;
                case GuildHouseKind.Bards:
                    if (role == GuildHouseRoomRole.PerformanceHall) return A(GuildSignatureKind.BardStageRiser, GuildSignatureKind.InstrumentCabinet, GuildSignatureKind.SongBoard, GuildSignatureKind.CostumeTrunk);
                    break;
                case GuildHouseKind.Alchemists:
                    if (role == GuildHouseRoomRole.Workshop) return A(GuildSignatureKind.AlchemistFumeHood, GuildSignatureKind.ReagentSortingWheel, GuildSignatureKind.UnstableExperimentCage);
                    break;
            }
            if (role == GuildHouseRoomRole.GuildmasterOffice)
                return A(GuildSignatureKind.GuildmasterChair, GuildSignatureKind.GuildmasterDesk);
            return Array.Empty<GuildSignatureKind>();
        }

        private static GuildSignatureKind[] A(params GuildSignatureKind[] values) => values;
    }
}
