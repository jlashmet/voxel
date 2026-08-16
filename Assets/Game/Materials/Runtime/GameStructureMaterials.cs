using Game.Materials.Api;
using VoxelEngine.Structures.Api;

namespace Game.Materials.Runtime
{
    /// <summary>Game-owned binding from semantic materials to generic structure-generation roles.</summary>
    public static class GameStructureMaterials
    {
        public static readonly StructureMaterialSet Default = new(
            @void: GameMaterialIds.Empty,
            primaryMasonry: GameMaterialIds.Stone,
            timber: GameMaterialIds.Wood,
            looseAggregate: GameMaterialIds.Sand,
            transparentInfill: GameMaterialIds.Glass,
            indestructibleBase: GameMaterialIds.Bedrock,
            darkMasonry: GameMaterialIds.DarkStone,
            slateRoof: GameMaterialIds.Slate,
            tileRoof: GameMaterialIds.Tile,
            textileAccent: GameMaterialIds.Cloth,
            groundCover: GameMaterialIds.Grass,
            water: GameMaterialIds.Water,
            metalAccent: GameMaterialIds.Gold,
            earth: GameMaterialIds.Dirt,
            overgrowth: GameMaterialIds.Moss,
            warmWindow: GameMaterialIds.LitWindow,
            aeratedWater: GameMaterialIds.Cascade,
            coolEmissiveAccent: GameMaterialIds.Crystal,
            fineMasonry: GameMaterialIds.MasonrySmall,
            mediumMasonry: GameMaterialIds.MasonryMedium,
            largeMasonry: GameMaterialIds.MasonryLarge,
            paleFlora: GameMaterialIds.FlowerWhite);
    }
}
