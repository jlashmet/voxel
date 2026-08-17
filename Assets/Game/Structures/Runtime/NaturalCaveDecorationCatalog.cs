using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public static class NaturalCaveDecorationCatalog
    {
        public const uint SceneId = 0x4E415431u; // NAT1
        public const int KindCount = 8;

        public static NaturalCaveDecorationDescriptor Describe(
            in DecorationContext context,
            NaturalCaveDecorationKind kind,
            uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, SceneId, slotId);
            switch (kind)
            {
                case NaturalCaveDecorationKind.Stone:
                    return D(kind, DecorationRenderBackend.VoxelStamp,
                        DecorationInteractionFlags.Destructible,
                        new int3(3 + (int)(seed & 3u), 2 + (int)((seed >> 2) & 3u), 3 + (int)((seed >> 4) & 3u)), false, seed, 0x5704E001u);
                case NaturalCaveDecorationKind.Root:
                    return D(kind, DecorationRenderBackend.ProceduralMesh,
                        DecorationInteractionFlags.Destructible,
                        new int3(3 + (int)(seed & 1u), 7 + (int)((seed >> 2) & 7u), 3 + (int)((seed >> 5) & 1u)), true, seed, 0x20070001u);
                case NaturalCaveDecorationKind.Mushroom:
                    return D(kind, DecorationRenderBackend.ProceduralMesh,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Lootable,
                        new int3(2 + (int)(seed & 1u), 3 + (int)((seed >> 2) & 3u), 2 + (int)((seed >> 4) & 1u)), false, seed, 0xA05A0001u);
                case NaturalCaveDecorationKind.Crystal:
                    return D(kind, DecorationRenderBackend.VoxelStamp,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Lootable,
                        new int3(3 + (int)(seed & 3u), 5 + (int)((seed >> 2) & 7u), 3 + (int)((seed >> 5) & 3u)), false, seed, 0xC2A57001u);
                case NaturalCaveDecorationKind.Bones:
                    return D(kind, DecorationRenderBackend.ProceduralMesh,
                        DecorationInteractionFlags.Destructible,
                        new int3(5 + (int)(seed & 3u), 2, 4 + (int)((seed >> 3) & 3u)), false, seed, 0xB04E5001u);
                case NaturalCaveDecorationKind.Puddle:
                    return D(kind, DecorationRenderBackend.ThinSurface,
                        DecorationInteractionFlags.None,
                        new int3(6 + (int)(seed & 7u), 1, 6 + (int)((seed >> 3) & 7u)), false, seed, 0xA0DD1E01u);
                case NaturalCaveDecorationKind.Stalagmite:
                    return D(kind, DecorationRenderBackend.VoxelStamp,
                        DecorationInteractionFlags.Destructible,
                        new int3(4 + (int)(seed & 3u), 7 + (int)((seed >> 2) & 15u), 4 + (int)((seed >> 6) & 3u)), false, seed, 0x57A1A601u);
                default:
                    return D(NaturalCaveDecorationKind.Stalactite, DecorationRenderBackend.VoxelStamp,
                        DecorationInteractionFlags.Destructible,
                        new int3(4 + (int)(seed & 3u), 7 + (int)((seed >> 2) & 15u), 4 + (int)((seed >> 6) & 3u)), true, seed, 0x57A1AC71u);
            }
        }

        public static uint SlotId(NaturalCaveDecorationKind kind, int ordinal) =>
            DecorationSeed.Derive(SceneId ^ (uint)kind, (uint)(ordinal + 1));

        private static NaturalCaveDecorationDescriptor D(
            NaturalCaveDecorationKind kind,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            int3 size,
            bool ceiling,
            uint seed,
            uint salt) => new NaturalCaveDecorationDescriptor
            {
                Kind = kind,
                Backend = backend,
                Interaction = interaction,
                Size = size,
                CeilingMounted = ceiling,
                Variant = DecorationSeed.Derive(seed, salt),
            };
    }
}
