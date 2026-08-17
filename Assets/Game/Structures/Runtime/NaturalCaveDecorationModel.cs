using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum NaturalCaveDecorationKind : byte
    {
        Stone = 0,
        Root = 1,
        Mushroom = 2,
        Crystal = 3,
        Bones = 4,
        Puddle = 5,
        Stalagmite = 6,
        Stalactite = 7,
    }

    public struct NaturalCaveDecorationDescriptor
    {
        public NaturalCaveDecorationKind Kind;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public bool CeilingMounted;
        public uint Variant;
        public bool IsWellFormed => math.all(Size > 0);
    }

    public struct NaturalCaveDecorationInstance
    {
        public GeneratedPropId Id;
        public NaturalCaveDecorationKind Kind;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;

        public bool IsWellFormed =>
            Id.Value != 0 && Bounds.IsWellFormed && math.csum(math.abs(Facing)) == 1;
    }
}
