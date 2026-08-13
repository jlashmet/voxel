namespace VoxelEngine.Core.Storage
{
    /// <summary>
    /// The dimensional constants the whole engine is built on. Everything here is a
    /// power of two so that coordinate decomposition is shifts and masks rather than
    /// division — this runs inside the innermost loop of extraction, collision, and
    /// edit expansion.
    ///
    /// See specs/001-destructible-voxel-engine/device-matrix.md for the world extent
    /// these produce (4 km x 4 km x 1 km at 10 cm voxels).
    /// </summary>
    public static class VoxelDimensions
    {
        /// <summary>Voxels per brick edge.</summary>
        public const int BrickEdge = 8;

        public const int BrickEdgeLog2 = 3;
        public const int BrickEdgeMask = BrickEdge - 1;

        /// <summary>512 voxels, one byte each.</summary>
        public const int VoxelsPerBrick = BrickEdge * BrickEdge * BrickEdge;

        /// <summary>512 occupancy bits = 8 x 64-bit words.</summary>
        public const int OccupancyWordsPerBrick = VoxelsPerBrick / 64;

        /// <summary>Bricks per region edge.</summary>
        public const int RegionEdge = 64;

        public const int RegionEdgeLog2 = 6;
        public const int RegionEdgeMask = RegionEdge - 1;

        public const int BricksPerRegion = RegionEdge * RegionEdge * RegionEdge;

        /// <summary>Voxels per region edge: 8 * 64 = 512.</summary>
        public const int RegionVoxelEdgeLog2 = BrickEdgeLog2 + RegionEdgeLog2;
        public const int RegionVoxelEdge = 1 << RegionVoxelEdgeLog2;

        /// <summary>
        /// Bytes for one mixed brick: 512 material + 1024 surface semantics + 512 authored
        /// boundary samples + 64 occupancy = 2112 B.
        /// Empty and uniform bricks cost zero — that asymmetry is what makes a
        /// kilometre-scale world fit a capped memory budget.
        /// </summary>
        public const int BytesPerMixedBrick = VoxelsPerBrick
            + VoxelsPerBrick * sizeof(ushort)
            + VoxelsPerBrick
            + OccupancyWordsPerBrick * sizeof(ulong);

        /// <summary>Material index reserved for empty space. Never stored in a palette.</summary>
        public const byte MaterialEmpty = 0;
    }
}
