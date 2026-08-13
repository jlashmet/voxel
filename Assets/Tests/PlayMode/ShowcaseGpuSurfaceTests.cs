using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering;
using VoxelEngine.Rendering.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseGpuSurfaceTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ShowcaseBuildsSolidGeometryOnGpu()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            Assert.AreEqual(VoxelRenderBridge.SolidSurfaceBackend.GpuSurfaceNets,
                            VoxelRenderBridge.SolidBackend);
            Assert.IsTrue(VoxelRenderBridge.TryGetWorld(out VoxelWorldView world),
                "Showcase did not register a valid render-world view.");
            Debug.Log($"GPU test world: regions={world.Table.ResidentCount}, "
                    + $"pool={world.Pool.AllocatedCount}/{world.Pool.Capacity}, "
                    + $"surfaceHash={world.SurfaceCatalogue.CatalogueHash}, "
                    + $"coatingHash={world.CoatingCatalogue.CatalogueHash}");
            Camera camera = Camera.main;
            Assert.NotNull(camera);

            // Batch mode does not guarantee a Game-view camera render for every yielded frame.
            // Drive a bounded number of renders into one persistent target: this exercises URP
            // without repeatedly creating full-screen render resources (the source of the old
            // runaway test). Ordinary frames between renders still advance streaming work.
            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseGpuSurfaceTests.Target",
                antiAliasing = 1
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;
            try
            {
                bool converged = false;
                for (int frame = 0; frame < 180; frame++)
                {
                    if (frame % 6 == 0)
                        camera.Render();
                    yield return null;

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    if (frame % 30 == 0)
                        Debug.Log($"GPU surface frame {frame}: changes={metrics.ChangeRecords}, "
                                + $"surfaceBricks={metrics.DiscoveredSurfaceBricks}, "
                                + $"known={metrics.SolidKnownChunks}, "
                                + $"resident={metrics.SolidResidentChunks}, "
                                + $"visible={metrics.VisibleSolidChunks}, "
                                + $"enqueues={VoxelRenderBridge.RenderFeatureEnqueueCount}, "
                                + $"records={VoxelRenderBridge.SurfacePassRecordCount}, "
                                + $"state={VoxelRenderBridge.LastSurfacePassState}");
                    Assert.AreEqual(0ul, metrics.UploadedGeometryBytes,
                        "GPU extraction must not upload CPU-authored vertex/index geometry.");
                    if (metrics.SolidKnownChunks > 0 && metrics.SolidResidentChunks > 0
                        && metrics.VisibleSolidChunks > 0)
                    {
                        Assert.AreEqual(0ul, metrics.RejectedStaleSolidBuilds);
                        converged = true;
                        // Keep several later renders so the bounded extractor can fill more than
                        // the first visible chunk before the image is judged.
                        if (frame >= 120) break;
                    }
                }
                VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.IsTrue(converged,
                    $"GPU surface did not become visible: changes={finalMetrics.ChangeRecords}, "
                  + $"surfaceBricks={finalMetrics.DiscoveredSurfaceBricks}, "
                  + $"known={finalMetrics.SolidKnownChunks}, "
                  + $"resident={finalMetrics.SolidResidentChunks}, "
                  + $"visible={finalMetrics.VisibleSolidChunks}.");

                camera.Render();
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = target;
                var capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                capture.Apply(false, false);
                RenderTexture.active = previousActive;

                string captureDirectory = Path.GetFullPath("Artifacts/GpuShowcase");
                Directory.CreateDirectory(captureDirectory);
                string capturePath = Path.Combine(captureDirectory, "showcase-gpu.png");
                File.WriteAllBytes(capturePath, capture.EncodeToPNG());
                Object.Destroy(capture);
                Assert.IsTrue(File.Exists(capturePath),
                    $"Screenshot was not written to {capturePath}.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
