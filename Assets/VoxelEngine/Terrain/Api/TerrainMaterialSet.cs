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
        public readonly byte Surface;

        public TerrainMaterialSet(byte deep, byte subsurface, byte surface)
        {
            Deep = deep;
            Subsurface = subsurface;
            Surface = surface;
        }
    }
}
