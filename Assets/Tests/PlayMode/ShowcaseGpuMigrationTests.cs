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
    /// Regression for SceneIssue 20260825-192751-413: the legacy per-worker GPU-v1 cutover must
    /// stay out of production after two exact traversal runs lost every visible voxel draw. The
    /// optimized CPU renderer remains authoritative for production presentation while GPU-v1 stays
    /// available only through the explicit diagnostic opt-in used by future GPU-v2 work.
    /// </summary>
    public sealed class ShowcaseGpuMigrationTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(900000)]
        public IEnumerator MovingShowcaseKeepsLegacyGpuV1OffAndPreservesCoverage()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(camera);
            Assert.True(CpuTransvoxelChunkCache.GpuCutoverDisabled,
                "Production startup did not apply the legacy GPU-v1 safety gate.");

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseGpuMigrationTests.ProductionSafety",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                Vector3 origin = showcase.transform.position;
                ulong initialGpuCompleted = VoxelRenderBridge.SurfaceMetrics.GpuCompletedSolidBuilds;
                VoxelSurfaceMetrics last = default;

                for (int frame = 0; frame < 420; frame++)
                {
                    showcase.transform.position = origin + new Vector3(
                        frame * 0.5f,
                        0f,
                        Mathf.Sin(frame * 0.04f) * 12f);

                    yield return null;
                    camera.Render();
                    last = VoxelRenderBridge.SurfaceMetrics;

                    Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                        $"Production safety frame {frame} synchronously completed geometry work.");
                    Assert.Greater(last.VisibleSolidChunks, 0,
                        $"Production safety frame {frame} lost every visible voxel draw.");
                    Assert.AreEqual(0, last.GpuResidentBackends,
                        $"Production safety frame {frame} allocated a legacy GPU-v1 backend.");
                }

                Assert.False(last.GpuCutoverAvailable,
                    "Production workers still advertised the legacy GPU-v1 cutover.");
                Assert.AreEqual(initialGpuCompleted, last.GpuCompletedSolidBuilds,
                    "Production completed GPU-v1 surface builds despite the safety rollback.");
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
