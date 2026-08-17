from pathlib import Path


def replace_once(path_text, old, new, label):
    path = Path(path_text)
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one source match, found {count}")
    path.write_text(text.replace(old, new, 1))


replace_once(
    "Assets/Tests/EditMode/SurfaceBrickDiscoveryTests.cs",
    '''            Assert.AreEqual(1, cache.KnownCount);\n            Assert.AreEqual(1, cache.DirtyCount);\n\n            Assert.AreEqual(0, cache.DiscoverSurfaceBricks(new[] { brick }),\n                "Later publication slices for the same unchanged region must not create a new "\n              + "source generation for an already-known chunk.");\n            Assert.AreEqual(1, cache.KnownCount);\n            Assert.AreEqual(1, cache.DirtyCount);\n\n            // Real edits keep the old semantics: known chunks are explicitly invalidated. The\n            // dirty set coalesces membership, but the call is still routed through the mutation\n            // path rather than discovery admission.\n''',
    '''            Assert.AreEqual(1, cache.KnownCount);\n            Assert.AreEqual(0, cache.DirtyCount,\n                "Immutable discovery should establish authoritative known/version state without "\n              + "creating build demand until the chunk belongs to the active ring traversal.");\n\n            Assert.AreEqual(0, cache.DiscoverSurfaceBricks(new[] { brick }),\n                "Later publication slices for the same unchanged region must not create a new "\n              + "source generation for an already-known chunk.");\n            Assert.AreEqual(1, cache.KnownCount);\n            Assert.AreEqual(0, cache.DirtyCount,\n                "Repeated immutable discovery must remain passive and must not re-dirty the chunk.");\n\n            // Real edits keep the old semantics: known chunks are explicitly invalidated. Unlike\n            // immutable discovery, the mutation path must activate dirty rebuild demand immediately.\n''',
    "passive discovery regression contract",
)

replace_once(
    "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs",
    '''            int water = scheduler.IndexOf("_water.Prepare(storage, camera, voxelSize, WaterBuildBudgetMs);",\n                                          StringComparison.Ordinal);\n            int visibility = scheduler.IndexOf("CollectVisibility(camera, voxelSize, frame);", first,\n                                               StringComparison.Ordinal);\n            Assert.Greater(first, water, "Flush must include water and solid jobs scheduled this frame.");\n            Assert.Greater(visibility, first, "Flush must happen before the frame returns to draw traversal.");\n''',
    '''            int water = scheduler.IndexOf("_water.Prepare(storage, camera, voxelSize, WaterBuildBudgetMs);",\n                                          StringComparison.Ordinal);\n            int visibility = scheduler.LastIndexOf(\n                "CollectVisibility(camera, voxelSize, frame);", StringComparison.Ordinal);\n            int frameAccounting = scheduler.IndexOf(\n                "_prepareTiming.Add(ElapsedMs(prepareStart));", first, StringComparison.Ordinal);\n            Assert.Greater(first, water,\n                "Flush must include water and solid jobs scheduled this frame.");\n            Assert.Greater(first, visibility,\n                "Current-ring/visible demand must be collected before worker admission and its "\n              + "single non-blocking batch flush.");\n            Assert.Greater(frameAccounting, first,\n                "The non-blocking batch flush must occur before the scheduler returns the frame.");\n''',
    "flush architecture contract",
)

replace_once(
    ".claude/plans/voxel-showcase-rendering-repair-v2.md",
    '''- [x] Promote real frustum-visible missing/stale chunks ahead of the 360-degree in-band prefetch FIFO while preserving background streaming and the unchanged global build budget.\n- [ ] Validate `SurfaceRingBuildAdmissionTests.FrustumVisibleDemandBypassesBackgroundPrefetchBacklog` in Unity and remeasure production coarse coverage/convergence.\n- [ ] Fix the remaining coarse-coverage defect: current-head PR run 32014802229 still observes 44 visible coarse chunks without ready geometry. The measured scheduler defect is in-band prefetch starvation: 4,225 valid dirty records remain after 10 s while bounded 64-record FIFO sampling leaves visible demand at queue p95 4,578.88 ms.\n''',
    '''- [x] Promote real frustum-visible missing/stale chunks ahead of the 360-degree in-band prefetch FIFO while preserving background streaming and the unchanged global build budget.\n- [x] Validate `SurfaceRingBuildAdmissionTests.FrustumVisibleDemandBypassesBackgroundPrefetchBacklog` in Unity; PR run 32019198712 passes the focused regression.\n- [ ] Remeasure production coarse coverage/convergence with the visible-demand priority path active.\n- [ ] Fix the remaining coarse-coverage defect: current-head PR run 32014802229 still observes 44 visible coarse chunks without ready geometry. The measured scheduler defect is in-band prefetch starvation: 4,225 valid dirty records remain after 10 s while bounded 64-record FIFO sampling leaves visible demand at queue p95 4,578.88 ms.\n''',
    "plan priority validation split",
)

replace_once(
    ".claude/plans/voxel-showcase-rendering-repair-v2.md",
    '''Current-head PR #88 run 32014802229 (`2621fd00`) has green Architecture, EditMode (650/650), bake, CombatPrototype, CI PlayMode, Features and Parity. The isolated PlayMode sequence continued after individual failures, proving the crash-session cleanup, but two single-method synthetic residency tests still exceed the unchanged RSS watchdog. Renderer acceptance remains open: coarse lookdev reports 44 holes, 10-second convergence is 131/5,672 resident with 4,225 dirty and 1,567 missing visible chunks, LOD step 4 does not stabilize, and fidelity capture is blocked by a `RenderRequest` destination setup error. Discovery is already passive and off-band work is already parked; the next measured repair promotes true frustum-visible demand ahead of the valid 360-degree in-band prefetch FIFO that produced 4.58 s queue p95.\n''',
    '''PR #88 run 32019198712 (`9987bfb5`) proves the new visible-demand priority regression itself passes. That run stops in EditMode on two stale validation contracts inherited from the preceding passive-discovery/current-ring change: immutable discovery still expected immediate dirty work, and the batch-flush architecture test still expected a post-flush visibility traversal even though demand collection now intentionally precedes worker admission. Those contracts are being reconciled without changing production renderer behavior or any acceptance threshold. The last production measurement remains run 32014802229 (`2621fd00`): coarse lookdev reports 44 holes, 10-second convergence is 131/5,672 resident with 4,225 dirty and 1,567 missing visible chunks, LOD step 4 does not stabilize, and fidelity capture is blocked by a `RenderRequest` destination setup error.\n''',
    "plan run176 classification",
)
