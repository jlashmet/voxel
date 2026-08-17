from pathlib import Path


def replace_once(path_text, old, new, label):
    path = Path(path_text)
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one source match, found {count}")
    path.write_text(text.replace(old, new, 1))


replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs",
    '''                    if (!OwnsShard(chunk) || _known.Contains(chunk)) continue;\n                    if (!TrackKnown(chunk)) continue;\n                    Invalidate(chunk);\n                    admitted++;\n''',
    '''                    if (!OwnsShard(chunk) || _known.Contains(chunk)) continue;\n                    if (!TrackKnown(chunk)) continue;\n\n                    // Discovery establishes authoritative source state, not immediate build\n                    // demand. Every LOD ring learns the same surface summaries, but only the\n                    // ring currently owning this chunk should consume the renderer-wide build\n                    // budget. CollectVisibleCoordinate activates in-band demand before worker\n                    // admission; retaining only the desired generation here prevents thousands\n                    // of finer/coarser off-band chunks from filling the dirty FIFO at startup.\n                    _desiredVersions[chunk] = ++_versionCounter;\n                    admitted++;\n''',
    "discovery records desired generation without dirtying every LOD",
)

replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs",
    '''            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);\n            if (!WithinRingBand(bounds, cameraPosition)) return;\n            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;\n\n            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);\n            bool currentGenerationInFlight = CurrentBuildCoversDesiredGeneration(\n                coordinate, hasDesired, desired);\n            if (_entries.TryGetValue(coordinate, out Entry entry) && entry.Ready)\n            {\n                // Keep the previous mesh drawable while a newer authoritative generation waits\n                // for this ring to need it. Parking background work must never turn an edit into\n                // stale visible geometry when the chunk comes back into the active shell. Do not\n                // enqueue the same generation again while its replacement is already building or\n                // awaiting upload; admission removes active builds from _dirty, so visibility\n                // would otherwise recreate a permanent duplicate rebuild loop every frame.\n                if (hasDesired && desired > entry.SourceVersion && !currentGenerationInFlight)\n                    MarkDirty(coordinate);\n                if (entry.IndexCount == 0) return;\n                entry.LastUsedFrame = frame;\n                _visible.Add(entry);\n                return;\n            }\n\n            // A current known-empty result is complete, not a visual hole. Any other in-band\n            // visible coordinate is demand: reactivate work that discovery parked while the\n            // coordinate belonged to another LOD ring (or was evicted under pressure). An active\n            // build/pending upload already satisfies that demand for its exact source generation.\n            if (_emptyVersions.TryGetValue(coordinate, out ulong emptyVersion)\n                && (!hasDesired || emptyVersion >= desired))\n                return;\n\n            if (!currentGenerationInFlight) MarkDirty(coordinate);\n            MissingVisibleCount++;\n''',
    '''            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);\n            if (!WithinRingBand(bounds, cameraPosition))\n            {\n                // Authoritative discovery is shared across LODs. Keep the known/version state,\n                // but never let a chunk owned wholly by another ring remain active build demand.\n                if (_dirty.Contains(coordinate)) ParkDirty(coordinate);\n                return;\n            }\n\n            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);\n            bool currentGenerationInFlight = CurrentBuildCoversDesiredGeneration(\n                coordinate, hasDesired, desired);\n            bool ready = _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;\n            bool currentReady = ready && (!hasDesired || entry.SourceVersion >= desired);\n            bool currentEmpty = _emptyVersions.TryGetValue(coordinate, out ulong emptyVersion)\n                             && (!hasDesired || emptyVersion >= desired);\n\n            // This traversal covers the ring's dense active-slot list. Activate build demand for\n            // every in-band chunk before the frustum test so geometry is prefetched around the\n            // viewer, while still excluding the thousands of known chunks owned by other LODs.\n            if (!currentReady && !currentEmpty && !currentGenerationInFlight)\n                MarkDirty(coordinate);\n\n            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;\n\n            if (ready)\n            {\n                // Keep the previous mesh drawable while a newer authoritative generation builds.\n                // CurrentBuildCoversDesiredGeneration above prevents visibility from recreating a\n                // duplicate dirty record for the exact generation already in flight.\n                if (entry.IndexCount == 0) return;\n                entry.LastUsedFrame = frame;\n                _visible.Add(entry);\n                return;\n            }\n\n            // A current known-empty result is complete, not a visual hole. Any other in-band\n            // visible coordinate remains missing until its authoritative generation publishes.\n            if (currentEmpty) return;\n\n            MissingVisibleCount++;\n''',
    "ring demand activated before frustum while off-band demand parks",
)

replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs",
    '''            // Discovery is correctness work rather than build admission: every worker must learn\n            // about newly surfaced bricks even if this frame has no time left to rebuild them.\n            for (int i = 0; i < _allWorkers.Length; i++)\n                _allWorkers[i].DiscoverSurfaceBricks(_discoveredSurfaceBricks);\n\n            double workersStart = Time.realtimeSinceStartupAsDouble;\n''',
    '''            // Discovery is correctness work rather than build admission: every worker must learn\n            // about newly surfaced bricks even if this frame has no time left to rebuild them.\n            // CpuTransvoxelChunkCache records authoritative desired generations here but defers\n            // dirty admission until the current ring traversal below establishes ownership.\n            for (int i = 0; i < _allWorkers.Length; i++)\n                _allWorkers[i].DiscoverSurfaceBricks(_discoveredSurfaceBricks);\n\n            // Collect the current ring demand before spending the renderer-wide build budget.\n            // The same bounded active-slot traversal also computes draw visibility, so visible\n            // chunks enter the queue in this frame instead of one frame after admission.\n            CollectVisibility(camera, voxelSize, frame);\n\n            double workersStart = Time.realtimeSinceStartupAsDouble;\n''',
    "visibility and ring demand precede worker admission",
)

replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs",
    '''            JobHandle.ScheduleBatchedJobs();\n\n            CollectVisibility(camera, voxelSize, frame);\n            _prepareTiming.Add(ElapsedMs(prepareStart));\n''',
    '''            JobHandle.ScheduleBatchedJobs();\n\n            // Visibility was collected before admission so newly discovered in-band demand could\n            // participate in this frame's fixed build budget. Newly published geometry becomes\n            // drawable on the next frame; no second active-slot traversal is spent here.\n            _prepareTiming.Add(ElapsedMs(prepareStart));\n''',
    "remove duplicate post-admission visibility traversal",
)

replace_once(
    "Assets/Tests/EditMode/SurfaceRingBuildAdmissionTests.cs",
    '''            int admitted = (int)discover.Invoke(cache, new object[]\n            {\n                new List<int3> { new int3(1, 1, 1) }\n            });\n            Assert.AreEqual(1, admitted);\n\n            var cameraObject = new GameObject("SurfaceRingBuildAdmissionTests.ActiveCamera");\n''',
    '''            int admitted = (int)discover.Invoke(cache, new object[]\n            {\n                new List<int3> { new int3(1, 1, 1) }\n            });\n            Assert.AreEqual(1, admitted);\n            Assert.AreEqual(0, cache.DirtyCount,\n                "Authoritative discovery should not consume build admission before ring demand is known.");\n\n            var cameraObject = new GameObject("SurfaceRingBuildAdmissionTests.ActiveCamera");\n''',
    "active-build regression expects demand-deferred discovery",
)

