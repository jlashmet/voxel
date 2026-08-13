using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering;
using VoxelEngine.Rendering.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class TerrainLookdevScreenshotTests
    {
        [UnityTest]
        public IEnumerator CaptureTerrainLookdev()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "Artifacts", "Terrain");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("Terrain Lookdev Test Camera");
            root.tag = "MainCamera";
            TerrainLookdev lookdev = root.AddComponent<TerrainLookdev>();
            Camera camera = lookdev.SceneCamera;

            Assert.IsTrue(VoxelRenderBridge.TryGetWorld(out _),
                "Terrain lookdev did not register a valid voxel world.");
            VoxelRenderBridge.ResetSurfacePassDiagnostics("terrain-capture-start");

            int stableFrames = 0;
            for (int frame = 0; frame < 360 && stableFrames < 3; frame++)
            {
                camera.Render();
                yield return null;

                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                bool converged = metrics.SolidKnownChunks > 0
                    && metrics.SolidDirtyChunks == 0
                    && metrics.SolidResidentChunks >= metrics.SolidKnownChunks;
                stableFrames = converged ? stableFrames + 1 : 0;
            }

            VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
            Assert.Greater(VoxelRenderBridge.RenderFeatureEnqueueCount, 0,
                "VoxelRenderFeature never enqueued for the terrain camera.");
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0,
                "Voxel surface pass never recorded for the terrain camera.");
            Assert.GreaterOrEqual(stableFrames, 3,
                $"Terrain surface did not converge: known={finalMetrics.SolidKnownChunks}, " +
                $"resident={finalMetrics.SolidResidentChunks}, dirty={finalMetrics.SolidDirtyChunks}, " +
                $"featureEnqueues={VoxelRenderBridge.RenderFeatureEnqueueCount}, " +
                $"surfaceRecords={VoxelRenderBridge.SurfacePassRecordCount}, " +
                $"state={VoxelRenderBridge.LastSurfacePassState}");

            MethodInfo capture = typeof(CastleScreenshotTests).GetMethod(
                "Capture", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(capture, "shared screenshot helper was not found");
            capture.Invoke(null, new object[]
            {
                camera,
                Path.Combine(outputDirectory, "terrain.png")
            });

            File.WriteAllText(Path.Combine(outputDirectory, "terrain.txt"),
                $"knownChunks={finalMetrics.SolidKnownChunks}\n" +
                $"residentChunks={finalMetrics.SolidResidentChunks}\n" +
                $"dirtyChunks={finalMetrics.SolidDirtyChunks}\n" +
                $"featureEnqueues={VoxelRenderBridge.RenderFeatureEnqueueCount}\n" +
                $"surfaceRecords={VoxelRenderBridge.SurfacePassRecordCount}\n" +
                $"surfaceState={VoxelRenderBridge.LastSurfacePassState}\n");

            lookdev.Shutdown();
            Object.Destroy(root);
            yield return null;
        }
    }
}
