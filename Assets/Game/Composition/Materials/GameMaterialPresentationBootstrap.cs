using Game.Materials.Runtime;
using UnityEngine;
using VoxelEngine.Composition;

namespace Game.Composition.Materials
{
    /// <summary>
    /// Installs this game's material presentation and semantic-to-role bindings before scenes begin
    /// constructing engine state. Engine systems receive only semantic-free rows and opaque roles.
    /// </summary>
    public static class GameMaterialPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            MaterialPresentationComposition.Apply(GameMaterialRenderingDefinitions.Create());
            TerrainMaterialComposition.Configure(in GameTerrainMaterials.Default);
            ShowcaseMaterialComposition.Configure(in GameShowcaseMaterials.Default);
            StructureMaterialComposition.Configure(in GameStructureMaterials.Default);
        }
    }
}
