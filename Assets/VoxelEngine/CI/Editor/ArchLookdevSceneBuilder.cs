using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Showcase;

namespace VoxelEngine.CI
{
    public static class ArchLookdevSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/ArchLookdev.unity";

        [MenuItem("Voxel Engine/Build Arch Lookdev Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                       NewSceneMode.Single);
            var cameraObject = new GameObject("Hero Arch Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.depth = 0;
            cameraObject.AddComponent<ArchLookdev>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException($"Could not save {ScenePath}");
            AssetDatabase.SaveAssets();
            Debug.Log($"Arch look-development scene written to {ScenePath}");
        }
    }
}
