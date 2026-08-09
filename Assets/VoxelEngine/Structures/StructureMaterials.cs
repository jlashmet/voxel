namespace VoxelEngine.Structures
{
    /// <summary>
    /// Palette indices for authored structures.
    ///
    /// A castle read as programmer art partly because it had three materials. Stone that is all
    /// one grey has no plinth, no string course, no weathering — the eye needs the bands to read
    /// masonry rather than a extruded rectangle.
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
    }
}
