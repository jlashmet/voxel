using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion360SceneKind : byte
    {
        VillageShrine = 0,
        GrandTemple = 1,
        SacredCrypt = 2,
    }

    public struct DecorationExpansion360SceneSlot
    {
        public uint SlotId;
        public DecorationExpansion360Kind Kind;
        public DecorationSocketKind Socket;
        public ushort Weight;
        public bool Required;
    }

    public static class DecorationExpansion360SceneCatalog
    {
        public static uint SceneId(DecorationExpansion360SceneKind kind) => 0xE3600000u | ((uint)kind + 1u);

        public static DecorationExpansion360SceneSlot[] Slots(DecorationExpansion360SceneKind kind)
        {
            switch (kind)
            {
                case DecorationExpansion360SceneKind.VillageShrine:
                    return new[]
                    {
                        S(1, DecorationExpansion360Kind.SideShrine, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion360Kind.VotiveCandleStand, DecorationSocketKind.Floor, true, 6),
                        S(3, DecorationExpansion360Kind.OfferingChest, DecorationSocketKind.Floor, false, 5),
                        S(4, DecorationExpansion360Kind.PrayerBench, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion360Kind.PilgrimTokenBoard, DecorationSocketKind.Wall, false, 4),
                        S(6, DecorationExpansion360Kind.SacredCurtain, DecorationSocketKind.Wall, false, 3),
                    };
                case DecorationExpansion360SceneKind.GrandTemple:
                    return new[]
                    {
                        S(1, DecorationExpansion360Kind.SacredAltar, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion360Kind.ReliquaryShrine, DecorationSocketKind.Floor, true, 6),
                        S(3, DecorationExpansion360Kind.SacredLectern, DecorationSocketKind.Floor, true, 6),
                        S(4, DecorationExpansion360Kind.PrayerBench, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion360Kind.HolyWaterFont, DecorationSocketKind.Floor, false, 5),
                        S(6, DecorationExpansion360Kind.ShrineBell, DecorationSocketKind.Ceiling, false, 4),
                        S(7, DecorationExpansion360Kind.SacredBannerStand, DecorationSocketKind.Floor, false, 4),
                        S(8, DecorationExpansion360Kind.DivineCrystalFocus, DecorationSocketKind.Floor, false, 3),
                    };
                default:
                    return new[]
                    {
                        S(1, DecorationExpansion360Kind.ReliquaryShrine, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion360Kind.RelicPedestal, DecorationSocketKind.Floor, true, 6),
                        S(3, DecorationExpansion360Kind.IncenseStand, DecorationSocketKind.Floor, false, 5),
                        S(4, DecorationExpansion360Kind.RitualBasin, DecorationSocketKind.Floor, false, 4),
                        S(5, DecorationExpansion360Kind.BlessingBrazier, DecorationSocketKind.Floor, false, 4),
                        S(6, DecorationExpansion360Kind.ProcessionalStaffRack, DecorationSocketKind.Wall, false, 3),
                    };
            }
        }

        public static int OptionalBudget(DecorationExpansion360SceneKind kind, DecorationRegionTheme region, in DecorationContext context)
        {
            int budget = 2 + (int)context.Wealth / 2;
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            if (profile.IsWellFormed && profile.Prefers(DecorationRegionContentTags.Sacred)) budget += 2;
            if (context.Condition == DecorationConditionTier.Ruined) budget = 1;
            return budget;
        }

        private static DecorationExpansion360SceneSlot S(uint id, DecorationExpansion360Kind kind, DecorationSocketKind socket, bool required, ushort weight) =>
            new DecorationExpansion360SceneSlot { SlotId = id, Kind = kind, Socket = socket, Required = required, Weight = weight };
    }

    public static class DecorationExpansion360SceneResolver
    {
        public static bool TryResolve(
            DecorationExpansion360SceneKind kind,
            DecorationRegionTheme region,
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed || space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind) return false;
            DecorationExpansion360SceneSlot[] slots = DecorationExpansion360SceneCatalog.Slots(kind);
            uint sceneId = DecorationExpansion360SceneCatalog.SceneId(kind);
            var core = new DecorationSceneSlot[slots.Length];
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            for (int i = 0; i < slots.Length; i++)
            {
                DecorationExpansion360Recipe recipe = DecorationExpansion360Catalog.Recipe(slots[i].Kind);
                if (!recipe.IsWellFormed || (recipe.Sockets & slots[i].Socket) == 0) return false;
                ushort weight = slots[i].Weight;
                if (!slots[i].Required && profile.IsWellFormed && profile.Prefers(DecorationRegionContentTags.Sacred))
                    weight = (ushort)(weight + 4);
                core[i] = new DecorationSceneSlot
                {
                    SlotId = slots[i].SlotId, Family = recipe.ProxyFamily, RequestedSocket = slots[i].Socket,
                    Required = slots[i].Required, Weight = weight,
                };
            }
            if (!DecorationSceneScheduler.TrySelectAndOrder(in context, sceneId, core,
                    DecorationExpansion360SceneCatalog.OptionalBudget(kind, region, in context), out DecorationSceneSlot[] ordered)) return false;
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationExpansion360SceneSlot slot = Find(slots, ordered[i].SlotId);
                DecorationPropDescriptor descriptor = DecorationExpansion360Catalog.Describe(in context, sceneId, slot.SlotId, slot.Kind);
                if (!descriptor.IsWellFormed) return false;
                bool placed = DecorationPlacementResolver.TryPlace(in space, in context, sceneId, slot.SlotId,
                    in descriptor, sockets, exclusions, resolved, count, out DecorationPlacement placement);
                if (!placed) { if (slot.Required) return false; continue; }
                resolved[count++] = placement;
            }
            placements = new DecorationPlacement[count];
            for (int i = 0; i < count; i++) placements[i] = resolved[i];
            return true;
        }

        private static DecorationExpansion360SceneSlot Find(DecorationExpansion360SceneSlot[] slots, uint slotId)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].SlotId == slotId) return slots[i];
            return default;
        }
    }
}
