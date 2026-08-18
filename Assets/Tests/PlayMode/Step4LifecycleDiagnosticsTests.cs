using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Focused measurement fixture for the remaining step-4 ready-empty regression.
    ///
    /// This test does not define a new rendering contract. It reproduces the same castle-centred
    /// step-4 view as LodRenderingTests, then reports the production lifecycle counters that tell
    /// us whether exact ownership, profile suppression, the feature-preserving fallback, or final
    /// publication adjudicated the visible chunks as empty. Keep the production fix driven by
    /// this measurement rather than by another speculative coarse-geometry change.
    /// </summary>
    public sealed class Step4LifecycleDiagnosticsTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(900000)]
        public IEnumerator CastleStep4ReadyEmpty_ReportsExactLifecycleCause()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return WaitForAtomicWorldReady();

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            ShowcaseWorld world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Assert.NotNull(world);
            Camera camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase)
                .GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase)
                .GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = StructuresComposition.PlanCastle(
                new int3(256, ground, 376), world.Seed);
            Vector3 centre = new(
                plan.Centre.x * 0.1f,
                (plan.Centre.y + plan.PlateauHeight) * 0.1f,
                plan.Centre.z * 0.1f);
            Vector3 lookAt = centre + Vector3.up * 10f;

            var target = new RenderTexture(120, 90, 24, RenderTextureFormat.ARGB32);
            bool oldOrthographic = camera.orthographic;
            float oldOrthographicSize = camera.orthographicSize;
            float oldNear = camera.nearClipPlane;
            float oldFar = camera.farClipPlane;
            double oldBuildBudget = VoxelRenderBridge.SolidBuildBudgetMs;

            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.orthographic = true;
                camera.orthographicSize = 24f;
                camera.transform.position = centre + new Vector3(0f, 20f, -240f);
                camera.transform.LookAt(lookAt);
                camera.nearClipPlane = 208f;
                camera.farClipPlane = 272f;

                // Match the offline LOD capture. This fixture measures lifecycle adjudication,
                // not the production frame-budget threshold.
                VoxelRenderBridge.SolidBuildBudgetMs = 8.0;
                Step4FalseEmptyDiagnostics.Reset();

                VoxelSurfaceMetrics metrics = default;
                bool reachedReadyEmpty = false;
                int frames = 0;
                double deadline = Time.realtimeSinceStartupAsDouble + 20.0;
                while (frames++ < 1200 && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    metrics = VoxelRenderBridge.SurfaceMetrics;

                    if (metrics.Step4VisibilityFrustum <= 0
                        || metrics.Step4VisibilityEmpty <= 0)
                        continue;

                    // The production regression is a completed step-4 state: the frustum has
                    // authoritative known-empty results and no remaining step-4 work to explain
                    // them away as transient streaming holes.
                    if (metrics.Step4DirtyChunks == 0
                        && metrics.Step4RunningJobs == 0)
                    {
                        reachedReadyEmpty = true;
                        break;
                    }
                }

                Step4FalseEmptyDiagnostics.Snapshot lifecycle =
                    Step4FalseEmptyDiagnostics.Current;
                string evidence =
                    $"frames={frames} step4=known:{metrics.Step4KnownChunks}/"
                  + $"resident:{metrics.Step4ResidentChunks}/dirty:{metrics.Step4DirtyChunks}/"
                  + $"missing:{metrics.Step4MissingVisibleChunks}/jobs:{metrics.Step4RunningJobs} "
                  + $"visibility=known:{metrics.Step4VisibilityKnown}/"
                  + $"inBand:{metrics.Step4VisibilityInBand}/"
                  + $"frustum:{metrics.Step4VisibilityFrustum}/"
                  + $"ready:{metrics.Step4VisibilityReady}/"
                  + $"empty:{metrics.Step4VisibilityEmpty} "
                  + $"fallback=s:{metrics.Step4FeatureFallbackScheduled}/"
                  + $"c:{metrics.Step4FeatureFallbackCompleted}/"
                  + $"n:{metrics.Step4FeatureFallbackNonEmpty}/"
                  + $"p:{metrics.Step4FeatureFallbackPublished} "
                  + $"lifecycle={lifecycle}";
                TestContext.Out.WriteLine("STEP4_LIFECYCLE " + evidence);
                Debug.Log("STEP4_LIFECYCLE " + evidence);

                Assert.True(reachedReadyEmpty,
                    "Step-4 diagnostic view did not reproduce the completed ready-empty state; "
                  + evidence);
                Assert.Greater(
                    lifecycle.ExactOwnedSolidSnapshots + lifecycle.ExactUnownedSnapshots,
                    0,
                    "Step-4 exact classification never reached the instrumented adjudication path; "
                  + evidence);
            }
            finally
            {
                VoxelRenderBridge.SolidBuildBudgetMs = oldBuildBudget;
                camera.targetTexture = null;
                camera.orthographic = oldOrthographic;
                camera.orthographicSize = oldOrthographicSize;
                camera.nearClipPlane = oldNear;
                camera.farClipPlane = oldFar;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static IEnumerator WaitForAtomicWorldReady()
        {
            int frames = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 60.0;
            while (!VoxelRenderBridge.SurfaceBuildEnabled
                   && frames++ < 3600
                   && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;

            Assert.True(VoxelRenderBridge.SurfaceBuildEnabled,
                "Showcase atomic world did not commit before step-4 lifecycle measurement.");
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "Showcase lost the render-world binding before step-4 lifecycle measurement.");
        }

        private static void RenderUrpCamera(Camera camera)
        {
            Assert.NotNull(camera);
            Assert.NotNull(camera.targetTexture);
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = camera.targetTexture,
            };
            Assert.True(RenderPipeline.SupportsRenderRequest(camera, request));
            VoxelRenderBridge.ResetSurfacePassDiagnostics("step4-lifecycle");
            RenderPipeline.SubmitRenderRequest(camera, request);
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0);
            Assert.AreEqual("feature-aware", VoxelRenderBridge.LastSurfacePassState);
        }
    }
}
