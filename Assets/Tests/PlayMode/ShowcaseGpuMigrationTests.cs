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
    /// Behavioral regression for SceneIssue 20260825-192751-413.
    ///
    /// Supported step-1/step-2 chunks must take the production GPU surface path with no silent CPU
    /// fallback once classified GPU-eligible. Geometry/rings the GPU backend does not implement may
    /// continue through the CPU path. The old ready geometry must remain visible during replacement,
    /// traversal must move at the scene's actual fly-speed cap, and the final run reports both
    /// moving-frame latency and settled stationary headroom.
    /// </summary>
    public sealed class ShowcaseGpuMigrationTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const double MaxCoverageWarmupSeconds = 30.0;
        // Liveness only. Movement intentionally caps displacement per rendered frame, so slow
        // frames cannot be hidden by catch-up motion; the moving p95/p99 assertions remain the
        // performance gates for the same traversal.
        private const double MaxTraversalSeconds = 45.0;
        private const double MaxStationarySettleSeconds = 20.0;
        private const float TraversalDistanceMetres = 210f;
        private const float MaxTraversalStepMetres = 0.5f;
        private const int MinimumGpuBuildsDuringTraversal = 8;
        private const int StationaryBenchmarkFrames = 240;
        private const double MaxMovingP95FrameMs = 18.0;
        private const double MaxMovingP99FrameMs = 25.0;
        private const double MaxStationaryP95FrameMs = 8.0;

        [UnityTest, Timeout(900000)]
        public IEnumerator MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage()
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
            Assert.False(CpuTransvoxelChunkCache.GpuCutoverDisabled,
                "Production startup disabled the validated near-ring GPU surface path.");

            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);
            float productionFlySpeed = GetShowcaseField<float>(showcase, "m_FlySpeed");
            Assert.Greater(productionFlySpeed, 0f,
                "Showcase production fly speed must be positive for traversal coverage.");

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseGpuMigrationTests.ProductionGpu",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                yield return WaitForFallbackSafeVisibleCoverage(
                    camera, far, MaxCoverageWarmupSeconds);

                VoxelSurfaceMetrics initial = VoxelRenderBridge.SurfaceMetrics;
                Assert.True(initial.GpuCutoverAvailable,
                    "Production workers do not advertise the near-ring GPU cutover.");

                Vector3 origin = showcase.transform.position;
                Quaternion originRotation = showcase.transform.rotation;
                Vector3 position = origin;
                float pathMetres = 0f;
                ulong initialGpuCompleted = initial.GpuCompletedSolidBuilds;
                ulong initialCompleted = initial.CompletedSolidBuilds;
                ulong initialGpuFallback = initial.GpuFallbackSolidBuilds;
                ulong initialGpuWaits = initial.GpuReadbackWaitSlices;
                var frameTimesMs = new List<double>(1024);
                var frameClock = new Stopwatch();
                var traversalClock = Stopwatch.StartNew();
                double previousMotionSeconds = traversalClock.Elapsed.TotalSeconds;
                bool sawStreamingWork = false;
                bool sawGpuBackend = initial.GpuResidentBackends > 0;
                int maxGpuBackends = initial.GpuResidentBackends;
                int crossedRegionBoundaries = 0;
                int previousRegionX = Mathf.FloorToInt(origin.x / ShowcaseWorld.RegionMetres);
                int frame = 0;
                VoxelSurfaceMetrics last = initial;

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
                        $"GPU traversal frame {frame} synchronously completed geometry work.");
                    if (last.VisibleSolidChunks <= 0)
                        Assert.Fail(DescribeVisibilityFailure(
                            frame, in last, showcase.transform, far));

                    Assert.True(last.GpuCutoverAvailable,
                        $"GPU traversal frame {frame} lost production cutover availability.");
                    sawGpuBackend |= last.GpuResidentBackends > 0;
                    maxGpuBackends = Mathf.Max(maxGpuBackends, last.GpuResidentBackends);

                    if (NearCoverageIsIncomplete(in last))
                    {
                        Assert.LessOrEqual(far.HoleRadiusMetres, 0.05f,
                            $"GPU traversal frame {frame} had incomplete near coverage but opened "
                          + $"a {far.HoleRadiusMetres:F2} m far-field hole.");
                    }

                    sawStreamingWork |= last.SolidDirtyChunks > 0
                                     || last.RunningSolidJobs > 0
                                     || last.SolidMeshesAwaitingUpload > 0
                                     || last.SolidPendingUploadBytes > 0
                                     || last.LastFrameSolidUploadedBytes > 0;

                    if (traversalClock.Elapsed.TotalSeconds > MaxTraversalSeconds)
                    {
                        Assert.Fail(
                            $"Production-speed GPU traversal exceeded {MaxTraversalSeconds:F0}s after "
                          + $"{position.x - origin.x:F1}/{TraversalDistanceMetres:F0} m and "
                          + $"{frame + 1} rendered frames.");
                    }
                    frame++;
                }

                Assert.GreaterOrEqual(position.x - origin.x, TraversalDistanceMetres - 0.01f,
                    "GPU traversal did not cover the required world-space distance.");
                Assert.GreaterOrEqual(crossedRegionBoundaries, 4,
                    "GPU traversal did not cross enough region boundaries.");
                Assert.True(sawStreamingWork,
                    "GPU traversal never exercised streaming/publication work.");
                Assert.True(sawGpuBackend,
                    "No production worker allocated the GPU surface backend during traversal.");

                ulong gpuCompletedDelta = last.GpuCompletedSolidBuilds - initialGpuCompleted;
                ulong completedDelta = last.CompletedSolidBuilds - initialCompleted;
                ulong gpuFallbackDelta = last.GpuFallbackSolidBuilds - initialGpuFallback;
                ulong gpuWaitDelta = last.GpuReadbackWaitSlices - initialGpuWaits;
                ulong gpuEligibleAttempts = gpuCompletedDelta + gpuFallbackDelta;
                double gpuEligibleAdoption = gpuEligibleAttempts > 0
                    ? (double)gpuCompletedDelta / gpuEligibleAttempts : 0.0;
                double overallGpuShare = completedDelta > 0
                    ? (double)gpuCompletedDelta / completedDelta : 0.0;

                Assert.GreaterOrEqual(gpuCompletedDelta, (ulong)MinimumGpuBuildsDuringTraversal,
                    $"Production GPU path completed only {gpuCompletedDelta} chunks during the "
                  + $"210 m traversal; this is not sustained cutover adoption.");
                Assert.AreEqual(0ul, gpuFallbackDelta,
                    $"{gpuFallbackDelta} GPU-eligible solid builds fell back to CPU during the "
                  + $"210 m traversal. Implemented GPU paths require 100% adoption; "
                  + $"completed={gpuCompletedDelta}, eligibleAttempts={gpuEligibleAttempts}.");

                frameTimesMs.Sort();
                double movingP95 = Percentile(frameTimesMs, 0.95);
                double movingP99 = Percentile(frameTimesMs, 0.99);
                Assert.Less(movingP95, MaxMovingP95FrameMs,
                    $"Production GPU traversal p95 regressed to {movingP95:F3} ms.");
                Assert.Less(movingP99, MaxMovingP99FrameMs,
                    $"Production GPU traversal p99 regressed to {movingP99:F3} ms.");

                yield return WaitForSettledVisibleCoverage(
                    camera, MaxStationarySettleSeconds);

                var stationaryTimesMs = new List<double>(StationaryBenchmarkFrames);
                for (int i = 0; i < StationaryBenchmarkFrames; i++)
                {
                    frameClock.Restart();
                    yield return null;
                    camera.Render();
                    frameClock.Stop();
                    stationaryTimesMs.Add(frameClock.Elapsed.TotalMilliseconds);

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    last = metrics;
                    Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                        $"Stationary benchmark frame {i} synchronously completed geometry work.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"Stationary benchmark frame {i} lost visible voxel geometry.");
                }

                ulong gpuFallbackThroughStationary =
                    last.GpuFallbackSolidBuilds - initialGpuFallback;
                Assert.AreEqual(0ul, gpuFallbackThroughStationary,
                    $"{gpuFallbackThroughStationary} GPU-eligible solid builds fell back to CPU "
                  + "during traversal/settle. Implemented GPU paths require 100% adoption.");

                stationaryTimesMs.Sort();
                double stationaryP50 = Percentile(stationaryTimesMs, 0.50);
                double stationaryP95 = Percentile(stationaryTimesMs, 0.95);
                double stationaryFpsP50 = stationaryP50 > 0.0 ? 1000.0 / stationaryP50 : 0.0;
                double stationaryFpsP95 = stationaryP95 > 0.0 ? 1000.0 / stationaryP95 : 0.0;

                UnityEngine.Debug.Log(
                    $"### SHOWCASE_GPU_TRAVERSAL frames={frameTimesMs.Count} "
                  + $"distance={position.x - origin.x:F1}m path={pathMetres:F1}m "
                  + $"speedCap={productionFlySpeed:F1}m/s movingP95={movingP95:F3}ms "
                  + $"movingP99={movingP99:F3}ms movingMax={frameTimesMs[^1]:F3}ms "
                  + $"gpuCompleted={gpuCompletedDelta} totalCompleted={completedDelta} "
                  + $"gpuEligibleAdoption={gpuEligibleAdoption:P1} "
                  + $"overallGpuShare={overallGpuShare:P1} gpuFallback={gpuFallbackThroughStationary} "
                  + $"gpuWaitSlices={gpuWaitDelta} gpuBackendsMax={maxGpuBackends} "
                  + $"stationaryP50={stationaryP50:F3}ms stationaryP95={stationaryP95:F3}ms "
                  + $"stationaryFpsP50={stationaryFpsP50:F0} stationaryFpsP95={stationaryFpsP95:F0}");

                Assert.Less(stationaryP95, MaxStationaryP95FrameMs,
                    $"Settled full-showcase p95 is {stationaryP95:F3} ms "
                  + $"(~{stationaryFpsP95:F0} FPS), above the {MaxStationaryP95FrameMs:F1} ms gate.");
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
            var clock = Stopwatch.StartNew();
            int stableFrames = 0;
            VoxelSurfaceMetrics last = default;
            while (clock.Elapsed.TotalSeconds < maxSeconds)
            {
                yield return null;
                camera.Render();
                last = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                    "Geometry work blocked while preparing GPU traversal coverage.");

                bool nearIncomplete = NearCoverageIsIncomplete(in last);
                bool fallbackSafe = !nearIncomplete || far.HoleRadiusMetres <= 0.05f;
                stableFrames = last.VisibleSolidChunks > 0 && fallbackSafe
                    ? stableFrames + 1 : 0;
                if (stableFrames >= 4) yield break;
            }

            Assert.Fail(
                $"Showcase never reached fallback-safe visible coverage within {maxSeconds:F0}s; "
              + $"known={last.SolidKnownChunks} resident={last.SolidResidentChunks} "
              + $"dirty={last.SolidDirtyChunks} visible={last.VisibleSolidChunks} "
              + $"missing={last.MissingVisibleSolidChunks} jobs={last.RunningSolidJobs} "
              + $"gpu={last.GpuCompletedSolidBuilds}/{last.GpuFallbackSolidBuilds} "
              + $"farHole={far.HoleRadiusMetres:F2}m.");
        }

        private static IEnumerator WaitForSettledVisibleCoverage(Camera camera, double maxSeconds)
        {
            var clock = Stopwatch.StartNew();
            int stableFrames = 0;
            VoxelSurfaceMetrics last = default;
            while (clock.Elapsed.TotalSeconds < maxSeconds)
            {
                yield return null;
                camera.Render();
                last = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                    "Geometry work blocked while waiting for the stationary benchmark.");

                bool settled = last.VisibleSolidChunks > 0
                            && last.MissingVisibleSolidChunks == 0
                            && last.RunningSolidJobs == 0
                            && last.SolidMeshesAwaitingUpload == 0
                            && last.SolidPendingUploadBytes == 0;
                stableFrames = settled ? stableFrames + 1 : 0;
                if (stableFrames >= 8) yield break;
            }

            Assert.Fail(
                $"Showcase did not settle before the stationary benchmark within {maxSeconds:F0}s; "
              + $"visible={last.VisibleSolidChunks} missing={last.MissingVisibleSolidChunks} "
              + $"dirty={last.SolidDirtyChunks} jobs={last.RunningSolidJobs} "
              + $"uploads={last.SolidMeshesAwaitingUpload}/{last.SolidPendingUploadBytes}B "
              + $"gpu={last.GpuCompletedSolidBuilds}/{last.GpuFallbackSolidBuilds}.");
        }

        private static string DescribeVisibilityFailure(
            int frame, in VoxelSurfaceMetrics metrics, Transform pose, VoxelFarTerrain far)
        {
            Vector3 position = pose.position;
            return $"GPU traversal frame {frame} lost every visible voxel draw; "
                 + $"camera=({position.x:F2},{position.y:F2},{position.z:F2}) "
                 + $"farHole={far.HoleRadiusMetres:F2}m "
                 + $"known={metrics.SolidKnownChunks} resident={metrics.SolidResidentChunks} "
                 + $"dirty={metrics.SolidDirtyChunks} missing={metrics.MissingVisibleSolidChunks} "
                 + $"jobs={metrics.RunningSolidJobs} uploads={metrics.SolidMeshesAwaitingUpload} "
                 + $"gpuAvailable={metrics.GpuCutoverAvailable} "
                 + $"gpuBackends={metrics.GpuResidentBackends} "
                 + $"gpuCompleted={metrics.GpuCompletedSolidBuilds} "
                 + $"gpuFallback={metrics.GpuFallbackSolidBuilds} "
                 + $"gpuWaitSlices={metrics.GpuReadbackWaitSlices}.";
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