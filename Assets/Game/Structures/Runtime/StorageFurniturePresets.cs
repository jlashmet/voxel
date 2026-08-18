using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Reusable wall/furniture storage recipes with caller-owned scene identity.</summary>
    public static class StorageFurniturePresets
    {
        public static DecorationPropDescriptor Chest(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Chest,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.FloorAgainstWall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation |
                              DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.Container |
                              DecorationInteractionFlags.Lootable |
                              DecorationInteractionFlags.Movable,
                Size = new int3(
                    14 + wealth * 2 + (int)(seed & 1u) * 2,
                    8 + math.max(0, style.SilhouetteBias) + (int)((seed >> 2) & 1u) * 2,
                    9 + (int)((seed >> 4) & 1u) * 2),
                Clearance = new int3(3, 0, 4),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xC4E57001u),
            };
        }

        public static DecorationPropDescriptor Shelf(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Shelf,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.Wall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Destructible,
                Size = new int3(
                    12 + (int)context.Wealth * 2 + (int)(seed & 3u) * 2,
                    7 + math.max(0, style.SilhouetteBias) + (int)((seed >> 3) & 1u) * 2,
                    3),
                Clearance = new int3(2, 2, 1),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0x5E1F0001u),
            };
        }

        public static DecorationPropDescriptor Bookcase(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Bookcase,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.FloorAgainstWall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(
                    18 + wealth * 2 + (int)(seed & 3u) * 2,
                    25 + wealth * 2 + math.max(0, style.SilhouetteBias) * 2 + (int)((seed >> 4) & 1u) * 3,
                    6 + (int)((seed >> 5) & 1u)),
                Clearance = new int3(3, 0, 4),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xB00CCA5Eu),
            };
        }
    }
}
