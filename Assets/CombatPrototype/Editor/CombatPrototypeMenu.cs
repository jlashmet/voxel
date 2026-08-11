using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MountingForce.CombatPrototype.Editor
{
    public static class CombatPrototypeMenu
    {
        private const string MenuPath = "Mounting Force/Chain Combat Prototype/Open & Play";

        [MenuItem(MenuPath)]
        private static void OpenAndPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Chain Combat Prototype: stop Play Mode before reopening the prototype.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("Chain Combat Cascade Lab");
            root.AddComponent<ChainCombatLabController>();
            root.AddComponent<ChainCombatSetupActionsPanel>();
            root.AddComponent<ChainCombatEventMarker>();
            root.AddComponent<ChainCombatMotionPlayback>();
            EditorApplication.isPlaying = true;
        }
    }
}
