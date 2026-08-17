using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion200SceneKind : byte
    {
        AlchemyLab, RitualChamber, Observatory, Graveyard, Catacomb, Farmyard, GardenCourt, CivicStreet,
    }

    public struct DecorationExpansion200SceneSlot
    {
        public uint SlotId;
        public DecorationExpandedContentKind Kind;
        public DecorationSocketKind Socket;
        public ushort Weight;
        public bool Required;

        public DecorationSceneSlot ToCoreSlot(DecorationPropFamily family) => new DecorationSceneSlot
        {
            SlotId = SlotId, Family = family, RequestedSocket = Socket, Weight = Weight, Required = Required,
        };
    }

    public static class DecorationExpansion200SceneCatalog
    {
        public static uint SceneId(DecorationExpansion200SceneKind kind) => 0xE2000000u | ((uint)kind + 1u);

        public static DecorationExpansion200SceneSlot[] Slots(DecorationExpansion200SceneKind kind)
        {
            switch (kind)
            {
                case DecorationExpansion200SceneKind.AlchemyLab:
                    return new[]
                    {
                        S(1, DecorationExpandedContentKind.AlchemyTable, DecorationSocketKind.Floor, true, 6),
                        S(2, DecorationExpandedContentKind.DistillationFurnace, DecorationSocketKind.Wall, true, 5),
                        S(3, DecorationExpandedContentKind.IngredientCabinet, DecorationSocketKind.Wall, true, 4),
                        S(4, DecorationExpandedContentKind.AlembicStand, DecorationSocketKind.Floor, false, 4),
                        S(5, DecorationExpandedContentKind.PotionShelf, DecorationSocketKind.Wall, false, 4),
                        S(6, DecorationExpandedContentKind.ReagentChest, DecorationSocketKind.Floor, false, 3),
                        S(7, DecorationExpandedContentKind.SpecimenJarRack, DecorationSocketKind.Floor, false, 3),
                        S(8, DecorationExpandedContentKind.ManaCrystalCluster, DecorationSocketKind.Floor, false, 2),
                    };
                case DecorationExpansion200SceneKind.RitualChamber:
                    return new[]
                    {
                        S(1, DecorationExpandedContentKind.SummoningCircle, DecorationSocketKind.Floor, true, 6),
                        S(2, DecorationExpandedContentKind.RitualPedestal, DecorationSocketKind.Floor, true, 5),
                        S(3, DecorationExpandedContentKind.ArcaneBrazier, DecorationSocketKind.Floor, true, 4),
                        S(4, DecorationExpandedContentKind.CandleCluster, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpandedContentKind.SkullReliquary, DecorationSocketKind.Wall, false, 3),
                        S(6, DecorationExpandedContentKind.ChalkRuneBoard, DecorationSocketKind.Wall, false, 3),
                        S(7, DecorationExpandedContentKind.SpecimenCage, DecorationSocketKind.Floor, false, 2),
                    };
                case DecorationExpansion200SceneKind.Observatory:
                    return new[]
                    {
                        S(1, DecorationExpandedContentKind.TelescopeTripod, DecorationSocketKind.Floor, true, 6),
                        S(2, DecorationExpandedContentKind.Orrery, DecorationSocketKind.Floor, true, 5),
                        S(3, DecorationExpandedContentKind.StarChart, DecorationSocketKind.Wall, true, 5),
                        S(4, DecorationExpandedContentKind.AstrolabeStand, DecorationSocketKind.Floor, false, 4),
                        S(5, DecorationExpandedContentKind.ScryingBasin, DecorationSocketKind.Floor, false, 3),
                        S(6, DecorationExpandedContentKind.SpellbookLectern, DecorationSocketKind.Wall, false, 3),
                    };
                case DecorationExpansion200SceneKind.Graveyard:
                    return new[]
                    {
                        S(1, DecorationExpandedContentKind.TombSlab, DecorationSocketKind.Floor, true, 6),
                        S(2, DecorationExpandedContentKind.GraveStone, DecorationSocketKind.Floor, true, 5),
                        S(3, DecorationExpandedContentKind.GraveCross, DecorationSocketKind.Floor, false, 4),
                        S(4, DecorationExpandedContentKind.MourningBench, DecorationSocketKind.Floor, false, 3),
                        S(5, DecorationExpandedContentKind.FlowerOffering, DecorationSocketKind.Wall, false, 4),
                        S(6, DecorationExpandedContentKind.SoilMound, DecorationSocketKind.Floor, false, 3),
                        S(7, DecorationExpandedContentKind.BrokenHeadstone, DecorationSocketKind.Floor, false, 2),
                        S(8, DecorationExpandedContentKind.GraveDiggerTools, DecorationSocketKind.Wall, false, 2),
                    };
                case DecorationExpansion200SceneKind.Catacomb:
                    return new[]
                    {
                        S(1, DecorationExpandedContentKind.OssuaryNiche, DecorationSocketKind.Wall, true, 6),
                        S(2, DecorationExpandedContentKind.CatacombShelf, DecorationSocketKind.Floor, true, 5),
                        S(3, DecorationExpandedContentKind.ReliquaryCasket, DecorationSocketKind.Floor, true, 4),
                        S(4, DecorationExpandedContentKind.BonePile, DecorationSocketKind.Floor, false, 4),
                        S(5, DecorationExpandedContentKind.SkullStack, DecorationSocketKind.Floor, false, 3),
                        S(6, DecorationExpandedContentKind.IncenseBrazier, DecorationSocketKind.Floor, false, 3),
                        S(7, DecorationExpandedContentKind.BurialChest, DecorationSocketKind.Floor, false, 2),
                    };
                case DecorationExpansion200SceneKind.Farmyard:
                    return new[]
                    {
                        S(1, DecorationExpandedContentKind.GrainSilo, DecorationSocketKind.Floor, true, 6),
                        S(2, DecorationExpandedContentKind.WaterPump, DecorationSocketKind.Floor, true, 5),
                        S(3, DecorationExpandedContentKind.ChickenCoop, DecorationSocketKind.Floor, true, 4),
                        S(4, DecorationExpandedContentKind.Haystack, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpandedContentKind.Wheelbarrow, DecorationSocketKind.Floor, false, 4),
                        S(6, DecorationExpandedContentKind.Plow, DecorationSocketKind.Floor, false, 3),
                        S(7, DecorationExpandedContentKind.SeedChest, DecorationSocketKind.Floor, false, 3),
                        S(8, DecorationExpandedContentKind.RainBarrel, DecorationSocketKind.Floor, false, 2),
                    };
                case DecorationExpansion200SceneKind.GardenCourt:
                    return new[]
                    {
                        S(1, DecorationExpandedContentKind.Statue, DecorationSocketKind.Floor, true, 6),
                        S(2, DecorationExpandedContentKind.GardenBench, DecorationSocketKind.Floor, true, 5),
                        S(3, DecorationExpandedContentKind.FlowerPlanter, DecorationSocketKind.Floor, true, 5),
                        S(4, DecorationExpandedContentKind.Sundial, DecorationSocketKind.Floor, false, 4),
                        S(5, DecorationExpandedContentKind.Arbor, DecorationSocketKind.Floor, false, 3),
                        S(6, DecorationExpandedContentKind.Trellis, DecorationSocketKind.Wall, false, 3),
                        S(7, DecorationExpandedContentKind.HedgeSection, DecorationSocketKind.Wall, false, 2),
                    };
                default:
                    return new[]
                    {
                        S(1, DecorationExpandedContentKind.StreetBench, DecorationSocketKind.Floor, true, 6),
                        S(2, DecorationExpandedContentKind.Signpost, DecorationSocketKind.Floor, true, 5),
                        S(3, DecorationExpandedContentKind.Bollard, DecorationSocketKind.Floor, false, 4),
                        S(4, DecorationExpandedContentKind.Milestone, DecorationSocketKind.Floor, false, 3),
                        S(5, DecorationExpandedContentKind.TrashHeap, DecorationSocketKind.Floor, false, 2),
                        S(6, DecorationExpandedContentKind.FirewoodPile, DecorationSocketKind.Floor, false, 2),
                        S(7, DecorationExpandedContentKind.WateringCanRack, DecorationSocketKind.Wall, false, 2),
                    };
            }
        }

        public static int OptionalBudget(DecorationExpansion200SceneKind kind, in DecorationContext context)
        {
            if (context.Condition == DecorationConditionTier.Ruined) return 1;
            int budget = 2 + (int)context.Wealth / 2;
            if (kind == DecorationExpansion200SceneKind.AlchemyLab || kind == DecorationExpansion200SceneKind.RitualChamber ||
                kind == DecorationExpansion200SceneKind.Farmyard) budget++;
            return budget;
        }

        private static DecorationExpansion200SceneSlot S(uint id, DecorationExpandedContentKind kind,
            DecorationSocketKind socket, bool required, ushort weight) =>
            new DecorationExpansion200SceneSlot { SlotId = id, Kind = kind, Socket = socket, Required = required, Weight = weight };
    }

    public static class DecorationExpansion200SceneResolver
    {
        public static bool TryResolve(DecorationExpansion200SceneKind kind, in DecorationSpace space,
            in DecorationContext context, DecorationExclusion[] exclusions, out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed || space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind)
                return false;

            uint sceneId = DecorationExpansion200SceneCatalog.SceneId(kind);
            DecorationExpansion200SceneSlot[] slots = DecorationExpansion200SceneCatalog.Slots(kind);
            var core = new DecorationSceneSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                DecorationExpandedContentRecipe recipe = DecorationExpansion200Catalog.Recipe(slots[i].Kind);
                if (!recipe.IsWellFormed || (recipe.AcceptedSockets & slots[i].Socket) == 0) return false;
                core[i] = slots[i].ToCoreSlot(recipe.ProxyFamily);
            }
            if (!DecorationSceneScheduler.TrySelectAndOrder(in context, sceneId, core,
                    DecorationExpansion200SceneCatalog.OptionalBudget(kind, in context), out DecorationSceneSlot[] ordered))
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationExpansion200SceneSlot slot = Find(slots, ordered[i].SlotId);
                DecorationPropDescriptor descriptor = DecorationExpansion200Catalog.Describe(in context, sceneId, slot.SlotId, slot.Kind);
                if (!descriptor.IsWellFormed) return false;
                bool placed = DecorationPlacementResolver.TryPlace(in space, in context, sceneId, slot.SlotId,
                    in descriptor, sockets, exclusions, resolved, count, out DecorationPlacement placement);
                if (!placed)
                {
                    if (slot.Required) return false;
                    continue;
                }
                resolved[count++] = placement;
            }
            placements = new DecorationPlacement[count];
            for (int i = 0; i < count; i++) placements[i] = resolved[i];
            return true;
        }

        private static DecorationExpansion200SceneSlot Find(DecorationExpansion200SceneSlot[] slots, uint id)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].SlotId == id) return slots[i];
            return default;
        }
    }
}
