namespace VoxelEngine.Terrain.Api
{
    /// <summary>
    /// Opaque material indices consumed by generic terrain generation.
    /// The game owns the semantic meaning of each index.
    /// </summary>
    public readonly struct TerrainMaterialSet
    {
        public readonly byte Deep;
        public readonly byte Subsurface;

        /// <summary>
        /// Ground cover in hollows, below the surface split height.
        ///
        /// Terrain needs two surface materials rather than one because a single one paints an
        /// entire valley a single flat colour, and the eye reads that as untextured rather than as
        /// uniform. Splitting on height is the cheapest variation that still costs nothing at
        /// runtime and stays a pure function of the height field, so the near voxel surface and the
        /// distant analytic mesh can derive the same answer independently.
        /// </summary>
        public readonly byte LowSurface;

        /// <summary>Ground cover at and above the surface split height.</summary>
        public readonly byte Surface;

        public TerrainMaterialSet(byte deep, byte subsurface, byte surface)
            : this(deep, subsurface, surface, surface)
        {
        }

        public TerrainMaterialSet(byte deep, byte subsurface, byte lowSurface, byte surface)
        {
            Deep = deep;
            Subsurface = subsurface;
            LowSurface = lowSurface;
            Surface = surface;
        }

        /// <summary>
        /// Ground cover for a column whose surface sits at <paramref name="surfaceHeight"/>.
        ///
        /// This is the single rule both terrain representations answer with. It used to live twice
        /// — once here for voxels and once in the far mesh's own material set — and the two
        /// disagreed, so the near ground and the horizon were different materials meeting at a hard
        /// line across the middle of the world.
        /// </summary>
        public byte SurfaceAt(int surfaceHeight, int splitHeight) =>
            surfaceHeight < splitHeight ? LowSurface : Surface;
    }
}
