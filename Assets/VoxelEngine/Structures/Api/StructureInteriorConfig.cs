using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Reusable semantic kinds for bounded interior clear volumes.</summary>
    public enum StructureInteriorVolumeKind : byte
    {
        Room = 0,
        Hall = 1,
        Chamber = 2,
    }

    /// <summary>
    /// One definition-local interior volume. Size includes its optional authored shell; the clear
    /// navigable dimensions are derived from wall/floor/ceiling thicknesses without floating point.
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
        public int ClearHeight => Size.y - FloorThickness - CeilingThickness;
        public int ClearDepth => Size.z - (WallThickness * 2);

        public bool IsWellFormed =>
            Size.x > 0 && Size.y > 0 && Size.z > 0 &&
            WallThickness >= 0 && FloorThickness >= 0 && CeilingThickness >= 0 &&
            ClearWidth > 0 && ClearHeight > 0 && ClearDepth > 0;
    }

    /// <summary>How two authored interior volumes, or an interior and exterior, are connected.</summary>
    public enum InteriorConnectionKind : byte
    {
        Doorway = 0,
        Arch = 1,
        Passage = 2,
        OpenPassage = Passage,
        Stairwell = 3,
    }

    /// <summary>
    /// Explicit bounded carve joining two room volumes. ToVolumeIndex = -1 denotes an exterior
    /// connection; otherwise both indices refer to distinct entries in the containing layout.
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

        public int3 MaxExclusive => Min + Size;
        public bool IsExterior => ToVolumeIndex == -1;

        public bool IsWellFormed =>
            FromVolumeIndex >= 0 &&
            ToVolumeIndex >= -1 &&
            ToVolumeIndex != FromVolumeIndex &&
            Size.x > 0 && Size.y > 0 && Size.z > 0 &&
            FrameThickness >= 0;
    }

    /// <summary>
    /// Bounded, blittable room-and-connection graph for generated interiors. Fixed lists keep the
    /// validation work capped; connectivity is evaluated directly from explicit room indices.
    /// </summary>
    public struct InteriorLayoutConfig
    {
        public FixedList512Bytes<InteriorVolumeConfig> Volumes;
        public FixedList512Bytes<ConnectiveOpeningConfig> Connections;

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
                    if (!connection.IsWellFormed || connection.FromVolumeIndex >= Volumes.Length)
                        return false;
                    if (!connection.IsExterior && connection.ToVolumeIndex >= Volumes.Length)
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Returns true when every interior volume belongs to one undirected connection graph.
        /// Exterior connections do not connect otherwise disconnected interior components.
        /// </summary>
        public bool HasConnectedInteriorGraph()
        {
            if (!IsWellFormed || Volumes.Length > 63)
                return false;
            if (Volumes.Length == 1)
                return true;

            ulong visited = 1ul;
            bool changed;
            do
            {
                changed = false;
                for (var i = 0; i < Connections.Length; i++)
                {
                    ConnectiveOpeningConfig connection = Connections[i];
                    if (connection.IsExterior)
                        continue;

                    ulong fromBit = 1ul << connection.FromVolumeIndex;
                    ulong toBit = 1ul << connection.ToVolumeIndex;
                    bool fromVisited = (visited & fromBit) != 0;
                    bool toVisited = (visited & toBit) != 0;
                    if (fromVisited == toVisited)
                        continue;

                    visited |= fromBit | toBit;
                    changed = true;
                }
            } while (changed);

            ulong expected = (1ul << Volumes.Length) - 1ul;
            return visited == expected;
        }

        /// <summary>
        /// Checks graph connectivity plus minimum clear room and opening dimensions. Horizontal
        /// opening width uses the larger X/Z span because the carve may cross either cardinal wall.
        /// </summary>
        public bool IsNavigable(int minimumWidth, int minimumHeight)
        {
            if (minimumWidth <= 0 || minimumHeight <= 0 || !HasConnectedInteriorGraph())
                return false;

            for (var i = 0; i < Volumes.Length; i++)
            {
                InteriorVolumeConfig volume = Volumes[i];
                if (volume.ClearWidth < minimumWidth ||
                    volume.ClearDepth < minimumWidth ||
                    volume.ClearHeight < minimumHeight)
                    return false;
            }

            for (var i = 0; i < Connections.Length; i++)
            {
                ConnectiveOpeningConfig connection = Connections[i];
                int horizontalWidth = math.max(connection.Size.x, connection.Size.z);
                if (horizontalWidth < minimumWidth || connection.Size.y < minimumHeight)
                    return false;
            }

            return true;
        }
    }
}
