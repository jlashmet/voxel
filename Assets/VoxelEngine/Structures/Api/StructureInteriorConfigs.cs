using Unity.Collections;
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
    /// Optional shell thicknesses describe material retained around the clear carved volume.
    /// </summary>
    public struct InteriorVolumeConfig
    {
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
            Size.x > 0 && Size.y > 0 && Size.z > 0
            && WallThickness >= 0 && FloorThickness >= 0 && CeilingThickness >= 0
            && ClearWidth > 0 && ClearDepth > 0 && ClearHeight > 0;
    }

    /// <summary>Archetype-neutral connective opening types for carved interiors.</summary>
    public enum InteriorConnectionKind : byte
    {
        Doorway = 0,
        Arch = 1,
        Passage = 2,
    }

    /// <summary>
    /// Explicit connective opening between two interior volumes. ToVolumeIndex may be -1 for an
    /// exterior opening; internal connectivity otherwise uses bounded volume indices. Size stores
    /// the clear carved opening, while frame thickness/material describe optional retained trim.
    /// </summary>
    public struct ConnectiveOpeningConfig
    {
        public InteriorConnectionKind Kind;
        public int FromVolumeIndex;
        public int ToVolumeIndex;
        public int3 Min;
        public int3 Size;
        public int FrameThickness;
        public StructureMaterialRole FrameMaterialRole;

        public int ClearWidth => math.max(Size.x, Size.z);
        public int ClearHeight => Size.y;
        public bool IsExterior => ToVolumeIndex < 0;

        public bool IsWellFormed =>
            FromVolumeIndex >= 0
            && ToVolumeIndex >= -1
            && FromVolumeIndex != ToVolumeIndex
            && Size.x > 0 && Size.y > 0 && Size.z > 0
            && FrameThickness >= 0
            && FrameThickness * 2 < ClearWidth
            && FrameThickness * 2 < ClearHeight;

        public bool SupportsClearance(int minimumWidth, int minimumHeight)
        {
            return IsWellFormed
                && minimumWidth > 0 && minimumHeight > 0
                && ClearWidth >= minimumWidth && ClearHeight >= minimumHeight;
        }
    }

    /// <summary>
    /// Bounded room-carving layout plus explicit connections. Connectivity checks are allocation-free
    /// and use only integer configuration so catalogue validation remains deterministic and Burst-safe.
    /// </summary>
    public struct InteriorLayoutConfig
    {
        public FixedList4096Bytes<InteriorVolumeConfig> Volumes;
        public FixedList4096Bytes<ConnectiveOpeningConfig> Connections;

        public bool IsWellFormed
        {
            get
            {
                if (Volumes.Length == 0)
                    return false;

                for (var i = 0; i < Volumes.Length; i++)
                {
                    if (!Volumes[i].IsWellFormed)
                        return false;
                }

                for (var i = 0; i < Connections.Length; i++)
                {
                    ConnectiveOpeningConfig connection = Connections[i];
                    if (!connection.IsWellFormed)
                        return false;
                    if (connection.FromVolumeIndex >= Volumes.Length)
                        return false;
                    if (!connection.IsExterior && connection.ToVolumeIndex >= Volumes.Length)
                        return false;
                }

                return true;
            }
        }

        public bool HasConnectedInteriorGraph()
        {
            return IsWellFormed && AllVolumesReachable(0, 0, requireClearance: false);
        }

        public bool IsNavigable(int minimumPassageWidth, int minimumPassageHeight)
        {
            if (!IsWellFormed || minimumPassageWidth <= 0 || minimumPassageHeight <= 0)
                return false;

            return AllVolumesReachable(minimumPassageWidth, minimumPassageHeight, requireClearance: true);
        }

        private bool AllVolumesReachable(int minimumWidth, int minimumHeight, bool requireClearance)
        {
            if (Volumes.Length <= 1)
                return true;

            var visited = new FixedList4096Bytes<byte>();
            for (var i = 0; i < Volumes.Length; i++)
                visited.Add(0);
            visited[0] = 1;

            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = 0; i < Connections.Length; i++)
                {
                    ConnectiveOpeningConfig connection = Connections[i];
                    if (connection.IsExterior)
                        continue;
                    if (requireClearance && !connection.SupportsClearance(minimumWidth, minimumHeight))
                        continue;

                    int from = connection.FromVolumeIndex;
                    int to = connection.ToVolumeIndex;
                    if (visited[from] == visited[to])
                        continue;

                    visited[from] = 1;
                    visited[to] = 1;
                    changed = true;
                }
            }

            for (var i = 0; i < visited.Length; i++)
            {
                if (visited[i] == 0)
                    return false;
            }

            return true;
        }
    }
}
