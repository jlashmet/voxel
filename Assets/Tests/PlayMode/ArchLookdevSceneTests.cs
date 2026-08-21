using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: this is a visual acceptance entry point and
    /// one of the slowest things in the suite. The test itself proves the production surface reaches
    /// a stable rendered state. When selected through the single-test workflow, the shared
    /// real-player capture utility builds <c>ArchLookdev.unity</c> as a standalone app and publishes
    /// presented-frame screenshots every ten seconds.
    /// </remarks>
    [NUnit.Framework.Explicit("Visual acceptance for human review; run by name.")]
    public sealed class ArchLookdevSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator SceneBuildsHeroThroughProductionSurfacePath()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/ArchLookdev.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("ArchLookdev", LoadSceneMode.Single);
#endif
            yield return null;
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            Assert.NotNull(lookdev);
            Assert.True(VoxelRenderBridge.TryGetWorld(out var world));
            Assert.NotNull(world.ProfileBlocks);
            Assert.Greater(world.ProfileBlocks.Count, 0);

            // Batch PlayMode does not submit the Game view automatically. Keep explicit camera
            // submissions so surface extraction advances, but do not read these pixels back or
            // persist an editor screenshot. Human-review images come only from the standalone app.
            Camera camera = lookdev.SceneCamera;
            Assert.NotNull(camera);
            var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
            target.Create();
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;

            int stableFrames = 0;
            for (int frame = 0; frame < 240 && stableFrames < 3; frame++)
            {
                camera.Render();
                yield return null;

                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                bool converged = metrics.SolidKnownChunks > 0
                    && metrics.SolidDirtyChunks == 0
                    && metrics.SolidResidentChunks >= metrics.SolidKnownChunks;
                stableFrames = converged ? stableFrames + 1 : 0;
            }

            camera.targetTexture = previousTarget;
            target.Release();
            Object.DestroyImmediate(target);

            VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
            Assert.GreaterOrEqual(stableFrames, 3,
                $"Arch lookdev production surface did not converge: "
              + $"known={finalMetrics.SolidKnownChunks}, "
              + $"resident={finalMetrics.SolidResidentChunks}, "
              + $"dirty={finalMetrics.SolidDirtyChunks}.");
        }
    }
}
