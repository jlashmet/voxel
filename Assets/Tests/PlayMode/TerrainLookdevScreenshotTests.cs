using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Production-path acceptance for the terrain lookdev scene.
    ///
    /// This test used to read an editor RenderTexture back to the CPU, write terrain.png plus
    /// derived diff/reference PNGs, and compare those editor pixels with the art reference. That
    /// made the persisted visual result come from a different rendering environment than the game.
    /// The PlayMode phase now owns only renderer/convergence assertions. Selecting this test through
    /// the single-test workflow automatically builds TerrainLookdev.unity as a standalone app and
    /// publishes actual presented-frame screenshots every ten seconds through the shared real-player
    /// capture utility.
    /// </summary>
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: visual acceptance for human review; run by
    /// name when you want the real-player artifacts.
    /// </remarks>
    [NUnit.Framework.Explicit("Visual acceptance for human review; run by name.")]
    public sealed class TerrainLookdevScreenshotTests
    {
        private const int RenderWidth = 512;
        private const int RenderHeight = 768;

        [UnityTest]
        public IEnumerator CaptureTerrainLookdev()
        {
            var root = new GameObject("Terrain Lookdev Test Camera");
            root.tag = "MainCamera";
            TerrainLookdev lookdev = root.AddComponent<TerrainLookdev>();
            Camera camera = lookdev.SceneCamera;

            Assert.IsTrue(VoxelRenderBridge.TryGetWorld(out _),
                "Terrain lookdev did not register a valid voxel world.");
            VoxelRenderBridge.ResetSurfacePassDiagnostics("terrain-visual-acceptance-start");

            var convergenceTarget = new RenderTexture(
                RenderWidth, RenderHeight, 24, RenderTextureFormat.ARGB32);
            convergenceTarget.Create();
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = convergenceTarget;

            int stableFrames = 0;
            try
            {
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
            }
            finally
            {
                camera.targetTexture = previousTarget;
                convergenceTarget.Release();
                UnityEngine.Object.DestroyImmediate(convergenceTarget);
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

            lookdev.Shutdown();
            UnityEngine.Object.Destroy(root);
            yield return null;
        }
    }
}
