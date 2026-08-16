using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class LodRenderingTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [Test]
        public void StepEightUsesFeaturePreservingBlockHlod()
        {
            Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(8),
                "Step 8 must not turn an any-solid 8^3 storage block into a render sample.");
            using var cache = new CpuTransvoxelChunkCache(8);
            Assert.False(cache.SamplesFromMips,
                "The castle's outer LOD must never use OR-collapsed Storage occupancy as density.");
            Assert.True(cache.UsesBlockHlod,
                "Step 8 must mesh spatial 4^3 HLOD subcells derived from exact COW inputs.");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator CastleKeepsVoxelGeometryAcrossEveryLodBand()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return WaitForAtomicWorldReady();

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Camera camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            Vector3 centre = new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                                         plan.Centre.z) * 0.1f;
            Vector3 lookAt = centre + Vector3.up * 10f;
            var bands = new (int step, float distance)[]
            {
                (1, 48f), (2, 144f), (4, 240f), (8, 340f),
            };

            // 4:3 keeps the 24 m orthographic half-height to ~64 m horizontal, matching the
            // maximum bailey+tower width instead of admitting unrelated terrain at 16:9.
            var target = new RenderTexture(120, 90, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(target.width, target.height,
                                         TextureFormat.RGB24, false, true);
            bool oldOrthographic = camera.orthographic;
            float oldOrthographicSize = camera.orthographicSize;
            float oldNearClipPlane = camera.nearClipPlane;
            float oldFarClipPlane = camera.farClipPlane;
            double oldSolidBuildBudgetMs = VoxelRenderBridge.SolidBuildBudgetMs;
            try
            {
                // This fixture is an offline visual-fidelity capture, not the frame-budget gate.
                // The capture is castle-focused rather than a whole-clipmap residency test. Give
                // extraction enough CPU admission to make the existing 8-second hole-free precondition
                // meaningful; the separate budget/stress fixtures keep production constraints.
                VoxelRenderBridge.SolidBuildBudgetMs = 8.0;
                camera.targetTexture = target;
                camera.orthographic = true;
                // Bailey+tower masonry is at most about 64 m wide and the keep is below ~28 m
                // tall. With the 4:3 target, a 24 m half-height gives a ~64x48 m architectural
                // frame: enough for the castle while intentionally excluding most of its broad
                // sculpted terrain skirt, which is not what this LOD edge-regression compares.
                camera.orthographicSize = 24f;
                CastleStructureSignature reference = default;

                foreach (var band in bands)
                {
                    // Fixed orthographic framing means camera distance selects the LOD ring without
                    // shrinking the castle on screen. Loss of openings/architectural edges is now
                    // measurable rather than hidden by perspective.
                    camera.transform.position = centre + new Vector3(0f, 20f, -band.distance);
                    camera.transform.LookAt(lookAt);
                    // The bailey plus towers fits within roughly +/-32 m in depth. Scope the
                    // strict MissingVisible==0 precondition to that architecture instead of the
                    // wider terrain skirt and unrelated rings in front of/behind the landmark.
                    camera.nearClipPlane = Mathf.Max(0.3f, band.distance - 32f);
                    camera.farClipPlane = band.distance + 32f;

                    // Geometry is deliberately asynchronous, but this fixture is a visual LOD
                    // fidelity test rather than a clipmap-residency test. The fixed arena cannot
                    // and should not make every frustum-intersecting terrain chunk resident just
                    // to take a castle screenshot. Instead sample the same central castle crop
                    // every 12 frames and require it to be structurally stable across three
                    // captures. Runtime hole/backpressure behavior is covered by stress fixtures.
                    VoxelSurfaceMetrics metrics = default;
                    CastleStructureSignature signature = default;
                    CastleStructureSignature previousSignature = default;
                    bool hasPreviousSignature = false;
                    bool converged = false;
                    int stableSamples = 0;
                    int convergenceFrames = 0;
                    double convergenceDeadline = Time.realtimeSinceStartupAsDouble + 8.0;
                    while (convergenceFrames++ < 480
                           && Time.realtimeSinceStartupAsDouble < convergenceDeadline)
                    {
                        RenderUrpCamera(camera);
                        yield return null;
                        metrics = VoxelRenderBridge.SurfaceMetrics;
                        if ((convergenceFrames % 12) != 0) continue;

                        CastleStructureSignature candidate = CaptureCastleStructure(target, readback);
                        bool substantial = candidate.EdgeCount > 40;
                        bool stable = false;
                        if (substantial && hasPreviousSignature && previousSignature.EdgeCount > 0)
                        {
                            float edgeRatio = Mathf.Min(candidate.EdgeCount, previousSignature.EdgeCount)
                                            / (float)Mathf.Max(candidate.EdgeCount, previousSignature.EdgeCount);
                            float recall = MatchedEdgeRecall(previousSignature, candidate, 1);
                            stable = edgeRatio >= 0.97f && recall >= 0.97f;
                        }

                        stableSamples = stable ? stableSamples + 1 : 0;
                        previousSignature = candidate;
                        hasPreviousSignature = true;
                        if (stableSamples < 2) continue;

                        signature = candidate;
                        converged = true;
                        break;
                    }

                    Assert.True(converged,
                        $"LOD step {band.step} did not reach a stable castle capture "
                      + $"within {convergenceFrames} frames / 8 seconds; "
                      + $"known={metrics.SolidKnownChunks} resident={metrics.SolidResidentChunks} "
                      + $"dirty={metrics.SolidDirtyChunks} visible={metrics.VisibleSolidChunks} "
                      + $"missing={metrics.MissingVisibleSolidChunks} jobs={metrics.RunningSolidJobs} "
                      + $"pendingUpload={metrics.SolidPendingUploadBytes} "
                      + $"completed={metrics.CompletedSolidBuilds} uploaded={metrics.UploadedGeometryBytes} "
                      + $"prepareP95={metrics.SchedulerPrepareTiming.P95Ms:F2}ms "
                      + $"queueP95={metrics.QueueLatencyTiming.P95Ms:F1}ms "
                      + $"buildP95={metrics.BuildLatencyTiming.P95Ms:F1}ms "
                      + $"snapshotP95={metrics.SnapshotTiming.P95Ms:F2}ms "
                      + $"arena={metrics.SolidArenaUsedBytes}/{metrics.SolidArenaCommittedBytes}B "
                      + $"leases={metrics.SolidArenaActiveLeases} "
                      + $"arenaFailures={metrics.SolidArenaAllocationFailures} "
                      + $"pressureEvictions={metrics.SolidArenaPressureEvictions} "
                      + $"capacityEvents={metrics.SolidCapacityPressureEvents}.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"LOD step {band.step} produced no visible voxel geometry.");
                    Assert.Greater(metrics.UploadedGeometryBytes, 0ul,
                        $"LOD step {band.step} did not use the voxel surface extractor.");
                    Assert.Greater(signature.EdgeCount, 40,
                        $"LOD step {band.step} produced too little stable castle structure to inspect.");

                    if (band.step == 1)
                    {
                        reference = signature;
                        Assert.Greater(reference.EdgeCount, 120,
                            "Step-1 castle reference lacks enough internal edges for a useful LOD regression.");
                        continue;
                    }

                    float retainedEdges = signature.EdgeCount / (float)reference.EdgeCount;
                    float matchedReference = MatchedEdgeRecall(reference, signature, 2);
                    Assert.GreaterOrEqual(retainedEdges, 0.18f,
                        $"LOD step {band.step} collapsed too much architectural edge structure "
                      + $"({retainedEdges:P0} of step-1). A filled grey mass must not pass.");
                    Assert.GreaterOrEqual(matchedReference, 0.18f,
                        $"LOD step {band.step} no longer preserves the castle's step-1 edge layout "
                      + $"({matchedReference:P0} matched). Openings/silhouette were likely collapsed.");
                }
            }
            finally
            {
                VoxelRenderBridge.SolidBuildBudgetMs = oldSolidBuildBudgetMs;
                camera.targetTexture = null;
                camera.orthographic = oldOrthographic;
                camera.orthographicSize = oldOrthographicSize;
                camera.nearClipPlane = oldNearClipPlane;
                camera.farClipPlane = oldFarClipPlane;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator GeometryUploadStaysWithinGlobalBudgetWhileCrossingLodBands()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return WaitForAtomicWorldReady();

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Camera camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            Vector3 centre = new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                                         plan.Centre.z) * 0.1f;
            Vector3 lookAt = centre + Vector3.up * 10f;

            int oldBudget = VoxelRenderBridge.SolidUploadBudgetBytes;
            int oldSlice = VoxelRenderBridge.SolidUploadSliceBytes;
            int oldWorkers = VoxelRenderBridge.SolidUploadWorkerBudget;
            double oldUploadMs = VoxelRenderBridge.SolidUploadBudgetMs;
            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            bool sawUpload = false;
            bool sawQueuedReplacement = false;
            try
            {
                // Make payload bytes, not wall-clock time, the limiting factor so this test proves
                // a large replacement spans frames instead of being published in one render call.
                VoxelRenderBridge.SolidUploadBudgetBytes = 16 * 1024;
                VoxelRenderBridge.SolidUploadSliceBytes = 4 * 1024;
                VoxelRenderBridge.SolidUploadWorkerBudget = 4;
                VoxelRenderBridge.SolidUploadBudgetMs = 5.0;
                camera.targetTexture = target;

                for (int frame = 0; frame < 180; frame++)
                {
                    float phase = Mathf.PingPong(frame / 90f, 1f);
                    float distance = Mathf.Lerp(48f, 380f, phase);
                    camera.transform.position = centre
                        + new Vector3(Mathf.Sin(frame * 0.07f) * 18f, 20f, -distance);
                    camera.transform.LookAt(lookAt);
                    RenderUrpCamera(camera);
                    yield return null;

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(16 * 1024, metrics.SolidUploadBudgetBytes,
                        "Render pass did not apply the renderer-wide upload budget.");
                    Assert.LessOrEqual(metrics.LastFrameSolidUploadedBytes,
                        metrics.SolidUploadBudgetBytes,
                        "Solid geometry upload exceeded the renderer-wide frame budget.");
                    sawUpload |= metrics.LastFrameSolidUploadedBytes > 0;
                    sawQueuedReplacement |= metrics.SolidPendingUploadBytes > 0;
                }

                Assert.IsTrue(sawUpload,
                    "LOD traversal never exercised solid geometry publication.");
                Assert.IsTrue(sawQueuedReplacement,
                    "A 16 KiB frame cap should force at least one replacement to remain queued.");
            }
            finally
            {
                VoxelRenderBridge.SolidUploadBudgetBytes = oldBudget;
                VoxelRenderBridge.SolidUploadSliceBytes = oldSlice;
                VoxelRenderBridge.SolidUploadWorkerBudget = oldWorkers;
                VoxelRenderBridge.SolidUploadBudgetMs = oldUploadMs;
                camera.targetTexture = null;
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
            {
                yield return null;
            }

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = showcase != null
                ? (ShowcaseWorld)typeof(VoxelShowcase)
                    .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(showcase)
                : null;
            string startup = world == null
                ? "world=null"
                : $"castleRegions={world.ReadyCastleRegions}/{world.RequiredCastleRegions} "
                + $"pendingLoads={world.PendingRegionLoads} generated={world.RegionsGenerated} "
                + $"buildStage={world.CastleBuildStage} lastStage={world.LastCastleStage} "
                + $"lastStageMs={world.LastCastleStageMs:F2} maxStage={world.MaxCastleStage} "
                + $"maxStageMs={world.MaxCastleStageMs:F2} castleVoxels={world.CastleVoxels}";

            Assert.True(VoxelRenderBridge.SurfaceBuildEnabled,
                $"Showcase atomic world did not commit within {frames} frames / 60 seconds; {startup}.");
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                $"Showcase lost its render-world binding while waiting for atomic publication; {startup}.");
        }

        private static void RenderUrpCamera(Camera camera)
        {
            Assert.NotNull(camera);
            Assert.NotNull(camera.targetTexture,
                "LOD render-request tests require an explicit RenderTexture destination.");
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "VoxelShowcase did not register a valid render world before the URP request.");

            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = camera.targetTexture,
            };
            Assert.True(RenderPipeline.SupportsRenderRequest(camera, request),
                "Active URP renderer does not support SingleCameraRequest for the showcase camera.");

            VoxelRenderBridge.ResetSurfacePassDiagnostics("before-render-request");
            RenderPipeline.SubmitRenderRequest(camera, request);

            Assert.Greater(VoxelRenderBridge.RenderFeatureEnqueueCount, 0,
                "URP render request did not enqueue VoxelRenderFeature.");
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0,
                "URP render request did not record VoxelRenderPass.");
            Assert.AreEqual("feature-aware", VoxelRenderBridge.LastSurfacePassState,
                $"VoxelRenderPass returned early: {VoxelRenderBridge.LastSurfacePassState}.");
        }

        private readonly struct CastleStructureSignature
        {
            public readonly int Width;
            public readonly int Height;
            public readonly bool[] Edges;
            public readonly int EdgeCount;

            public CastleStructureSignature(int width, int height, bool[] edges, int edgeCount)
            {
                Width = width;
                Height = height;
                Edges = edges;
                EdgeCount = edgeCount;
            }
        }

        private static CastleStructureSignature CaptureCastleStructure(
            RenderTexture target, Texture2D readback)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                readback.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Color32[] pixels = readback.GetPixels32();
            var edges = new bool[target.width * target.height];
            int edgeCount = 0;

            // Central keep/inner-castle crop. Excluding most sky/terrain prevents a stable horizon
            // from making a blob look structurally similar to the reference.
            int minX = target.width / 4;
            int maxX = target.width * 3 / 4;
            int minY = target.height / 5;
            int maxY = target.height * 17 / 20;
            const float threshold = 0.045f;

            for (int y = minY; y < maxY - 1; y++)
            for (int x = minX; x < maxX - 1; x++)
            {
                int i = x + y * target.width;
                float l = Luminance(pixels[i]);
                float dx = Mathf.Abs(l - Luminance(pixels[i + 1]));
                float dy = Mathf.Abs(l - Luminance(pixels[i + target.width]));
                if (Mathf.Max(dx, dy) < threshold) continue;
                edges[i] = true;
                edgeCount++;
            }

            return new CastleStructureSignature(target.width, target.height, edges, edgeCount);
        }

        private static float MatchedEdgeRecall(in CastleStructureSignature reference,
                                               in CastleStructureSignature candidate,
                                               int tolerancePixels)
        {
            Assert.AreEqual(reference.Width, candidate.Width);
            Assert.AreEqual(reference.Height, candidate.Height);
            if (reference.EdgeCount == 0) return 0f;

            int matched = 0;
            for (int y = 0; y < reference.Height; y++)
            for (int x = 0; x < reference.Width; x++)
            {
                int i = x + y * reference.Width;
                if (!reference.Edges[i]) continue;

                bool found = false;
                int minY = Mathf.Max(0, y - tolerancePixels);
                int maxY = Mathf.Min(candidate.Height - 1, y + tolerancePixels);
                int minX = Mathf.Max(0, x - tolerancePixels);
                int maxX = Mathf.Min(candidate.Width - 1, x + tolerancePixels);
                for (int cy = minY; cy <= maxY && !found; cy++)
                for (int cx = minX; cx <= maxX; cx++)
                {
                    if (!candidate.Edges[cx + cy * candidate.Width]) continue;
                    found = true;
                    break;
                }
                if (found) matched++;
            }
            return matched / (float)reference.EdgeCount;
        }

        private static float Luminance(Color32 colour) =>
            (0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b) / 255f;
    }
}
