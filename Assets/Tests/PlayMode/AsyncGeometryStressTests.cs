using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class AsyncGeometryStressTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const double MaxGeometryOrchestrationP99Ms = 12.0;

        [UnityTest, Timeout(900000)]
        public IEnumerator ContinuousLodTraversalAndDestructionRespectFrameUploadBudget()
        {
            yield return LoadShowcaseScene();
            GetShowcaseContext(out _, out ShowcaseWorld world,
                               out Camera camera, out CastlePlan plan, out Vector3 centre);

            int oldBudget = VoxelRenderBridge.SolidUploadBudgetBytes;
            int oldSlice = VoxelRenderBridge.SolidUploadSliceBytes;
            int oldWorkers = VoxelRenderBridge.SolidUploadWorkerBudget;
            double oldUploadMs = VoxelRenderBridge.SolidUploadBudgetMs;
            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            int successfulExplosions = 0;
            bool sawUpload = false;
            bool sawPendingReplacement = false;
            int maxVisible = 0;
            double peakSchedulerP99Ms = 0.0;
            try
            {
                VoxelRenderBridge.SolidUploadBudgetBytes = 16 * 1024;
                VoxelRenderBridge.SolidUploadSliceBytes = 4 * 1024;
                VoxelRenderBridge.SolidUploadWorkerBudget = 4;
                VoxelRenderBridge.SolidUploadBudgetMs = 5.0;
                camera.targetTexture = target;

                // Warm the initial ring before mixing camera churn and edits.
                camera.transform.position = centre + new Vector3(0f, 20f, -96f);
                camera.transform.LookAt(centre + Vector3.up * 10f);
                for (int frame = 0; frame < 90; frame++)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                }

                for (int frame = 0; frame < 240; frame++)
                {
                    float phase = Mathf.PingPong(frame / 120f, 1f);
                    float distance = Mathf.Lerp(60f, 380f, phase);
                    camera.transform.position = centre + new Vector3(
                        Mathf.Sin(frame * 0.061f) * 22f,
                        18f + Mathf.Sin(frame * 0.037f) * 5f,
                        -distance);
                    camera.transform.LookAt(centre + Vector3.up * 10f);

                    if ((frame % 12) == 0)
                    {
                        float angle = frame * 0.31f;
                        int x = plan.Centre.x + Mathf.RoundToInt(Mathf.Cos(angle) * 28f);
                        int z = plan.Centre.z + Mathf.RoundToInt(Mathf.Sin(angle) * 28f);
                        int y = world.SurfaceHeight(x, z);
                        if (world.Explode(new int3(x, y, z), 2) > 0)
                            successfulExplosions++;
                    }

                    RenderUrpCamera(camera);
                    yield return null;

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(16 * 1024, metrics.SolidUploadBudgetBytes,
                        "Stress path lost the renderer-wide upload budget.");
                    Assert.LessOrEqual(metrics.LastFrameSolidUploadedBytes,
                        metrics.SolidUploadBudgetBytes,
                        "Camera/edit churn exceeded the renderer-wide geometry upload cap.");
                    Assert.GreaterOrEqual(metrics.LastFrameSolidUploadedBytes, 0);
                    Assert.AreEqual(0UL, metrics.FramePathBlockingCompletionViolations,
                        "A geometry frame path attempted to wait for an unfinished JobHandle.");
                    peakSchedulerP99Ms = Math.Max(peakSchedulerP99Ms,
                                                   metrics.SchedulerPrepareTiming.P99Ms);
                    maxVisible = Mathf.Max(maxVisible, metrics.VisibleSolidChunks);
                    sawUpload |= metrics.LastFrameSolidUploadedBytes > 0;
                    sawPendingReplacement |= metrics.SolidPendingUploadBytes > 0;
                }

                Assert.GreaterOrEqual(successfulExplosions, 6,
                    "Stress test did not produce enough real voxel mutations.");
                Assert.Greater(maxVisible, 0,
                    "Camera traversal never produced visible solid geometry.");
                Assert.True(sawUpload,
                    "Camera/edit stress never exercised GPU geometry publication.");
                Assert.True(sawPendingReplacement,
                    "A 16 KiB frame cap should produce queued replacement geometry under stress.");
                Assert.Greater(VoxelRenderBridge.SurfaceMetrics.SchedulerPrepareTiming.SampleCount, 0UL,
                    "Scheduler timing instrumentation recorded no stressed frames.");
                Assert.LessOrEqual(peakSchedulerP99Ms, MaxGeometryOrchestrationP99Ms,
                    $"Geometry scheduler P99 {peakSchedulerP99Ms:F3} ms exceeded the {MaxGeometryOrchestrationP99Ms:F1} ms stress gate.");
            }
            finally
            {
                RestoreUploadBudget(oldBudget, oldSlice, oldWorkers, oldUploadMs);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EditedVisibleChunkKeepsOldGeometryUntilReplacementPublishes()
        {
            yield return LoadShowcaseScene();
            GetShowcaseContext(out _, out ShowcaseWorld world,
                               out Camera camera, out CastlePlan plan, out Vector3 centre);

            int oldBudget = VoxelRenderBridge.SolidUploadBudgetBytes;
            int oldSlice = VoxelRenderBridge.SolidUploadSliceBytes;
            int oldWorkers = VoxelRenderBridge.SolidUploadWorkerBudget;
            double oldUploadMs = VoxelRenderBridge.SolidUploadBudgetMs;
            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            bool sawPendingReplacement = false;
            try
            {
                // Make even a modest rebuilt chunk span multiple frames so the invariant is
                // observable: the old published lease must remain draw-visible until swap.
                VoxelRenderBridge.SolidUploadBudgetBytes = 4 * 1024;
                VoxelRenderBridge.SolidUploadSliceBytes = 1024;
                VoxelRenderBridge.SolidUploadWorkerBudget = 2;
                VoxelRenderBridge.SolidUploadBudgetMs = 5.0;
                camera.targetTexture = target;
                camera.transform.position = centre + new Vector3(0f, 18f, -48f);
                camera.transform.LookAt(centre + Vector3.up * 8f);

                bool warmed = false;
                for (int frame = 0; frame < 180; frame++)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    warmed = metrics.VisibleSolidChunks > 0
                          && metrics.MissingVisibleSolidChunks == 0
                          && metrics.SolidPendingUploadBytes == 0;
                    if (warmed) break;
                }
                Assert.True(warmed, "Could not establish a fully published near-LOD baseline.");

                // Keep edits well inside one 64-voxel step-1 extraction chunk so a halo crossing
                // does not manufacture a newly visible neighbour and confuse replacement-hole
                // detection. These points remain close to the castle and in the camera view.
                var editOffsets = new[]
                {
                    new int2(24, -24), new int2(28, -18), new int2(20, -30),
                    new int2(30, -28), new int2(18, -20), new int2(26, -32),
                };

                int successfulExplosions = 0;
                foreach (int2 offset in editOffsets)
                {
                    int x = plan.Centre.x + offset.x;
                    int z = plan.Centre.z + offset.y;
                    int y = world.SurfaceHeight(x, z);
                    if (world.Explode(new int3(x, y, z), 2) > 0)
                        successfulExplosions++;

                    for (int frame = 0; frame < 24; frame++)
                    {
                        RenderUrpCamera(camera);
                        yield return null;
                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        Assert.LessOrEqual(metrics.LastFrameSolidUploadedBytes,
                            metrics.SolidUploadBudgetBytes,
                            "Replacement upload exceeded the frame budget.");
                        sawPendingReplacement |= metrics.SolidPendingUploadBytes > 0;
                        if (metrics.SolidPendingUploadBytes > 0)
                        {
                            Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                                "Visible edited geometry disappeared while its replacement was still queued.");
                            Assert.Greater(metrics.VisibleSolidChunks, 0,
                                "Replacement staging created a visible geometry hole.");
                        }
                    }
                }

                Assert.GreaterOrEqual(successfulExplosions, 3,
                    "Replacement test did not mutate enough visible terrain near the castle.");
                Assert.True(sawPendingReplacement,
                    "Tiny publication slices never exposed a pending replacement window.");
            }
            finally
            {
                RestoreUploadBudget(oldBudget, oldSlice, oldWorkers, oldUploadMs);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator VisibleEditDuringRunningBuildRejectsStaleGeneration()
        {
            yield return LoadShowcaseScene();
            GetShowcaseContext(out _, out ShowcaseWorld world,
                               out Camera camera, out CastlePlan plan, out Vector3 centre);

            double oldBuildBudgetMs = VoxelRenderBridge.SolidBuildBudgetMs;
            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = target;
                camera.transform.position = centre + new Vector3(0f, 18f, -48f);
                camera.transform.LookAt(centre + Vector3.up * 8f);

                // Start from a fully converged view so the running job below can only be work
                // caused by this test's first edit, not leftover showcase streaming.
                bool warmed = false;
                for (int frame = 0; frame < 240; frame++)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    warmed = metrics.VisibleSolidChunks > 0
                          && metrics.MissingVisibleSolidChunks == 0
                          && metrics.SolidDirtyChunks == 0
                          && metrics.RunningSolidJobs == 0
                          && metrics.SolidPendingUploadBytes == 0;
                    if (warmed) break;
                }
                Assert.True(warmed,
                    "Could not establish an idle, fully published near-LOD baseline.");

                ulong staleBaseline = VoxelRenderBridge.SurfaceMetrics.RejectedStaleSolidBuilds;
                // Make snapshot/job admission span frames without changing geometry semantics.
                // This creates a deterministic window in which a second edit can invalidate the
                // generation currently being built.
                VoxelRenderBridge.SolidBuildBudgetMs = 0.05;

                Assert.Greater(ExplodeAtOffset(world, plan, 24, -24), 0,
                    "First edit did not mutate the visible step-1 chunk.");

                bool injectedSecondEditDuringJob = false;
                for (int frame = 0; frame < 240; frame++)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                        "First edit created a visible hole before its replacement was ready.");
                    if (metrics.RunningSolidJobs <= 0) continue;

                    Assert.Greater(ExplodeAtOffset(world, plan, 28, -18), 0,
                        "Second edit did not mutate the same visible extraction chunk.");
                    injectedSecondEditDuringJob = true;
                    break;
                }
                Assert.True(injectedSecondEditDuringJob,
                    "The first edit never produced an observable in-flight geometry job.");

                bool staleObserved = false;
                for (int frame = 0; frame < 360; frame++)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.LessOrEqual(metrics.LastFrameSolidUploadedBytes,
                        metrics.SolidUploadBudgetBytes,
                        "Stale-build retry exceeded the renderer-wide upload budget.");
                    if (metrics.RejectedStaleSolidBuilds <= staleBaseline) continue;

                    staleObserved = true;
                    Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                        "Rejecting stale in-flight work removed the old visible geometry.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        "Rejecting stale in-flight work created a visible geometry hole.");
                    break;
                }

                Assert.True(staleObserved,
                    "A second edit during an in-flight build was not counted as a rejected stale generation.");
            }
            finally
            {
                VoxelRenderBridge.SolidBuildBudgetMs = oldBuildBudgetMs;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }


        [UnityTest, Timeout(900000)]
        public IEnumerator GeometryArenaPressureKeepsPublishedLeaseUntilReplacementConverges()
        {
            // Two aligned leases exactly fill this arena. A replacement for A therefore cannot
            // stage until another published lease retires; pressure must become backlog, not a
            // fallback buffer allocation or a visible hole.
            yield return null;

            var arena = new SurfaceGeometryArena(
                vertexCapacity: 512,
                indexCapacity: 1024,
                argsRecordCapacity: 2);
            var entryA = new CpuTransvoxelChunkCache.Entry(
                int3.zero, CpuTransvoxelChunkCache.BaseVoxelsPerAxis,
                CpuTransvoxelChunkCache.BaseSourceStep, arena);
            var entryB = new CpuTransvoxelChunkCache.Entry(
                new int3(1, 0, 0), CpuTransvoxelChunkCache.BaseVoxelsPerAxis,
                CpuTransvoxelChunkCache.BaseSourceStep, arena);
            var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.Persistent);
            var sixIndices = new NativeList<uint>(6, Allocator.Persistent);
            var threeIndices = new NativeList<uint>(3, Allocator.Persistent);
            try
            {
                vertices.Add(new SmoothSurfaceVertex { Position = new Vector3(0f, 0f, 0f) });
                vertices.Add(new SmoothSurfaceVertex { Position = new Vector3(1f, 0f, 0f) });
                vertices.Add(new SmoothSurfaceVertex { Position = new Vector3(1f, 1f, 0f) });
                vertices.Add(new SmoothSurfaceVertex { Position = new Vector3(0f, 1f, 0f) });
                sixIndices.Add(0); sixIndices.Add(1); sixIndices.Add(2);
                sixIndices.Add(0); sixIndices.Add(2); sixIndices.Add(3);
                threeIndices.Add(0); threeIndices.Add(1); threeIndices.Add(2);

                Assert.True(entryA.AdvanceUpload(vertices, sixIndices, int.MaxValue, out _));
                Assert.True(entryB.AdvanceUpload(vertices, sixIndices, int.MaxValue, out _));
                Assert.AreEqual(2, arena.UsedArgsRecords,
                    "Fixture did not fill both arena draw slots.");

                long liveBytesBeforePressure = entryA.GpuBytes;
                Assert.False(entryA.AdvanceUpload(
                    vertices, threeIndices, int.MaxValue, out int blockedUploadBytes),
                    "Replacement unexpectedly staged despite a completely full arena.");
                Assert.AreEqual(0, blockedUploadBytes,
                    "Arena pressure copied geometry before a staging lease existed.");
                Assert.Greater(arena.AllocationFailureCount, 0UL,
                    "Pressure path did not report bounded arena backpressure.");
                Assert.True(entryA.WaitingForArena,
                    "Blocked replacement was not left queued for later convergence.");
                Assert.True(entryA.Ready,
                    "Arena pressure removed A's previously published geometry.");
                Assert.AreEqual(6, entryA.IndexCount,
                    "Arena pressure mutated A's live draw record before replacement publication.");
                Assert.AreEqual(liveBytesBeforePressure, entryA.GpuBytes,
                    "Arena pressure changed A's live lease before replacement publication.");

                // Reclaiming one unrelated/off-screen lease is the scheduler's pressure response.
                // The next attempt must acquire that fixed arena range, publish atomically, and
                // release A's old range without creating any extra GPU buffer.
                entryB.Dispose();
                Assert.AreEqual(1, arena.UsedArgsRecords);
                Assert.True(entryA.AdvanceUpload(
                    vertices, threeIndices, int.MaxValue, out int convergedUploadBytes),
                    "Queued replacement did not converge after fixed-arena space was reclaimed.");
                Assert.Greater(convergedUploadBytes, 0);
                Assert.True(entryA.Ready);
                Assert.False(entryA.WaitingForArena);
                Assert.AreEqual(3, entryA.IndexCount,
                    "Replacement did not atomically become the new live draw record.");
                Assert.AreEqual(1, arena.UsedArgsRecords,
                    "Atomic swap should leave exactly one live arena lease after B is retired.");
            }
            finally
            {
                entryA.Dispose();
                entryB.Dispose();
                if (vertices.IsCreated) vertices.Dispose();
                if (sixIndices.IsCreated) sixIndices.Dispose();
                if (threeIndices.IsCreated) threeIndices.Dispose();
                arena.Dispose();
            }
        }


        [UnityTest, Timeout(900000)]
        public IEnumerator ArenaPressureDelaysConvergenceWithoutGrowingBuffersOrOpeningHoles()
        {
            yield return LoadShowcaseScene();
            GetShowcaseContext(out _, out ShowcaseWorld world,
                               out Camera camera, out CastlePlan plan, out Vector3 centre);

            int oldArenaLeaseCap = VoxelRenderBridge.SolidArenaMaxActiveLeases;
            int oldBudget = VoxelRenderBridge.SolidUploadBudgetBytes;
            int oldSlice = VoxelRenderBridge.SolidUploadSliceBytes;
            int oldWorkers = VoxelRenderBridge.SolidUploadWorkerBudget;
            double oldUploadMs = VoxelRenderBridge.SolidUploadBudgetMs;
            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = target;
                camera.transform.position = centre + new Vector3(0f, 18f, -48f);
                camera.transform.LookAt(centre + Vector3.up * 8f);

                bool warmed = false;
                for (int frame = 0; frame < 300; frame++)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    warmed = metrics.VisibleSolidChunks > 0
                          && metrics.MissingVisibleSolidChunks == 0
                          && metrics.SolidDirtyChunks == 0
                          && metrics.RunningSolidJobs == 0
                          && metrics.SolidPendingUploadBytes == 0
                          && metrics.SolidArenaActiveLeases >= 2;
                    if (warmed) break;
                }
                Assert.True(warmed,
                    "Could not establish an idle published baseline with multiple solid arena leases.");

                VoxelSurfaceMetrics baseline = VoxelRenderBridge.SurfaceMetrics;
                long committedBytes = baseline.SolidArenaCommittedBytes;
                int leaseCap = baseline.SolidArenaActiveLeases;
                ulong failureBaseline = baseline.SolidArenaAllocationFailures;
                ulong evictionBaseline = baseline.SolidArenaPressureEvictions;
                ulong completedBaseline = baseline.CompletedSolidBuilds;

                // Keep the physical arena exactly as-is but disallow one extra staging lease.
                // A visible replacement must hit pressure first, after which the scheduler may
                // retire one different offscreen live lease and retry on a later frame.
                VoxelRenderBridge.SolidArenaMaxActiveLeases = leaseCap;
                VoxelRenderBridge.SolidUploadBudgetBytes = 16 * 1024;
                VoxelRenderBridge.SolidUploadSliceBytes = 4 * 1024;
                VoxelRenderBridge.SolidUploadWorkerBudget = 2;
                VoxelRenderBridge.SolidUploadBudgetMs = 5.0;

                Assert.Greater(ExplodeAtOffset(world, plan, 24, -24), 0,
                    "Arena-pressure test did not mutate the visible step-1 chunk.");

                bool sawFailure = false;
                bool sawPressureEviction = false;
                bool sawBacklog = false;
                bool converged = false;
                for (int frame = 0; frame < 480; frame++)
                {
                    RenderUrpCamera(camera);
                    yield return null;
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;

                    Assert.AreEqual(committedBytes, metrics.SolidArenaCommittedBytes,
                        "Arena pressure changed committed GPU bytes instead of applying backpressure.");
                    Assert.LessOrEqual(metrics.SolidArenaActiveLeases, leaseCap,
                        "Arena soft pressure ceiling was exceeded by a staging publication.");
                    Assert.LessOrEqual(metrics.LastFrameSolidUploadedBytes,
                        metrics.SolidUploadBudgetBytes,
                        "Arena-pressure retry exceeded the renderer-wide upload cap.");
                    Assert.AreEqual(0UL, metrics.FramePathBlockingCompletionViolations,
                        "Arena pressure caused a frame-path geometry wait.");

                    sawFailure |= metrics.SolidArenaAllocationFailures > failureBaseline;
                    sawPressureEviction |= metrics.SolidArenaPressureEvictions > evictionBaseline;
                    sawBacklog |= metrics.SolidPendingUploadBytes > 0;
                    if (metrics.SolidPendingUploadBytes > 0)
                    {
                        Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                            "Arena pressure removed visible old geometry while replacement was queued.");
                        Assert.Greater(metrics.VisibleSolidChunks, 0,
                            "Arena pressure created a visible geometry hole.");
                    }

                    if (sawFailure && sawPressureEviction && sawBacklog
                        && metrics.CompletedSolidBuilds > completedBaseline
                        && metrics.SolidPendingUploadBytes == 0
                        && metrics.RunningSolidJobs == 0
                        && metrics.SolidDirtyChunks == 0)
                    {
                        converged = true;
                        break;
                    }
                }

                Assert.True(sawFailure,
                    "The soft arena ceiling never produced a real staging allocation failure.");
                Assert.True(sawPressureEviction,
                    "Arena pressure did not reclaim one bounded offscreen lease for retry.");
                Assert.True(sawBacklog,
                    "Arena pressure never delayed publication into a queued replacement state.");
                Assert.True(converged,
                    "Arena pressure did not converge after bounded eviction/backpressure.");
            }
            finally
            {
                VoxelRenderBridge.SolidArenaMaxActiveLeases = oldArenaLeaseCap;
                RestoreUploadBudget(oldBudget, oldSlice, oldWorkers, oldUploadMs);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }


        [UnityTest, Timeout(900000)]
        public IEnumerator WarmRepeatedClipmapTraversalAllocatesNoManagedGeometryMemory()
        {
            yield return LoadShowcaseScene();
            GetShowcaseContext(out _, out _, out Camera camera,
                               out _, out Vector3 centre);

            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            const int pathFrames = 160;
            try
            {
                camera.targetTexture = target;
                Vector3 lookAt = centre + Vector3.up * 8f;

                // Repeat exactly the same clipmap path twice before measuring. The first pass may
                // grow bounded dictionaries/queues and fill entry pools; the second makes every
                // coordinate/slot transition that the measured pass will make. Any allocation on
                // the third pass is therefore steady-state geometry growth, not warmup.
                for (int cycle = 0; cycle < 2; cycle++)
                for (int frame = 0; frame < pathFrames; frame++)
                {
                    PositionAllocationPathCamera(camera, centre, lookAt, frame, pathFrames);
                    RenderUrpCamera(camera);
                    yield return null;
                }

                long maxAllocated = 0;
                int observedFrames = 0;
                for (int frame = 0; frame < pathFrames; frame++)
                {
                    PositionAllocationPathCamera(camera, centre, lookAt, frame, pathFrames);
                    RenderUrpCamera(camera);
                    yield return null;

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0UL, metrics.FramePathBlockingCompletionViolations,
                        "Allocation traversal encountered a blocking geometry completion attempt.");
                    maxAllocated = System.Math.Max(maxAllocated,
                                                  metrics.LastFrameManagedAllocationBytes);
                    observedFrames++;
                    Assert.AreEqual(0L, metrics.LastFrameManagedAllocationBytes,
                        $"Steady-state geometry allocated managed memory on traversal frame {frame}.");
                }

                Assert.AreEqual(pathFrames, observedFrames);
                Assert.AreEqual(0L, maxAllocated,
                    "Warm repeated clipmap traversal must not allocate managed geometry memory.");
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void RenderUrpCamera(Camera camera)
        {
            Assert.NotNull(camera);
            Assert.NotNull(camera.targetTexture,
                "Async geometry stress tests require an explicit RenderTexture destination.");
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
                "URP stress render request did not enqueue VoxelRenderFeature.");
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0,
                "URP stress render request did not record VoxelRenderPass.");
            Assert.AreEqual("feature-aware", VoxelRenderBridge.LastSurfacePassState,
                $"VoxelRenderPass returned early during stress render: {VoxelRenderBridge.LastSurfacePassState}.");
        }

        private static void PositionAllocationPathCamera(Camera camera, Vector3 centre,
                                                         Vector3 lookAt, int frame,
                                                         int pathFrames)
        {
            float angle = frame * (Mathf.PI * 2f / pathFrames);
            camera.transform.position = centre + new Vector3(
                Mathf.Sin(angle) * 14f,
                18f + Mathf.Sin(angle * 2f) * 2f,
                -96f + Mathf.Cos(angle) * 8f);
            camera.transform.LookAt(lookAt);
        }

        private static int ExplodeAtOffset(ShowcaseWorld world, CastlePlan plan,
                                           int xOffset, int zOffset)
        {
            int x = plan.Centre.x + xOffset;
            int z = plan.Centre.z + zOffset;
            int y = world.SurfaceHeight(x, z);
            return world.Explode(new int3(x, y, z), 2);
        }

        private static IEnumerator LoadShowcaseScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return WaitForAtomicWorldReady();
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

        private static void GetShowcaseContext(out VoxelShowcase showcase,
                                               out ShowcaseWorld world,
                                               out Camera camera,
                                               out CastlePlan plan,
                                               out Vector3 centre)
        {
            showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            centre = new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                                 plan.Centre.z) * 0.1f;
        }

        private static void RestoreUploadBudget(int bytes, int slice, int workers, double milliseconds)
        {
            VoxelRenderBridge.SolidUploadBudgetBytes = bytes;
            VoxelRenderBridge.SolidUploadSliceBytes = slice;
            VoxelRenderBridge.SolidUploadWorkerBudget = workers;
            VoxelRenderBridge.SolidUploadBudgetMs = milliseconds;
        }
    }
}
