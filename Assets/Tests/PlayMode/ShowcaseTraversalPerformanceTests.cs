using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
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
    /// Player-visible performance regressions for the production showcase while the camera is
    /// actually moving. Stationary convergence is not enough: region streaming, clipmap snaps,
    /// LOD replacement and geometry publication all happen during traversal and none of them may
    /// turn into a hitch or expose an uncovered near/far handoff.
    /// </summary>
    public sealed class ShowcaseTraversalPerformanceTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const double MaxMovingP95FrameMs = 18.0;
        private const double MaxMovingP99FrameMs = 25.0;
        private const double MaxMovingSingleFrameMs = 33.34;

        // The historical showcase rendered terrain + castle at roughly 700 fps (~1.43 ms/frame)
        // on the validation machine. Editor/PlayMode timing includes more overhead than that old
        // raw number, so this first guard deliberately allows roughly 2x the historical median
        // instead of pretending the two measurements are identical. It is still far stronger
        // than the general 33 ms stable-render acceptance gate and will catch a large regression.
        private const double MaxSteadyMedianRenderMs = 3.0;
        private const double MaxSteadyP95RenderMs = 6.0;
        private const double MaxSteadySingleRenderMs = 16.67;

        [UnityTest, Timeout(900000)]
        public IEnumerator ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap()
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

            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseTraversalPerformanceTests.Traversal",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                yield return WaitForVisibleCoverage(camera, 1200);

                Vector3 origin = showcase.transform.position;
                Quaternion originRotation = showcase.transform.rotation;
                var frameTimesMs = new List<double>(420);
                var frameClock = new Stopwatch();
                bool sawStreamingWork = false;
                int crossedRegionBoundaries = 0;
                int previousRegionX = Mathf.FloorToInt(origin.x / ShowcaseWorld.RegionMetres);

                // 420 rendered player-loop frames, advancing 0.5 m every frame, traverses 210 m
                // and crosses at least four 51.2 m region boundaries. The lateral weave also
                // forces clipmap snap changes instead of testing one perfectly aligned axis only.
                for (int frame = 0; frame < 420; frame++)
                {
                    float progress = frame / 419f;
                    Vector3 position = origin + new Vector3(
                        frame * 0.5f,
                        0f,
                        Mathf.Sin(progress * Mathf.PI * 6f) * 18f);
                    showcase.transform.position = position;
                    showcase.transform.rotation = originRotation;

                    int regionX = Mathf.FloorToInt(position.x / ShowcaseWorld.RegionMetres);
                    if (regionX != previousRegionX)
                    {
                        crossedRegionBoundaries++;
                        previousRegionX = regionX;
                    }

                    frameClock.Restart();
                    yield return null;
                    camera.Render();
                    frameClock.Stop();
                    frameTimesMs.Add(frameClock.Elapsed.TotalMilliseconds);

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                        $"Traversal frame {frame} synchronously completed geometry work on the player frame.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"Traversal frame {frame} lost every visible voxel draw.");

                    if (NearCoverageIsIncomplete(in metrics))
                    {
                        Assert.LessOrEqual(far.HoleRadiusMetres, 0.05f,
                            $"Traversal frame {frame} had incomplete near coverage but opened a "
                          + $"{far.HoleRadiusMetres:F2} m far-field hole. Missing/dirty near "
                          + "geometry must be covered by the far fallback while the player moves.");
                    }

                    sawStreamingWork |= metrics.SolidDirtyChunks > 0
                                     || metrics.RunningSolidJobs > 0
                                     || metrics.SolidMeshesAwaitingUpload > 0
                                     || metrics.SolidPendingUploadBytes > 0
                                     || metrics.LastFrameSolidUploadedBytes > 0;
                }

                Assert.GreaterOrEqual(crossedRegionBoundaries, 4,
                    "Traversal did not cross enough production region boundaries to exercise streaming.");
                Assert.True(sawStreamingWork,
                    "The moving-player window never exercised geometry streaming/publication work.");

                AssertMovingFrameTimes(frameTimesMs, "continuous traversal");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator RepeatedLodBoundaryCrossingsNeverStutterOrLoseCoverage()
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

            ShowcaseWorld world = GetWorld(showcase);
            Assert.NotNull(world);
            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = StructuresComposition.PlanCastle(new int3(256, ground, 376), world.Seed);
            Vector3 centre = new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                                         plan.Centre.z) * 0.1f;
            Vector3 lookAt = centre + Vector3.up * 10f;

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseTraversalPerformanceTests.LodSweep",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                yield return WaitForVisibleCoverage(camera, 1200);

                var frameTimesMs = new List<double>(360);
                var frameClock = new Stopwatch();
                bool sawStep4Demand = false;

                // Sweep repeatedly through the production 96/192/288 m LOD boundaries instead
                // of teleporting to one static sample per band. This catches replacement bursts,
                // clipmap snap work and publication spikes that a settled screenshot cannot see.
                for (int frame = 0; frame < 360; frame++)
                {
                    float phase = Mathf.PingPong(frame / 90f, 1f);
                    float distance = Mathf.Lerp(72f, 340f, phase);
                    Vector3 position = centre + new Vector3(
                        Mathf.Sin(frame * 0.09f) * 12f,
                        20f,
                        -distance);
                    showcase.transform.position = position;
                    showcase.transform.LookAt(lookAt);

                    frameClock.Restart();
                    yield return null;
                    camera.Render();
                    frameClock.Stop();
                    frameTimesMs.Add(frameClock.Elapsed.TotalMilliseconds);

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                        $"LOD sweep frame {frame} synchronously completed geometry work.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"LOD sweep frame {frame} lost every visible voxel draw at {distance:F1} m.");

                    sawStep4Demand |= metrics.Step4VisibilityInBand > 0;
                    if (NearCoverageIsIncomplete(in metrics))
                    {
                        Assert.LessOrEqual(far.HoleRadiusMetres, 0.05f,
                            $"LOD sweep frame {frame} opened a {far.HoleRadiusMetres:F2} m far-field "
                          + $"hole while near geometry was incomplete at distance {distance:F1} m.");
                    }
                }

                Assert.True(sawStep4Demand,
                    "The moving LOD sweep never exercised production step-4 demand.");
                AssertMovingFrameTimes(frameTimesMs, "repeated LOD-boundary traversal");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator ConvergedTerrainAndCastleRetainHighSteadyStateRenderThroughput()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            Camera camera = Camera.main;
            Assert.NotNull(camera);
            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseTraversalPerformanceTests.SteadyThroughput",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                yield return WaitForIdleConvergence(camera, 1800);

                var renderTimesMs = new List<double>(240);
                for (int frame = 0; frame < 240; frame++)
                {
                    var renderClock = Stopwatch.StartNew();
                    camera.Render();
                    renderClock.Stop();
                    renderTimesMs.Add(renderClock.Elapsed.TotalMilliseconds);
                    yield return null;

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                        "Steady-state rendering synchronously completed worker geometry.");
                    Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                        $"Steady-state frame {frame} regressed to missing visible geometry.");
                }

                renderTimesMs.Sort();
                double median = Percentile(renderTimesMs, 0.50);
                double p95 = Percentile(renderTimesMs, 0.95);
                double maximum = renderTimesMs[^1];
                double medianFps = median > 0.0001 ? 1000.0 / median : double.PositiveInfinity;

                UnityEngine.Debug.Log(
                    $"### SHOWCASE_STEADY_THROUGHPUT median={median:F3}ms ({medianFps:F0} fps) "
                  + $"p95={p95:F3}ms max={maximum:F3}ms historicalTarget=1.43ms(~700fps)");

                Assert.Less(median, MaxSteadyMedianRenderMs,
                    $"Converged terrain+castle median render time regressed to {median:F3} ms "
                  + $"({medianFps:F0} fps). Historical showcase was ~1.43 ms/~700 fps; "
                  + $"this guard currently allows {MaxSteadyMedianRenderMs:F1} ms while preserving "
                  + "headroom for Editor/PlayMode overhead.");
                Assert.Less(p95, MaxSteadyP95RenderMs,
                    $"Converged terrain+castle p95 render time is {p95:F3} ms; median={median:F3} ms.");
                Assert.Less(maximum, MaxSteadySingleRenderMs,
                    $"Converged terrain+castle produced a {maximum:F3} ms isolated render hitch.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static IEnumerator WaitForVisibleCoverage(Camera camera, int maxFrames)
        {
            int stableFrames = 0;
            VoxelSurfaceMetrics last = default;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                yield return null;
                camera.Render();
                last = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                    "Geometry work blocked the player frame while preparing the traversal gate.");

                bool ready = last.VisibleSolidChunks > 0
                          && last.MissingVisibleSolidChunks == 0;
                stableFrames = ready ? stableFrames + 1 : 0;
                if (stableFrames >= 4)
                    yield break;
            }

            Assert.Fail(
                $"Showcase never reached four consecutive hole-free visible frames before traversal; "
              + $"known={last.SolidKnownChunks} resident={last.SolidResidentChunks} "
              + $"dirty={last.SolidDirtyChunks} visible={last.VisibleSolidChunks} "
              + $"missing={last.MissingVisibleSolidChunks} jobs={last.RunningSolidJobs}.");
        }

        private static IEnumerator WaitForIdleConvergence(Camera camera, int maxFrames)
        {
            int stableFrames = 0;
            VoxelSurfaceMetrics last = default;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                yield return null;
                camera.Render();
                last = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                    "Geometry work blocked the player frame while waiting for steady-state convergence.");

                bool idle = last.VisibleSolidChunks > 0
                         && last.MissingVisibleSolidChunks == 0
                         && last.SolidDirtyChunks == 0
                         && last.RunningSolidJobs == 0
                         && last.SolidMeshesAwaitingUpload == 0
                         && last.SolidPendingUploadBytes == 0;
                stableFrames = idle ? stableFrames + 1 : 0;
                if (stableFrames >= 8)
                    yield break;
            }

            Assert.Fail(
                $"Showcase did not become idle enough for a steady-throughput measurement; "
              + $"known={last.SolidKnownChunks} resident={last.SolidResidentChunks} "
              + $"dirty={last.SolidDirtyChunks} visible={last.VisibleSolidChunks} "
              + $"missing={last.MissingVisibleSolidChunks} jobs={last.RunningSolidJobs} "
              + $"uploadMeshes={last.SolidMeshesAwaitingUpload} "
              + $"uploadBytes={last.SolidPendingUploadBytes}.");
        }

        private static bool NearCoverageIsIncomplete(in VoxelSurfaceMetrics metrics) =>
            metrics.MissingVisibleSolidChunks > 0
            || metrics.SolidDirtyChunks > 0
            || metrics.RunningSolidJobs > 0
            || metrics.SolidMeshesAwaitingUpload > 0
            || metrics.SolidPendingUploadBytes > 0;

        private static void AssertMovingFrameTimes(List<double> frameTimesMs, string phase)
        {
            frameTimesMs.Sort();
            double p95 = Percentile(frameTimesMs, 0.95);
            double p99 = Percentile(frameTimesMs, 0.99);
            double maximum = frameTimesMs[^1];

            UnityEngine.Debug.Log(
                $"### SHOWCASE_MOVING_PERF phase={phase} frames={frameTimesMs.Count} "
              + $"p95={p95:F2}ms p99={p99:F2}ms max={maximum:F2}ms");

            Assert.Less(p95, MaxMovingP95FrameMs,
                $"{phase} p95 was {p95:F2} ms (p99={p99:F2}, max={maximum:F2}).");
            Assert.Less(p99, MaxMovingP99FrameMs,
                $"{phase} p99 was {p99:F2} ms (max={maximum:F2}).");
            Assert.Less(maximum, MaxMovingSingleFrameMs,
                $"{phase} produced a {maximum:F2} ms player-visible hitch; "
              + "no measured movement frame may fall below 30 fps on the validation machine.");
        }

        private static double Percentile(List<double> sorted, double percentile)
        {
            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(sorted.Count * percentile)) - 1,
                0,
                sorted.Count - 1);
            return sorted[index];
        }

        private static ShowcaseWorld GetWorld(VoxelShowcase showcase)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                "_world", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(showcase) as ShowcaseWorld;
        }

        private static void SetShowcaseField<T>(VoxelShowcase showcase, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"VoxelShowcase.{fieldName} was not found.");
            field.SetValue(showcase, value);
        }
    }
}
