using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion320SceneKind : byte
    {
        EnchantedGrove = 0,
        FairyClearing = 1,
        DruidShrine = 2,
    }

    public struct DecorationExpansion320SceneSlot
    {
        public uint SlotId;
        public DecorationExpansion320Kind Kind;
        public DecorationSocketKind Socket;
        public ushort Weight;
        public bool Required;
    }

    public static class DecorationExpansion320SceneCatalog
    {
        public static uint SceneId(DecorationExpansion320SceneKind kind) => 0xE3200000u | ((uint)kind + 1u);

        public static DecorationExpansion320SceneSlot[] Slots(DecorationExpansion320SceneKind kind)
        {
            switch (kind)
            {
                case DecorationExpansion320SceneKind.EnchantedGrove:
                    return new[]
                    {
                        S(1, DecorationExpansion320Kind.EnchantedTreeShrine, true, 7),
                        S(2, DecorationExpansion320Kind.GlowingMushroomCluster, true, 6),
                        S(3, DecorationExpansion320Kind.ManaBlossom, false, 6),
                        S(4, DecorationExpansion320Kind.CrystalFlowerPatch, false, 5),
                        S(5, DecorationExpansion320Kind.EnchantedVineCluster, false, 5, DecorationSocketKind.Wall),
                        S(6, DecorationExpansion320Kind.WhisperingStone, false, 4),
                        S(7, DecorationExpansion320Kind.WispNest, false, 4),
                        S(8, DecorationExpansion320Kind.MagicalPondLilies, false, 3),
                    };
                case DecorationExpansion320SceneKind.FairyClearing:
                    return new[]
                    {
                        S(1, DecorationExpansion320Kind.FairyRing, true, 7),
                        S(2, DecorationExpansion320Kind.FairyHouseNook, true, 6, DecorationSocketKind.Wall),
                        S(3, DecorationExpansion320Kind.SpiritLanternPlant, true, 6),
                        S(4, DecorationExpansion320Kind.FloatingSeedCluster, false, 5),
                        S(5, DecorationExpansion320Kind.GiantMushroomSeat, false, 5),
                        S(6, DecorationExpansion320Kind.SunCrystalBloom, false, 4),
                        S(7, DecorationExpansion320Kind.ManaBlossom, false, 4),
                    };
                default:
                    return new[]
                    {
                        S(1, DecorationExpansion320Kind.DruidStoneAltar, true, 7),
                        S(2, DecorationExpansion320Kind.RuneStoneCircle, true, 6),
                        S(3, DecorationExpansion320Kind.Moonwell, true, 6),
                        S(4, DecorationExpansion320Kind.LivingRootArch, false, 5),
                        S(5, DecorationExpansion320Kind.EnchantedTreeShrine, false, 4),
                        S(6, DecorationExpansion320Kind.HerbalistWildPatch, false, 4),
                        S(7, DecorationExpansion320Kind.PetrifiedMagicTree, false, 3),
                    };
            }
        }

        public static int OptionalBudget(DecorationExpansion320SceneKind kind, DecorationRegionTheme region, in DecorationContext context)
        {
            int budget = 2 + (int)context.Wealth / 2;
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            if (profile.IsWellFormed)
            {
                if (profile.Prefers(DecorationRegionContentTags.Organic)) budget += 2;
                else if (profile.Prefers(DecorationRegionContentTags.Enchanted)) budget += 1;
            }
            if (context.Condition == DecorationConditionTier.Ruined) budget = 1;
            return budget;
        }

        private static DecorationExpansion320SceneSlot S(uint id, DecorationExpansion320Kind kind, bool required, ushort weight, DecorationSocketKind socket = DecorationSocketKind.Floor) =>
            new DecorationExpansion320SceneSlot { SlotId = id, Kind = kind, Socket = socket, Required = required, Weight = weight };
    }

    public static class DecorationExpansion320SceneResolver
    {
        public static bool TryResolve(DecorationExpansion320SceneKind kind, DecorationRegionTheme region, in DecorationSpace space,
            in DecorationContext context, DecorationExclusion[] exclusions, out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed || space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind) return false;
            DecorationExpansion320SceneSlot[] slots = DecorationExpansion320SceneCatalog.Slots(kind);
            uint sceneId = DecorationExpansion320SceneCatalog.SceneId(kind);
            var core = new DecorationSceneSlot[slots.Length];
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            for (int i = 0; i < slots.Length; i++)
            {
                DecorationExpansion320Recipe recipe = DecorationExpansion320Catalog.Recipe(slots[i].Kind);
                if (!recipe.IsWellFormed || (recipe.Sockets & slots[i].Socket) == 0) return false;
                core[i] = new DecorationSceneSlot { SlotId = slots[i].SlotId, Family = recipe.ProxyFamily, RequestedSocket = slots[i].Socket, Required = slots[i].Required, Weight = slots[i].Weight };
                if (!slots[i].Required && profile.IsWellFormed)
                {
                    if (profile.Prefers(DecorationRegionContentTags.Organic)) core[i].Weight = (ushort)(core[i].Weight + 5);
                    else if (profile.Prefers(DecorationRegionContentTags.Enchanted)) core[i].Weight = (ushort)(core[i].Weight + 2);
                }
            }
            if (!DecorationSceneScheduler.TrySelectAndOrder(in context, sceneId, core,
                    DecorationExpansion320SceneCatalog.OptionalBudget(kind, region, in context), out DecorationSceneSlot[] ordered)) return false;
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationExpansion320SceneSlot slot = Find(slots, ordered[i].SlotId);
                DecorationPropDescriptor descriptor = DecorationExpansion320Catalog.Describe(in context, sceneId, slot.SlotId, slot.Kind);
                if (!descriptor.IsWellFormed) return false;
                bool placed = DecorationPlacementResolver.TryPlace(in space, in context, sceneId, slot.SlotId, in descriptor,
                    sockets, exclusions, resolved, count, out DecorationPlacement placement);
                if (!placed) { if (slot.Required) return false; continue; }
                resolved[count++] = placement;
            }
            placements = new DecorationPlacement[count];
            for (int i = 0; i < count; i++) placements[i] = resolved[i];
            return true;
        }

        private static DecorationExpansion320SceneSlot Find(DecorationExpansion320SceneSlot[] slots, uint slotId)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].SlotId == slotId) return slots[i];
            return default;
        }
    }
}
