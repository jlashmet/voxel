namespace VoxelEngine.Composition.Api
{
    /// <summary>
    /// Opaque material indices required by the showcase world's generation, worldgen projection,
    /// and structural rules. The engine understands roles only; the game decides which semantic
    /// material occupies each role.
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

        public readonly byte WorldgenFoundation;
        public readonly byte WorldgenMasonry;
        public readonly byte WorldgenDarkMasonry;
        public readonly byte WorldgenTimber;
        public readonly byte WorldgenGlass;
        public readonly byte WorldgenWarmWindow;
        public readonly byte WorldgenRoofTile;
        public readonly byte WorldgenSlate;
        public readonly byte WorldgenCloth;
        public readonly byte WorldgenMoss;
        public readonly byte WorldgenWater;
        public readonly byte WorldgenRoadSurface;

        public readonly uint StructuralMask;

        public ShowcaseMaterialSet(
            byte terrainDeep,
            byte terrainSubsurface,
            byte terrainLowSurface,
            byte terrainHighSurface,
            byte gate,
            byte referenceArch,
            byte farStructure,
            byte worldgenFoundation,
            byte worldgenMasonry,
            byte worldgenDarkMasonry,
            byte worldgenTimber,
            byte worldgenGlass,
            byte worldgenWarmWindow,
            byte worldgenRoofTile,
            byte worldgenSlate,
            byte worldgenCloth,
            byte worldgenMoss,
            byte worldgenWater,
            byte worldgenRoadSurface,
            uint structuralMask)
        {
            TerrainDeep = terrainDeep;
            TerrainSubsurface = terrainSubsurface;
            TerrainLowSurface = terrainLowSurface;
            TerrainHighSurface = terrainHighSurface;
            Gate = gate;
            ReferenceArch = referenceArch;
            FarStructure = farStructure;
            WorldgenFoundation = worldgenFoundation;
            WorldgenMasonry = worldgenMasonry;
            WorldgenDarkMasonry = worldgenDarkMasonry;
            WorldgenTimber = worldgenTimber;
            WorldgenGlass = worldgenGlass;
            WorldgenWarmWindow = worldgenWarmWindow;
            WorldgenRoofTile = worldgenRoofTile;
            WorldgenSlate = worldgenSlate;
            WorldgenCloth = worldgenCloth;
            WorldgenMoss = worldgenMoss;
            WorldgenWater = worldgenWater;
            WorldgenRoadSurface = worldgenRoadSurface;
            StructuralMask = structuralMask;
        }

        public byte SurfaceAt(int surfaceHeight, int splitHeight) =>
            surfaceHeight < splitHeight ? TerrainLowSurface : TerrainHighSurface;

        public bool IsStructural(byte materialIndex) =>
            materialIndex < 32 && (StructuralMask & (1u << materialIndex)) != 0;
    }
}
