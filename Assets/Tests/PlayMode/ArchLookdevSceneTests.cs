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

            // Preserve the original visual acceptance's render-driving behavior: assigning the
            // target lets Unity's normal frame loop submit the camera. Do not read pixels back or
            // persist an editor screenshot; review images come only from the standalone app.
            Camera camera = lookdev.GetComponent<Camera>();
            var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            try
            {
                int stableFrames = 0;
                for (int frame = 0; frame < 240; frame++)
                {
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    bool converged = metrics.SolidKnownChunks > 0
                        && metrics.SolidDirtyChunks == 0
                        && metrics.SolidResidentChunks >= metrics.SolidKnownChunks;
                    stableFrames = converged ? stableFrames + 1 : 0;
                    if (stableFrames >= 3)
                        yield break;
                    yield return null;
                }

                VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.Fail($"Arch lookdev production surface did not converge: "
                          + $"known={finalMetrics.SolidKnownChunks}, "
                          + $"resident={finalMetrics.SolidResidentChunks}, "
                          + $"dirty={finalMetrics.SolidDirtyChunks}.");
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator IndexedIndirectContractProbeShaderCompiles()
        {
            Assert.AreEqual(UnityEngine.Rendering.GraphicsDeviceType.Metal,
                SystemInfo.graphicsDeviceType,
                "the indexed-indirect contract experiment must execute on the Metal backend");
            Shader shader = Resources.Load<Shader>("IndexedIndirectContract");
            Assert.NotNull(shader, "standalone indexed-indirect probe shader was not included");
            Assert.IsTrue(shader.isSupported, "standalone indexed-indirect probe shader is unsupported");
            yield return null;
        }
    }
}
