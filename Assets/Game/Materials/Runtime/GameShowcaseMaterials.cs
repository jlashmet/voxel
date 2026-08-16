using Game.Materials.Api;
using VoxelEngine.Composition.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Game-owned binding from semantic material identity to the showcase engine's opaque roles.
    /// Changing these choices is game/content policy; ShowcaseWorld never names the materials.
    /// </summary>
    public static class GameShowcaseMaterials
    {
        private const uint StructuralMask =
            (1u << GameMaterialIds.Wood) |
            (1u << GameMaterialIds.Glass) |
            (1u << GameMaterialIds.DarkStone) |
            (1u << GameMaterialIds.Slate) |
            (1u << GameMaterialIds.Tile) |
            (1u << GameMaterialIds.Cloth) |
            (1u << GameMaterialIds.Gold) |
            (1u << GameMaterialIds.LitWindow);

        public static readonly ShowcaseMaterialSet Default = new(
            terrainDeep: GameMaterialIds.Bedrock,
            terrainSubsurface: GameMaterialIds.Stone,
            terrainLowSurface: GameMaterialIds.Sand,
            terrainHighSurface: GameMaterialIds.Grass,
            gate: GameMaterialIds.Wood,
            referenceArch: GameMaterialIds.DarkStone,
            farStructure: GameMaterialIds.Stone,
            worldgenFoundation: GameMaterialIds.Stone,
            worldgenMasonry: GameMaterialIds.Stone,
            worldgenDarkMasonry: GameMaterialIds.DarkStone,
            worldgenTimber: GameMaterialIds.Wood,
            worldgenGlass: GameMaterialIds.Glass,
            worldgenWarmWindow: GameMaterialIds.LitWindow,
            worldgenRoofTile: GameMaterialIds.Tile,
            worldgenSlate: GameMaterialIds.Slate,
            worldgenCloth: GameMaterialIds.Cloth,
            worldgenMoss: GameMaterialIds.Moss,
            worldgenWater: GameMaterialIds.Water,
            worldgenRoadSurface: GameMaterialIds.Dirt,
            structuralMask: StructuralMask);
    }
}
