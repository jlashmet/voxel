using Game.Materials.Runtime;
using UnityEngine;
using VoxelEngine.Composition;

namespace Game.Composition.Materials
{
    /// <summary>
    /// Installs this game's material presentation before scenes begin constructing renderer state.
    /// The engine receives only semantic-free presentation rows.
    /// </summary>
    public static class GameMaterialPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() =>
            MaterialPresentationComposition.Apply(GameMaterialRenderingDefinitions.Create());
    }
}
