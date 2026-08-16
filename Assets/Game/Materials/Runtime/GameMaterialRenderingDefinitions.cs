using VoxelEngine.Rendering.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Compiles the game-owned material catalogue into Rendering's semantic-free GPU rows.
    /// </summary>
    public static class GameMaterialRenderingDefinitions
    {
        public const int Count = GameMaterialRuntimeCatalogue.Count;

        public static MaterialPresentationDefinition[] Create()
        {
            var result = new MaterialPresentationDefinition[Count];
            for (byte materialId = 0; materialId < Count; materialId++)
                result[materialId] = GameMaterialRuntimeCatalogue.Get(materialId).Rendering;
            return result;
        }
    }
}
