namespace VoxelEngine.Composition.Api
{
    /// <summary>
    /// Opaque material indices required by the showcase world's generic generation and structural
    /// rules. The engine understands these as roles only; the game decides which semantic material
    /// occupies each role.
    /// </summary>
    public readonly struct ShowcaseMaterialSet
    {
        public readonly byte TerrainDeep;
        public readonly byte TerrainSubsurface;
        public readonly byte TerrainLowSurface;
        public readonly byte TerrainHighSurface;
        public readonly byte Gate;
        public readonly byte ReferenceArch;
        public readonly byte FarStructure;
        public readonly uint StructuralMask;

        public ShowcaseMaterialSet(
            byte terrainDeep,
            byte terrainSubsurface,
            byte terrainLowSurface,
            byte terrainHighSurface,
            byte gate,
            byte referenceArch,
            byte farStructure,
            uint structuralMask)
        {
            TerrainDeep = terrainDeep;
            TerrainSubsurface = terrainSubsurface;
            TerrainLowSurface = terrainLowSurface;
            TerrainHighSurface = terrainHighSurface;
            Gate = gate;
            ReferenceArch = referenceArch;
            FarStructure = farStructure;
            StructuralMask = structuralMask;
        }

        public byte SurfaceAt(int surfaceHeight, int splitHeight) =>
            surfaceHeight < splitHeight ? TerrainLowSurface : TerrainHighSurface;

        public bool IsStructural(byte materialIndex) =>
            materialIndex < 32 && (StructuralMask & (1u << materialIndex)) != 0;
    }
}
