using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
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
                // Batch PlayMode can advance hundreds of frames per second. A frame-count timeout
                // therefore rejected the scene after < 1 second even though the standalone player
                // reaches READY around 12 seconds. Bound convergence by wall-clock time instead.
                const float convergenceTimeoutSeconds = 30f;
                float deadline = Time.realtimeSinceStartup + convergenceTimeoutSeconds;
                int stableFrames = 0;
                while (Time.realtimeSinceStartup < deadline)
                {
                    // The scheduler deliberately retains discovered off-camera candidates without
                    // requiring them all to be resident. The shared coverage contract is therefore
                    // the right base gate, but this visual test is stricter than near/far handoff:
                    // the Arch camera must actually be drawing at least one solid chunk too.
                    RenderingComposition.GetVoxelSurfaceCounts(
                        out int visibleChunks, out int missingVisibleChunks);
                    bool converged = visibleChunks > 0
                        && missingVisibleChunks == 0
                        && RenderingComposition.HasCompletePublishedNearSurfaceCoverage();
                    stableFrames = converged ? stableFrames + 1 : 0;
                    if (stableFrames >= 3)
                        yield break;
                    yield return null;
                }

                VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.Fail($"Arch lookdev production surface did not reach complete visible coverage within "
                          + $"{convergenceTimeoutSeconds:0}s: "
                          + $"known={finalMetrics.SolidKnownChunks}, "
                          + $"resident={finalMetrics.SolidResidentChunks}, "
                          + $"dirty={finalMetrics.SolidDirtyChunks}, "
                          + $"visible={finalMetrics.VisibleSolidChunks}, "
                          + $"missing={finalMetrics.MissingVisibleSolidChunks}.");
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}