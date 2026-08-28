using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Regression for SceneIssue 20260825-192751-413: the legacy per-worker GPU-v1 cutover must
    /// stay out of production after two exact traversal runs lost every visible voxel draw. The
    /// optimized CPU renderer must preserve moving coverage and frame-time gates at the scene's
    /// actual unsprinted fly-speed ceiling while GPU-v1 remains explicit diagnostic opt-in only.
    /// </summary>
    public sealed class ShowcaseGpuMigrationTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const double MaxCoverageWarmupSeconds = 30.0;
        private const double MaxTraversalSeconds = 30.0;
        private const float TraversalDistanceMetres = 210f;
        private const float MaxTraversalStepMetres = 0.5f;
        private const double MaxMovingP95FrameMs = 18.0;
        private const double MaxMovingP99FrameMs = 25.0;

        [UnityTest, Timeout(900000)]
        public IEnumerator MovingShowcaseKeepsLegacyGpuV1OffAndPreservesCoverage()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            VoxelFarTerrain far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(far);
            Assert.NotNull(camera);
            Assert.True(CpuTransvoxelChunkCache.GpuCutoverDisabled,
                "Production startup did not apply the legacy GPU-v1 safety gate.");

            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);
            float productionFlySpeed = GetShowcaseField<float>(showcase, "m_FlySpeed");
            Assert.Greater(productionFlySpeed, 0f,
                "Showcase production fly speed must be positive for traversal coverage.");

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseGpuMigrationTests.ProductionSafety",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                yield return WaitForFallbackSafeVisibleCoverage(
                    camera, far, MaxCoverageWarmupSeconds);

                Vector3 origin = showcase.transform.position;
                Quaternion originRotation = showcase.transform.rotation;
                Vector3 position = origin;
                float pathMetres = 0f;
                ulong initialGpuCompleted = VoxelRenderBridge.SurfaceMetrics.GpuCompletedSolidBuilds;
                var frameTimesMs = new List<double>(1024);
                var frameClock = new Stopwatch();
                var traversalClock = Stopwatch.StartNew();
                double previousMotionSeconds = traversalClock.Elapsed.TotalSeconds;
                bool sawStreamingWork = false;
                int crossedRegionBoundaries = 0;
                int previousRegionX = Mathf.FloorToInt(origin.x / ShowcaseWorld.RegionMetres);
                int frame = 0;
                VoxelSurfaceMetrics last = default;

                // The old regression advanced 0.5 m on every rendered frame. At its own 18 ms
                // budget that is already ~28 m/s, and at the scene's target frame rates it becomes
                // hundreds of metres per second. That tests a frame-rate-dependent teleport, not
                // player movement. Advance by real elapsed time at the scene's unsprinted fly-speed
                // ceiling, retaining the 0.5 m per-frame cap so a slow CI frame can never make the
                // test less strict than the old spatial step. A gentle heading weave still forces
                // clipmap changes off one perfectly aligned axis.
                while (position.x - origin.x < TraversalDistanceMetres)
                {
                    double nowSeconds = traversalClock.Elapsed.TotalSeconds;
                    float deltaSeconds = (float)(nowSeconds - previousMotionSeconds);
                    previousMotionSeconds = nowSeconds;
                    float step = Mathf.Min(
                        MaxTraversalStepMetres,
                        productionFlySpeed * Mathf.Max(0f, deltaSeconds));

                    if (step > 0f)
                    {
                        float phase = pathMetres / TraversalDistanceMetres * Mathf.PI * 6f;
                        float headingRadians = Mathf.Sin(phase) * 20f * Mathf.Deg2Rad;
                        Vector3 direction = new(
                            Mathf.Cos(headingRadians), 0f, Mathf.Sin(headingRadians));
                        float remainingX = TraversalDistanceMetres - (position.x - origin.x);
                        step = Mathf.Min(step, remainingX / Mathf.Max(0.01f, direction.x));
                        position += direction * step;
                        pathMetres += step;
                    }

                    showcase.transform.position = position;
                    showcase.transform.rotation = originRotation;

                    int regionX = Mathf.FloorToInt(position.x / ShowcaseWorld.RegionMetres);
                    if (regionX != previousRegionX)
                    {
                        crossedRegionBoundaries += Mathf.Abs(regionX - previousRegionX);
                        previousRegionX = regionX;
                    }

                    frameClock.Restart();
                    yield return null;
                    camera.Render();
                    frameClock.Stop();
                    frameTimesMs.Add(frameClock.Elapsed.TotalMilliseconds);
                    last = VoxelRenderBridge.SurfaceMetrics;

                    Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                        $"Production safety frame {frame} synchronously completed geometry work.");
                    if (last.VisibleSolidChunks <= 0)
                        Assert.Fail(DescribeVisibilityFailure(
                            frame, in last, showcase.transform, far));
                    Assert.AreEqual(0, last.GpuResidentBackends,
                        $"Production safety frame {frame} allocated a legacy GPU-v1 backend.");

                    if (NearCoverageIsIncomplete(in last))
                    {
                        Assert.LessOrEqual(far.HoleRadiusMetres, 0.05f,
                            $"Production safety frame {frame} had incomplete near coverage but "
                          + $"opened a {far.HoleRadiusMetres:F2} m far-field hole.");
                    }

                    sawStreamingWork |= last.SolidDirtyChunks > 0
                                     || last.RunningSolidJobs > 0
                                     || last.SolidMeshesAwaitingUpload > 0
                                     || last.SolidPendingUploadBytes > 0
                                     || last.LastFrameSolidUploadedBytes > 0;

                    if (traversalClock.Elapsed.TotalSeconds > MaxTraversalSeconds)
                    {
                        Assert.Fail(
                            $"Production-speed traversal exceeded {MaxTraversalSeconds:F0}s after "
                          + $"{position.x - origin.x:F1}/{TraversalDistanceMetres:F0} m and "
                          + $"{frame + 1} rendered frames.");
                    }
                    frame++;
                }

                Assert.GreaterOrEqual(position.x - origin.x, TraversalDistanceMetres - 0.01f,
                    "Production safety traversal did not cover the required world-space distance.");
                Assert.GreaterOrEqual(crossedRegionBoundaries, 4,
                    "Production safety traversal did not cross enough region boundaries.");
                Assert.True(sawStreamingWork,
                    "Production safety traversal never exercised streaming/publication work.");
                Assert.False(last.GpuCutoverAvailable,
                    "Production workers still advertised the legacy GPU-v1 cutover.");
                Assert.AreEqual(initialGpuCompleted, last.GpuCompletedSolidBuilds,
                    "Production completed GPU-v1 surface builds despite the safety rollback.");

                frameTimesMs.Sort();
                double p95 = Percentile(frameTimesMs, 0.95);
                double p99 = Percentile(frameTimesMs, 0.99);
                UnityEngine.Debug.Log(
                    $"### SHOWCASE_GPU_ROLLBACK_TRAVERSAL frames={frameTimesMs.Count} "
                  + $"distance={position.x - origin.x:F1}m path={pathMetres:F1}m "
                  + $"speedCap={productionFlySpeed:F1}m/s p95={p95:F3}ms p99={p99:F3}ms "
                  + $"max={frameTimesMs[^1]:F3}ms");
                Assert.Less(p95, MaxMovingP95FrameMs,
                    $"Production CPU traversal p95 regressed to {p95:F3} ms.");
                Assert.Less(p99, MaxMovingP99FrameMs,
                    $"Production CPU traversal p99 regressed to {p99:F3} ms.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static IEnumerator WaitForFallbackSafeVisibleCoverage(
            Camera camera, VoxelFarTerrain far, double maxSeconds)
        {
            var warmupClock = Stopwatch.StartNew();
            int renderedFrames = 0;
            int stableFrames = 0;
            VoxelSurfaceMetrics last = default;
            while (warmupClock.Elapsed.TotalSeconds < maxSeconds)
            {
                renderedFrames++;
                yield return null;
                camera.Render();
                last = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                    "Geometry work blocked while preparing production traversal coverage.");

                bool nearIncomplete = NearCoverageIsIncomplete(in last);
                bool fallbackSafe = !nearIncomplete || far.HoleRadiusMetres <= 0.05f;
                stableFrames = last.VisibleSolidChunks > 0 && fallbackSafe
                    ? stableFrames + 1 : 0;
                if (stableFrames >= 4) yield break;
            }

            Assert.Fail(
                $"Showcase never reached fallback-safe visible coverage within "
              + $"{maxSeconds:F0}s ({renderedFrames} rendered frames); "
              + $"known={last.SolidKnownChunks} resident={last.SolidResidentChunks} "
              + $"dirty={last.SolidDirtyChunks} visible={last.VisibleSolidChunks} "
              + $"missing={last.MissingVisibleSolidChunks} jobs={last.RunningSolidJobs} "
              + $"farHole={far.HoleRadiusMetres:F2}m.");
        }

        private static string DescribeVisibilityFailure(
            int frame,
            in VoxelSurfaceMetrics metrics,
            Transform pose,
            VoxelFarTerrain far)
        {
            Vector3 position = pose.position;
            return $"Production safety frame {frame} lost every visible voxel draw; "
                 + $"camera=({position.x:F2},{position.y:F2},{position.z:F2}) "
                 + $"farHole={far.HoleRadiusMetres:F2}m "
                 + $"known={metrics.SolidKnownChunks} resident={metrics.SolidResidentChunks} "
                 + $"dirty={metrics.SolidDirtyChunks} missing={metrics.MissingVisibleSolidChunks} "
                 + $"jobs={metrics.RunningSolidJobs} uploads={metrics.SolidMeshesAwaitingUpload} "
                 + $"candidates={metrics.VisibilityKnownCandidates}/{metrics.VisibilityInBandCandidates}/"
                 + $"{metrics.VisibilityFrustumCandidates} "
                 + $"step4={metrics.Step4VisibilityKnown}/{metrics.Step4VisibilityInBand}/"
                 + $"{metrics.Step4VisibilityFrustum}/{metrics.Step4VisibilityReady}/"
                 + $"{metrics.Step4VisibilityEmpty}; "
                 + DescribePhysicalWorkerVisibility();
        }

        /// <summary>
        /// Test-only minimal isolation for the first zero-draw frame. Production workers have
        /// already populated their read-only Visible lists when this runs. If physicalTotal is
        /// positive while aggregate VisibleSolidChunks is zero, cross-ring aggregation erased
        /// valid geometry. If physicalTotal is also zero, the loss occurred earlier in the
        /// worker/ring visibility funnel. Reflection avoids adding a runtime diagnostics API.
        /// </summary>
        private static string DescribePhysicalWorkerVisibility()
        {
            PropertyInfo activePassProperty = typeof(VoxelRenderBridge).GetProperty(
                "ActivePass", BindingFlags.Static | BindingFlags.NonPublic);
            object pass = activePassProperty?.GetValue(null);
            if (pass == null) return "physicalVisibility=unavailable(pass-null)";

            FieldInfo schedulerField = pass.GetType().GetField(
                "_scheduler", BindingFlags.Instance | BindingFlags.NonPublic);
            object scheduler = schedulerField?.GetValue(pass);
            if (scheduler == null) return "physicalVisibility=unavailable(scheduler-null)";

            FieldInfo ringsField = scheduler.GetType().GetField(
                "_rings", BindingFlags.Instance | BindingFlags.NonPublic);
            System.Array rings = ringsField?.GetValue(scheduler) as System.Array;
            if (rings == null) return "physicalVisibility=unavailable(rings-null)";

            int physicalTotal = 0;
            var details = new System.Text.StringBuilder();
            for (int r = 0; r < rings.Length; r++)
            {
                object ring = rings.GetValue(r);
                if (ring == null) continue;
                FieldInfo sourceStepField = ring.GetType().GetField(
                    "SourceStep", BindingFlags.Instance | BindingFlags.Public);
                FieldInfo workersField = ring.GetType().GetField(
                    "Workers", BindingFlags.Instance | BindingFlags.Public);
                int sourceStep = sourceStepField != null
                    ? (int)sourceStepField.GetValue(ring) : -1;
                CpuTransvoxelChunkCache[] workers =
                    workersField?.GetValue(ring) as CpuTransvoxelChunkCache[];
                if (workers == null) continue;

                int known = 0;
                int inBand = 0;
                int frustum = 0;
                int ready = 0;
                int empty = 0;
                int visible = 0;
                for (int w = 0; w < workers.Length; w++)
                {
                    CpuTransvoxelChunkCache worker = workers[w];
                    known += worker.LastVisibilityKnownCount;
                    inBand += worker.LastVisibilityInBandCount;
                    frustum += worker.LastVisibilityFrustumCount;
                    ready += worker.LastVisibilityReadyCount;
                    empty += worker.LastVisibilityEmptyCount;
                    visible += worker.Visible.Count;
                }

                physicalTotal += visible;
                if (details.Length > 0) details.Append(' ');
                details.Append("s").Append(sourceStep)
                    .Append("=").Append(known).Append('/')
                    .Append(inBand).Append('/').Append(frustum).Append('/')
                    .Append(ready).Append('/').Append(empty)
                    .Append(" physical=").Append(visible);
            }

            return $"physicalTotal={physicalTotal} rings[{details}]";
        }

        private static bool NearCoverageIsIncomplete(in VoxelSurfaceMetrics metrics) =>
            metrics.MissingVisibleSolidChunks > 0
            || metrics.SolidDirtyChunks > 0
            || metrics.RunningSolidJobs > 0
            || metrics.SolidMeshesAwaitingUpload > 0
            || metrics.SolidPendingUploadBytes > 0;

        private static double Percentile(List<double> sorted, double percentile)
        {
            if (sorted.Count == 0) return 0.0;
            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(percentile * sorted.Count)) - 1,
                0, sorted.Count - 1);
            return sorted[index];
        }

        private static T GetShowcaseField<T>(VoxelShowcase showcase, string name)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"VoxelShowcase field '{name}' was not found.");
            return (T)field.GetValue(showcase);
        }

        private static void SetShowcaseField<T>(VoxelShowcase showcase, string name, T value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"VoxelShowcase field '{name}' was not found.");
            field.SetValue(showcase, value);
        }
    }
}