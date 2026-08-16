using Game.Materials.Runtime;
using UnityEditor;
using VoxelEngine.Composition;

namespace Game.Composition.Materials.Editor
{
    /// <summary>
    /// Keeps edit-mode lookdev and capture tools on the same game-owned material presentation and
    /// role bindings as Play Mode. Loading this editor assembly is enough; no Rendering.Runtime
    /// dependency escapes the Composition boundary.
    /// </summary>
    [InitializeOnLoad]
    public static class GameMaterialPresentationEditorBootstrap
    {
        static GameMaterialPresentationEditorBootstrap()
        {
            MaterialPresentationComposition.Apply(GameMaterialRenderingDefinitions.Create());
            TerrainMaterialComposition.Configure(in GameTerrainMaterials.Default);
            ShowcaseMaterialComposition.Configure(in GameShowcaseMaterials.Default);
            StructureMaterialComposition.Configure(in GameStructureMaterials.Default);
        }
    }
}
