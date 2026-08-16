using UnityEngine;

namespace Game.Composition.Materials
{
    /// <summary>
    /// Installs this game's material presentation and semantic-to-role bindings before scenes begin
    /// constructing engine state.
    /// </summary>
    public static class GameMaterialPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => GameMaterialComposition.Install();
    }
}
