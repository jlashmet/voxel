using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion300SceneKind : byte
    {
        MonsterDen = 0,
        SpiderNest = 1,
        AdventurerGuildHall = 2,
        CaravanStaging = 3,
    }

    public struct DecorationExpansion300SceneSlot
    {
        public uint SlotId;
        public DecorationExpansion300Kind Kind;
        public DecorationSocketKind Socket;
        public ushort Weight;
        public bool Required;

        public DecorationSceneSlot ToCore(DecorationPropFamily family) => new DecorationSceneSlot
        {
            SlotId = SlotId,
            Family = family,
            RequestedSocket = Socket,
            Weight = Weight,
            Required = Required,
        };
    }

    public static class DecorationExpansion300SceneCatalog
    {
        private const DecorationSocketKind Floor = DecorationSocketKind.Floor;
        private const DecorationSocketKind Wall = DecorationSocketKind.Wall;

        public static uint SceneId(DecorationExpansion300SceneKind kind) =>
            0xE3000000u | ((uint)kind + 1u);

        public static DecorationExpansion300SceneSlot[] Slots(DecorationExpansion300SceneKind kind)
        {
            switch (kind)
            {
                case DecorationExpansion300SceneKind.MonsterDen:
                    return new[]
                    {
                        S(1, DecorationExpansion300Kind.MonsterNest, Floor, true, 7),
                        S(2, DecorationExpansion300Kind.BoneTotem, Floor, true, 5),
                        S(3, DecorationExpansion300Kind.TrophySkullPile, Floor, false, 5),
                        S(4, DecorationExpansion300Kind.GnawedBonePile, Floor, false, 4),
                        S(5, DecorationExpansion300Kind.BeastBedding, Floor, false, 4),
                        S(6, DecorationExpansion300Kind.MonsterFoodCache, Floor, false, 3),
                        S(7, DecorationExpansion300Kind.HoardScrapPile, Floor, false, 3),
                        S(8, DecorationExpansion300Kind.ChainedPreyCage, Floor, false, 2),
                    };

                case DecorationExpansion300SceneKind.SpiderNest:
                    return new[]
                    {
                        S(1, DecorationExpansion300Kind.MonsterNest, Floor, true, 7),
                        S(2, DecorationExpansion300Kind.EggClutch, Floor, true, 6),
                        S(3, DecorationExpansion300Kind.GiantWebSheet, Wall, true, 6),
                        S(4, DecorationExpansion300Kind.CocoonBundle, Wall, false, 5),
                        S(5, DecorationExpansion300Kind.WebbedVictim, Wall, false, 4),
                        S(6, DecorationExpansion300Kind.GnawedBonePile, Floor, false, 3),
                        S(7, DecorationExpansion300Kind.MoltedShellPile, Floor, false, 2),
                    };

                case DecorationExpansion300SceneKind.AdventurerGuildHall:
                    return new[]
                    {
                        S(1, DecorationExpansion300Kind.QuestBoard, Wall, true, 7),
                        S(2, DecorationExpansion300Kind.GuildRegistryDesk, Floor, true, 6),
                        S(3, DecorationExpansion300Kind.AdventurerMapTable, Floor, true, 6),
                        S(4, DecorationExpansion300Kind.GuildStrongbox, Floor, false, 4),
                        S(5, DecorationExpansion300Kind.MemberLockerBank, Wall, false, 4),
                        S(6, DecorationExpansion300Kind.GuildTrophyWall, Wall, false, 4),
                        S(7, DecorationExpansion300Kind.MonsterContractBoard, Wall, false, 3),
                        S(8, DecorationExpansion300Kind.TrainingManualShelf, Wall, false, 3),
                    };

                default:
                    return new[]
                    {
                        S(1, DecorationExpansion300Kind.ExpeditionSupplyRack, Wall, true, 6),
                        S(2, DecorationExpansion300Kind.CaravanSupplyCrate, Floor, true, 6),
                        S(3, DecorationExpansion300Kind.PackSaddleStand, Floor, true, 5),
                        S(4, DecorationExpansion300Kind.BedrollRack, Wall, false, 4),
                        S(5, DecorationExpansion300Kind.RopeGearRack, Wall, false, 4),
                        S(6, DecorationExpansion300Kind.LanternGearRack, Wall, false, 4),
                        S(7, DecorationExpansion300Kind.TravelCharmDisplay, Wall, false, 3),
                        S(8, DecorationExpansion300Kind.WaystoneAttunementPedestal, Floor, false, 3),
                    };
            }
        }

        public static int OptionalBudget(DecorationExpansion300SceneKind kind, in DecorationContext context)
        {
            if (context.Condition == DecorationConditionTier.Ruined)
                return 1;
            int budget = 2 + (int)context.Wealth / 2;
            if (kind == DecorationExpansion300SceneKind.MonsterDen ||
                kind == DecorationExpansion300SceneKind.SpiderNest)
                budget++;
            return budget;
        }

        private static DecorationExpansion300SceneSlot S(
            uint slotId,
            DecorationExpansion300Kind kind,
            DecorationSocketKind socket,
            bool required,
            ushort weight) => new DecorationExpansion300SceneSlot
        {
            SlotId = slotId,
            Kind = kind,
            Socket = socket,
            Required = required,
            Weight = weight,
        };
    }

    public static class DecorationExpansion300SceneResolver
    {
        public static bool TryResolve(
            DecorationExpansion300SceneKind kind,
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed ||
                space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind)
                return false;

            uint sceneId = DecorationExpansion300SceneCatalog.SceneId(kind);
            DecorationExpansion300SceneSlot[] slots = DecorationExpansion300SceneCatalog.Slots(kind);
            var core = new DecorationSceneSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                DecorationExpansion300Recipe recipe = DecorationExpansion300Catalog.Recipe(slots[i].Kind);
                if (!recipe.IsWellFormed || (recipe.Sockets & slots[i].Socket) == 0)
                    return false;
                core[i] = slots[i].ToCore(recipe.ProxyFamily);
            }

            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context,
                    sceneId,
                    core,
                    DecorationExpansion300SceneCatalog.OptionalBudget(kind, in context),
                    out DecorationSceneSlot[] ordered))
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;

            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationExpansion300SceneSlot slot = Find(slots, ordered[i].SlotId);
                DecorationPropDescriptor descriptor = DecorationExpansion300Catalog.Describe(
                    in context, sceneId, slot.SlotId, slot.Kind);
                if (!descriptor.IsWellFormed || !descriptor.Accepts(slot.Socket))
                    return false;

                bool placed = DecorationPlacementResolver.TryPlace(
                    in space,
                    in context,
                    sceneId,
                    slot.SlotId,
                    in descriptor,
                    sockets,
                    exclusions,
                    resolved,
                    count,
                    out DecorationPlacement placement);

                if (!placed)
                {
                    if (slot.Required)
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

        private static DecorationExpansion300SceneSlot Find(
            DecorationExpansion300SceneSlot[] slots,
            uint slotId)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].SlotId == slotId)
                    return slots[i];
            return default;
        }
    }
}
