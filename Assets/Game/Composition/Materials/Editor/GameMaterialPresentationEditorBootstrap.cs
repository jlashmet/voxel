using UnityEditor;

namespace Game.Composition.Materials.Editor
{
    /// <summary>
    /// Keeps edit-mode lookdev and capture tools on the same material composition as Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class GameMaterialPresentationEditorBootstrap
    {
        static GameMaterialPresentationEditorBootstrap() => GameMaterialComposition.Install();
    }
}
