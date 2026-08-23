using System.Collections;
using System.IO;
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
            PublishReferenceForVisualEvidence();

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

        private static void PublishReferenceForVisualEvidence()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.Combine(projectRoot, "References", "arch_reference.png");
            Assert.That(File.Exists(source), Is.True,
                $"Arch visual acceptance requires the tracked reference image at {source}.");

            // The single-test workflow uploads Artifacts/SingleTest/** after the editor assertion
            // and real-player capture phases. Put the source artwork under the RealPlayer root so
            // a remote reviewer receives the reference beside the screenshots generated from the
            // production standalone app, even when repository binary fetches are unavailable.
            string evidenceDirectory = Path.Combine(
                projectRoot, "Artifacts", "SingleTest", "RealPlayer", "Reference");
            Directory.CreateDirectory(evidenceDirectory);
            File.Copy(source, Path.Combine(evidenceDirectory, "arch_reference.png"), true);
        }
    }
}
