using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Applies style/wealth-driven optional density after the required bedroom baseline has resolved.
    /// Optional placement failure never invalidates the required scene.
    /// </summary>
    public static class BedroomSceneContextVariation
    {
        public const uint AccentTorchSlot = 6;
        public const int MaximumPlacementCount = BedroomSceneResolver.PlacementCount + 1;

        public static bool TryApply(
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            DecorationPlacement[] baseline,
            out DecorationPlacement[] placements)
        {
            placements = baseline ?? new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed || baseline == null ||
                baseline.Length < BedroomSceneResolver.PlacementCount)
                return false;

            int optionalBudget = DecorationContextProfiles.OptionalSceneBudget(in context);
            if (optionalBudget <= 0)
                return true;

            DecorationSceneSlot[] required = BedroomSceneDefinition.CreateSlots();
            var slots = new DecorationSceneSlot[required.Length + 1];
            for (int i = 0; i < required.Length; i++)
                slots[i] = required[i];

            slots[required.Length] = new DecorationSceneSlot
            {
                SlotId = AccentTorchSlot,
                Family = DecorationPropFamily.WallTorch,
                RequestedSocket = DecorationSocketKind.Wall,
                Weight = 4,
                Required = false,
            };

            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context,
                    BedroomSceneDefinition.SceneId,
                    slots,
                    optionalBudget,
                    out DecorationSceneSlot[] selected))
                return false;

            if (!ContainsSlot(selected, AccentTorchSlot))
                return true;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            DecorationPropDescriptor torch = DecorationPropPresets.WallTorch(in context);
            var expanded = new DecorationPlacement[baseline.Length + 1];
            for (int i = 0; i < baseline.Length; i++)
                expanded[i] = baseline[i];

            if (!DecorationPlacementResolver.TryPlace(
                    in space,
                    in context,
                    BedroomSceneDefinition.SceneId,
                    AccentTorchSlot,
                    in torch,
                    sockets,
                    exclusions,
                    expanded,
                    baseline.Length,
                    out expanded[baseline.Length]))
            {
                // Optional density may be suppressed by a constrained room without invalidating
                // the required five-prop composition.
                return true;
            }

            placements = expanded;
            return true;
        }

        private static bool ContainsSlot(DecorationSceneSlot[] slots, uint slotId)
        {
            if (slots == null)
                return false;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].SlotId == slotId)
                    return true;
            return false;
        }
    }
}
