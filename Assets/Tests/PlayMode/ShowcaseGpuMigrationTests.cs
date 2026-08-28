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
    /// <summary>
    /// Guards the architectural direction of SceneIssue 20260825-192751-413: supported near terrain
    /// must actually leave the CPU mesher and complete through the production GPU surface backend.
    /// This is intentionally separate from the existing traversal percentile regression so a later
    /// fallback cannot make performance tests green by silently returning to CPU extraction.
    /// </summary>
    public sealed class ShowcaseGpuMigrationTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(900000)]
        public IEnumerator MovingShowcaseActuallyCompletesGpuSurfaceBuilds()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(camera);

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseGpuMigrationTests.GpuCutover",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                Vector3 origin = showcase.transform.position;
                ulong initialGpuCompleted = VoxelRenderBridge.SurfaceMetrics.GpuCompletedSolidBuilds;
                bool sawGpuBackend = false;
                bool sawGpuCompletion = false;
                VoxelSurfaceMetrics last = default;

                // Move far enough to force new near-ring demand rather than merely observing a
                // backend allocated by startup. The existing production fallback remains valid for
                // unsupported semantics, but at least one supported near chunk must finish on GPU.
                for (int frame = 0; frame < 600; frame++)
                {
                    showcase.transform.position = origin + new Vector3(
                        frame * 0.35f,
                        0f,
                        Mathf.Sin(frame * 0.04f) * 12f);

                    yield return null;
                    camera.Render();
                    last = VoxelRenderBridge.SurfaceMetrics;

                    Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                        $"GPU migration frame {frame} synchronously completed geometry work.");
                    Assert.Greater(last.VisibleSolidChunks, 0,
                        $"GPU migration frame {frame} lost every visible voxel draw.");

                    sawGpuBackend |= last.GpuResidentBackends > 0;
                    sawGpuCompletion |= last.GpuCompletedSolidBuilds > initialGpuCompleted;
                    if (sawGpuBackend && sawGpuCompletion)
                        break;
                }

                Assert.True(last.GpuCutoverAvailable,
                    "Production near-ring workers reported no available GPU cutover.");
                Assert.True(sawGpuBackend,
                    "Traversal never allocated a production GPU surface backend.");
                Assert.True(sawGpuCompletion,
                    $"Traversal completed no new GPU surface builds; gpuCompleted="
                  + $"{last.GpuCompletedSolidBuilds}, gpuFallback={last.GpuFallbackSolidBuilds}. "
                  + "A CPU-only fallback is not an acceptable fix for this SceneIssue.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
