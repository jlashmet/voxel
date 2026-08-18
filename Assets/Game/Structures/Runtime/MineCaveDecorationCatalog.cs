using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public static class MineCaveDecorationCatalog
    {
        public const uint SceneId = 0x4D494E31u; // MIN1
        public const int KindCount = 8;

        public static MineCaveDecorationDescriptor Describe(
            in DecorationContext context,
            MineCaveDecorationKind kind,
            uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, SceneId, slotId);
            switch (kind)
            {
                case MineCaveDecorationKind.SupportBeam:
                    return D(kind, MineCaveMountKind.Route, DecorationRenderBackend.VoxelStamp,
                        DecorationInteractionFlags.Destructible,
                        new int3(18 + (int)(seed & 3u) * 2, 18 + (int)((seed >> 2) & 7u), 4), seed, 0x5A770201u);
                case MineCaveDecorationKind.Rail:
                    return D(kind, MineCaveMountKind.Route, DecorationRenderBackend.VoxelStamp,
                        DecorationInteractionFlags.None,
                        new int3(8, 1, 28 + (int)(seed & 7u) * 2), seed, 0x2A110001u);
                case MineCaveDecorationKind.MineCart:
                    return D(kind, MineCaveMountKind.Route, DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible |
                        DecorationInteractionFlags.Movable | DecorationInteractionFlags.Container |
                        DecorationInteractionFlags.Lootable,
                        new int3(10 + (int)(seed & 1u) * 2, 8 + (int)((seed >> 2) & 1u), 14), seed, 0xCA270001u);
                case MineCaveDecorationKind.Rope:
                    return D(kind, MineCaveMountKind.Wall, DecorationRenderBackend.ProceduralMesh,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(3, 10 + (int)(seed & 7u), 3), seed, 0x20AE0001u);
                case MineCaveDecorationKind.Lantern:
                    return D(kind, MineCaveMountKind.Wall, DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.EmitsLight,
                        new int3(3, 6 + (int)(seed & 1u), 3), seed, 0x1A47E201u);
                case MineCaveDecorationKind.Crate:
                    return D(kind, MineCaveMountKind.Floor, DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible |
                        DecorationInteractionFlags.Movable | DecorationInteractionFlags.Container |
                        DecorationInteractionFlags.Lootable,
                        new int3(8 + (int)(seed & 3u) * 2, 8 + (int)((seed >> 2) & 3u) * 2, 8 + (int)((seed >> 4) & 3u) * 2), seed, 0xC2A7E101u);
                case MineCaveDecorationKind.ToolRack:
                    return D(kind, MineCaveMountKind.Wall, DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Lootable,
                        new int3(16 + (int)(seed & 3u) * 2, 14 + (int)((seed >> 2) & 3u), 4), seed, 0x70012AC1u);
                default:
                    return D(MineCaveDecorationKind.Ladder, MineCaveMountKind.Wall, DecorationRenderBackend.VoxelStamp,
                        DecorationInteractionFlags.Destructible,
                        new int3(8, 18 + (int)(seed & 7u) * 2, 3), seed, 0x1ADDE201u);
            }
        }

        public static uint SlotId(MineCaveDecorationKind kind, int ordinal) =>
            DecorationSeed.Derive(SceneId ^ (uint)kind, (uint)(ordinal + 1));

        private static MineCaveDecorationDescriptor D(
            MineCaveDecorationKind kind,
            MineCaveMountKind mount,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            int3 size,
            uint seed,
            uint salt) => new MineCaveDecorationDescriptor
            {
                Kind = kind,
                Mount = mount,
                Backend = backend,
                Interaction = interaction,
                Size = size,
                Variant = DecorationSeed.Derive(seed, salt),
            };
    }
}
