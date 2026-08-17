using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationContentFoodSceneKind : byte
    {
        Kitchen = 0,
        Bakery = 1,
        Brewery = 2,
        Winery = 3,
        Pantry = 4,
    }

    public static class DecorationContentFoodScenes
    {
        private enum RelationMode : byte
        {
            None = 0,
            Around = 1,
            InFront = 2,
        }

        public static bool TryResolve(
            DecorationContentFoodSceneKind kind,
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed ||
                space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind)
                return false;

            uint sceneId = SceneId(kind);
            DecorationContentSceneSlot[] slots = Slots(kind);
            var coreSlots = new DecorationSceneSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                coreSlots[i] = slots[i].ToCoreSlot();

            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context, sceneId, coreSlots, OptionalBudget(in context), out DecorationSceneSlot[] ordered))
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;

            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationContentSceneSlot slot = Find(slots, ordered[i].SlotId);
                if (slot.SlotId == 0)
                    return false;

                DecorationPropDescriptor descriptor = DecorationContentCatalog.Describe(
                    in context, sceneId, slot.SlotId, slot.Kind);
                if (!descriptor.IsWellFormed || !descriptor.Accepts(slot.RequestedSocket))
                    return false;

                RelationMode relation = Relation(kind, slot.Kind, out uint anchorSlot);
                bool placed = false;
                if (relation != RelationMode.None &&
                    TryFind(resolved, count, anchorSlot, out DecorationPlacement anchor))
                {
                    if (relation == RelationMode.InFront)
                    {
                        placed = DecorationContentRelationalPlacement.TryPlaceFloorNearAnchor(
                            in space, in context, sceneId, slot.SlotId, in descriptor, in anchor,
                            4, 68, 50, exclusions, resolved, count, out DecorationPlacement relationPlacement);
                        if (placed)
                            resolved[count++] = relationPlacement;
                    }
                    else
                    {
                        placed = DecorationContentRelationalPlacement.TryPlaceFloorAroundAnchor(
                            in space, in context, sceneId, slot.SlotId, in descriptor, in anchor,
                            74, exclusions, resolved, count, out DecorationPlacement relationPlacement);
                        if (placed)
                            resolved[count++] = relationPlacement;
                    }
                }
                else if (relation == RelationMode.None)
                {
                    placed = DecorationPlacementResolver.TryPlace(
                        in space, in context, sceneId, slot.SlotId, in descriptor,
                        sockets, exclusions, resolved, count, out DecorationPlacement normalPlacement);
                    if (placed)
                        resolved[count++] = normalPlacement;
                }

                if (!placed && slot.Required)
                    return false;
            }

            placements = new DecorationPlacement[count];
            for (int i = 0; i < count; i++)
                placements[i] = resolved[i];
            return true;
        }

        public static uint SceneId(DecorationContentFoodSceneKind kind)
        {
            switch (kind)
            {
                case DecorationContentFoodSceneKind.Kitchen: return 0x4B495431u; // KIT1
                case DecorationContentFoodSceneKind.Bakery: return 0x42414B31u; // BAK1
                case DecorationContentFoodSceneKind.Brewery: return 0x42525731u; // BRW1
                case DecorationContentFoodSceneKind.Winery: return 0x57494E31u; // WIN1
                default: return 0x504E5431u; // PNT1
            }
        }

        public static DecorationContentSceneSlot[] Slots(DecorationContentFoodSceneKind kind)
        {
            switch (kind)
            {
                case DecorationContentFoodSceneKind.Kitchen:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.PrepTable, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.WashSink, DecorationSocketKind.Wall, true, 5),
                        Slot(3, DecorationContentKind.CauldronStand, DecorationSocketKind.Floor, true, 4),
                        Slot(4, DecorationContentKind.HangingPotRack, DecorationSocketKind.Ceiling, false, 4),
                        Slot(5, DecorationContentKind.PanRack, DecorationSocketKind.Wall, false, 3),
                        Slot(6, DecorationContentKind.RoastingSpit, DecorationSocketKind.Floor, false, 3),
                        Slot(7, DecorationContentKind.HerbDryingRack, DecorationSocketKind.Ceiling, false, 2),
                        Slot(8, DecorationContentKind.MeatHookRail, DecorationSocketKind.Wall, false, 2),
                        Slot(9, DecorationContentKind.WaterBarrel, DecorationSocketKind.Floor, false, 3),
                    };
                case DecorationContentFoodSceneKind.Bakery:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.BreadOven, DecorationSocketKind.Wall, true, 5),
                        Slot(2, DecorationContentKind.PrepTable, DecorationSocketKind.Floor, true, 5),
                        Slot(3, DecorationContentKind.BreadCoolingRack, DecorationSocketKind.Wall, true, 4),
                        Slot(4, DecorationContentKind.FlourBin, DecorationSocketKind.Floor, false, 4),
                        Slot(5, DecorationContentKind.GrainSackStack, DecorationSocketKind.Floor, false, 3),
                        Slot(6, DecorationContentKind.PieRack, DecorationSocketKind.Wall, false, 3),
                        Slot(7, DecorationContentKind.FoodPrepShelf, DecorationSocketKind.Wall, false, 2),
                        Slot(8, DecorationContentKind.KettleStand, DecorationSocketKind.Floor, false, 2),
                    };
                case DecorationContentFoodSceneKind.Brewery:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.BreweryVat, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.MashTun, DecorationSocketKind.Floor, true, 5),
                        Slot(3, DecorationContentKind.Fermenter, DecorationSocketKind.Floor, true, 4),
                        Slot(4, DecorationContentKind.CaskStand, DecorationSocketKind.Wall, true, 4),
                        Slot(5, DecorationContentKind.WaterBarrel, DecorationSocketKind.Floor, false, 3),
                        Slot(6, DecorationContentKind.KettleStand, DecorationSocketKind.Floor, false, 2),
                        Slot(7, DecorationContentKind.CellarCaskStack, DecorationSocketKind.Floor, false, 3),
                    };
                case DecorationContentFoodSceneKind.Winery:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.WinePress, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.Fermenter, DecorationSocketKind.Floor, true, 5),
                        Slot(3, DecorationContentKind.BottleRack, DecorationSocketKind.Wall, true, 4),
                        Slot(4, DecorationContentKind.CaskStand, DecorationSocketKind.Wall, true, 4),
                        Slot(5, DecorationContentKind.CellarCaskStack, DecorationSocketKind.Floor, false, 4),
                        Slot(6, DecorationContentKind.WaterBarrel, DecorationSocketKind.Floor, false, 2),
                    };
                default:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.PantryCabinet, DecorationSocketKind.Wall, true, 5),
                        Slot(2, DecorationContentKind.FlourBin, DecorationSocketKind.Floor, true, 4),
                        Slot(3, DecorationContentKind.CheeseShelf, DecorationSocketKind.Wall, true, 4),
                        Slot(4, DecorationContentKind.SpiceShelf, DecorationSocketKind.Wall, false, 4),
                        Slot(5, DecorationContentKind.GrainSackStack, DecorationSocketKind.Floor, false, 3),
                        Slot(6, DecorationContentKind.VegetableBasket, DecorationSocketKind.Floor, false, 3),
                        Slot(7, DecorationContentKind.FishCrate, DecorationSocketKind.Floor, false, 2),
                        Slot(8, DecorationContentKind.SausageRack, DecorationSocketKind.Ceiling, false, 2),
                    };
            }
        }

        private static int OptionalBudget(in DecorationContext context)
        {
            if (context.Condition == DecorationConditionTier.Ruined)
                return 0;
            if (context.Condition == DecorationConditionTier.Abandoned)
                return 1;
            return 3 + (int)context.Wealth / 2;
        }

        private static RelationMode Relation(
            DecorationContentFoodSceneKind scene,
            DecorationContentKind kind,
            out uint anchorSlot)
        {
            anchorSlot = 1u;
            switch (scene)
            {
                case DecorationContentFoodSceneKind.Kitchen:
                    return kind == DecorationContentKind.CauldronStand ||
                           kind == DecorationContentKind.RoastingSpit ||
                           kind == DecorationContentKind.WaterBarrel
                        ? RelationMode.Around : RelationMode.None;
                case DecorationContentFoodSceneKind.Bakery:
                    if (kind == DecorationContentKind.PrepTable)
                        return RelationMode.InFront;
                    anchorSlot = 2u;
                    return kind == DecorationContentKind.FlourBin ||
                           kind == DecorationContentKind.GrainSackStack ||
                           kind == DecorationContentKind.KettleStand
                        ? RelationMode.Around : RelationMode.None;
                case DecorationContentFoodSceneKind.Brewery:
                    return kind == DecorationContentKind.MashTun ||
                           kind == DecorationContentKind.Fermenter ||
                           kind == DecorationContentKind.WaterBarrel ||
                           kind == DecorationContentKind.KettleStand ||
                           kind == DecorationContentKind.CellarCaskStack
                        ? RelationMode.Around : RelationMode.None;
                case DecorationContentFoodSceneKind.Winery:
                    return kind == DecorationContentKind.Fermenter ||
                           kind == DecorationContentKind.CellarCaskStack ||
                           kind == DecorationContentKind.WaterBarrel
                        ? RelationMode.Around : RelationMode.None;
                default:
                    if (kind == DecorationContentKind.FlourBin)
                        return RelationMode.InFront;
                    anchorSlot = 2u;
                    return kind == DecorationContentKind.GrainSackStack ||
                           kind == DecorationContentKind.VegetableBasket ||
                           kind == DecorationContentKind.FishCrate
                        ? RelationMode.Around : RelationMode.None;
            }
        }

        private static DecorationContentSceneSlot Slot(
            uint id,
            DecorationContentKind kind,
            DecorationSocketKind socket,
            bool required,
            ushort weight) => new DecorationContentSceneSlot
        {
            SlotId = id,
            Kind = kind,
            RequestedSocket = socket,
            Weight = weight,
            Required = required,
        };

        private static DecorationContentSceneSlot Find(DecorationContentSceneSlot[] slots, uint slotId)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].SlotId == slotId)
                    return slots[i];
            return default;
        }

        private static bool TryFind(
            DecorationPlacement[] placements,
            int count,
            uint slotId,
            out DecorationPlacement placement)
        {
            int safeCount = placements == null ? 0 : System.Math.Min(count, placements.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (placements[i].SlotId == slotId)
                {
                    placement = placements[i];
                    return true;
                }
            }
            placement = default;
            return false;
        }
    }
}
