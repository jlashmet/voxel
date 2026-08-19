using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace VoxelEngine.Tests.PlayMode
{
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: this captures images for a human to
    /// look at rather than asserting behaviour, and it is one of the slowest things in the
    /// suite. Run it by name when you want the artefacts:
    /// <c>tools/unity-run.sh ... -testFilter ShowcaseGpuSurfaceTests</c>
    /// </remarks>
    [NUnit.Framework.Explicit("Artefact capture for human review; run by name.")]
    public sealed class ShowcaseSurfaceTests
    {
        /// <summary>
        /// Fixed measurement window. It is deliberately not shortened when the view converges
        /// early: the CPU and GPU back ends are compared by how much they finish inside the same
        /// wall clock, so a run that stopped sooner would look better than it is.
        /// </summary>
        private const double SettleSeconds = 20.0;

        /// <summary>Percentile of an already-sorted sample, clamped to the ends.</summary>
        private static double Percentile(List<double> sorted, double fraction)
        {
            if (sorted.Count == 0) return 0.0;
            int index = (int)(fraction * (sorted.Count - 1));
            return sorted[Mathf.Clamp(index, 0, sorted.Count - 1)];
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ShowcaseBuildsFeatureAwareSolidGeometry()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            Assert.IsTrue(VoxelRenderBridge.TryGetWorld(out VoxelWorldView world),
                "Showcase did not register a valid render-world view.");
            using (var resident = world.Storage.GetResidentRegionCoords(Allocator.Temp))
                Debug.Log($"GPU test world: regions={resident.Length}, "
                        + $"surfaceHash={world.SurfaceCatalogueView.CatalogueHash}, "
                        + $"coatingHash={world.CoatingCatalogueView.CatalogueHash}");
            Camera camera = Camera.main;
            Assert.NotNull(camera);

            // Batch mode does not guarantee a Game-view camera render for every yielded frame.
            // Drive a bounded number of renders into one persistent target: this exercises URP
            // without repeatedly creating full-screen render resources (the source of the old
            // runaway test). Ordinary frames between renders still advance streaming work.
            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseGpuSurfaceTests.Target",
                antiAliasing = 1
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;
            try
            {
                bool converged = false;
                int frame = 0;
                var timeout = Stopwatch.StartNew();
                while (timeout.Elapsed.TotalSeconds < 30.0)
                {
                    if (frame % 6 == 0)
                        camera.Render();
                    yield return null;

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    if (frame % 30 == 0)
                        Debug.Log($"GPU surface frame {frame}: changes={metrics.ChangeRecords}, "
                                + $"surfaceBricks={metrics.DiscoveredSurfaceBricks}, "
                                + $"known={metrics.SolidKnownChunks}, "
                                + $"resident={metrics.SolidResidentChunks}, "
                                + $"visible={metrics.VisibleSolidChunks}, "
                                + $"missing={metrics.MissingVisibleSolidChunks}, "
                                + $"jobs={metrics.RunningSolidJobs}, "
                                + $"gpuAvailable={metrics.GpuCutoverAvailable}, "
                                + $"gpuBackends={metrics.GpuResidentBackends}, "
                                + $"gpuReady={metrics.GpuCompletedSolidBuilds}, "
                                + $"gpuFallback={metrics.GpuFallbackSolidBuilds}, "
                                + $"snapshot={metrics.LastSolidSnapshotMs:0.00}ms, "
                                + $"compact={metrics.LastSolidTopologyCompactMs:0.00}ms, "
                                + $"upload={metrics.LastSolidUploadMs:0.00}ms, "
                                + $"enqueues={VoxelRenderBridge.RenderFeatureEnqueueCount}, "
                                + $"records={VoxelRenderBridge.SurfacePassRecordCount}, "
                                + $"state={VoxelRenderBridge.LastSurfacePassState}");
                    if (metrics.SolidKnownChunks > 0 && metrics.SolidResidentChunks > 0
                        && metrics.VisibleSolidChunks > 0)
                    {
                        converged = true;
                        // Keep rendering until the throughput assertion itself is satisfiable.
                        // A frame-count settle condition becomes meaningless when world
                        // transactions are deliberately split across many inexpensive frames.
                        if (metrics.SolidResidentChunks >= 24
                            && metrics.GpuCompletedSolidBuilds > 0) break;
                    }
                    frame++;
                }
                VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.IsTrue(converged,
                    $"Feature-aware surface did not become visible: changes={finalMetrics.ChangeRecords}, "
                  + $"surfaceBricks={finalMetrics.DiscoveredSurfaceBricks}, "
                  + $"known={finalMetrics.SolidKnownChunks}, "
                  + $"resident={finalMetrics.SolidResidentChunks}, "
                  + $"visible={finalMetrics.VisibleSolidChunks}.");
                Assert.Greater(finalMetrics.UploadedGeometryBytes, 0ul,
                    "The authoritative extractor did not publish any complete geometry.");
                // Skipped when the run is deliberately measuring the CPU mesher it replaces.
                if (!CpuTransvoxelChunkCache.GpuCutoverDisabled)
                {
                    Assert.IsTrue(finalMetrics.GpuCutoverAvailable,
                        "The showcase base ring could not create the production GPU extraction backend.");
                    Assert.Greater(finalMetrics.GpuCompletedSolidBuilds, 0ul,
                        "No production base-ring chunk was published through the compute mesher.");
                }
                Assert.LessOrEqual(finalMetrics.RejectedStaleSolidBuilds,
                                   finalMetrics.CompletedSolidBuilds
                                 + (ulong)finalMetrics.RunningSolidJobs + 64ul,
                    "Streaming invalidation is repeatedly starving completed surface work.");
                Assert.GreaterOrEqual(finalMetrics.SolidResidentChunks, 24,
                    "Feature-aware geometry throughput regressed below four workers completing "
                  + "one chunk per render cadence during startup.");
                Assert.Less(finalMetrics.LastSolidUploadMs, 25.0,
                    "A single mesh publication caused a visible frame-length upload stall.");

                // The capture below is kept in the repository as a render reference, so taking it
                // here would photograph a half-built world: the loop above exits as soon as the
                // throughput assertion is satisfiable, with streaming still in flight. Settle
                // first. The budget stops a genuine hole from hanging the suite, and the log line
                // records which of the two the image actually shows.
                var settle = Stopwatch.StartNew();
                var frameMs = new List<double>(4096);
                double previousSeconds = Time.realtimeSinceStartupAsDouble;
                VoxelSurfaceMetrics settled = finalMetrics;
                while (settle.Elapsed.TotalSeconds < SettleSeconds)
                {
                    camera.Render();
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    frameMs.Add((now - previousSeconds) * 1000.0);
                    previousSeconds = now;
                    settled = VoxelRenderBridge.SurfaceMetrics;
                    if (settled.MissingVisibleSolidChunks == 0 && settled.RunningSolidJobs == 0)
                        break;
                }

                frameMs.Sort();
                Debug.Log($"PERF backend={(CpuTransvoxelChunkCache.GpuCutoverDisabled ? "cpu" : "gpu")} "
                        + $"settleSeconds={settle.Elapsed.TotalSeconds:0.0} frames={frameMs.Count} "
                        + $"resident={settled.SolidResidentChunks} "
                        + $"visible={settled.VisibleSolidChunks} "
                        + $"missing={settled.MissingVisibleSolidChunks} "
                        + $"jobs={settled.RunningSolidJobs} "
                        + $"gpuBackends={settled.GpuResidentBackends} "
                        + $"gpuReady={settled.GpuCompletedSolidBuilds} "
                        + $"gpuFallback={settled.GpuFallbackSolidBuilds} "
                        + $"frameP50={Percentile(frameMs, 0.50):0.00}ms "
                        + $"frameP95={Percentile(frameMs, 0.95):0.00}ms "
                        + $"frameP99={Percentile(frameMs, 0.99):0.00}ms "
                        + $"frameMax={Percentile(frameMs, 1.00):0.00}ms "
                        + $"workerPrepareP95={settled.WorkerPrepareTiming.P95Ms:0.000}ms "
                        + $"workerPrepareMax={settled.WorkerPrepareTiming.MaxMs:0.000}ms "
                        + $"schedulerPrepareP95={settled.SchedulerPrepareTiming.P95Ms:0.000}ms "
                        + $"schedulerPrepareMax={settled.SchedulerPrepareTiming.MaxMs:0.000}ms "
                        + $"snapshotP95={settled.SnapshotTiming.P95Ms:0.000}ms "
                        + $"snapshotMax={settled.SnapshotTiming.MaxMs:0.000}ms "
                        + $"blockingCompletions={settled.FramePathBlockingCompletionViolations} "
                        + $"completedBuilds={settled.CompletedSolidBuilds} "
                        + $"staleBuilds={settled.RejectedStaleSolidBuilds} "
                        + $"gpuWaitSlices={settled.GpuReadbackWaitSlices} "
                        + $"buildLatencyP50={settled.BuildLatencyTiming.P50Ms:0.0}ms "
                        + $"buildLatencyP95={settled.BuildLatencyTiming.P95Ms:0.0}ms "
                        + $"gpuBuildLatencyP50={settled.GpuBuildLatencyTiming.P50Ms:0.0}ms "
                        + $"gpuBuildLatencyP95={settled.GpuBuildLatencyTiming.P95Ms:0.0}ms "
                        + $"arenaFailures={settled.SolidArenaAllocationFailures} "
                        + $"arenaEvictions={settled.SolidArenaPressureEvictions} "
                        + $"capacityPressure={settled.SolidCapacityPressureEvents}");

                camera.Render();
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = target;
                var capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                capture.Apply(false, false);
                RenderTexture.active = previousActive;

                string captureDirectory = Path.GetFullPath("Artifacts/GpuShowcase");
                Directory.CreateDirectory(captureDirectory);
                string capturePath = Path.Combine(captureDirectory, "showcase-gpu.png");
                File.WriteAllBytes(capturePath, capture.EncodeToPNG());
                Object.Destroy(capture);
                Assert.IsTrue(File.Exists(capturePath),
                    $"Screenshot was not written to {capturePath}.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
