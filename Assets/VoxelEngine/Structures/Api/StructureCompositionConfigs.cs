using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Stable semantic attachment identities shared across archetypes. Consumers depend on these
    /// meanings rather than knowing which wall, room, tower, or facade produced the anchor.
    /// </summary>
    public enum StructureAttachmentKind : byte
    {
        MainEntrance = 0,
        RearEntrance = 1,
        Road = 2,
        Basement = 3,
        Crypt = 4,
        Cave = 5,
        Extension = 6,
    }

    /// <summary>Authored local attachment point resolved to an ordinary engine anchor at generation.</summary>
    public struct AttachmentAnchorConfig
    {
        public StructureAttachmentKind Kind;
        public int3 LocalPosition;
        public Facing Facing;
        public bool SnapToGround;

        public bool IsWellFormed =>
            Facing == Facing.North || Facing == Facing.East ||
            Facing == Facing.South || Facing == Facing.West ||
            Facing == Facing.Up || Facing == Facing.Down;
    }

    /// <summary>Canonical anchor names for external consumers and existing anchor contracts.</summary>
    public static class StructureAttachmentNames
    {
        public static FixedString32Bytes Resolve(StructureAttachmentKind kind)
        {
            switch (kind)
            {
                case StructureAttachmentKind.MainEntrance: return new FixedString32Bytes("MainEntrance");
                case StructureAttachmentKind.RearEntrance: return new FixedString32Bytes("RearEntrance");
                case StructureAttachmentKind.Road: return new FixedString32Bytes("Road");
                case StructureAttachmentKind.Basement: return new FixedString32Bytes("Basement");
                case StructureAttachmentKind.Crypt: return new FixedString32Bytes("Crypt");
                case StructureAttachmentKind.Cave: return new FixedString32Bytes("Cave");
                case StructureAttachmentKind.Extension: return new FixedString32Bytes("Extension");
                default: return new FixedString32Bytes("Extension");
            }
        }
    }
}
