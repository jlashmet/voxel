using Game.Materials.Runtime;
using VoxelEngine.Composition;

namespace Game.Composition.Materials
{
    /// <summary>
    /// Single application composition entry point for the game's material catalogue. Runtime and
    /// Editor bootstraps both call this method so rendering and all semantic-to-role projections
    /// cannot drift between modes.
    /// </summary>
    public static class GameMaterialComposition
    {
        public static void Install()
        {
            MaterialPresentationComposition.Apply(GameMaterialRenderingDefinitions.Create());
            TerrainMaterialComposition.Configure(in GameTerrainMaterials.Default);
            ShowcaseMaterialComposition.Configure(in GameShowcaseMaterials.Default);
            StructureMaterialComposition.Configure(in GameStructureMaterials.Default);
        }
    }
}
