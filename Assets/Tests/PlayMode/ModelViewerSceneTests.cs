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
            Assert.True(RenderingComposition.TryGetWorld(out var world, out _),
                "Model Viewer did not bind authored voxel storage to the production renderer.");
            Assert.NotNull(world.Storage,
                "Model Viewer production binding must expose the canonical voxel storage source.");

            Camera camera = lookdev.GetComponent<Camera>();
            Assert.NotNull(camera);

            // Drive convergence at the same modest render-target scale as the known-good Arch
            // acceptance. Unity's coroutine test runner can otherwise advance hundreds of `yield
            // return null` iterations in well under two seconds, which is faster than the async
            // surface workers can complete a dragon chunk on a cold CI import.
            var convergenceTarget = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            convergenceTarget.Create();
            camera.targetTexture = convergenceTarget;

            try
            {
                bool converged = false;
                int stableFrames = 0;
                float deadline = Time.realtimeSinceStartup + 45f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return new WaitForEndOfFrame();
                    camera.Render();

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
                    Assert.Fail($"Model Viewer production surface did not converge: "
                        + $"known={finalMetrics.SolidKnownChunks}, "
                        + $"resident={finalMetrics.SolidResidentChunks}, "
                        + $"dirty={finalMetrics.SolidDirtyChunks}, "
                        + $"missingVisible={finalMetrics.MissingVisibleSolidChunks}.");
                }

                // Once production surface residency is stable, capture at review resolution. The
                // renderer/material path is unchanged; only the final pixel readback is test-only.
                camera.targetTexture = null;
                convergenceTarget.Release();
                Object.Destroy(convergenceTarget);

                var target = new RenderTexture(1440, 1100, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                try
                {
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
            finally
            {
                if (camera.targetTexture == convergenceTarget)
                    camera.targetTexture = null;
                if (convergenceTarget != null)
                {
                    convergenceTarget.Release();
                    Object.Destroy(convergenceTarget);
                }
            }
        }
    }
}
