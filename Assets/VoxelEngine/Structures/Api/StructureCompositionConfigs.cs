using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>A local interior volume to carve or line inside a structure footprint.</summary>
    public struct RoomVolumeConfig
    {
        public int3 LocalMin;
        public int3 Size;
        public int WallThickness;
        public int FloorThickness;
        public int CeilingThickness;
        public StructureMaterialRole WallMaterialRole;
        public StructureMaterialRole FloorMaterialRole;
        public StructureMaterialRole CeilingMaterialRole;
    }

    /// <summary>
    /// A required navigable connection between two authored room volumes. The opening geometry uses
    /// the same shared opening contract as exterior doors/windows instead of a separate room system.
    /// </summary>
    public struct ConnectiveOpeningConfig
    {
        public int FromRoomIndex;
        public int ToRoomIndex;
        public Facing Facing;
        public int LocalOffset;
        public OpeningConfig Opening;
    }

    /// <summary>
    /// Bounded interior layout input. Fixed-capacity lists keep room/connectivity authoring blittable
    /// and make the maximum composition cost explicit before primitive emission.
    /// </summary>
    public struct InteriorLayoutConfig
    {
        public FixedList512Bytes<RoomVolumeConfig> Rooms;
        public FixedList512Bytes<ConnectiveOpeningConfig> Connections;
    }

    /// <summary>Reusable enclosed/open courtyard volume and perimeter treatment.</summary>
    public struct CourtyardConfig
    {
        public int OffsetX;
        public int OffsetZ;
        public int Width;
        public int Depth;
        public bool FloorEnabled;
        public int FloorThickness;
        public bool PerimeterWallEnabled;
        public WallRunConfig PerimeterWall;
        public StructureMaterialRole FloorMaterialRole;
    }

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
