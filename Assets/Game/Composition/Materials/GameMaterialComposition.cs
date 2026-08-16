using Game.Materials.Runtime;
using VoxelEngine.Composition;

namespace Game.Composition.Materials
{
    /// <summary>
    /// Single application composition entry point for the game's installed material presentation.
    /// Runtime and Editor bootstraps both call this method so the renderer sees the same game-owned
    /// projection in both modes.
    /// </summary>
    public static class GameMaterialComposition
    {
        public static void Install()
        {
            MaterialPresentationComposition.Apply(GameMaterialRenderingDefinitions.Create());
        }
    }
}
