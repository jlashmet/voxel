using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationContentWorkshopSceneKind : byte
    {
        Carpentry = 0,
        Textile = 1,
        Leather = 2,
        Pottery = 3,
    }

    /// <summary>
    /// Coherent room compositions for the first two content expansion packs. They reuse the same
    /// scene scheduler, socket extraction, collision/exclusion resolver, and relational sub-spaces.
    /// </summary>
    public static class DecorationContentWorkshopScenes
    {
        public static bool TryResolve(
            DecorationContentWorkshopSceneKind kind,
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
            DecorationContentSceneSlot[] contentSlots = Slots(kind);
            var coreSlots = new DecorationSceneSlot[contentSlots.Length];
            for (int i = 0; i < contentSlots.Length; i++)
                coreSlots[i] = contentSlots[i].ToCoreSlot();

            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context, sceneId, coreSlots, OptionalBudget(in context), out DecorationSceneSlot[] ordered))
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;

            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationContentSceneSlot slot = Find(contentSlots, ordered[i].SlotId);
                if (slot.SlotId == 0)
                    return false;

                DecorationPropDescriptor descriptor = DecorationContentCatalog.Describe(
                    in context, sceneId, slot.SlotId, slot.Kind);
                if (!descriptor.IsWellFormed || !descriptor.Accepts(slot.RequestedSocket))
                    return false;

                bool placed = false;
                if (Relation(kind, slot.Kind, out uint anchorSlot, out bool inFront) &&
                    TryFind(resolved, count, anchorSlot, out DecorationPlacement anchor))
                {
                    if (inFront)
                    {
                        placed = DecorationContentRelationalPlacement.TryPlaceFloorNearAnchor(
                            in space, in context, sceneId, slot.SlotId, in descriptor, in anchor,
                            4, 62, 48, exclusions, resolved, count, out DecorationPlacement relationPlacement);
                        if (placed)
                            resolved[count++] = relationPlacement;
                    }
                    else
                    {
                        placed = DecorationContentRelationalPlacement.TryPlaceFloorAroundAnchor(
                            in space, in context, sceneId, slot.SlotId, in descriptor, in anchor,
                            68, exclusions, resolved, count, out DecorationPlacement relationPlacement);
                        if (placed)
                            resolved[count++] = relationPlacement;
                    }
                }
                else if (!Relation(kind, slot.Kind, out _, out _))
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

        public static uint SceneId(DecorationContentWorkshopSceneKind kind)
        {
            switch (kind)
            {
                case DecorationContentWorkshopSceneKind.Carpentry: return 0x43525031u; // CRP1
                case DecorationContentWorkshopSceneKind.Textile: return 0x54585431u; // TXT1
                case DecorationContentWorkshopSceneKind.Leather: return 0x4C544831u; // LTH1
                default: return 0x504F5431u; // POT1
            }
        }

        public static DecorationContentSceneSlot[] Slots(DecorationContentWorkshopSceneKind kind)
        {
            switch (kind)
            {
                case DecorationContentWorkshopSceneKind.Carpentry:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.CarpenterBench, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.PlankRack, DecorationSocketKind.Wall, true, 5),
                        Slot(3, DecorationContentKind.SawHorse, DecorationSocketKind.Floor, true, 4),
                        Slot(4, DecorationContentKind.Lathe, DecorationSocketKind.Floor, false, 3),
                        Slot(5, DecorationContentKind.ToolChest, DecorationSocketKind.Floor, false, 4),
                        Slot(6, DecorationContentKind.ChiselBoard, DecorationSocketKind.Wall, false, 3),
                        Slot(7, DecorationContentKind.LumberStack, DecorationSocketKind.Floor, false, 3),
                        Slot(8, DecorationContentKind.RepairTrestle, DecorationSocketKind.Floor, false, 2),
                        Slot(9, DecorationContentKind.ShavingPile, DecorationSocketKind.Floor, false, 2),
                    };
                case DecorationContentWorkshopSceneKind.Textile:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.Loom, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.BoltRack, DecorationSocketKind.Wall, true, 5),
                        Slot(3, DecorationContentKind.SpinningWheel, DecorationSocketKind.Floor, true, 4),
                        Slot(4, DecorationContentKind.YarnBasket, DecorationSocketKind.Floor, false, 4),
                        Slot(5, DecorationContentKind.SpindleRack, DecorationSocketKind.Wall, false, 3),
                        Slot(6, DecorationContentKind.CuttingTable, DecorationSocketKind.Floor, false, 3),
                        Slot(7, DecorationContentKind.DressForm, DecorationSocketKind.Floor, false, 2),
                        Slot(8, DecorationContentKind.FoldedClothStack, DecorationSocketKind.Floor, false, 3),
                        Slot(9, DecorationContentKind.DryingLine, DecorationSocketKind.Wall, false, 2),
                        Slot(10, DecorationContentKind.SewingStool, DecorationSocketKind.Floor, false, 3),
                    };
                case DecorationContentWorkshopSceneKind.Leather:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.TanningTub, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.LeatherStretchingFrame, DecorationSocketKind.Wall, true, 5),
                        Slot(3, DecorationContentKind.BootmakerBench, DecorationSocketKind.Floor, true, 4),
                        Slot(4, DecorationContentKind.HideRack, DecorationSocketKind.Wall, false, 4),
                        Slot(5, DecorationContentKind.LeatherToolBoard, DecorationSocketKind.Wall, false, 3),
                        Slot(6, DecorationContentKind.SewingStool, DecorationSocketKind.Floor, false, 2),
                        Slot(7, DecorationContentKind.DyeVat, DecorationSocketKind.Floor, false, 2),
                    };
                default:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.Kiln, DecorationSocketKind.Wall, true, 5),
                        Slot(2, DecorationContentKind.PotteryWheel, DecorationSocketKind.Floor, true, 5),
                        Slot(3, DecorationContentKind.DryingShelf, DecorationSocketKind.Wall, true, 4),
                        Slot(4, DecorationContentKind.ClayBin, DecorationSocketKind.Floor, false, 4),
                        Slot(5, DecorationContentKind.AmphoraRack, DecorationSocketKind.Wall, false, 3),
                        Slot(6, DecorationContentKind.GlazeJarRack, DecorationSocketKind.Wall, false, 3),
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

        private static bool Relation(
            DecorationContentWorkshopSceneKind scene,
            DecorationContentKind kind,
            out uint anchorSlot,
            out bool inFront)
        {
            anchorSlot = 1u;
            inFront = false;
            switch (scene)
            {
                case DecorationContentWorkshopSceneKind.Carpentry:
                    return kind == DecorationContentKind.SawHorse ||
                           kind == DecorationContentKind.Lathe ||
                           kind == DecorationContentKind.ToolChest ||
                           kind == DecorationContentKind.LumberStack ||
                           kind == DecorationContentKind.RepairTrestle ||
                           kind == DecorationContentKind.ShavingPile;
                case DecorationContentWorkshopSceneKind.Textile:
                    return kind == DecorationContentKind.SpinningWheel ||
                           kind == DecorationContentKind.YarnBasket ||
                           kind == DecorationContentKind.CuttingTable ||
                           kind == DecorationContentKind.DressForm ||
                           kind == DecorationContentKind.FoldedClothStack ||
                           kind == DecorationContentKind.SewingStool;
                case DecorationContentWorkshopSceneKind.Leather:
                    return kind == DecorationContentKind.BootmakerBench ||
                           kind == DecorationContentKind.SewingStool ||
                           kind == DecorationContentKind.DyeVat;
                case DecorationContentWorkshopSceneKind.Pottery:
                    inFront = true;
                    return kind == DecorationContentKind.PotteryWheel ||
                           kind == DecorationContentKind.ClayBin;
                default:
                    return false;
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
