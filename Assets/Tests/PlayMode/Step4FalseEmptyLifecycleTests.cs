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

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Focused lifecycle gate for the production step-4 castle disappearance. This is deliberately
    /// narrower than the multi-band fidelity fixture: it waits until every step-4 frustum chunk is
    /// authoritatively adjudicated, then reports whether exact ownership, ordinary geometry,
    /// feature-preserving fallback or final empty publication removed the castle from rendering.
    /// </summary>
    public sealed class Step4FalseEmptyLifecycleTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(900000)]
        public IEnumerator Step4CastleFrustumCannotSettleEntirelyAsAuthoritativeEmpty()
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
            RenderTexture oldTarget = camera.targetTexture;
            double oldBuildBudget = VoxelRenderBridge.SolidBuildBudgetMs;

            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.orthographic = true;
                camera.orthographicSize = 24f;
                const float distance = 240f;
                camera.transform.position = centre + new Vector3(0f, 20f, -distance);
                camera.transform.LookAt(lookAt);
                camera.nearClipPlane = distance - 32f;
                camera.farClipPlane = distance + 32f;

                // Match the offline LOD fixture: this test diagnoses coarse ownership/publication,
                // not the production frame-budget gate. No production budget or threshold changes.
                VoxelRenderBridge.SolidBuildBudgetMs = 8.0;
                Step4FalseEmptyDiagnostics.Reset();

                VoxelSurfaceMetrics metrics = default;
                int settledSamples = 0;
                double deadline = Time.realtimeSinceStartupAsDouble + 20.0;
                for (int frame = 0;
                     frame < 1200 && Time.realtimeSinceStartupAsDouble < deadline;
                     frame++)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    if ((frame % 10) != 0) continue;

                    metrics = VoxelRenderBridge.SurfaceMetrics;
                    bool fullyAdjudicated = metrics.Step4VisibilityFrustum > 0
                        && metrics.Step4DirtyChunks == 0
                        && metrics.Step4RunningJobs == 0
                        && metrics.Step4VisibilityReady + metrics.Step4VisibilityEmpty
                           == metrics.Step4VisibilityFrustum;
                    settledSamples = fullyAdjudicated ? settledSamples + 1 : 0;
                    if (settledSamples >= 2) break;
                }

                Step4FalseEmptyDiagnostics.Snapshot lifecycle =
                    Step4FalseEmptyDiagnostics.Current;
                Assert.Greater(metrics.Step4VisibilityFrustum, 0,
                    $"Step-4 diagnostic never observed the castle frustum; lifecycle={lifecycle}.");
                Assert.Greater(metrics.Step4VisibilityReady, 0,
                    "Step-4 castle frustum settled without a ready coarse chunk; "
                  + $"known={metrics.Step4KnownChunks} resident={metrics.Step4ResidentChunks} "
                  + $"dirty={metrics.Step4DirtyChunks} jobs={metrics.Step4RunningJobs} "
                  + $"frustum={metrics.Step4VisibilityFrustum} ready={metrics.Step4VisibilityReady} "
                  + $"empty={metrics.Step4VisibilityEmpty}; lifecycle={lifecycle}.");
            }
            finally
            {
                VoxelRenderBridge.SolidBuildBudgetMs = oldBuildBudget;
                camera.targetTexture = oldTarget;
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
                "Showcase atomic world did not publish before the step-4 lifecycle test.");
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "Showcase lost its render-world binding before the step-4 lifecycle test.");
        }

        private static void RenderUrpCamera(Camera camera)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = camera.targetTexture,
            };
            Assert.True(RenderPipeline.SupportsRenderRequest(camera, request));
            VoxelRenderBridge.ResetSurfacePassDiagnostics("step4-false-empty-lifecycle");
            RenderPipeline.SubmitRenderRequest(camera, request);
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0);
            Assert.AreEqual("feature-aware", VoxelRenderBridge.LastSurfacePassState);
        }
    }
}
