using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    public sealed class ShowcasePerformanceTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator FullShowcaseConvergesWithinTenSecondsWithoutLaterStalls()
        {
            var total = Stopwatch.StartNew();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            ShowcaseWorld world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Camera camera = Camera.main;
            Assert.NotNull(camera);
            var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcasePerformanceTests.Target", antiAliasing = 1,
            };
            RenderTexture previous = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;
            try
            {
                bool complete = false;
                int frames = 0;
                while (total.Elapsed.TotalSeconds < 10.0)
                {
                    camera.Render();
                    yield return null;
                    frames++;
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    complete = world.CastleVoxels > 100_000
                            && metrics.VisibleSolidChunks > 0
                            && metrics.MissingVisibleSolidChunks == 0;
                    if (complete) break;
                }

                VoxelSurfaceMetrics converged = VoxelRenderBridge.SurfaceMetrics;
                Assert.True(complete,
                    $"Full showcase did not converge within 10 seconds: "
                  + $"elapsed={total.Elapsed.TotalSeconds:0.00}s frames={frames} "
                  + $"castle={world.CastleVoxels:N0} "
                  + $"castleRegions={world.ReadyCastleRegions}/{world.RequiredCastleRegions} "
                  + $"castleStage={world.CastleBuildStage} "
                  + $"lastCastleStage={world.LastCastleStage}:"
                  + $"{world.LastCastleStageMs:0.00}ms "
                  + $"maxCastleStage={world.MaxCastleStage}:"
                  + $"{world.MaxCastleStageMs:0.00}ms "
                  + $"regionProgress={world.GenerationProgress:0.00} "
                  + $"lastGenerate={world.LastGenerateMs:0.00}ms "
                  + $"resident={converged.SolidResidentChunks}/"
                  + $"{converged.SolidKnownChunks} dirty={converged.SolidDirtyChunks} "
                  + $"visible={converged.VisibleSolidChunks} "
                  + $"missing={converged.MissingVisibleSolidChunks} "
                  + $"jobs={converged.RunningSolidJobs} "
                  + $"sections.p95=prepare:{converged.SchedulerPrepareTiming.P95Ms:0.00},"
                  + $"journal:{converged.ChangeJournalTiming.P95Ms:0.00},"
                  + $"invalidate:{converged.InvalidationTiming.P95Ms:0.00},"
                  + $"discover:{converged.SurfaceDiscoveryTiming.P95Ms:0.00},"
                  + $"workers:{converged.WorkerPrepareTiming.P95Ms:0.00},"
                  + $"rules:{converged.RuleSyncTiming.P95Ms:0.00},"
                  + $"residency:{converged.ResidencyPruneTiming.P95Ms:0.00},"
                  + $"capacity:{converged.CapacityTiming.P95Ms:0.00},"
                  + $"select:{converged.BuildSelectionTiming.P95Ms:0.00},"
                  + $"visibility:{converged.VisibilityTiming.P95Ms:0.00},"
                  + $"snapshot:{converged.SnapshotTiming.P95Ms:0.00},"
                  + $"compact:{converged.TopologyCompactTiming.P95Ms:0.00},"
                  + $"merge:{converged.FacetedMergeTiming.P95Ms:0.00},"
                  + $"upload:{converged.UploadTiming.P95Ms:0.00},"
                  + $"queue:{converged.QueueLatencyTiming.P95Ms:0.00},"
                  + $"build:{converged.BuildLatencyTiming.P95Ms:0.00}ms "
                  + $"state={VoxelRenderBridge.LastSurfacePassState}");

                ulong buildsAtConvergence = converged.CompletedSolidBuilds;
                var renderMilliseconds = new List<double>(120);
                for (int frame = 0; frame < 120; frame++)
                {
                    var render = Stopwatch.StartNew();
                    camera.Render();
                    render.Stop();
                    renderMilliseconds.Add(render.Elapsed.TotalMilliseconds);
                    yield return null;
                    VoxelSurfaceMetrics stable = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0, stable.MissingVisibleSolidChunks,
                        $"visible geometry regressed after convergence on stable frame {frame}");
                }

                renderMilliseconds.Sort();
                double p95 = renderMilliseconds[(int)(renderMilliseconds.Count * 0.95) - 1];
                double maximum = renderMilliseconds[^1];
                VoxelSurfaceMetrics final = VoxelRenderBridge.SurfaceMetrics;
                Assert.Less(p95, 33.0,
                    $"stable showcase render p95 is {p95:0.00} ms; max={maximum:0.00} ms");
                Assert.Less(maximum, 100.0,
                    $"stable showcase suffered a {maximum:0.00} ms rendering hitch");
                Assert.GreaterOrEqual(final.CompletedSolidBuilds, buildsAtConvergence);
                Assert.Less(final.LastSolidUploadMs, 25.0);
                Assert.Greater(final.SchedulerPrepareTiming.SampleCount, 0ul,
                    "surface scheduler timing was not sampled");
                Assert.Greater(final.SnapshotTiming.SampleCount, 0ul,
                    "surface snapshot timing was not sampled");
                Assert.Greater(final.UploadTiming.SampleCount, 0ul,
                    "surface upload timing was not sampled");
                AssertTimingIsOrdered(in final.SchedulerPrepareTiming, "scheduler prepare");
                AssertTimingIsOrdered(in final.SnapshotTiming, "snapshot");
                AssertTimingIsOrdered(in final.UploadTiming, "upload");
                UnityEngine.Debug.Log($"### SHOWCASE_PERF load={total.Elapsed.TotalSeconds:0.00}s "
                    + $"p95={p95:0.00}ms max={maximum:0.00}ms "
                    + $"resident={final.SolidResidentChunks} visible={final.VisibleSolidChunks} "
                    + $"sections.p95=prepare:{final.SchedulerPrepareTiming.P95Ms:0.00},"
                    + $"journal:{final.ChangeJournalTiming.P95Ms:0.00},"
                    + $"invalidate:{final.InvalidationTiming.P95Ms:0.00},"
                    + $"discover:{final.SurfaceDiscoveryTiming.P95Ms:0.00},"
                    + $"workers:{final.WorkerPrepareTiming.P95Ms:0.00},"
                    + $"visibility:{final.VisibilityTiming.P95Ms:0.00},"
                    + $"rules:{final.RuleSyncTiming.P95Ms:0.00},"
                    + $"residency:{final.ResidencyPruneTiming.P95Ms:0.00},"
                    + $"capacity:{final.CapacityTiming.P95Ms:0.00},"
                    + $"select:{final.BuildSelectionTiming.P95Ms:0.00},"
                    + $"snapshot:{final.SnapshotTiming.P95Ms:0.00},"
                    + $"density-turnaround:{final.DensityJobTurnaroundTiming.P95Ms:0.00},"
                    + $"topology-turnaround:{final.TopologyJobTurnaroundTiming.P95Ms:0.00},"
                    + $"compact:{final.TopologyCompactTiming.P95Ms:0.00},"
                    + $"faceted-turnaround:{final.FacetedJobTurnaroundTiming.P95Ms:0.00},"
                    + $"merge:{final.FacetedMergeTiming.P95Ms:0.00},"
                    + $"profiles:{final.ProfileEmitTiming.P95Ms:0.00},"
                    + $"upload:{final.UploadTiming.P95Ms:0.00},"
                    + $"queue:{final.QueueLatencyTiming.P95Ms:0.00},"
                    + $"build:{final.BuildLatencyTiming.P95Ms:0.00}ms");
            }
            finally
            {
                camera.targetTexture = previous;
                target.Release();
                Object.Destroy(target);
            }
        }

        private static void AssertTimingIsOrdered(in VoxelTimingSummary timing, string section)
        {
            Assert.GreaterOrEqual(timing.LastMs, 0.0, section);
            Assert.GreaterOrEqual(timing.P50Ms, 0.0, section);
            Assert.GreaterOrEqual(timing.P95Ms, timing.P50Ms, section);
            Assert.GreaterOrEqual(timing.MaxMs, timing.P95Ms, section);
        }
    }
}
