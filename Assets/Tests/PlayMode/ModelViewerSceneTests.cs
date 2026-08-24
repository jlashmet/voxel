using System.Collections;
using System.IO;
using Game.ModelViewer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Production-surface acceptance for the generic authored-object Model Viewer. The test saves a
    /// camera render after visible production chunks have converged so remote visual iteration is
    /// grounded in RenderingComposition rather than the diagnostic exposed-voxel capture.
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
            Assert.NotNull(world.ProfileBlocks);
            Assert.Greater(world.ProfileBlocks.Count, 0);

            Camera camera = lookdev.GetComponent<Camera>();
            Assert.NotNull(camera);
            var target = new RenderTexture(1440, 1100, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;

            try
            {
                bool converged = false;
                int stableFrames = 0;
                for (int frame = 0; frame < 360; frame++)
                {
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    bool frameConverged = metrics.SolidKnownChunks > 0
                        && metrics.SolidResidentChunks > 0
                        && metrics.MissingVisibleSolidChunks == 0;
                    stableFrames = frameConverged ? stableFrames + 1 : 0;
                    if (stableFrames >= 3)
                    {
                        converged = true;
                        break;
                    }
                    yield return null;
                }

                if (!converged)
                {
                    VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.Fail($"Model Viewer production surface did not converge: "
                        + $"known={finalMetrics.SolidKnownChunks}, "
                        + $"resident={finalMetrics.SolidResidentChunks}, "
                        + $"dirty={finalMetrics.SolidDirtyChunks}, "
                        + $"missingVisible={finalMetrics.MissingVisibleSolidChunks}.");
                }

                // Let the converged state participate in a normal frame, then explicitly render the
                // viewer camera into the evidence target. This is the production renderer/material
                // path; only the final pixel readback is test-only.
                yield return new WaitForEndOfFrame();
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    pixels.Apply(false, false);

                    string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                    string outputDirectory = Path.Combine(
                        projectRoot, "Artifacts", "SingleTest", "ModelViewer");
                    Directory.CreateDirectory(outputDirectory);
                    string outputPath = Path.Combine(
                        outputDirectory, "dragon-statue-production.png");
                    File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
                    Assert.That(new FileInfo(outputPath).Length, Is.GreaterThan(4096),
                        "Production Model Viewer capture was unexpectedly empty.");
                }
                finally
                {
                    Object.Destroy(pixels);
                    RenderTexture.active = previous;
                }
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