replace_once(
    "Assets/Tests/EditMode/SurfaceRingBuildAdmissionTests.cs",
    '''                camera.nearClipPlane = 0.3f;\n                camera.farClipPlane = 200f;\n\n                bool selected = (bool)select.Invoke(cache, new object[]\n''',
    '''                camera.nearClipPlane = 0.3f;\n                camera.farClipPlane = 200f;\n\n                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);\n                cache.BeginVisibilityCollection();\n                cache.CollectVisibleCoordinate(int3.zero, planes,\n                    camera.transform.position, 0.1f, 1);\n                Assert.AreEqual(1, cache.DirtyCount,\n                    "Current-ring visibility did not activate the discovered authoritative generation.");\n\n                bool selected = (bool)select.Invoke(cache, new object[]\n''',
    "activate current ring demand before selecting build",
)

replace_once(
    "Assets/Tests/EditMode/SurfaceRingBuildAdmissionTests.cs",
    '''                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);\n                cache.BeginVisibilityCollection();\n                cache.CollectVisibleCoordinate(int3.zero, planes,\n                    camera.transform.position, 0.1f, 1);\n\n                Assert.AreEqual(1, cache.MissingVisibleCount);\n''',
    '''                cache.BeginVisibilityCollection();\n                cache.CollectVisibleCoordinate(int3.zero, planes,\n                    camera.transform.position, 0.1f, 2);\n\n                Assert.AreEqual(1, cache.MissingVisibleCount);\n''',
    "reuse planes for duplicate-admission assertion",
)

replace_once(
    "Assets/Tests/EditMode/SurfaceRingBuildAdmissionTests.cs",
    '''            Assert.Greater(admitted, 0);\n            Assert.Greater(cache.DirtyCount, 0,\n                "Discovery must retain authoritative work/version state before admission is evaluated.");\n\n            var cameraObject = new GameObject("SurfaceRingBuildAdmissionTests.Camera");\n''',
    '''            Assert.Greater(admitted, 0);\n            Assert.AreEqual(0, cache.DirtyCount,\n                "Discovery should retain authoritative versions without dirtying an LOD before ring ownership is known.");\n\n            var cameraObject = new GameObject("SurfaceRingBuildAdmissionTests.Camera");\n''',
    "out-of-band discovery stays inactive until ring demand",
)

replace_once(
    ".claude/plans/voxel-showcase-rendering-repair-v2.md",
    '''- [x] Validate the HLOD capacity-resolution regression in EditMode; the current PR run contains no `Feature-preserving HLOD output overflow` diagnostic.\n- [ ] Fix the remaining coarse-coverage defect: `CastleExteriorLookdevTests` still observes 43 visible coarse chunks without ready geometry even after overflow removal.\n''',
    '''- [x] Validate the HLOD capacity-resolution regression in EditMode; the current PR run contains no `Feature-preserving HLOD output overflow` diagnostic.\n- [x] Prevent visibility from re-enqueuing the exact authoritative generation already building/awaiting publication; current-head PR run 32014802229 passes the `VisibleCurrentGenerationBuildDoesNotQueueDuplicateAdmission` regression.\n- [ ] Fix the remaining coarse-coverage defect: current-head PR run 32014802229 still observes 44 visible coarse chunks without ready geometry. The next measured defect is startup discovery dirtying every LOD before ring ownership, leaving 4,225 dirty chunks after 10 s and visible demand behind off-band work.\n''',
    "record current coarse evidence and completed duplicate-admission task",
)

replace_once(
    ".claude/plans/voxel-showcase-rendering-repair-v2.md",
    '''- [ ] Confirm the new per-fixture PlayMode shard layout no longer hits the Unity RSS watchdog on the current head.\n- [ ] Classify any remaining CI failures as rendering-repair regressions vs unrelated baseline failures; do not mask either category.\n''',
    '''- [ ] Confirm the new per-fixture PlayMode shard layout no longer hits the Unity RSS watchdog on the current head. PR run 32014802229 proves crash/session isolation works and later shards continue, but the already-single-method `MemoryStaysWithinTierBudgetOverTwoHours` and `ContinuousTraversalOverKilometresShowsNoGaps` shards still hit 14,357 MB and 14,381 MB respectively against the unchanged 14,336 MB ceiling; further YAML splitting cannot solve those two tests.\n- [x] Classify current-head PR run 32014802229 failures without masking them: rendering-repair blockers are coarse/scheduler convergence (`CastleExteriorLookdevTests`, `ShowcasePerformanceTests`, `LodRenderingTests`, `TerrainLookdevScreenshotTests` and the async-geometry published-baseline fixtures); `LodVisualFidelityTests` is currently blocked by its `RenderRequest` destination setup rather than a fidelity threshold; `ShowcaseNoStutterTests` is a validation-path failure because its measured window never observes production build work. Baseline/non-repair failures reproduced independently include `DistantAlterationTests` length validation, `FallingVoxelPhysicsTests` debris lifetime, far-terrain relief/ground-ahead assertions, plus the two synthetic residency-memory watchdog tests above.\n''',
    "record watchdog evidence and failure classification",
)

