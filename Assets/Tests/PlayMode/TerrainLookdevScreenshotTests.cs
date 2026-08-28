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
    /// The PlayMode phase owns renderer/convergence assertions. Selecting this test through the
    /// single-test workflow builds TerrainLookdev.unity as a standalone app and publishes actual
    /// presented-frame screenshots through the shared real-player capture utility.
    /// </summary>
    [NUnit.Framework.Explicit("Visual acceptance for human review; run by name.")]
    public sealed class TerrainLookdevScreenshotTests
    {
        private const int RenderWidth = 512;
        private const int RenderHeight = 768;
        private const float CapturedAspect = 1928f / 836f;

        [UnityTest]
        public IEnumerator CapturedReplayFramesReadablePathAndLandmarkDepth()
        {
            var root = new GameObject("Terrain Lookdev Captured Composition Test Camera");
            root.tag = "MainCamera";
            TerrainLookdev lookdev = root.AddComponent<TerrainLookdev>();
            Camera camera = lookdev.SceneCamera;

            // Cross the real production startup boundary: OnEnable/Rebuild authors the voxel world.
            yield return null;
            Assert.IsTrue(VoxelRenderBridge.TryGetWorld(out _),
                "Terrain lookdev did not register its production voxel world.");

            // Acceptance replays this exact capture. A startup-only camera regression is insufficient
            // because SceneIssueCameraReplayHarness re-pins these values every LateUpdate.
            camera.transform.SetPositionAndRotation(
                new Vector3(-0.699999988f, 18.7999992f, -18.5f),
                new Quaternion(0.207069993f, 0.00793157984f, -0.00167883548f, 0.978292525f));
            camera.fieldOfView = 29f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 160f;
            camera.aspect = CapturedAspect;

            // These probes are derived by the same production helpers used to author the continuous
            // tapered path. From the captured camera they must form a strong lower/centre/upper-frame
            // S-curve rather than collapsing into scattered beige dots on a survey-like green sheet.
            Vector3 nearPath = camera.WorldToViewportPoint(TerrainLookdev.CapturedPathProbeWorld(100));
            Vector3 midPath = camera.WorldToViewportPoint(TerrainLookdev.CapturedPathProbeWorld(200));
            Vector3 farPath = camera.WorldToViewportPoint(TerrainLookdev.CapturedPathProbeWorld(300));

            Assert.Greater(nearPath.z, 0f);
            Assert.Greater(midPath.z, 0f);
            Assert.Greater(farPath.z, 0f);
            Assert.That(nearPath.y, Is.InRange(0.15f, 0.32f),
                $"Captured near path must read in the lower frame, y={nearPath.y:F3}.");
            Assert.That(midPath.y, Is.InRange(0.42f, 0.60f),
                $"Captured mid path must cross the frame centre, y={midPath.y:F3}.");
            Assert.That(farPath.y, Is.InRange(0.72f, 0.88f),
                $"Captured far path must remain visible high in frame, y={farPath.y:F3}.");
            Assert.Greater(midPath.y - nearPath.y, 0.20f,
                "Captured view needs readable near-to-mid path depth separation.");
            Assert.Greater(farPath.y - midPath.y, 0.20f,
                "Captured view needs readable mid-to-far path depth separation.");

            // BuildRockFields consumes these same five anchors. Require all large masses to remain
            // visible through the captured camera, with foreground framing on both sides and later
            // landmarks climbing into the distance. This prevents random rock density from again
            // replacing the reference-like near/mid/far silhouette hierarchy.
            Assert.AreEqual(5, TerrainLookdev.CapturedCompositionLandmarkCount);
            var viewport = new Vector3[TerrainLookdev.CapturedCompositionLandmarkCount];
            for (int i = 0; i < viewport.Length; i++)
            {
                viewport[i] = camera.WorldToViewportPoint(TerrainLookdev.CapturedCompositionLandmarkWorld(i));
                Assert.Greater(viewport[i].z, 0f, $"Landmark {i} must remain in front of captured camera.");
                Assert.That(viewport[i].x, Is.InRange(0.12f, 0.88f),
                    $"Landmark {i} must remain inside captured horizontal framing, x={viewport[i].x:F3}.");
                Assert.That(viewport[i].y, Is.InRange(0.25f, 0.90f),
                    $"Landmark {i} must remain inside captured vertical framing, y={viewport[i].y:F3}.");
            }

            Assert.Less(viewport[0].x, 0.30f,
                "Near-left landmark must strongly frame the captured foreground.");
            Assert.Greater(viewport[1].x, 0.70f,
                "Near-right landmark must strongly frame the captured foreground.");
            Assert.Greater(viewport[2].y, viewport[0].y + 0.15f,
                "Mid-left landmark must climb visibly behind the foreground mass.");
            Assert.Greater(viewport[4].y, viewport[3].y + 0.14f,
                "Far landmark must establish a distinct upper-frame distance layer.");

            Debug.Log($"TERRAIN_CAPTURED_COMPOSITION pathY={nearPath.y:F3}/{midPath.y:F3}/{farPath.y:F3} " +
                      $"landmarks={viewport[0].x:F3},{viewport[0].y:F3};" +
                      $"{viewport[1].x:F3},{viewport[1].y:F3};" +
                      $"{viewport[2].x:F3},{viewport[2].y:F3};" +
                      $"{viewport[3].x:F3},{viewport[3].y:F3};" +
                      $"{viewport[4].x:F3},{viewport[4].y:F3}");

            lookdev.Shutdown();
            UnityEngine.Object.Destroy(root);
            yield return null;
        }

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
