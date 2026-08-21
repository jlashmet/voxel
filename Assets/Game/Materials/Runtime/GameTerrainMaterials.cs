using Game.Materials.Api;
using VoxelEngine.Terrain.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Game-owned semantic assignment for the engine's generic terrain material slots.
    /// Terrain itself only consumes the resulting opaque material indices.
    /// </summary>
    public static class GameTerrainMaterials
    {
        /// <summary>
        /// Ground cover for the inhabited valley: turf on the rises, bare earth in the hollows,
        /// subsoil beneath both, bedrock at depth.
        ///
        /// This was bedrock/stone/sand, which surfaced the whole basin in rock and sand and made a
        /// temperate farmland valley read as a quarry floor. Sand is a shoreline material and is
        /// left to the features that actually author a shoreline; it is not what a settled valley
        /// is made of.
        /// </summary>
        public static readonly TerrainMaterialSet Default = new TerrainMaterialSet(
            deep: GameMaterialIds.Bedrock,
            subsurface: GameMaterialIds.Dirt,
            lowSurface: GameMaterialIds.Dirt,
            surface: GameMaterialIds.Grass);
    }
}
