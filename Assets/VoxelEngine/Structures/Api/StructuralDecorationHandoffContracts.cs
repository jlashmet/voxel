using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Minimum solved-instance identity and placement needed by downstream decoration.</summary>
    public struct StructuralDecorationInstanceHandoff
    {
        public ulong SemanticStructureId;
        public ulong InstanceId;
        public uint PieceId;
        public int3 Position;
        public byte Orientation;
    }

    /// <summary>Minimum accepted socket metadata needed to derive decoration sockets.</summary>
    public struct StructuralDecorationAttachmentHandoff
    {
        public uint SocketId;
        public int3 AttachmentPosition;
        public StructuralSocketFlags SocketFlags;
        public StructuralDecorationHandoff DecorationHandoff;
        public bool Accepted;
    }
}
