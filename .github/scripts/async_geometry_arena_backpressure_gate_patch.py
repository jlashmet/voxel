from pathlib import Path


def replace_once(path: str, old: str, new: str, label: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one match, found {count}")
    p.write_text(text.replace(old, new, 1))


def insert_before(path: str, marker: str, addition: str, label: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(marker)
    if count != 1:
        raise SystemExit(f"{label}: expected one marker, found {count}")
    p.write_text(text.replace(marker, addition + marker, 1))


arena = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceGeometryArena.cs"
scheduler = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs"
bridge = "Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderBridge.cs"
render_pass = "Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderPass.cs"
arch = "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs"
stress = "Assets/Tests/PlayMode/AsyncGeometryStressTests.cs"

replace_once(
    arena,
    """        public int UsedArgsRecords => _argsRanges.Used / ArgsWordsPerDraw;
        public ulong AllocationFailureCount { get; private set; }
""",
    """        public int UsedArgsRecords => _argsRanges.Used / ArgsWordsPerDraw;
        private int _maxActiveLeases = int.MaxValue;
        /// <summary>
        /// Soft publication-pressure ceiling. This never resizes the GPU buffers; it only makes
        /// new staging leases observe backpressure once the configured number of live/staging
        /// draws is reached. Production defaults to unlimited relative to the fixed arena.
        /// </summary>
        public int MaxActiveLeases
        {
            get => _maxActiveLeases;
            set => _maxActiveLeases = math.max(1, value);
        }
        public ulong AllocationFailureCount { get; private set; }
""",
    "arena soft lease cap",
)

replace_once(
    arena,
    """            lease = default;
            if (_disposed) return false;

            int vertices = Align(math.max(1, vertexCount), VertexAlignment);
""",
    """            lease = default;
            if (_disposed) return false;
            if (UsedArgsRecords >= _maxActiveLeases)
            {
                AllocationFailureCount++;
                return false;
            }

            int vertices = Align(math.max(1, vertexCount), VertexAlignment);
""",
    "arena cap enforcement",
)

replace_once(
    scheduler,
    """        public readonly long SolidArenaUsedBytes;
        public readonly ulong SolidArenaAllocationFailures;
""",
    """        public readonly long SolidArenaUsedBytes;
        public readonly int SolidArenaActiveLeases;
        public readonly ulong SolidArenaAllocationFailures;
""",
    "metrics active lease field",
)

replace_once(
    scheduler,
    """            SolidArenaCommittedBytes = 0;
            SolidArenaUsedBytes = 0;
            SolidArenaAllocationFailures = 0;
""",
    """            SolidArenaCommittedBytes = 0;
            SolidArenaUsedBytes = 0;
            SolidArenaActiveLeases = 0;
            SolidArenaAllocationFailures = 0;
""",
    "single worker active lease default",
)

replace_once(
    scheduler,
    """                                     long solidArenaCommittedBytes,
                                     long solidArenaUsedBytes,
                                     ulong solidArenaAllocationFailures,
""",
    """                                     long solidArenaCommittedBytes,
                                     long solidArenaUsedBytes,
                                     int solidArenaActiveLeases,
                                     ulong solidArenaAllocationFailures,
""",
    "metrics active lease parameter",
)

replace_once(
    scheduler,
    """            SolidArenaCommittedBytes = solidArenaCommittedBytes;
            SolidArenaUsedBytes = solidArenaUsedBytes;
            SolidArenaAllocationFailures = solidArenaAllocationFailures;
""",
    """            SolidArenaCommittedBytes = solidArenaCommittedBytes;
            SolidArenaUsedBytes = solidArenaUsedBytes;
            SolidArenaActiveLeases = solidArenaActiveLeases;
            SolidArenaAllocationFailures = solidArenaAllocationFailures;
""",
    "metrics active lease assignment",
)

replace_once(
    scheduler,
    """        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;
        public long LastFrameManagedAllocationBytes => _lastFrameManagedAllocationBytes;
""",
    """        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;
        public long LastFrameManagedAllocationBytes => _lastFrameManagedAllocationBytes;
        public int SolidArenaMaxActiveLeases
        {
            get => _geometryArena.MaxActiveLeases;
            set => _geometryArena.MaxActiveLeases = value;
        }
""",
    "scheduler arena cap property",
)

replace_once(
    scheduler,
    """            _lastFrameSolidUploadCompletions, _geometryArena.CommittedGpuBytes,
            _geometryArena.UsedGpuBytes, _geometryArena.AllocationFailureCount,
""",
    """            _lastFrameSolidUploadCompletions, _geometryArena.CommittedGpuBytes,
            _geometryArena.UsedGpuBytes, _geometryArena.UsedArgsRecords,
            _geometryArena.AllocationFailureCount,
""",
    "scheduler active lease metric plumbing",
)

replace_once(
    bridge,
    """        public static int SolidUploadWorkerBudget = 4;
        public static double SolidUploadBudgetMs = 0.20;
        public static double WaterBuildBudgetMs = 0.15;
""",
    """        public static int SolidUploadWorkerBudget = 4;
        public static double SolidUploadBudgetMs = 0.20;
        /// <summary>
        /// Soft cap for active solid arena leases. The default does not constrain the fixed
        /// arena; tests/debugging may lower it to exercise real backpressure without reallocating
        /// GPU buffers or changing the arena's committed byte size.
        /// </summary>
        public static int SolidArenaMaxActiveLeases = int.MaxValue;
        public static double WaterBuildBudgetMs = 0.15;
""",
    "bridge arena cap",
)

replace_once(
    render_pass,
    """            _scheduler.SolidUploadWorkerBudget = Math.Max(0, VoxelRenderBridge.SolidUploadWorkerBudget);
            _scheduler.SolidUploadBudgetMs = Math.Max(0.0, VoxelRenderBridge.SolidUploadBudgetMs);
            _scheduler.WaterBuildBudgetMs = Math.Max(0.0, VoxelRenderBridge.WaterBuildBudgetMs);
""",
    """            _scheduler.SolidUploadWorkerBudget = Math.Max(0, VoxelRenderBridge.SolidUploadWorkerBudget);
            _scheduler.SolidUploadBudgetMs = Math.Max(0.0, VoxelRenderBridge.SolidUploadBudgetMs);
            _scheduler.SolidArenaMaxActiveLeases = Math.Max(
                1, VoxelRenderBridge.SolidArenaMaxActiveLeases);
            _scheduler.WaterBuildBudgetMs = Math.Max(0.0, VoxelRenderBridge.WaterBuildBudgetMs);
""",
    "render pass arena cap plumbing",
)

arch_test = r'''

        [Test]
        public void SolidArenaPressureIsBackpressureNotBufferGrowth()
        {
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));
            string bridge = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderBridge.cs"));

            StringAssert.Contains("public int MaxActiveLeases", arena);
            StringAssert.Contains("if (UsedArgsRecords >= _maxActiveLeases)", arena);
            StringAssert.Contains("AllocationFailureCount++", arena);
            StringAssert.Contains("SolidArenaMaxActiveLeases", scheduler);
            StringAssert.Contains("SolidArenaActiveLeases", scheduler);
            StringAssert.Contains("SolidArenaMaxActiveLeases", bridge);
            StringAssert.Contains("_scheduler.SolidArenaMaxActiveLeases", renderPass);

            int acquire = arena.IndexOf("public bool TryAcquire", StringComparison.Ordinal);
            int release = arena.IndexOf("public void Release", acquire, StringComparison.Ordinal);
            Assert.GreaterOrEqual(acquire, 0);
            Assert.Greater(release, acquire);
            string streamingAcquire = arena.Substring(acquire, release - acquire);
            StringAssert.DoesNotContain("new ComputeBuffer", streamingAcquire);
        }
'''
insert_before(
    arch,
    "\n        [Test]\n        public void KnownFramePathJobCompletionsAreReadinessGated()",
    arch_test,
    "arena pressure architecture test",
)

stress_test = r'''

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
                    camera.Render();
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
                    camera.Render();
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
'''
insert_before(
    stress,
    "\n\n        [UnityTest, Timeout(900000)]\n        public IEnumerator WarmRepeatedClipmapTraversalAllocatesNoManagedGeometryMemory()",
    stress_test,
    "arena pressure PlayMode test",
)

# Final static guards.
a = Path(arena).read_text()
s = Path(scheduler).read_text()
b = Path(bridge).read_text()
r = Path(render_pass).read_text()
t = Path(stress).read_text()
assert 'if (UsedArgsRecords >= _maxActiveLeases)' in a
assert 'SolidArenaActiveLeases' in s
assert 'SolidArenaMaxActiveLeases' in b
assert '_scheduler.SolidArenaMaxActiveLeases' in r
assert 'ArenaPressureDelaysConvergenceWithoutGrowingBuffersOrOpeningHoles' in t
print('arena backpressure gate patch applied')
