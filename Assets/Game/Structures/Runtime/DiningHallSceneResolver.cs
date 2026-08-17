using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Dining-hall composition layered over the relational dining scene. Core dining furniture keeps
    /// its DIN1 identity while hall-scale lighting and heraldry use the separate DHL1 identity.
    /// </summary>
    public static class DiningHallSceneResolver
    {
        public const uint SceneId = 0x44484C31u; // DHL1

        public static bool TryResolve(
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (space.Kind != DecorationSpaceKind.DiningRoom ||
                context.SpaceKind != DecorationSpaceKind.DiningRoom ||
                !DiningSceneResolver.TryResolve(in space, in context, exclusions, out DecorationPlacement[] dining))
                return false;

            DecorationSceneSlot[] extras =
            {
                Slot(1, DecorationPropFamily.Chandelier, DecorationSocketKind.Ceiling, true),
                Slot(2, DecorationPropFamily.Banner, DecorationSocketKind.Wall, false),
                Slot(3, DecorationPropFamily.Banner, DecorationSocketKind.Wall, false),
            };
            int budget = context.Condition == DecorationConditionTier.Ruined
                ? 0
                : math.min(2, 1 + (int)context.Wealth / 2);
            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context, SceneId, extras, budget, out DecorationSceneSlot[] ordered))
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[dining.Length + ordered.Length];
            for (int i = 0; i < dining.Length; i++) resolved[i] = dining[i];
            int count = dining.Length;

            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationSceneSlot slot = ordered[i];
                DecorationPropDescriptor descriptor = slot.Family == DecorationPropFamily.Chandelier
                    ? LightingPropPresets.Chandelier(in context, SceneId, slot.SlotId)
                    : TextileDisplayPresets.Banner(in context, SceneId, slot.SlotId);
                bool placed = DecorationPlacementResolver.TryPlace(
                    in space, in context, SceneId, slot.SlotId, in descriptor,
                    sockets, exclusions, resolved, count, out resolved[count]);
                if (!placed)
                {
                    if (slot.Required) return false;
                    continue;
                }
                count++;
            }

            placements = new DecorationPlacement[count];
            for (int i = 0; i < count; i++) placements[i] = resolved[i];
            return true;
        }

        private static DecorationSceneSlot Slot(
            uint id, DecorationPropFamily family, DecorationSocketKind socket, bool required) =>
            new DecorationSceneSlot
            {
                SlotId = id,
                Family = family,
                RequestedSocket = socket,
                Weight = 1,
                Required = required,
            };
    }
}
