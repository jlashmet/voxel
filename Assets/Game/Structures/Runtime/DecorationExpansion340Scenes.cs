using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion340SceneKind : byte
    {
        TrapCorridor = 0,
        PuzzleChamber = 1,
        TreasureVault = 2,
    }

    public struct DecorationExpansion340SceneSlot
    {
        public uint SlotId;
        public DecorationExpansion340Kind Kind;
        public DecorationSocketKind Socket;
        public ushort Weight;
        public bool Required;
    }

    public static class DecorationExpansion340SceneCatalog
    {
        public static uint SceneId(DecorationExpansion340SceneKind kind) => 0xE3400000u | ((uint)kind + 1u);

        public static DecorationExpansion340SceneSlot[] Slots(DecorationExpansion340SceneKind kind)
        {
            switch (kind)
            {
                case DecorationExpansion340SceneKind.TrapCorridor:
                    return new[]
                    {
                        S(1, DecorationExpansion340Kind.StonePressurePlate, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion340Kind.DartSlit, DecorationSocketKind.Wall, true, 6),
                        S(3, DecorationExpansion340Kind.SpikeFloorPanel, DecorationSocketKind.Floor, false, 6),
                        S(4, DecorationExpansion340Kind.FlameJetNozzle, DecorationSocketKind.Wall, false, 5),
                        S(5, DecorationExpansion340Kind.PoisonVent, DecorationSocketKind.Wall, false, 5),
                        S(6, DecorationExpansion340Kind.SwingingBladePivot, DecorationSocketKind.Ceiling, false, 4),
                        S(7, DecorationExpansion340Kind.PendulumAxeMount, DecorationSocketKind.Ceiling, false, 4),
                        S(8, DecorationExpansion340Kind.FallingBlockTrigger, DecorationSocketKind.Ceiling, false, 3),
                    };

                case DecorationExpansion340SceneKind.PuzzleChamber:
                    return new[]
                    {
                        S(1, DecorationExpansion340Kind.PuzzleLeverPedestal, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion340Kind.RuneDial, DecorationSocketKind.Floor, true, 6),
                        S(3, DecorationExpansion340Kind.GemSocketPuzzle, DecorationSocketKind.Floor, true, 6),
                        S(4, DecorationExpansion340Kind.RotatingStatuePedestal, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion340Kind.FloorTilePuzzle, DecorationSocketKind.Floor, false, 5),
                        S(6, DecorationExpansion340Kind.MirrorPuzzleStand, DecorationSocketKind.Floor, false, 4),
                        S(7, DecorationExpansion340Kind.MagicSealDoor, DecorationSocketKind.Wall, false, 4),
                        S(8, DecorationExpansion340Kind.WardEmitterPillar, DecorationSocketKind.Floor, false, 3),
                    };

                default:
                    return new[]
                    {
                        S(1, DecorationExpansion340Kind.TreasureTrapChest, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion340Kind.MagicSealDoor, DecorationSocketKind.Wall, true, 6),
                        S(3, DecorationExpansion340Kind.WardEmitterPillar, DecorationSocketKind.Floor, true, 6),
                        S(4, DecorationExpansion340Kind.RunePressurePlate, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion340Kind.GemSocketPuzzle, DecorationSocketKind.Floor, false, 5),
                        S(6, DecorationExpansion340Kind.ChainWinch, DecorationSocketKind.Floor, false, 3),
                        S(7, DecorationExpansion340Kind.PortcullisWinch, DecorationSocketKind.Floor, false, 3),
                    };
            }
        }

        public static int OptionalBudget(DecorationExpansion340SceneKind kind, in DecorationContext context)
        {
            if (context.Condition == DecorationConditionTier.Ruined) return 1;
            int budget = kind == DecorationExpansion340SceneKind.TrapCorridor ? 3 : 2;
            if ((byte)context.Wealth >= (byte)DecorationWealthTier.Wealthy) budget++;
            return budget;
        }

        private static DecorationExpansion340SceneSlot S(
            uint slotId, DecorationExpansion340Kind kind, DecorationSocketKind socket, bool required, ushort weight) =>
            new DecorationExpansion340SceneSlot
            {
                SlotId = slotId,
                Kind = kind,
                Socket = socket,
                Required = required,
                Weight = weight,
            };
    }

    public static class DecorationExpansion340SceneResolver
    {
        public static bool TryResolve(
            DecorationExpansion340SceneKind kind,
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed ||
                space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind)
                return false;

            DecorationExpansion340SceneSlot[] slots = DecorationExpansion340SceneCatalog.Slots(kind);
            uint sceneId = DecorationExpansion340SceneCatalog.SceneId(kind);
            var core = new DecorationSceneSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                DecorationExpansion340Recipe recipe = DecorationExpansion340Catalog.Recipe(slots[i].Kind);
                if (!recipe.IsWellFormed || (recipe.Sockets & slots[i].Socket) == 0) return false;
                core[i] = new DecorationSceneSlot
                {
                    SlotId = slots[i].SlotId,
                    Family = recipe.ProxyFamily,
                    RequestedSocket = slots[i].Socket,
                    Required = slots[i].Required,
                    Weight = slots[i].Weight,
                };
            }

            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context, sceneId, core,
                    DecorationExpansion340SceneCatalog.OptionalBudget(kind, in context),
                    out DecorationSceneSlot[] ordered))
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationExpansion340SceneSlot slot = Find(slots, ordered[i].SlotId);
                DecorationPropDescriptor descriptor = DecorationExpansion340Catalog.Describe(
                    in context, sceneId, slot.SlotId, slot.Kind);
                if (!descriptor.IsWellFormed || !descriptor.Accepts(slot.Socket)) return false;

                bool placed = DecorationPlacementResolver.TryPlace(
                    in space, in context, sceneId, slot.SlotId, in descriptor,
                    sockets, exclusions, resolved, count, out DecorationPlacement placement);
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

        private static DecorationExpansion340SceneSlot Find(DecorationExpansion340SceneSlot[] slots, uint slotId)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].SlotId == slotId) return slots[i];
            return default;
        }
    }
}
