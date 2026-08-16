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


scheduler = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs"
arch = "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs"
stress = "Assets/Tests/PlayMode/AsyncGeometryStressTests.cs"

replace_once(
    scheduler,
    """        public readonly int RunningGeometryJobs;
        public readonly ulong FramePathBlockingCompletionViolations;
        public readonly int SolidMeshesAwaitingUpload;
""",
    """        public readonly int RunningGeometryJobs;
        public readonly ulong FramePathBlockingCompletionViolations;
        public readonly long LastFrameManagedAllocationBytes;
        public readonly int SolidMeshesAwaitingUpload;
""",
    "metrics allocation field",
)

replace_once(
    scheduler,
    """            FramePathBlockingCompletionViolations =
                solids.FramePathBlockingCompletionViolations
                + water.FramePathBlockingCompletionViolations;
            SolidMeshesAwaitingUpload = solids.PendingUploadCount;
""",
    """            FramePathBlockingCompletionViolations =
                solids.FramePathBlockingCompletionViolations
                + water.FramePathBlockingCompletionViolations;
            LastFrameManagedAllocationBytes = 0;
            SolidMeshesAwaitingUpload = solids.PendingUploadCount;
""",
    "single-worker metrics allocation default",
)

replace_once(
    scheduler,
    """                                     in VoxelTimingSummary visibility,
                                     int schedulerRunningJobs,
                                     ulong schedulerCompletionViolations)
""",
    """                                     in VoxelTimingSummary visibility,
                                     int schedulerRunningJobs,
                                     ulong schedulerCompletionViolations,
                                     long lastFrameManagedAllocationBytes)
""",
    "metrics constructor allocation parameter",
)

replace_once(
    scheduler,
    """            FramePathBlockingCompletionViolations =
                completionViolations + schedulerCompletionViolations;
            SolidMeshesAwaitingUpload = uploads;
""",
    """            FramePathBlockingCompletionViolations =
                completionViolations + schedulerCompletionViolations;
            LastFrameManagedAllocationBytes = lastFrameManagedAllocationBytes;
            SolidMeshesAwaitingUpload = uploads;
""",
    "metrics allocation assignment",
)

replace_once(
    scheduler,
    """        private readonly VoxelTimingWindow _visibilityTiming = new();
        private ulong _framePathBlockingCompletionViolations;
""",
    """        private readonly VoxelTimingWindow _visibilityTiming = new();
        private ulong _framePathBlockingCompletionViolations;
        private long _lastFrameManagedAllocationBytes;
""",
    "scheduler allocation state",
)

replace_once(
    scheduler,
    """        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;
        internal int KnownChunkCountForSourceStep(int sourceStep)
""",
    """        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;
        public long LastFrameManagedAllocationBytes => _lastFrameManagedAllocationBytes;
        internal int KnownChunkCountForSourceStep(int sourceStep)
""",
    "scheduler allocation property",
)

replace_once(
    scheduler,
    """            _surfaceDiscoveryJobScheduled ? 1 : 0,
            _framePathBlockingCompletionViolations);
""",
    """            _surfaceDiscoveryJobScheduled ? 1 : 0,
            _framePathBlockingCompletionViolations,
            _lastFrameManagedAllocationBytes);
""",
    "metrics allocation plumbing",
)

replace_once(
    scheduler,
    """            if (_lastAdvancedFrame == frame)
            {
                CollectVisibility(camera, voxelSize, frame);
                return;
            }
            _lastAdvancedFrame = frame;

            double prepareStart = Time.realtimeSinceStartupAsDouble;
""",
    """            if (_lastAdvancedFrame == frame)
            {
                CollectVisibility(camera, voxelSize, frame);
                return;
            }

            // Measure only the once-per-world-frame geometry orchestration path. Secondary
            // camera visibility collection is intentionally excluded: this counter answers the
            // merge-gate question "did streaming/geometry allocate after warmup?" without being
            // polluted by unrelated camera/test-runner allocations.
            long managedAllocationStart = GC.GetAllocatedBytesForCurrentThread();
            _lastAdvancedFrame = frame;

            double prepareStart = Time.realtimeSinceStartupAsDouble;
""",
    "allocation measurement start",
)

replace_once(
    scheduler,
    """            CollectVisibility(camera, voxelSize, frame);
            _prepareTiming.Add(ElapsedMs(prepareStart));
        }
""",
    """            CollectVisibility(camera, voxelSize, frame);
            _prepareTiming.Add(ElapsedMs(prepareStart));
            _lastFrameManagedAllocationBytes = Math.Max(
                0L, GC.GetAllocatedBytesForCurrentThread() - managedAllocationStart);
        }
""",
    "allocation measurement end",
)

arch_test = r'''

        [Test]
        public void GeometrySchedulerExposesFrameScopedManagedAllocationCounter()
        {
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("LastFrameManagedAllocationBytes", scheduler);
            StringAssert.Contains("GC.GetAllocatedBytesForCurrentThread()", scheduler);
            StringAssert.Contains("long managedAllocationStart", scheduler);
            StringAssert.DoesNotContain("GC.GetTotalMemory", scheduler);

            int start = scheduler.IndexOf("long managedAllocationStart", StringComparison.Ordinal);
            int end = scheduler.IndexOf("private void CollectVisibility", start,
                                        StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            Assert.Greater(end, start);
            string framePath = scheduler.Substring(start, end - start);
            Assert.GreaterOrEqual(CountOccurrences(
                framePath, "GC.GetAllocatedBytesForCurrentThread()"), 2);
        }
'''
insert_before(
    arch,
    "\n\n        [Test]\n        public void SolidVisibilityTraversesOnlyActiveToroidalSlotsOncePerRing()",
    arch_test,
    "allocation architecture test",
)

stress_test = r'''

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
                    camera.Render();
                    yield return null;
                }

                long maxAllocated = 0;
                int observedFrames = 0;
                for (int frame = 0; frame < pathFrames; frame++)
                {
                    PositionAllocationPathCamera(camera, centre, lookAt, frame, pathFrames);
                    camera.Render();
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
'''
insert_before(
    stress,
    "\n        private static int ExplodeAtOffset(ShowcaseWorld world, CastlePlan plan,",
    stress_test,
    "steady-state allocation PlayMode test",
)

# Final source guards.
s = Path(scheduler).read_text()
assert 'public readonly long LastFrameManagedAllocationBytes;' in s
assert s.count('GC.GetAllocatedBytesForCurrentThread()') == 2
assert 'GC.GetTotalMemory' not in s
assert 'WarmRepeatedClipmapTraversalAllocatesNoManagedGeometryMemory' in Path(stress).read_text()
print('steady-state allocation instrumentation patch applied')
