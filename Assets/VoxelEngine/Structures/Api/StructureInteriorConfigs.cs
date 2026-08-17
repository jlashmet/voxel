using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Semantic role of a carved interior volume without imposing an archetype layout.</summary>
    public enum StructureInteriorVolumeKind : byte
    {
        Room = 0,
        Hall = 1,
        Corridor = 2,
        Stairwell = 3,
        Void = 4,
    }

    /// <summary>
    /// One definition-local interior volume to carve from a structure shell. Bounds are half-open
    /// integer voxel coordinates so adjacent rooms can share a wall without ambiguous ownership.
    /// Wall/floor/ceiling thicknesses describe the shell retained around the clear navigable volume.
    /// </summary>
    public struct InteriorVolumeConfig
    {
        public int VolumeId;
        public StructureInteriorVolumeKind Kind;
        public int3 Min;
        public int3 Size;
        public int WallThickness;
        public int FloorThickness;
        public int CeilingThickness;
        public StructureMaterialRole WallMaterialRole;
        public StructureMaterialRole FloorMaterialRole;
        public StructureMaterialRole CeilingMaterialRole;

        public int3 MaxExclusive => Min + Size;
        public int ClearWidth => Size.x - (WallThickness * 2);
        public int ClearDepth => Size.z - (WallThickness * 2);
        public int ClearHeight => Size.y - FloorThickness - CeilingThickness;

        public bool IsWellFormed =>
            VolumeId >= 0
            && Size.x > 0 && Size.y > 0 && Size.z > 0
            && WallThickness >= 0 && FloorThickness >= 0 && CeilingThickness >= 0
            && ClearWidth > 0 && ClearDepth > 0 && ClearHeight > 0;
    }

    /// <summary>Axis normal to a connective opening between two interior volumes.</summary>
    public enum StructureInteriorOpeningAxis : byte
    {
        X = 0,
        Z = 1,
    }

    /// <summary>
    /// A navigable opening connecting two carved interior volumes. Endpoints are stable volume ids,
    /// not references to builder internals. LocalMin identifies the opening in definition-local voxel
    /// coordinates; Width and Height are the required clear passage dimensions and Depth is the wall
    /// thickness to carve through. Vertical transitions remain owned by VerticalAccessConfig.
    /// </summary>
    public struct InteriorConnectionConfig
    {
        public int FromVolumeId;
        public int ToVolumeId;
        public StructureInteriorOpeningAxis Axis;
        public int3 LocalMin;
        public int Width;
        public int Height;
        public int Depth;
        public int FrameThickness;
        public StructureMaterialRole FrameMaterialRole;

        public bool IsWellFormed =>
            FromVolumeId >= 0 && ToVolumeId >= 0 && FromVolumeId != ToVolumeId
            && Width > 0 && Height > 0 && Depth > 0
            && FrameThickness >= 0
            && FrameThickness * 2 < Width
            && FrameThickness * 2 < Height;

        /// <summary>
        /// Returns whether the authored clear passage satisfies an archetype's integer traversal
        /// envelope. This keeps gameplay-specific character dimensions outside the shared contract.
        /// </summary>
        public bool SupportsClearance(int minimumWidth, int minimumHeight)
        {
            return IsWellFormed
                && minimumWidth > 0 && minimumHeight > 0
                && Width >= minimumWidth && Height >= minimumHeight;
        }
    }
}
