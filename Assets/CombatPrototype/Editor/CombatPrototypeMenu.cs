using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MountingForce.CombatPrototype.Editor
{
    public static class CombatPrototypeMenu
    {
        private const string DemoMenuPath = "Mounting Force/Combat Demo/Open & Play";
        private const string LegacyMenuPath = "Mounting Force/Chain Combat Prototype/Open & Play";

        [MenuItem(DemoMenuPath)]
        private static void OpenCombatDemo()
        {
            OpenAndPlay();
        }

        [MenuItem(LegacyMenuPath)]
        private static void OpenLegacyCombatPrototype()
        {
            OpenAndPlay();
        }

        private static void OpenAndPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Chain Combat Demo: stop Play Mode before reopening the demo.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("Chain Combat Demo");
            root.AddComponent<ChainCombatLabController>();
            root.AddComponent<ChainExecutionPlanner>();
            root.AddComponent<ChainPlanApprovalCoordinator>();
            root.AddComponent<ChainCombatActivationOverlay>();
            root.AddComponent<ChainCombatEventMarker>();
            root.AddComponent<ChainCombatMotionPlayback>();
            root.AddComponent<ChainEnemyIntentOverlay>();
            root.AddComponent<ChainCombatDemoGuide>();
            EditorApplication.isPlaying = true;
        }
    }
}
