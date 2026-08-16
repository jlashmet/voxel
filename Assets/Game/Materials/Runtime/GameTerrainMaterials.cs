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
        public static readonly TerrainMaterialSet Default = new TerrainMaterialSet(
            GameMaterialIds.Bedrock,
            GameMaterialIds.Stone,
            GameMaterialIds.Sand);
    }
}
