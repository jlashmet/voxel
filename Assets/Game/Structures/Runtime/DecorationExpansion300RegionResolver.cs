using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Region-aware companion to DecorationExpansion300SceneResolver. Required content is unchanged;
    /// optional slot weights are adjusted by region theme before the shared scheduler runs.
    /// </summary>
    public static class DecorationExpansion300RegionResolver
    {
        public static bool TryResolve(
            DecorationExpansion300SceneKind kind,
            DecorationRegionTheme region,
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
                if (!core[i].Required)
                    core[i].Weight = DecorationRegionContentPolicy.Weight(region, slots[i].Kind, core[i].Weight);
            }

            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            int optionalBudget = DecorationExpansion300SceneCatalog.OptionalBudget(kind, in context);
            if (profile.IsWellFormed)
            {
                optionalBudget += profile.ClutterBias > 1 ? 1 : 0;
                optionalBudget -= profile.ClutterBias < 0 ? 1 : 0;
                if (optionalBudget < 0) optionalBudget = 0;
            }

            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context,
                    sceneId,
                    core,
                    optionalBudget,
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
