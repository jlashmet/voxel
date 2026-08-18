using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Boots the exact worldbuilding gallery scene through its production MonoBehaviour path.
    /// This is intentionally a smoke test: CI must prove the showcase itself enables, binds a
    /// rendering world, and begins publishing resident surface geometry without throwing.
    /// </summary>
    public sealed class WorldbuildingGalleryShowcaseSmokeTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator WorldbuildingGallerySceneBootsAndPublishesGeometry()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/WorldbuildingGalleryShowcase.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("WorldbuildingGalleryShowcase", LoadSceneMode.Single);
#endif
            yield return null;

            WorldbuildingGalleryShowcase showcase =
                Object.FindAnyObjectByType<WorldbuildingGalleryShowcase>();
            Assert.NotNull(showcase, "Worldbuilding gallery driver was not present after scene load.");

            Camera camera = showcase.GetComponent<Camera>();
            Assert.NotNull(camera, "Worldbuilding gallery driver must run on its production camera.");
            Assert.True(camera.enabled, "Worldbuilding gallery camera should be enabled after boot.");

            bool worldBound = false;
            bool geometryPublished = false;
            for (int frame = 0; frame < 900; frame++)
            {
                if (VoxelRenderBridge.TryGetWorld(out var world))
                {
                    worldBound = world.ProfileBlocks != null && world.ProfileBlocks.Count > 0;

                    var metrics = VoxelRenderBridge.SurfaceMetrics;
                    geometryPublished = metrics.SolidKnownChunks > 0 &&
                                        metrics.SolidResidentChunks > 0;
                    if (worldBound && geometryPublished)
                        yield break;
                }

                yield return null;
            }

            var finalMetrics = VoxelRenderBridge.SurfaceMetrics;
            Assert.True(worldBound,
                "Worldbuilding gallery never bound its production rendering world.");
            Assert.True(geometryPublished,
                $"Worldbuilding gallery never published resident geometry: " +
                $"known={finalMetrics.SolidKnownChunks}, " +
                $"resident={finalMetrics.SolidResidentChunks}, " +
                $"dirty={finalMetrics.SolidDirtyChunks}.");
        }
    }
}