replace_once(
    ".claude/plans/voxel-showcase-rendering-repair-v2.md",
    '''- [x] Record the current failed convergence measurement: at 10.00 s the renderer had 128/5,672 resident chunks, 5,483 dirty chunks, 14 running jobs, 1,591 missing visible chunks, queue p95 4,356.37 ms and build p95 890.13 ms. This is evidence for the next scheduler/coverage repair, not an accepted performance result.\n''',
    '''- [x] Record the current failed convergence measurement: at 10.00 s the renderer had 128/5,672 resident chunks, 5,483 dirty chunks, 14 running jobs, 1,591 missing visible chunks, queue p95 4,356.37 ms and build p95 890.13 ms. This is evidence for the next scheduler/coverage repair, not an accepted performance result.\n- [x] Record the post-lifecycle-dedup measurement from PR run 32014802229: at 10.00 s the renderer had 131/5,672 resident chunks, 4,225 dirty chunks, 16 running jobs, 1,567 missing visible chunks, queue p95 4,578.88 ms and build p95 1,036.66 ms. The duplicate-generation fix reduces stale dirty work but does not solve ring-demand starvation.\n''',
    "record run 168 scheduler measurement",
)

replace_once(
    ".claude/plans/voxel-showcase-rendering-repair-v2.md",
    '''- `b0576ad4` — master workflow mirrors the same per-fixture PlayMode isolation.\n\nLatest measured validation before the per-fixture shard split: PR #88 run 32006271470 on head `82ae4aaf` had green EditMode and bake. PlayMode proved the far-terrain topology test passes and HLOD overflow is removed, but `ShowcaseNoStutterTests` and `ShowcasePerformanceTests` still fail, coarse lookdev still reports 43 missing chunks, and the old L-M/T-Z-rest groups still hit the RSS watchdog.\n''',
    '''- `b0576ad4` — master workflow mirrors the same per-fixture PlayMode isolation.\n- `def488ae` — Unity launcher owns/reaps a process session so a natural Burst/LLVM crash cannot poison later fresh-process shards.\n- `41528f01` — production `VoxelRenderPass` exposes an internal diagnostics-only active-pass handle for fidelity tests; no renderer ownership or thresholds changed.\n- `cad65015` — visible demand no longer recreates a duplicate dirty record for the same authoritative generation already in flight.\n\nCurrent-head PR #88 run 32014802229 (`2621fd00`) has green Architecture, EditMode (650/650), bake, CombatPrototype, CI PlayMode, Features and Parity. The isolated PlayMode sequence continued after individual failures, proving the crash-session cleanup, but two single-method synthetic residency tests still exceed the unchanged RSS watchdog. Renderer acceptance remains open: coarse lookdev reports 44 holes, 10-second convergence is 131/5,672 resident with 4,225 dirty and 1,567 missing visible chunks, LOD step 4 does not stabilize, and fidelity capture is blocked by a `RenderRequest` destination setup error. The next implementation separates authoritative surface discovery from current-ring build demand so off-band LOD knowledge cannot flood the build FIFO.\n''',
    "append current branch continuation evidence",
)
