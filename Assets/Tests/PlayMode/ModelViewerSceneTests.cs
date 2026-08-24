using System.Collections;
using System.IO;
using Game.ModelViewer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Production-surface acceptance for the generic authored-object Model Viewer. Both dragon
    /// interpretations are captured through the real renderer so visual iteration compares actual
    /// game output rather than a diagnostic or generated preview.
    /// </summary>
    [NUnit.Framework.Explicit("Visual acceptance for human review; run by name.")]
    public sealed class ModelViewerSceneTests
    {
        [UnityTest, Timeout(180000)]
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
            Assert.True(RenderingComposition.TryGetWorld(out var world, out _),
                "Model Viewer did not bind authored voxel storage to the production renderer.");
            Assert.NotNull(world.Storage,
                "Model Viewer production binding must expose the canonical voxel storage source.");

            Camera camera = lookdev.GetComponent<Camera>();
            Assert.NotNull(camera);

            yield return CaptureVariant(
                lookdev, camera, modelIndex: 0, outputName: "dragon-a-detailed-production.png");
            yield return CaptureVariant(
                lookdev, camera, modelIndex: 1, outputName: "dragon-b-organic-production.png");
        }

        private static IEnumerator CaptureVariant(
            ModelViewerLookdev lookdev,
            Camera camera,
            int modelIndex,
            string outputName)
        {
            lookdev.SelectModelForAutomation(modelIndex);
            yield return null;

            Assert.True(RenderingComposition.TryGetWorld(out var world, out _),
                $"Model Viewer variant {modelIndex} did not bind its production world.");
            Assert.NotNull(world.Storage);

            var convergenceTarget = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            convergenceTarget.Create();
            camera.targetTexture = convergenceTarget;

            try
            {
                bool converged = false;
                int stableFrames = 0;
                float deadline = Time.realtimeSinceStartup + 60f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    camera.Render();
                    yield return null;

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
                }

                if (!converged)
                {
                    VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.Fail($"Model Viewer variant {modelIndex} production surface did not converge: "
                        + $"known={finalMetrics.SolidKnownChunks}, "
                        + $"resident={finalMetrics.SolidResidentChunks}, "
                        + $"dirty={finalMetrics.SolidDirtyChunks}, "
                        + $"missingVisible={finalMetrics.MissingVisibleSolidChunks}.");
                }
            }
            finally
            {
                camera.targetTexture = null;
                convergenceTarget.Release();
                Object.Destroy(convergenceTarget);
            }

            var target = new RenderTexture(1440, 1100, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            try
            {
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
                    string outputPath = Path.Combine(outputDirectory, outputName);
                    File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
                    Assert.That(new FileInfo(outputPath).Length, Is.GreaterThan(4096),
                        $"Production Model Viewer capture '{outputName}' was unexpectedly empty.");
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
