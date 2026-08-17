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

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Focused lifecycle gate for the production step-4 castle disappearance. This deliberately
    /// does not compare screenshots or change renderer budgets. It parks the real showcase camera
    /// in the step-4 band, then aims at the nearest actual step-4 active slot rather than assuming
    /// the castle reference point happens to fall inside a coarse chunk's narrow test frustum.
    /// Once a witness enters the frustum, every intersecting step-4 chunk must be adjudicated as
    /// ready or authoritative-empty and at least one must remain ready. A failure prints the exact
    /// ownership -> ordinary geometry -> fallback -> publication counters so the next repair is
    /// chosen from evidence rather than another coarse-geometry guess.
    /// </summary>
    public sealed class Step4FalseEmptyLifecycleTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const int Step4SourceStep = 4;
        private const float Step4InnerRadiusMetres = 192f;
        private const float Step4OuterRadiusMetres = 288f;

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
            Vector3 centre = new Vector3(
                plan.Centre.x, plan.Centre.y + plan.PlateauHeight, plan.Centre.z) * 0.1f;
            Vector3 lookAt = centre + Vector3.up * 10f;

            var target = new RenderTexture(120, 90, 24, RenderTextureFormat.ARGB32);
            bool oldOrthographic = camera.orthographic;
            float oldOrthographicSize = camera.orthographicSize;
            float oldNear = camera.nearClipPlane;
            float oldFar = camera.farClipPlane;
            RenderTexture oldTarget = camera.targetTexture;

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

                Step4FalseEmptyDiagnostics.Reset();

                VoxelSurfaceMetrics metrics = default;
                bool aimedAtWitness = false;
                bool sawFrustum = false;
                bool adjudicated = false;
                int frames = 0;
                double deadline = Time.realtimeSinceStartupAsDouble + 20.0;
                while (frames++ < 1200 && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    metrics = VoxelRenderBridge.SurfaceMetrics;

                    // Surface discovery/clipmap admission does not require frustum ownership. Once
                    // the first real step-4 slot exists in this camera's shell, point the same
                    // fixed camera position directly at that slot. This keeps the regression about
                    // the step-4 worker lifecycle, not about accidental alignment between the
                    // authored castle reference point and a 25.6 m coarse chunk grid.
                    if (!aimedAtWitness)
                        aimedAtWitness = TryAimAtNearestStep4ActiveSlot(camera, centre);

                    sawFrustum |= metrics.Step4VisibilityFrustum > 0;
                    if (!sawFrustum) continue;

                    int decided = metrics.Step4VisibilityReady + metrics.Step4VisibilityEmpty;
                    adjudicated = metrics.Step4DirtyChunks == 0
                               && metrics.Step4RunningJobs == 0
                               && decided >= metrics.Step4VisibilityFrustum;
                    if (adjudicated) break;
                }

                string evidence = Evidence(metrics, frames)
                                + $" witnessAimed={aimedAtWitness}.";
                Debug.Log($"[Step4FalseEmptyGate] {evidence}");

                Assert.True(aimedAtWitness,
                    "Step-4 castle camera never found an in-band active slot to witness; " + evidence);
                Assert.True(sawFrustum,
                    "Step-4 castle camera never acquired frustum ownership; " + evidence);
                Assert.True(adjudicated,
                    "Step-4 castle frustum did not finish lifecycle adjudication; " + evidence);
                Assert.Greater(metrics.Step4VisibilityReady, 0,
                    "Step-4 castle frustum settled entirely as authoritative empty; " + evidence);
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.orthographic = oldOrthographic;
                camera.orthographicSize = oldOrthographicSize;
                camera.nearClipPlane = oldNear;
                camera.farClipPlane = oldFar;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static bool TryAimAtNearestStep4ActiveSlot(Camera camera, Vector3 castleCentre)
        {
            VoxelRenderPass pass = VoxelRenderBridge.ActivePass;
            if (pass == null) return false;

            FieldInfo schedulerField = typeof(VoxelRenderPass).GetField(
                "_scheduler", BindingFlags.NonPublic | BindingFlags.Instance);
            object scheduler = schedulerField?.GetValue(pass);
            if (scheduler == null) return false;

            FieldInfo ringsField = scheduler.GetType().GetField(
                "_rings", BindingFlags.NonPublic | BindingFlags.Instance);
            System.Array rings = ringsField?.GetValue(scheduler) as System.Array;
            if (rings == null) return false;

            bool found = false;
            Bounds bestBounds = default;
            float bestScore = float.PositiveInfinity;
            float voxelSize = pass.VoxelSize;
            Vector3 cameraPosition = camera.transform.position;

            foreach (object ring in rings)
            {
                System.Type ringType = ring.GetType();
                FieldInfo sourceStepField = ringType.GetField(
                    "SourceStep", BindingFlags.Public | BindingFlags.Instance);
                if (sourceStepField == null
                    || (int)sourceStepField.GetValue(ring) != Step4SourceStep)
                    continue;

                PropertyInfo activeCountProperty = ringType.GetProperty(
                    "ActiveSlotCount", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo activeCoordinateMethod = ringType.GetMethod(
                    "ActiveSlotCoordinate", BindingFlags.Public | BindingFlags.Instance);
                if (activeCountProperty == null || activeCoordinateMethod == null) return false;

                int activeCount = (int)activeCountProperty.GetValue(ring);
                for (int i = 0; i < activeCount; i++)
                {
                    int3 coordinate = (int3)activeCoordinateMethod.Invoke(
                        ring, new object[] { i });
                    Bounds bounds = Step4ChunkBounds(coordinate, voxelSize);
                    if (!WithinStep4Band(bounds, cameraPosition)) continue;

                    float score = (bounds.center - castleCentre).sqrMagnitude;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    bestBounds = bounds;
                    found = true;
                }
            }

            if (!found) return false;

            Vector3 toWitness = bestBounds.center - cameraPosition;
            float witnessDistance = toWitness.magnitude;
            if (witnessDistance <= 0.01f) return false;

            float witnessRadius = bestBounds.extents.magnitude;
            camera.transform.LookAt(bestBounds.center);
            camera.orthographicSize = Mathf.Max(24f, witnessRadius + 2f);
            camera.nearClipPlane = Mathf.Max(0.05f, witnessDistance - witnessRadius - 2f);
            camera.farClipPlane = witnessDistance + witnessRadius + 2f;
            return true;
        }

        private static Bounds Step4ChunkBounds(int3 coordinate, float voxelSize)
        {
            float size = CpuTransvoxelChunkCache.CellsPerAxis * Step4SourceStep * voxelSize;
            Vector3 min = new Vector3(coordinate.x, coordinate.y, coordinate.z) * size;
            return new Bounds(
                min + Vector3.one * (size * 0.5f),
                Vector3.one * (size + Step4SourceStep * voxelSize * 2f));
        }

        private static bool WithinStep4Band(Bounds bounds, Vector3 cameraPosition)
        {
            Vector3 extents = bounds.extents;
            Vector3 delta = bounds.center - cameraPosition;
            float nearX = Mathf.Max(0f, Mathf.Abs(delta.x) - extents.x);
            float nearY = Mathf.Max(0f, Mathf.Abs(delta.y) - extents.y);
            float nearZ = Mathf.Max(0f, Mathf.Abs(delta.z) - extents.z);
            float near = Mathf.Max(nearX, Mathf.Max(nearY, nearZ));
            if (near > Step4OuterRadiusMetres) return false;

            float far = Mathf.Max(Mathf.Abs(delta.x) + extents.x,
                        Mathf.Max(Mathf.Abs(delta.y) + extents.y,
                                  Mathf.Abs(delta.z) + extents.z));
            return far > Step4InnerRadiusMetres;
        }

        private static string Evidence(in VoxelSurfaceMetrics metrics, int frames) =>
            $"frames={frames} "
          + $"global=known:{metrics.SolidKnownChunks}/resident:{metrics.SolidResidentChunks}/"
          + $"dirty:{metrics.SolidDirtyChunks}/visible:{metrics.VisibleSolidChunks}/"
          + $"missing:{metrics.MissingVisibleSolidChunks}/jobs:{metrics.RunningSolidJobs} "
          + $"step4=known:{metrics.Step4KnownChunks}/resident:{metrics.Step4ResidentChunks}/"
          + $"dirty:{metrics.Step4DirtyChunks}/missing:{metrics.Step4MissingVisibleChunks}/"
          + $"jobs:{metrics.Step4RunningJobs}/phaseMask:0x{metrics.Step4BuildPhaseMask:X}/"
          + $"jobMask:0x{metrics.Step4ActiveJobMask:X} "
          + $"visibility=known:{metrics.Step4VisibilityKnown}/inBand:{metrics.Step4VisibilityInBand}/"
          + $"frustum:{metrics.Step4VisibilityFrustum}/ready:{metrics.Step4VisibilityReady}/"
          + $"empty:{metrics.Step4VisibilityEmpty} "
          + $"metadata={metrics.Step4ExactMetadataScheduled}/{metrics.Step4ExactMetadataCompleted}/"
          + $"revReject:{metrics.Step4ExactMetadataRevisionRejects}/"
          + $"pinReject:{metrics.Step4ExactMetadataPinRejects} "
          + $"lifecycle:{Step4FalseEmptyDiagnostics.Current} "
          + $"fallback=s:{metrics.Step4FeatureFallbackScheduled}/"
          + $"c:{metrics.Step4FeatureFallbackCompleted}/"
          + $"n:{metrics.Step4FeatureFallbackNonEmpty}/"
          + $"p:{metrics.Step4FeatureFallbackPublished}.";

        private static IEnumerator WaitForAtomicWorldReady()
        {
            int frames = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 60.0;
            while (!VoxelRenderBridge.SurfaceBuildEnabled
                   && frames++ < 3600
                   && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;

            Assert.True(VoxelRenderBridge.SurfaceBuildEnabled,
                "Showcase atomic world did not commit within 60 seconds.");
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "Showcase lost its render-world binding before step-4 lifecycle validation.");
        }

        private static void RenderUrpCamera(Camera camera)
        {
            Assert.NotNull(camera.targetTexture);
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = camera.targetTexture,
            };
            Assert.True(RenderPipeline.SupportsRenderRequest(camera, request));
            VoxelRenderBridge.ResetSurfacePassDiagnostics("step4-false-empty-lifecycle");
            RenderPipeline.SubmitRenderRequest(camera, request);
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0,
                "Step-4 lifecycle request did not execute VoxelRenderPass.");
            Assert.AreEqual("feature-aware", VoxelRenderBridge.LastSurfacePassState);
        }
    }
}
