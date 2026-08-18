using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Shared scheduler/placer for non-relational utility room recipes.</summary>
    public static class UtilityRoomSceneResolver
    {
        public static bool TryResolve(
            UtilityRoomSceneKind kind,
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!UtilityRoomSceneCatalog.IsCompatible(kind, in space, in context))
                return false;

            uint sceneId = UtilityRoomSceneCatalog.SceneId(kind);
            DecorationSceneSlot[] slots = UtilityRoomSceneCatalog.CreateSlots(kind);
            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context, sceneId, slots,
                    UtilityRoomSceneCatalog.OptionalBudget(kind, in context),
                    out DecorationSceneSlot[] ordered))
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationSceneSlot slot = ordered[i];
                DecorationPropDescriptor descriptor = UtilityRoomSceneDescriptors.Describe(
                    kind, in context, sceneId, slot.SlotId);
                bool placed = descriptor.IsWellFormed &&
                              descriptor.Accepts(slot.RequestedSocket) &&
                              DecorationPlacementResolver.TryPlace(
                                  in space, in context, sceneId, slot.SlotId,
                                  in descriptor, sockets, exclusions, resolved, count,
                                  out resolved[count]);
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
    }
}
