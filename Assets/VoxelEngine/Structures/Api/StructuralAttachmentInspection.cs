using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// API-owned view of a resolved structural attachment for cross-module consumers that need
    /// semantic socket identity and placement without depending on the composition runtime.
    /// </summary>
    public readonly struct StructuralAttachmentInspection
    {
        public readonly uint SocketId;
        public readonly int3 AttachmentPosition;
        public readonly bool Accepted;

        public StructuralAttachmentInspection(uint socketId, int3 attachmentPosition, bool accepted)
        {
            SocketId = socketId;
            AttachmentPosition = attachmentPosition;
            Accepted = accepted;
        }
    }
}
