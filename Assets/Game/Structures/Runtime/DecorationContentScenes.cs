using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationContentSceneKind : byte
    {
        Smithy = 0,
        TavernBar = 1,
        Crypt = 2,
        Market = 3,
        Stable = 4,
        Prison = 5,
        CivicCorner = 6,
    }

    public struct DecorationContentSceneSlot
    {
        public uint SlotId;
        public DecorationContentKind Kind;
        public DecorationSocketKind RequestedSocket;
        public uint AnchorSlotId;
        public ushort Weight;
        public bool Required;

        public DecorationSceneSlot ToCoreSlot() => new DecorationSceneSlot
        {
            SlotId = SlotId,
            Family = DecorationContentCatalog.ProxyFamilyOf(Kind),
            RequestedSocket = RequestedSocket,
            AnchorSlotId = AnchorSlotId,
            Weight = Weight,
            Required = Required,
        };
    }

    public static class DecorationContentSceneCatalog
    {
        public static uint SceneId(DecorationContentSceneKind kind)
        {
            switch (kind)
            {
                case DecorationContentSceneKind.Smithy: return 0x534D5431u; // SMT1
                case DecorationContentSceneKind.TavernBar: return 0x54565231u; // TVR1
                case DecorationContentSceneKind.Crypt: return 0x43525931u; // CRY1
                case DecorationContentSceneKind.Market: return 0x4D4B5431u; // MKT1
                case DecorationContentSceneKind.Stable: return 0x53544231u; // STB1
                case DecorationContentSceneKind.Prison: return 0x50525331u; // PRS1
                default: return 0x43495631u; // CIV1
            }
        }

        public static DecorationContentSceneSlot[] CreateSlots(DecorationContentSceneKind kind)
        {
            switch (kind)
            {
                case DecorationContentSceneKind.Smithy:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.ForgeHearth, DecorationSocketKind.Wall, true, 5),
                        Slot(2, DecorationContentKind.Anvil, DecorationSocketKind.Floor, true, 5),
                        Slot(3, DecorationContentKind.Bellows, DecorationSocketKind.Floor, true, 4),
                        Slot(4, DecorationContentKind.QuenchTub, DecorationSocketKind.Floor, false, 3),
                        Slot(5, DecorationContentKind.Grindstone, DecorationSocketKind.Floor, false, 2),
                        Slot(6, DecorationContentKind.SmithToolBoard, DecorationSocketKind.Wall, false, 3),
                    };
                case DecorationContentSceneKind.TavernBar:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.BarCounter, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.KegRack, DecorationSocketKind.Wall, true, 5),
                        Slot(3, DecorationContentKind.MugRack, DecorationSocketKind.Wall, false, 4),
                        Slot(4, DecorationContentKind.ServingShelf, DecorationSocketKind.Wall, false, 3),
                        Slot(5, DecorationContentKind.FirewoodStack, DecorationSocketKind.Floor, false, 2),
                        Slot(6, DecorationContentKind.GameTable, DecorationSocketKind.Floor, false, 3),
                    };
                case DecorationContentSceneKind.Crypt:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.Sarcophagus, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.OssuaryShelf, DecorationSocketKind.Wall, true, 4),
                        Slot(3, DecorationContentKind.Coffin, DecorationSocketKind.Floor, false, 3),
                        Slot(4, DecorationContentKind.FuneralBier, DecorationSocketKind.Floor, false, 2),
                        Slot(5, DecorationContentKind.UrnStand, DecorationSocketKind.Floor, false, 3),
                        Slot(6, DecorationContentKind.GraveMarker, DecorationSocketKind.Wall, false, 2),
                    };
                case DecorationContentSceneKind.Market:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.MarketStall, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.ProduceStand, DecorationSocketKind.Floor, true, 4),
                        Slot(3, DecorationContentKind.MerchantSign, DecorationSocketKind.Wall, false, 4),
                        Slot(4, DecorationContentKind.BasketStack, DecorationSocketKind.Floor, false, 3),
                        Slot(5, DecorationContentKind.HangingScale, DecorationSocketKind.Ceiling, false, 2),
                        Slot(6, DecorationContentKind.FabricCanopy, DecorationSocketKind.Ceiling, false, 2),
                    };
                case DecorationContentSceneKind.Stable:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.Manger, DecorationSocketKind.Wall, true, 5),
                        Slot(2, DecorationContentKind.WaterTrough, DecorationSocketKind.Floor, true, 5),
                        Slot(3, DecorationContentKind.HayBale, DecorationSocketKind.Floor, false, 4),
                        Slot(4, DecorationContentKind.SaddleRack, DecorationSocketKind.Wall, false, 3),
                        Slot(5, DecorationContentKind.TackHooks, DecorationSocketKind.Wall, false, 3),
                        Slot(6, DecorationContentKind.HitchingPost, DecorationSocketKind.Floor, false, 2),
                    };
                case DecorationContentSceneKind.Prison:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.IronCage, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.KeyBoard, DecorationSocketKind.Wall, true, 4),
                        Slot(3, DecorationContentKind.Stocks, DecorationSocketKind.Floor, false, 3),
                        Slot(4, DecorationContentKind.Shackles, DecorationSocketKind.Wall, false, 4),
                        Slot(5, DecorationContentKind.PrisonBucket, DecorationSocketKind.Floor, false, 2),
                        Slot(6, DecorationContentKind.RestraintBench, DecorationSocketKind.Floor, false, 2),
                    };
                default:
                    return new[]
                    {
                        Slot(1, DecorationContentKind.Fountain, DecorationSocketKind.Floor, true, 5),
                        Slot(2, DecorationContentKind.NoticeBoard, DecorationSocketKind.Wall, true, 4),
                        Slot(3, DecorationContentKind.Well, DecorationSocketKind.Floor, false, 2),
                        Slot(4, DecorationContentKind.LampPost, DecorationSocketKind.Floor, false, 4),
                        Slot(5, DecorationContentKind.PublicTrough, DecorationSocketKind.Floor, false, 2),
                        Slot(6, DecorationContentKind.Handcart, DecorationSocketKind.Floor, false, 3),
                    };
            }
        }

        public static int OptionalBudget(DecorationContentSceneKind kind, in DecorationContext context)
        {
            if (context.Condition == DecorationConditionTier.Ruined)
                return 0;
            if (context.Condition == DecorationConditionTier.Abandoned)
                return 1;

            int budget = 2 + (int)context.Wealth / 2;
            if (kind == DecorationContentSceneKind.TavernBar || kind == DecorationContentSceneKind.Market)
                budget++;
            return budget;
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
    }

    /// <summary>
    /// Adapts content-archetype slots to the existing scene scheduler and placement resolver. This
    /// intentionally does not create a second collision or deterministic scheduling system.
    /// </summary>
    public static class DecorationContentSceneResolver
    {
        public static bool TryResolve(
            DecorationContentSceneKind kind,
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed || space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind)
                return false;

            uint sceneId = DecorationContentSceneCatalog.SceneId(kind);
            DecorationContentSceneSlot[] contentSlots = DecorationContentSceneCatalog.CreateSlots(kind);
            var coreSlots = new DecorationSceneSlot[contentSlots.Length];
            for (int i = 0; i < contentSlots.Length; i++)
            {
                if (!DecorationContentCatalog.IsDefined(contentSlots[i].Kind))
                    return false;
                coreSlots[i] = contentSlots[i].ToCoreSlot();
            }

            int optionalBudget = DecorationContentSceneCatalog.OptionalBudget(kind, in context);
            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context, sceneId, coreSlots, optionalBudget, out DecorationSceneSlot[] ordered))
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;

            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationContentSceneSlot contentSlot = FindSlot(contentSlots, ordered[i].SlotId);
                if (contentSlot.SlotId == 0)
                    return false;

                DecorationPropDescriptor descriptor = DecorationContentCatalog.Describe(
                    in context, sceneId, contentSlot.SlotId, contentSlot.Kind);
                if (!descriptor.IsWellFormed || !descriptor.Accepts(contentSlot.RequestedSocket))
                    return false;

                bool placed = DecorationPlacementResolver.TryPlace(
                    in space,
                    in context,
                    sceneId,
                    contentSlot.SlotId,
                    in descriptor,
                    sockets,
                    exclusions,
                    resolved,
                    count,
                    out DecorationPlacement placement);

                if (!placed)
                {
                    if (contentSlot.Required)
                        return false;
                    continue;
                }

                resolved[count++] = placement;
            }

            placements = new DecorationPlacement[count];
            for (int i = 0; i < count; i++)
                placements[i] = resolved[i];
            return true;
        }

        private static DecorationContentSceneSlot FindSlot(DecorationContentSceneSlot[] slots, uint slotId)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].SlotId == slotId)
                    return slots[i];
            return default;
        }
    }
}
