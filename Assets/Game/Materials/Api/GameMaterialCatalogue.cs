namespace Game.Materials.Api
{
    /// <summary>
    /// Canonical game-facing metadata for the stable material vocabulary.
    /// Keep semantic names and player-facing groupings here rather than in engine modules.
    /// </summary>
    public static class GameMaterialCatalogue
    {
        public const int Count = 22;
        public const int BuildableCount = 4;

        public static bool IsCanonicalId(byte materialId) => materialId < Count;

        public static string NameOf(byte materialId)
        {
            switch (materialId)
            {
                case GameMaterialIds.Empty: return "empty";
                case GameMaterialIds.Stone: return "stone";
                case GameMaterialIds.Wood: return "wood";
                case GameMaterialIds.Sand: return "sand";
                case GameMaterialIds.Glass: return "glass";
                case GameMaterialIds.Bedrock: return "bedrock";
                case GameMaterialIds.DarkStone: return "dark stone";
                case GameMaterialIds.Slate: return "slate";
                case GameMaterialIds.Tile: return "tile";
                case GameMaterialIds.Cloth: return "cloth";
                case GameMaterialIds.Grass: return "grass";
                case GameMaterialIds.Water: return "water";
                case GameMaterialIds.Gold: return "gold";
                case GameMaterialIds.Dirt: return "dirt";
                case GameMaterialIds.Moss: return "moss";
                case GameMaterialIds.LitWindow: return "lit window";
                case GameMaterialIds.Cascade: return "cascade";
                case GameMaterialIds.Crystal: return "crystal";
                case GameMaterialIds.MasonrySmall: return "masonry small";
                case GameMaterialIds.MasonryMedium: return "masonry medium";
                case GameMaterialIds.MasonryLarge: return "masonry large";
                case GameMaterialIds.FlowerWhite: return "flower white";
                default: return "unknown";
            }
        }

        /// <summary>Player-buildable materials in stable hotkey order.</summary>
        public static byte BuildableAt(int index)
        {
            switch (index)
            {
                case 0: return GameMaterialIds.Stone;
                case 1: return GameMaterialIds.Wood;
                case 2: return GameMaterialIds.Sand;
                case 3: return GameMaterialIds.Glass;
                default: return GameMaterialIds.Empty;
            }
        }
    }
}
