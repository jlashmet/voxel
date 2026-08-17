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
    }
}
