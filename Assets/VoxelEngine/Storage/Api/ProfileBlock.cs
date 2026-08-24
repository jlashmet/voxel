using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Logical retained surface primitive used to preserve sub-voxel authored profile detail.
    /// Structural occupancy remains in the authoritative voxel field; this value contains no
    /// runtime storage owner or allocator state.
    /// </summary>
    public struct ProfileBlock
    {
        public int3 Centre;
        public int InnerRadiusQ4;
        public int OuterRadiusQ4;
        public int FrontQ4;
        public int BackQ4;
        /// <summary>Last occupied sample used to validate structural backing independently from
        /// projected presentation geometry.</summary>
        public int BackingDepthVoxel;
        public int2 StartDirection;
        public int2 EndDirection;
        public byte Axis;
        public byte Material;
        public ushort SurfaceStyle;
        public byte Coating;
        public byte SurfaceDetail;
        public byte JointHalfWidthQ4;
        public byte BevelQ4;

        public void Bounds(out int3 min, out int3 max)
        {
            int radius = (OuterRadiusQ4 + 15) >> 4;
            min = Centre - radius;
            max = Centre + radius;
            min[Axis] = FloorQ4(math.min(FrontQ4, BackQ4));
            max[Axis] = CeilQ4(math.max(FrontQ4, BackQ4));
        }

        private static int FloorQ4(int value) => value >= 0 ? value >> 4 : -((-value + 15) >> 4);
        private static int CeilQ4(int value) => value >= 0 ? (value + 15) >> 4 : -((-value) >> 4);
    }

    /// <summary>
    /// Read-only retained-profile capability. Implementations own mutation and lifetime; consumers
    /// observe a monotonically versioned immutable snapshot and never receive the mutable store.
    /// </summary>
    public interface IProfileBlockReadSource
    {
        uint Version { get; }
        int Count { get; }
        ProfileBlock[] Snapshot();
    }
}
