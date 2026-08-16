namespace Game.Materials.Api
{
    /// <summary>
    /// Stable material identity vocabulary owned by this game.
    /// Numeric values participate in world/catalogue identity and must never be silently reassigned.
    /// Engine modules consume these values only as opaque material indices.
    /// </summary>
    public static class GameMaterialIds
    {
        public const byte Empty = 0;
        public const byte Stone = 1;
        public const byte Wood = 2;
        public const byte Sand = 3;
        public const byte Glass = 4;
        public const byte Bedrock = 5;
        public const byte DarkStone = 6;
        public const byte Slate = 7;
        public const byte Tile = 8;
        public const byte Cloth = 9;
        public const byte Grass = 10;
        public const byte Water = 11;
        public const byte Gold = 12;
        public const byte Dirt = 13;
        public const byte Moss = 14;
        public const byte LitWindow = 15;
        public const byte Cascade = 16;
        public const byte Crystal = 17;
        public const byte MasonrySmall = 18;
        public const byte MasonryMedium = 19;
        public const byte MasonryLarge = 20;
        public const byte FlowerWhite = 21;

        public const byte TerrainTurf = Grass;
        public const byte TerrainLimestone = MasonryMedium;
        public const byte TerrainEarth = Dirt;
        public const byte TerrainPathStone = MasonrySmall;

        // Transitional aliases inherited from the current presentation-row sharing scheme.
        // Before these materials gain divergent gameplay properties, assign distinct stable IDs
        // and share presentation resources rather than semantic identity.
        public const byte FlowerYellow = Gold;
        public const byte FlowerPink = Cloth;
        public const byte FlowerBlue = Cascade;
    }
}
