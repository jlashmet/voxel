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
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Focused production regression for the step-4 exact-snapshot residency boundary.
    ///
    /// A chunk admitted into the active step-4 shell may not remain in a permanent metadata-pin
    /// retry merely because its owned core region never became resident. This fixture uses an
    /// actual active step-4 slot, aims the real showcase camera at it, derives the unpadded core
    /// region from production chunk/region dimensions, and requires Storage's region metadata pin
    /// to become available without relaxing the optional extraction halo contract.
    /// </summary>
    public sealed class Step4CoreResidencyAdmissionTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const int Step4SourceStep = 4;
        private const float Step4InnerRadiusMetres = 192f;
        private const float Step4OuterRadiusMetres = 288f;

        [UnityTest, Timeout(900000)]
        public IEnumerator FrustumStep4ChunkEventuallyPinsItsOwnedCoreRegion()
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
            Vector3 castleCentre = new Vector3(
                plan.Centre.x, plan.Centre.y + plan.PlateauHeight, plan.Centre.z) * 0.1f;

            var target = new RenderTexture(120, 90, 24, RenderTextureFormat.ARGB32);
            bool oldOrthographic = camera.orthographic;
            float oldOrthographicSize = camera.orthographicSize;
            float oldNear = camera.nearClipPlane;
            float oldFar = camera.farClipPlane;
            RenderTexture oldTarget = camera.targetTexture;

            int3 witnessChunk = default;
            int3 coreRegion = default;
            bool foundWitness = false;
            bool sawFrustum = false;
            bool sawCoreResident = false;
            bool sawCorePin = false;
            VoxelSurfaceMetrics metrics = default;
            int frames = 0;

            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.orthographic = true;
                camera.orthographicSize = 24f;
                const float distance = 240f;
                camera.transform.position = castleCentre + new Vector3(0f, 20f, -distance);
                camera.transform.LookAt(castleCentre + Vector3.up * 10f);
                camera.nearClipPlane = distance - 32f;
                camera.farClipPlane = distance + 32f;

                double deadline = Time.realtimeSinceStartupAsDouble + 20.0;
                while (frames++ < 1200 && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    metrics = VoxelRenderBridge.SurfaceMetrics;

                    if (!foundWitness
                        && TryFindNearestStep4ActiveSlot(
                            camera, castleCentre, out witnessChunk, out Bounds witnessBounds))
                    {
                        foundWitness = true;
                        coreRegion = Step4CoreRegion(witnessChunk);
                        AimAtWitness(camera, witnessBounds);
                        continue;
                    }

                    if (!foundWitness) continue;
                    sawFrustum |= metrics.Step4VisibilityFrustum > 0;
                    if (!sawFrustum) continue;

                    IRegionReadSource reads = world.ReadStorage;
                    bool residentNow = reads.IsRegionResident(coreRegion);
                    sawCoreResident |= residentNow;
                    if (!reads.TryPinRegionBlockRefs(coreRegion, out PinnedRegionBlockRefs pinned))
                        continue;

                    VoxelRegionPinToken token = pinned.Pin;
                    reads.ReleasePinnedRegion(in token);
                    sawCorePin = true;
                    sawCoreResident |= residentNow;
                    break;
                }

                string evidence = Evidence(
                    world, in metrics, frames, witnessChunk, coreRegion,
                    foundWitness, sawFrustum, sawCoreResident, sawCorePin);
                Debug.Log($"[Step4CoreResidencyGate] {evidence}");

                Assert.True(foundWitness,
                    "Step-4 residency gate never found a real active slot; " + evidence);
                Assert.True(sawFrustum,
                    "Step-4 residency witness never entered the production camera frustum; " + evidence);
                Assert.True(sawCoreResident,
                    "An active frustum step-4 chunk never acquired its owned core region; " + evidence);
                Assert.True(sawCorePin,
                    "An active frustum step-4 chunk's owned core region never became metadata-pin-able; "
                  + evidence);
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

        private static int3 Step4CoreRegion(int3 chunk)
        {
            int chunkVoxels = CpuTransvoxelChunkCache.CellsPerAxis * Step4SourceStep;
            int3 minVoxel = chunk * chunkVoxels;
            int3 maxVoxel = minVoxel + chunkVoxels - 1;
            int edge = ShowcaseWorld.RegionVoxelEdge;
            int3 minRegion = FloorDiv(minVoxel, edge);
            int3 maxRegion = FloorDiv(maxVoxel, edge);
            Assert.AreEqual(minRegion, maxRegion,
                $"Production step-4 chunk {chunk} unexpectedly spans multiple owned core regions: "
              + $"{minRegion}..{maxRegion}.");
            return minRegion;
        }

        private static int3 FloorDiv(int3 value, int divisor) => new(
            FloorDiv(value.x, divisor),
            FloorDiv(value.y, divisor),
            FloorDiv(value.z, divisor));

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static bool TryFindNearestStep4ActiveSlot(
            Camera camera, Vector3 castleCentre, out int3 coordinate, out Bounds bounds)
        {
            coordinate = default;
            bounds = default;
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
                    int3 candidate = (int3)activeCoordinateMethod.Invoke(
                        ring, new object[] { i });
                    Bounds candidateBounds = Step4ChunkBounds(candidate, voxelSize);
                    if (!WithinStep4Band(candidateBounds, cameraPosition)) continue;

                    float score = (candidateBounds.center - castleCentre).sqrMagnitude;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    coordinate = candidate;
                    bounds = candidateBounds;
                    found = true;
                }
            }

            return found;
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

        private static void AimAtWitness(Camera camera, Bounds bounds)
        {
            Vector3 toWitness = bounds.center - camera.transform.position;
            float distance = toWitness.magnitude;
            float radius = bounds.extents.magnitude;
            camera.transform.LookAt(bounds.center);
            camera.orthographicSize = Mathf.Max(24f, radius + 2f);
            camera.nearClipPlane = Mathf.Max(0.05f, distance - radius - 2f);
            camera.farClipPlane = distance + radius + 2f;
        }

        private static string Evidence(
            ShowcaseWorld world, in VoxelSurfaceMetrics metrics, int frames,
            int3 witnessChunk, int3 coreRegion, bool foundWitness, bool sawFrustum,
            bool sawCoreResident, bool sawCorePin) =>
            $"frames={frames} witnessFound={foundWitness} witness={witnessChunk} "
          + $"core={coreRegion} frustumSeen={sawFrustum} residentSeen={sawCoreResident} "
          + $"pinSeen={sawCorePin} pendingLoads={world.PendingRegionLoads} "
          + $"generation={world.GenerationProgress:0.00} "
          + $"step4=known:{metrics.Step4KnownChunks}/resident:{metrics.Step4ResidentChunks}/"
          + $"dirty:{metrics.Step4DirtyChunks}/missing:{metrics.Step4MissingVisibleChunks}/"
          + $"jobs:{metrics.Step4RunningJobs} "
          + $"visibility=known:{metrics.Step4VisibilityKnown}/inBand:{metrics.Step4VisibilityInBand}/"
          + $"frustum:{metrics.Step4VisibilityFrustum}/ready:{metrics.Step4VisibilityReady}/"
          + $"empty:{metrics.Step4VisibilityEmpty} "
          + $"metadata={metrics.Step4ExactMetadataScheduled}/{metrics.Step4ExactMetadataCompleted}/"
          + $"revReject:{metrics.Step4ExactMetadataRevisionRejects}/"
          + $"pinReject:{metrics.Step4ExactMetadataPinRejects}.";

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
                "Showcase lost its render-world binding before step-4 residency validation.");
        }

        private static void RenderUrpCamera(Camera camera)
        {
            Assert.NotNull(camera.targetTexture);
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = camera.targetTexture,
            };
            Assert.True(RenderPipeline.SupportsRenderRequest(camera, request));
            VoxelRenderBridge.ResetSurfacePassDiagnostics("step4-core-residency");
            RenderPipeline.SubmitRenderRequest(camera, request);
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0,
                "Step-4 residency request did not execute VoxelRenderPass.");
            Assert.AreEqual("feature-aware", VoxelRenderBridge.LastSurfacePassState);
        }
    }
}
