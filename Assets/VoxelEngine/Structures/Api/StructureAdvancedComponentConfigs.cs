using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Reusable enclosed open-space/courtyard composition in local X/Z coordinates.</summary>
    public struct CourtyardConfig
    {
        public int OffsetX;
        public int OffsetZ;
        public int Width;
        public int Depth;
        public int PerimeterClearance;
        public bool OpenToSky;
        public bool SurfaceEnabled;
        public StructureMaterialRole SurfaceMaterialRole;

        public bool IsWellFormed =>
            Width > 0 && Depth > 0 && PerimeterClearance >= 0;
    }

    /// <summary>Stable consumer-facing meanings for structure attachment points.</summary>
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

    /// <summary>Named local attachment request without exposing the producing structure's internals.</summary>
    public struct StructureAttachmentConfig
    {
        public StructureAttachmentKind Kind;
        public int3 LocalPosition;
        public Facing Facing;
    }

    public static class StructureAttachmentSemantics
    {
        public static FixedString32Bytes Name(StructureAttachmentKind kind)
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
                default: return default;
            }
        }
    }
}
