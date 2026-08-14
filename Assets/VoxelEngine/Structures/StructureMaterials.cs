namespace VoxelEngine.Structures
{
    /// <summary>
    /// Palette indices for authored structures and authored environment materials.
    /// IDs are semantic voxel materials; presentation is supplied by VoxelPresentationCatalogue.
    /// </summary>
    public static class Mat
    {
        public const byte Empty = 0;

        public const byte Stone = 1;        // main masonry
        public const byte Wood = 2;         // timber, doors, floors
        public const byte Sand = 3;
        public const byte Glass = 4;
        public const byte Bedrock = 5;

        public const byte DarkStone = 6;    // plinths, foundations, cliff face
        public const byte Slate = 7;        // tower roofs
        public const byte Tile = 8;         // hall roofs, warmer
        public const byte Cloth = 9;        // banners
        public const byte Grass = 10;       // ground cover
        public const byte Water = 11;       // moat, river, cave pools
        public const byte Gold = 12;        // finials, treasure
        public const byte Dirt = 13;        // paths, cave floor
        public const byte Moss = 14;        // weathering on old stone
        public const byte LitWindow = 15;   // dark leaded exterior glazing with warm interior
        public const byte Cascade = 16;     // bright aerated vertical waterfall surface
        public const byte Crystal = 17;     // cool emissive cave crystal
        public const byte MasonrySmall = 18;
        public const byte MasonryMedium = 19; // warm 40 cm dressed limestone
        public const byte MasonryLarge = 20;

        // Terrain authoring names intentionally reuse compatible rows from the existing voxel
        // presentation catalogue. Only the pale flower needs a new neutral row; everything else
        // keeps the established texture arrays, surface response and production shader path.
        public const byte TerrainTurf = Grass;
        public const byte TerrainLimestone = MasonryMedium;
        public const byte TerrainEarth = Dirt;
        public const byte TerrainPathStone = MasonrySmall;
        public const byte FlowerWhite = 21;
        public const byte FlowerYellow = Gold;
        public const byte FlowerPink = Cloth;
        public const byte FlowerBlue = Cascade;
    }
}
