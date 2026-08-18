using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion400SceneKind : byte
    {
        CursedLaboratory = 0,
        HauntedChamber = 1,
        CorruptedRuin = 2,
    }

    public struct DecorationExpansion400SceneSlot
    {
        public uint SlotId;
        public DecorationExpansion400Kind Kind;
        public DecorationSocketKind Socket;
        public ushort Weight;
        public bool Required;
    }

    public static class DecorationExpansion400SceneCatalog
    {
        public static uint SceneId(DecorationExpansion400SceneKind kind) => 0xE4000000u | ((uint)kind + 1u);

        public static DecorationExpansion400SceneSlot[] Slots(DecorationExpansion400SceneKind kind)
        {
            switch (kind)
            {
                case DecorationExpansion400SceneKind.CursedLaboratory:
                    return new[]
                    {
                        S(1, DecorationExpansion400Kind.AbandonedRitualCircle, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion400Kind.CrackedManaCrystal, DecorationSocketKind.Floor, true, 6),
                        S(3, DecorationExpansion400Kind.CollapsedSpellShelf, DecorationSocketKind.Wall, true, 6),
                        S(4, DecorationExpansion400Kind.ArcaneScorchPatch, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion400Kind.CursedVineCluster, DecorationSocketKind.Wall, false, 4),
                        S(6, DecorationExpansion400Kind.SealedCursedChest, DecorationSocketKind.Floor, false, 4),
                        S(7, DecorationExpansion400Kind.AncientMagicSeal, DecorationSocketKind.Floor, false, 3),
                    };
                case DecorationExpansion400SceneKind.HauntedChamber:
                    return new[]
                    {
                        S(1, DecorationExpansion400Kind.HauntedMirror, DecorationSocketKind.Wall, true, 7),
                        S(2, DecorationExpansion400Kind.SpectralCandleCluster, DecorationSocketKind.Floor, true, 6),
                        S(3, DecorationExpansion400Kind.PossessedFurniture, DecorationSocketKind.Floor, true, 6),
                        S(4, DecorationExpansion400Kind.FloatingDebrisCluster, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion400Kind.CursedChainBundle, DecorationSocketKind.Floor, false, 4),
                        S(6, DecorationExpansion400Kind.ShadowNest, DecorationSocketKind.Floor, false, 4),
                        S(7, DecorationExpansion400Kind.EctoplasmPool, DecorationSocketKind.Floor, false, 3),
                    };
                default:
                    return new[]
                    {
                        S(1, DecorationExpansion400Kind.BrokenPortalFrame, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion400Kind.BrokenRunePillar, DecorationSocketKind.Floor, true, 6),
                        S(3, DecorationExpansion400Kind.ShatteredMagicStatue, DecorationSocketKind.Floor, true, 6),
                        S(4, DecorationExpansion400Kind.CorruptionGrowth, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion400Kind.PetrifiedAdventurer, DecorationSocketKind.Floor, false, 4),
                        S(6, DecorationExpansion400Kind.PetrifiedMonster, DecorationSocketKind.Floor, false, 4),
                        S(7, DecorationExpansion400Kind.AncientMagicSeal, DecorationSocketKind.Floor, false, 3),
                    };
            }
        }

        public static int OptionalBudget(DecorationExpansion400SceneKind kind, in DecorationContext context)
        {
            int budget = 3;
            if (context.Condition == DecorationConditionTier.Ruined || context.Condition == DecorationConditionTier.Abandoned) budget += 2;
            if (context.Condition == DecorationConditionTier.Pristine) budget = 1;
            return budget;
        }

        private static DecorationExpansion400SceneSlot S(uint id, DecorationExpansion400Kind kind, DecorationSocketKind socket, bool required, ushort weight) =>
            new DecorationExpansion400SceneSlot { SlotId = id, Kind = kind, Socket = socket, Required = required, Weight = weight };
    }

    public static class DecorationExpansion400SceneResolver
    {
        public static bool TryResolve(DecorationExpansion400SceneKind kind, in DecorationSpace space,
            in DecorationContext context, DecorationExclusion[] exclusions, out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed || space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind) return false;
            DecorationExpansion400SceneSlot[] slots = DecorationExpansion400SceneCatalog.Slots(kind);
            uint sceneId = DecorationExpansion400SceneCatalog.SceneId(kind);
            var core = new DecorationSceneSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                DecorationExpansion400Recipe recipe = DecorationExpansion400Catalog.Recipe(slots[i].Kind);
                if (!recipe.IsWellFormed || (recipe.Sockets & slots[i].Socket) == 0) return false;
                core[i] = new DecorationSceneSlot { SlotId = slots[i].SlotId, Family = recipe.ProxyFamily, RequestedSocket = slots[i].Socket, Required = slots[i].Required, Weight = slots[i].Weight };
            }
            if (!DecorationSceneScheduler.TrySelectAndOrder(in context, sceneId, core,
                    DecorationExpansion400SceneCatalog.OptionalBudget(kind, in context), out DecorationSceneSlot[] ordered)) return false;
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationExpansion400SceneSlot slot = Find(slots, ordered[i].SlotId);
                DecorationPropDescriptor descriptor = DecorationExpansion400Catalog.Describe(in context, sceneId, slot.SlotId, slot.Kind);
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

        private static DecorationExpansion400SceneSlot Find(DecorationExpansion400SceneSlot[] slots, uint slotId)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].SlotId == slotId) return slots[i];
            return default;
        }
    }
}
