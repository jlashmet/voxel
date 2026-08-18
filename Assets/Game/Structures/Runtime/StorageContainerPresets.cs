using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Reusable portable storage recipes with caller-owned scene identity.</summary>
    public static class StorageContainerPresets
    {
        public static DecorationPropDescriptor Crate(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int edge = 8 + (int)(seed & 3u) * 2;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Crate,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation |
                              DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.Container |
                              DecorationInteractionFlags.Lootable |
                              DecorationInteractionFlags.Movable,
                Size = new int3(edge, edge, edge),
                Clearance = new int3(1, 0, 1),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xC2A7E001u),
            };
        }

        public static DecorationPropDescriptor Barrel(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int diameter = 8 + (int)(seed & 1u) * 2;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Barrel,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation |
                              DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.Container |
                              DecorationInteractionFlags.Lootable |
                              DecorationInteractionFlags.Movable,
                Size = new int3(diameter, 12 + (int)((seed >> 2) & 3u), diameter),
                Clearance = new int3(1, 0, 1),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xBA22E100u),
            };
        }
    }
}
