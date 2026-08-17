using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum MineCaveDecorationKind : byte
    {
        SupportBeam = 0,
        Rail = 1,
        MineCart = 2,
        Rope = 3,
        Lantern = 4,
        Crate = 5,
        ToolRack = 6,
        Ladder = 7,
    }

    public enum MineCaveMountKind : byte
    {
        Floor = 0,
        Wall = 1,
        Route = 2,
    }

    public struct MineCaveDecorationDescriptor
    {
        public MineCaveDecorationKind Kind;
        public MineCaveMountKind Mount;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public uint Variant;
        public bool IsWellFormed => math.all(Size > 0);
    }

    public struct MineCaveDecorationInstance
    {
        public GeneratedPropId Id;
        public MineCaveDecorationKind Kind;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;

        public bool IsWellFormed =>
            Id.Value != 0 && Bounds.IsWellFormed && math.csum(math.abs(Facing)) == 1;
    }
}
