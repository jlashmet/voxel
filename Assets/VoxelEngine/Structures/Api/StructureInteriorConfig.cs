using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// One definition-local interior clear volume. Bounds are half-open and intentionally describe
    /// the navigable void to carve rather than a second representation of surrounding wall geometry.
    /// </summary>
    public struct InteriorVolumeConfig
    {
        public int3 Min;
        public int3 Size;
        public StructureMaterialRole FloorMaterialRole;
        public StructureMaterialRole CeilingMaterialRole;

        public int3 MaxExclusive => Min + Size;
        public bool IsWellFormed => Size.x > 0 && Size.y > 0 && Size.z > 0;

        public bool ProvidesClearance(int minimumPassageWidth, int minimumPassageHeight) =>
            minimumPassageWidth > 0 && minimumPassageHeight > 0 &&
            Size.x >= minimumPassageWidth && Size.z >= minimumPassageWidth &&
            Size.y >= minimumPassageHeight;
    }

    /// <summary>How two authored interior volumes are connected.</summary>
    public enum InteriorConnectionKind : byte
    {
        Doorway = 0,
        Arch = 1,
        OpenPassage = 2,
        Stairwell = 3,
    }

    /// <summary>
    /// A bounded carve joining two room volumes. Room indices refer to the containing
    /// <see cref="InteriorLayoutConfig"/>. ToVolumeIndex may be -1 for an exterior connection;
    /// otherwise both indices must reference distinct rooms. The local carve volume is explicit so
    /// validators can prove headroom, width, bounds, and actual room intersection deterministically.
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

        public bool IsWellFormed =>
            FromVolumeIndex >= 0 &&
            ToVolumeIndex >= -1 &&
            ToVolumeIndex != FromVolumeIndex &&
            Size.x > 0 && Size.y > 0 && Size.z > 0 &&
            FrameThickness >= 0;

        /// <summary>
        /// The carve is thin along the wall normal, so horizontal clearance is the larger X/Z
        /// extent while Y is headroom. This stays orientation-neutral until component compilation.
        /// </summary>
        public bool ProvidesClearance(int minimumPassageWidth, int minimumPassageHeight)
        {
            int horizontalClearance = Size.x > Size.z ? Size.x : Size.z;
            return minimumPassageWidth > 0 && minimumPassageHeight > 0 &&
                   horizontalClearance >= minimumPassageWidth &&
                   Size.y >= minimumPassageHeight;
        }
    }

    /// <summary>
    /// Bounded, blittable room-and-connection graph for reusable generated interiors. Geometry
    /// remains authoritative only after these configs are compiled to the existing shape-program /
    /// primitive pipeline. The fixed lists cap authoring work and make graph validation predictable.
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
                    if (connection.ToVolumeIndex >= Volumes.Length)
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Validates the guarantees needed by archetypes that promise a navigable interior: every
        /// clear room meets minimum player clearance, every configured connective opening meets
        /// passage/headroom clearance, and every room is reachable from room zero through interior
        /// connections. Exterior openings do not participate in the room graph.
        /// </summary>
        public bool IsNavigable(int minimumPassageWidth, int minimumPassageHeight)
        {
            if (!IsWellFormed || minimumPassageWidth <= 0 || minimumPassageHeight <= 0)
                return false;

            for (var i = 0; i < Volumes.Length; i++)
            {
                if (!Volumes[i].ProvidesClearance(minimumPassageWidth, minimumPassageHeight))
                    return false;
            }

            for (var i = 0; i < Connections.Length; i++)
            {
                ConnectiveOpeningConfig connection = Connections[i];
                if (connection.ToVolumeIndex >= 0 &&
                    !connection.ProvidesClearance(minimumPassageWidth, minimumPassageHeight))
                    return false;
            }

            return HasConnectedInteriorGraph();
        }

        /// <summary>Allocation-free reachability check over the bounded room graph.</summary>
        public bool HasConnectedInteriorGraph()
        {
            if (!IsWellFormed || Volumes.Length > 64)
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
                    if (connection.ToVolumeIndex < 0)
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
            }
            while (changed);

            ulong expected = Volumes.Length == 64
                ? ulong.MaxValue
                : (1ul << Volumes.Length) - 1ul;
            return visited == expected;
        }
    }
}
