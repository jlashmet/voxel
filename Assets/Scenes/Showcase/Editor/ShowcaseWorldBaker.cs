using System;
using System.IO;
using Game.Composition.Materials;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Composition;
using VoxelEngine.Tiering.Api;

namespace VoxelEngine.Showcase.Editor
{
    /// <summary>
    /// Pays the procedural showcase cost in the editor and writes a portable semantic startup
    /// image into Resources. Gameplay only restores that image; it never authors the castle.
    /// </summary>
    public static class ShowcaseWorldBaker
    {
        private const string ShowcaseScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const string OutputAssetPath =
            "Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes";

        // Three 51.2 m region rings give the player a solid startup neighbourhood while keeping
        // the asset bounded. The complete castle footprint is always included even when it
        // extends outside this radius. The normal streamer owns everything beyond this disc.
        private const int StartupRadiusRegions = 3;

        [MenuItem("Tools/Voxel Engine/Bake Showcase World")]
        public static void BakeShowcaseWorld()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "Bake the showcase world while the editor is not entering or running Play mode.");

            VoxelShowcase showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>(
                FindObjectsInactive.Include);
            if (showcase == null && Application.isBatchMode)
            {
                // CI starts from a clean checkout and an arbitrary editor scene. The bake command
                // owns its source scene in batch mode so tests/builds never depend on whatever the
                // runner happened to have open before Unity launched.
                EditorSceneManager.OpenScene(ShowcaseScenePath, OpenSceneMode.Single);
                showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>(
                    FindObjectsInactive.Include);
            }

            if (showcase == null)
            {
                const string message =
                    "Open Assets/Scenes/VoxelShowcase.unity, then run the bake command again.";
                if (Application.isBatchMode)
                    throw new InvalidOperationException(message);

                EditorUtility.DisplayDialog("Voxel Showcase Bake", message, "OK");
                return;
            }

            var serialized = new SerializedObject(showcase);
            serialized.UpdateIfRequiredOrScript();
            uint seed = unchecked((uint)RequireProperty(serialized, "m_Seed").intValue);
            int requestedCapacity = RequireProperty(serialized, "m_BrickPoolCapacity").intValue;
            int loadRadius = RequireProperty(serialized, "m_LoadRadiusRegions").intValue;
            int unloadRadius = RequireProperty(serialized, "m_UnloadRadiusRegions").intValue;
            int startupRadius = Mathf.Clamp(StartupRadiusRegions, 0, loadRadius);

            int tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int capacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                requestedCapacity, tierBytes);

            try
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayProgressBar(
                        "Voxel Showcase Bake",
                        "Generating terrain, castle, and startup regions…",
                        0.15f);

                using var world = new ShowcaseWorld(
                    seed,
                    capacity,
                    loadRadius,
                    unloadRadius,
                    GameMaterialComposition.SimulationDefinitions(),
                    tierBytes);

                world.GenerateForBakeBlocking(startupRadius);

                if (!Application.isBatchMode)
                    EditorUtility.DisplayProgressBar(
                        "Voxel Showcase Bake",
                        "Capturing semantic region snapshots…",
                        0.75f);
                ShowcaseWorldBake bake = world.CaptureBake(startupRadius);
                byte[] bytes = ShowcaseWorldBakeCodec.Serialize(bake);

                string directory = Path.GetDirectoryName(OutputAssetPath);
                if (string.IsNullOrEmpty(directory))
                    throw new InvalidOperationException("Invalid showcase bake output path.");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(OutputAssetPath, bytes);
                AssetDatabase.ImportAsset(OutputAssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();

                float mebibytes = bytes.Length / (1024f * 1024f);
                Debug.Log(
                    $"Baked Voxel Showcase startup world: {bake.Regions.Count} regions, " +
                    $"{mebibytes:F1} MiB, seed 0x{bake.Seed:X8}, " +
                    $"castle voxels {bake.CastleVoxels:N0}. Asset: {OutputAssetPath}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog(
                        "Voxel Showcase Bake Failed",
                        ex.Message,
                        "OK");
                throw;
            }
            finally
            {
                if (!Application.isBatchMode)
                    EditorUtility.ClearProgressBar();
            }
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
                throw new MissingFieldException(
                    $"VoxelShowcase no longer has serialized field '{name}'. Update the baker.");
            return property;
        }
    }
}
