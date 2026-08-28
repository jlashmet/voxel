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

        private const string GalleryScenePath =
            "Assets/Scenes/WorldbuildingGalleryShowcase.unity";
        private const string GalleryOutputAssetPath =
            "Assets/Resources/WorldbuildingGallery/GalleryWorld.bytes";

        // Five 51.2 m region rings — 256 m — give the player a solid startup neighbourhood while
        // keeping the asset bounded. The complete castle footprint is always included even when it
        // extends outside this radius. The normal streamer owns everything beyond this disc.
        //
        // Three rings was not enough to hand over cleanly. Generation is budget-sliced on the main
        // thread and the queue is ~170 regions deep at spawn, so the streamer needs roughly 18
        // seconds to reach 200 m in every direction. Anything inside that horizon has to come from
        // the bake or the player walks into ground that is still being generated.
        //
        // Five was not enough either, and the handover never completed at all. The showcase
        // streams to eight regions but generates at 3 ms a frame, so the gap between the baked
        // disc and the streamed radius is produced at a few seconds of work per real minute: a
        // 420-second stationary run held resident ground at 262.6 m — the baked radius exactly —
        // and never advanced. The voxel LOD rings meanwhile claim the full 409.6 m, so they were
        // asking for terrain that was never going to exist. Baking the whole streamed radius
        // removes the handover instead of tuning it.
        private const int StartupRadiusRegions = 8;

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

            long tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
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

        /// <summary>
        /// Pays the gallery's startup cost offline.
        ///
        /// The gallery authors a castle, seven exhibit structures, a promenade, a cave walk and two
        /// furnished guild houses before it can show a frame, and it did all of it on entering Play
        /// mode. It also streams the same radius the original showcase does, so it inherits the
        /// same lesson: a baked disc smaller than the streamed radius leaves ground the budgeted
        /// streamer never catches up on.
        /// </summary>
        [MenuItem("Tools/Voxel Engine/Bake Worldbuilding Gallery World")]
        public static void BakeWorldbuildingGalleryWorld()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "Bake the gallery world while the editor is not entering or running Play mode.");

            WorldbuildingGalleryShowcase gallery =
                UnityEngine.Object.FindFirstObjectByType<WorldbuildingGalleryShowcase>(
                    FindObjectsInactive.Include);
            if (gallery == null && Application.isBatchMode)
            {
                EditorSceneManager.OpenScene(GalleryScenePath, OpenSceneMode.Single);
                gallery = UnityEngine.Object.FindFirstObjectByType<WorldbuildingGalleryShowcase>(
                    FindObjectsInactive.Include);
            }

            if (gallery == null)
            {
                const string message =
                    "Open Assets/Scenes/WorldbuildingGalleryShowcase.unity, then run the bake "
                    + "command again.";
                if (Application.isBatchMode)
                    throw new InvalidOperationException(message);

                EditorUtility.DisplayDialog("Worldbuilding Gallery Bake", message, "OK");
                return;
            }

            var serialized = new SerializedObject(gallery);
            serialized.UpdateIfRequiredOrScript();
            uint seed = unchecked((uint)RequireProperty(serialized, "m_Seed").intValue);
            int requestedCapacity = RequireProperty(serialized, "m_BrickPoolCapacity").intValue;
            int loadRadius = RequireProperty(serialized, "m_LoadRadiusRegions").intValue;
            int unloadRadius = RequireProperty(serialized, "m_UnloadRadiusRegions").intValue;
            int startupRadius = Mathf.Clamp(StartupRadiusRegions, 0, loadRadius);

            long tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int capacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                requestedCapacity, tierBytes);

            try
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayProgressBar(
                        "Worldbuilding Gallery Bake",
                        "Generating terrain, castle, exhibits and guild houses…",
                        0.15f);

                // Generate, explicitly: the whole point of this command is to run the source
                // authoring the scene no longer runs. Left on the default the world would try to
                // restore a bake in order to produce one.
                using var world = new ShowcaseWorld(
                    seed,
                    capacity,
                    loadRadius,
                    unloadRadius,
                    GameMaterialComposition.SimulationDefinitions(),
                    tierBytes,
                    ShowcaseFeatureContent.Full,
                    ShowcaseStartupSource.Generate);

                world.GenerateGalleryForBakeBlocking(startupRadius);

                if (!Application.isBatchMode)
                    EditorUtility.DisplayProgressBar(
                        "Worldbuilding Gallery Bake",
                        "Capturing semantic region snapshots…",
                        0.75f);
                ShowcaseWorldBake bake = world.CaptureBake(startupRadius);
                if (!bake.HasGallery)
                    throw new InvalidOperationException(
                        "Gallery bake captured no gallery district; refusing to write it.");

                byte[] bytes = ShowcaseWorldBakeCodec.Serialize(bake);

                string directory = Path.GetDirectoryName(GalleryOutputAssetPath);
                if (string.IsNullOrEmpty(directory))
                    throw new InvalidOperationException("Invalid gallery bake output path.");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(GalleryOutputAssetPath, bytes);
                AssetDatabase.ImportAsset(GalleryOutputAssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();

                float mebibytes = bytes.Length / (1024f * 1024f);
                Debug.Log(
                    $"Baked Worldbuilding Gallery startup world: {bake.Regions.Count} regions, "
                    + $"{mebibytes:F1} MiB, seed 0x{bake.Seed:X8}, "
                    + $"castle voxels {bake.CastleVoxels:N0}, cave chamber at "
                    + $"{bake.GalleryCavePathEnd}. Asset: {GalleryOutputAssetPath}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Worldbuilding Gallery Bake Failed", ex.Message, "OK");
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
