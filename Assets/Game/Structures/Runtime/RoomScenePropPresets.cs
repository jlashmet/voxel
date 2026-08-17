using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Caller-seeded common furniture used by multiple room scene recipes.</summary>
    public static class RoomScenePropPresets
    {
        public static DecorationPropDescriptor Bed(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Bed,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.FloorAgainstWall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(14 + wealth * 2 + (int)(seed & 1u) * 2, 7 + wealth, 26 + (int)((seed >> 2) & 1u) * 2),
                Clearance = new int3(3, 0, 5),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xBED20001u),
            };
        }

        public static DecorationPropDescriptor Bench(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Bench,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(18 + (int)context.Wealth * 3 + (int)(seed & 3u) * 2, 6, 7),
                Clearance = new int3(2, 0, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xBE4C2001u),
            };
        }

        public static DecorationPropDescriptor Chair(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Chair,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation |
                              DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.Movable,
                Size = new int3(7 + (int)(seed & 1u), 10 + (int)context.Wealth, 7 + (int)((seed >> 2) & 1u)),
                Clearance = new int3(2, 0, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xC4A12001u),
            };
        }

        public static DecorationPropDescriptor WorkTable(
            in DecorationContext context, uint sceneId, uint slotId, int length)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Table,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(length + (int)(seed & 1u) * 2, 9, 14 + (int)context.Wealth),
                Clearance = new int3(3, 0, 3),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0x7AB12001u),
            };
        }

        public static DecorationPropDescriptor WallTorch(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.WallTorch,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.Wall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.EmitsLight |
                              DecorationInteractionFlags.EmitsParticles,
                Size = new int3(3, 8, 3),
                Clearance = new int3(4, 3, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0x702C2001u),
            };
        }

        public static DecorationPropDescriptor Painting(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Painting,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.Wall,
                Backend = DecorationRenderBackend.ThinSurface,
                Interaction = DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                Size = new int3(10 + (int)(seed & 3u) * 2, 10 + (int)context.Wealth * 2, 1),
                Clearance = new int3(2, 2, 0),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xA2722001u),
            };
        }

        public static DecorationPropDescriptor Altar(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Altar,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.FloorAgainstWall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(18 + (int)context.Wealth * 2 + (int)(seed & 1u) * 2, 10 + (int)context.Wealth, 10),
                Clearance = new int3(5, 0, 8),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xA17A2001u),
            };
        }

        public static DecorationPropDescriptor Throne(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Chair,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(12 + (int)(seed & 1u) * 2, 20 + (int)context.Wealth * 2 + math.max(0, style.SilhouetteBias) * 2, 12),
                Clearance = new int3(6, 0, 8),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0x7A204001u),
            };
        }
    }
}
