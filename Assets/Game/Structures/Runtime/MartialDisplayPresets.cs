using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Reusable martial displays sharing the existing WeaponRack semantic family.</summary>
    public static class MartialDisplayPresets
    {
        public static DecorationPropDescriptor ShieldDisplay(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int edge = 9 + (int)context.Wealth + (int)(seed & 1u) * 2;
            return Build(DecorationMountMode.Wall, DecorationSocketKind.Wall,
                new int3(edge, edge + 2, 3), new int3(2, 2, 1),
                MartialDisplayKind.Shield,
                DecorationSeed.Derive(seed, context.StyleId ^ 0x5A1E1D01u));
        }

        public static DecorationPropDescriptor WeaponRack(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int wealth = (int)context.Wealth;
            return Build(DecorationMountMode.Wall, DecorationSocketKind.Wall,
                new int3(18 + wealth * 3 + (int)(seed & 3u) * 2, 16 + wealth * 2, 4),
                new int3(3, 2, 2), MartialDisplayKind.Weapons,
                DecorationSeed.Derive(seed, context.StyleId ^ 0xAEA90001u));
        }

        public static DecorationPropDescriptor ArmorDisplay(
            in DecorationContext context, uint sceneId, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            return Build(DecorationMountMode.Floor, DecorationSocketKind.Floor,
                new int3(
                    10 + (int)(seed & 1u) * 2,
                    22 + (int)context.Wealth * 2 + math.max(0, style.SilhouetteBias) * 2,
                    9 + (int)((seed >> 2) & 1u) * 2),
                new int3(3, 0, 3), MartialDisplayKind.Armor,
                DecorationSeed.Derive(seed, context.StyleId ^ 0xA2102001u));
        }

        private static DecorationPropDescriptor Build(
            DecorationMountMode mount,
            DecorationSocketKind socket,
            int3 size,
            int3 clearance,
            MartialDisplayKind kind,
            uint payload) => new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.WeaponRack,
                AcceptedSockets = socket,
                MountMode = mount,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Destructible,
                Size = size,
                Clearance = clearance,
                Variant = MartialDisplayVariants.Create(kind, payload),
            };
    }
}
