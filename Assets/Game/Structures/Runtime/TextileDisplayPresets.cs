using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public static class TextileDisplayPresets
    {
        public static DecorationPropDescriptor Banner(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Banner,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.Wall,
                Backend = DecorationRenderBackend.ThinSurface,
                Interaction = DecorationInteractionFlags.Destructible,
                Size = new int3(
                    10 + wealth * 2 + math.max(0, style.SilhouetteBias) + (int)(seed & 3u) * 2,
                    18 + wealth * 3 + (int)((seed >> 3) & 3u) * 2,
                    1),
                Clearance = new int3(2, 2, 0),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xBA44E201u),
            };
        }

        public static DecorationPropDescriptor Curtain(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Curtain,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.Wall,
                Backend = DecorationRenderBackend.ThinSurface,
                Interaction = DecorationInteractionFlags.Destructible,
                Size = new int3(
                    14 + wealth * 3 + (int)(seed & 3u) * 2,
                    24 + wealth * 3 + (int)((seed >> 4) & 1u) * 4,
                    1),
                Clearance = new int3(1, 1, 0),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xC027A101u),
            };
        }
    }
}
