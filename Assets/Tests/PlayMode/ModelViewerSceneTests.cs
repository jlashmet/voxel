using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using Game.ModelViewer;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Production-surface acceptance for the generic authored-object Model Viewer.
    /// Screenshot evidence is captured separately from a standalone player so visual review sees
    /// presented production frames rather than an editor-only readback or diagnostic voxel proxy.
    /// </summary>
    [NUnit.Framework.Explicit("Visual acceptance for human review; run by name.")]
    public sealed class ModelViewerSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator DragonStatueConvergesThroughProductionSurfacePath()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/ModelViewer.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("ModelViewer", LoadSceneMode.Single);
#endif
            yield return null;

            ModelViewerLookdev lookdev = Object.FindAnyObjectByType<ModelViewerLookdev>();
            Assert.NotNull(lookdev, "ModelViewer.unity must contain the generic ModelViewerLookdev camera.");
            Assert.True(VoxelRenderBridge.TryGetWorld(out var world),
                "Model Viewer did not bind authored voxel storage to the production renderer.");
            Assert.NotNull(world.Reads);

            Camera camera = lookdev.GetComponent<Camera>();
            Assert.NotNull(camera);
            var target = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;

            try
            {
                int stableFrames = 0;
                for (int frame = 0; frame < 360; frame++)
                {
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    bool converged = metrics.SolidKnownChunks > 0
                        && metrics.SolidResidentChunks > 0
                        && metrics.MissingVisibleSolidChunks == 0;
                    stableFrames = converged ? stableFrames + 1 : 0;
                    if (stableFrames >= 3)
                        yield break;
                    yield return null;
                }

                VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.Fail($"Model Viewer production surface did not converge: "
                    + $"known={finalMetrics.SolidKnownChunks}, "
                    + $"resident={finalMetrics.SolidResidentChunks}, "
                    + $"dirty={finalMetrics.SolidDirtyChunks}, "
                    + $"missingVisible={finalMetrics.MissingVisibleSolidChunks}.");
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
