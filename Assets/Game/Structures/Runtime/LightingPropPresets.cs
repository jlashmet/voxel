using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reusable light/fire fixture recipes. Standing lamps reuse the Lantern semantic family but are
    /// floor-mounted, leaving cave/wall lantern variants free to use the same family with wall mounts.
    /// Scene/slot identity is caller-owned so fixtures remain reusable across buildings and caves.
    /// </summary>
    public static class LightingPropPresets
    {
        public static DecorationPropDescriptor Fireplace(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Fireplace,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.FloorAgainstWall,
                Backend = DecorationRenderBackend.VoxelStamp,
                Interaction = DecorationInteractionFlags.BlocksNavigation |
                              DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.EmitsLight |
                              DecorationInteractionFlags.EmitsParticles,
                Size = new int3(
                    20 + wealth * 2 + (int)(seed & 1u) * 4,
                    24 + wealth * 2 + math.max(0, style.SilhouetteBias) * 2,
                    8 + (int)((seed >> 2) & 1u) * 2),
                Clearance = new int3(6, 0, 8),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xF12E0001u),
            };
        }

        public static DecorationPropDescriptor Candle(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Candle,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.Movable |
                              DecorationInteractionFlags.EmitsLight |
                              DecorationInteractionFlags.EmitsParticles,
                Size = new int3(2, 5 + (int)(seed & 3u), 2),
                Clearance = new int3(1, 0, 1),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xCA4D1E01u),
            };
        }

        public static DecorationPropDescriptor Chandelier(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            int span = 14 + wealth * 3 + math.max(0, style.SilhouetteBias) * 2 + (int)(seed & 3u) * 2;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Chandelier,
                AcceptedSockets = DecorationSocketKind.Ceiling,
                MountMode = DecorationMountMode.Ceiling,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.EmitsLight,
                Size = new int3(span, 10 + wealth + (int)((seed >> 4) & 3u), span),
                Clearance = new int3(4, 2, 4),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xC4A4D311u),
            };
        }

        public static DecorationPropDescriptor StandingLamp(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Lantern,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.Movable |
                              DecorationInteractionFlags.EmitsLight,
                Size = new int3(
                    5 + (wealth >= (int)DecorationWealthTier.Wealthy ? 2 : 0),
                    14 + wealth * 2 + math.max(0, style.SilhouetteBias) + (int)(seed & 1u) * 2,
                    5 + (wealth >= (int)DecorationWealthTier.Wealthy ? 2 : 0)),
                Clearance = new int3(2, 0, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0x57A4D1A1u),
            };
        }
    }
}
