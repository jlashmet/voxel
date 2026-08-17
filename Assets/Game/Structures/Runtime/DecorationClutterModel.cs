using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationClutterKind : byte
    {
        Book = 0,
        Pottery = 1,
        Food = 2,
        Tool = 3,
        Container = 4,
        TabletopMisc = 5,
    }

    /// <summary>Compact recipe output for tiny secondary detail that should not become a full prop object.</summary>
    public struct DecorationClutterDescriptor
    {
        public DecorationClutterKind Kind;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public uint Variant;

        public bool IsWellFormed => math.all(Size > 0);
    }

    /// <summary>Resolved tiny child detail with stable identity and a furniture parent.</summary>
    public struct DecorationClutterInstance
    {
        public GeneratedPropId Id;
        public GeneratedPropId ParentId;
        public DecorationClutterKind Kind;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public DecorationBounds Bounds;
        public uint Variant;

        public bool IsWellFormed =>
            Id.Value != 0 && ParentId.Value != 0 && Bounds.IsWellFormed;
    }

    public static class DecorationClutterCatalog
    {
        public const int KindCount = 6;

        public static DecorationClutterDescriptor Describe(
            in DecorationContext context,
            uint sceneId,
            uint clusterId,
            int itemIndex,
            DecorationClutterKind kind)
        {
            uint seed = Seed(in context, sceneId, clusterId, itemIndex);
            switch (kind)
            {
                case DecorationClutterKind.Book:
                    return Descriptor(kind, DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Movable,
                        new int3(4 + (int)(seed & 1u) * 2, 1 + (int)((seed >> 2) & 1u), 5), seed, 0xB00C0001u);
                case DecorationClutterKind.Pottery:
                    return Descriptor(kind, DecorationRenderBackend.ProceduralMesh,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(3, 3 + (int)(seed & 3u), 3), seed, 0xA077E201u);
                case DecorationClutterKind.Food:
                    return Descriptor(kind, DecorationRenderBackend.ProceduralMesh,
                        DecorationInteractionFlags.Movable | DecorationInteractionFlags.Lootable,
                        new int3(3 + (int)(seed & 1u), 2, 3 + (int)((seed >> 2) & 1u)), seed, 0xF00D0001u);
                case DecorationClutterKind.Tool:
                    return Descriptor(kind, DecorationRenderBackend.ProceduralMesh,
                        DecorationInteractionFlags.Movable | DecorationInteractionFlags.Lootable,
                        new int3(6, 1 + (int)(seed & 1u), 2), seed, 0x70010001u);
                case DecorationClutterKind.Container:
                    return Descriptor(kind, DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable |
                        DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable,
                        new int3(5, 3 + (int)(seed & 1u), 4), seed, 0xC0470001u);
                default:
                    return Descriptor(DecorationClutterKind.TabletopMisc, DecorationRenderBackend.ProceduralMesh,
                        DecorationInteractionFlags.Movable,
                        new int3(2 + (int)(seed & 1u), 2 + (int)((seed >> 1) & 1u), 2), seed, 0xA15C0001u);
            }
        }

        public static uint StableSlotId(uint clusterId, int itemIndex) =>
            DecorationSeed.Derive(clusterId == 0 ? 1u : clusterId, (uint)(itemIndex + 1));

        private static DecorationClutterDescriptor Descriptor(
            DecorationClutterKind kind,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            int3 size,
            uint seed,
            uint salt) => new DecorationClutterDescriptor
            {
                Kind = kind,
                Backend = backend,
                Interaction = interaction,
                Size = size,
                Variant = DecorationSeed.Derive(seed, salt),
            };

        private static uint Seed(
            in DecorationContext context,
            uint sceneId,
            uint clusterId,
            int itemIndex) =>
            DecorationSeed.ForSlot(in context, sceneId, StableSlotId(clusterId, itemIndex));
    }
}
