# Async Geometry Pipeline Refactor

**Branch:** `refactor/async-geometry-pipeline`  
**Status:** In progress — runtime acceptance  
**Primary invariant:** **The main frame never waits for geometry. Geometry waits for the main frame.**

This plan owns the rendering/streaming work that was intentionally out of scope for
`ARCHITECTURE_IMPLEMENTATION_PLAN.md`. Check an implementation item only after the code and its
source-level regression guard are committed. Runtime/PlayMode acceptance remains a separate gate.

## Required frame contract

- No `JobHandle.Complete()` may wait for unfinished geometry work on the frame path.
- CPU snapshot, admission, result merge, publication, residency and invalidation work are bounded.
- GPU payload bytes and publication work are bounded per frame.
- Old valid geometry remains visible until a complete replacement is ready.
- Stale generations are discarded instead of published.
- Arena exhaustion and fast camera movement reduce convergence rate, never frame rate.
- Steady-state streaming/rendering must not generate managed garbage.

## Implementation checklist

### Async scheduling and frame ownership

- [x] Use one renderer-wide solid build deadline across every LOD ring and worker.
- [x] Advance journal/build/upload only once per `Time.frameCount`; extra cameras only recollect visibility.
- [x] Make surface-brick discovery asynchronous and gate `Complete()` behind `IsCompleted`.
- [x] Treat surface discovery as admission-only after first sighting; repeated 512-brick publication slices never advance an already-known chunk generation.
- [x] Schedule transition-cell meshing instead of calling `TransitionMeshJob.Run()` inline.
- [x] Explicitly dispatch buffered geometry jobs once per world frame with non-blocking `JobHandle.ScheduleBatchedJobs()`; never flush per worker.
- [x] Defer residency removal when a chunk still owns an unfinished job instead of waiting for it.
- [x] Slice exact-brick and mip snapshot construction across frame deadlines.
- [x] Abort partial snapshots when a newer source generation arrives.
- [x] Slice completed topology/faceted/transition result merges across frame deadlines.
- [x] Bound dirty build selection with an incremental/coalescing queue instead of scanning all dirty chunks.
- [x] Limit capacity eviction to one off-screen entry per workspace `Prepare` call.
- [x] Bound residency liveness checks with a persistent round-robin queue.
- [x] Bound full-region invalidation with resumable candidate traversal; fine-grained brick edits remain immediate.
- [x] Bound change-journal reads, record-to-brick expansion, and retention-overflow recovery.

### Publication and GPU ownership

- [x] Separate CPU mesh completion from GPU publication with a real pending-publication state.
- [x] Enforce renderer-wide upload byte, slice, worker and wall-clock budgets.
- [x] Keep old published geometry live while a replacement uploads.
- [x] Validate source generation immediately before publication and discard stale results.
- [x] Replace per-entry vertex/index/args buffers with one shared solid geometry arena.
- [x] Use lease swap for atomic publication.
- [x] Treat arena exhaustion as backpressure; never allocate a fallback `ComputeBuffer` while streaming.
- [x] Reclaim at most one old off-screen arena lease per pressure frame.
- [x] Keep final solid build output native from Burst completion through GPU upload.
- [x] Remove per-entry managed indirect-args arrays; use persistent arena scratch.
- [x] Add a soft fixed-arena lease ceiling for deterministic pressure testing without changing committed GPU capacity.

### Workspace and allocation control

- [x] Cache the flattened worker array instead of allocating it when metrics are read.
- [x] Scale build workspace counts by LOD: 8 / 8 / 4 / 2 for source steps 1 / 2 / 4 / 8.
- [x] Disable formatted per-frame surface diagnostic strings by default; structured metrics remain always available.
- [x] Pool/reuse managed `Entry` objects so churn after residency eviction does not allocate.
- [x] Introduce persistent `SurfaceChunkSlot` identities with generation tokens; stale builds validate the slot before publication.
- [x] Split persistent surface chunk/slot state from reusable geometry build workspaces.
- [x] Share immutable regular/transition Transvoxel lookup tables across all solid workers; keep writable face scratch per workspace.
- [x] Instrument frame-scoped geometry managed allocations with `GC.GetAllocatedBytesForCurrentThread()` and add a repeated-path zero-allocation stress gate.
- [x] Preallocate render-pass solid/water draw staging to fixed arena draw capacities; camera motion cannot call `Array.Resize`.
- [ ] Remove/replace any remaining managed collection growth exposed by the zero-allocation gate.

### Visibility and clipmap residency

- [x] Replace `CollectVisible` scans of all known chunks with bounded ring/clipmap traversal.
- [x] Make the camera-centred clipmap window the render-residency admission boundary; retire out-of-window chunks incrementally.
- [x] Introduce fixed/toroidal `SurfaceChunkSlot` residency per LOD ring with slot generation IDs.
- [x] Recycle only newly exposed clipmap edges when the camera crosses a chunk boundary.
  - [x] Retire outgoing slabs incrementally rather than scanning lifetime residency.
  - [x] Rediscover/readmit newly exposed clipmap regions incrementally rather than rescanning the full window.
- [x] Cull solid visibility through the dense active set of toroidal slots rather than the full clipmap cube.
- [ ] Move final draw submission to batched/GPU-driven draw compaction only if CPU indirect-draw iteration remains material after profiling.

### Storage snapshot boundary

- [x] Never retain borrowed `RegionReadView` state across frame slices or jobs; copy into owned native snapshot memory first.
- [x] Add immutable/versioned or copy-on-write mixed-brick/page publication so snapshot copying can move off the frame thread.
  - [x] Add generation-stamped mixed-brick pins, COW cloning, and deferred slot retirement in `BrickPool`.
  - [x] Route every production mixed-brick mutation through `EnsureWritable` before rendering may pin payloads.
  - [x] Expose bounded Storage snapshot leases to rendering and retire them after jobs complete.
  - [x] Read mixed exact-snapshot payloads directly from pinned COW Storage arrays instead of copying 8^3 payloads into renderer lists.
  - [x] Exclude scoped borrowed writers from read pins and defer retired-slot reuse until both readers and writers exit.
  - [x] Move compact block-kind/ref snapshot traversal itself off the frame thread with versioned job-safe region metadata.
    - [x] Add generation/revision-pinned region block-ref leases and defer physical region eviction while jobs read metadata.
    - [x] Schedule exact block-kind/ref classification in Burst and validate every pinned region revision before accepting output.
- [x] Replace global-world version dependence with region/brick dependency revisions where appropriate.
  - [x] Use per-chunk source generations for render build invalidation/publication.
  - [x] Add per-region content revisions for optimistic rendering metadata jobs.

### LOD correctness

- [x] Preserve the step-8 correctness fix: conservative any-solid occupancy mips are not render density samples.
- [x] Keep the LOD regression test that exercises the castle in all four bands.
- [x] Replace the temporary exact step-8 Transvoxel fallback with a feature-preserving renderer-owned block HLOD representation.
  - [x] Compress each exact/COW 8^3 source block into eight spatial 4^3 material/occupancy subcells in Burst.
  - [x] Greedy-mesh the padded HLOD subcell grid in Burst with fixed-capacity native output and explicit overflow.
  - [x] Route step-8 extraction through `SurfaceBlockHlodSummaryJob -> SurfaceBlockHlodMeshJob` while retaining source-generation, pin/revision and arena-publication contracts.
- [x] Add a pixel/silhouette architectural regression so a grey blob cannot satisfy metric-only assertions.

### Water parity

- [x] Apply the same async snapshot/result/publication contract to water geometry.
  - [x] Bound water dirty selection, brick traversal, region invalidation, residency pruning, arena pressure, and GPU publication.
  - [x] Move water greedy mesh emission to owned immutable material snapshots + Burst jobs.
- [x] Give water bounded GPU publication and shared/pool-backed geometry ownership.

## Runtime acceptance gates

- [ ] Unity C# compile is clean on the current branch head.
- [ ] `GeometryPipelineArchitectureTests` and focused HLOD/Storage fixtures pass.
- [ ] LOD PlayMode regression passes across steps 1 / 2 / 4 / 8 with the new step-8 HLOD path.
- [ ] Continuous-camera streaming + voxel destruction stress passes.
- [ ] `LastFrameSolidUploadedBytes <= SolidUploadBudgetBytes` on every stressed frame.
- [ ] Stale results are discarded and old geometry remains visible until replacement publication.
  - Coverage: `AsyncGeometryStressTests.VisibleEditDuringRunningBuildRejectsStaleGeneration` injects a second edit while a solid geometry job is running and requires `RejectedStaleSolidBuilds` to advance without a visible hole.
- [ ] No unfinished geometry job is synchronously completed on the frame path.
  - Instrumentation: `GeometryFrameJobCompletionGuard` refuses premature completion and increments `FramePathBlockingCompletionViolations`; the stress gate requires zero violations.
- [ ] P99 main-thread geometry orchestration remains under the merge-gate threshold.
  - Instrumentation: stressed `SchedulerPrepareTiming.P99Ms` is asserted against the configured threshold.
- [ ] Zero steady-state managed geometry allocation after warmup.
  - Coverage: `WarmRepeatedClipmapTraversalAllocatesNoManagedGeometryMemory` repeats the same clipmap path twice, then requires `LastFrameManagedAllocationBytes == 0` on every measured frame.
- [ ] Arena pressure causes backlog/convergence delay rather than buffer growth, frame waits, or visible holes.
  - Unit/fixture coverage: `GeometryArenaPressureKeepsPublishedLeaseUntilReplacementConverges` proves a fixed arena keeps the old live lease until space is reclaimed.
  - Full-world coverage: `ArenaPressureDelaysConvergenceWithoutGrowingBuffersOrOpeningHoles` requires allocation failure, bounded pressure eviction, queued replacement, unchanged committed GPU bytes, zero frame-path waits, and eventual convergence.

## Current next slices

1. Complete the current self-hosted focused EditMode checkpoint and Metal LOD checkpoint for the explicit job-dispatch fix; repair any remaining compile, convergence, seam, capacity, or anti-blob failure immediately.
2. If LOD passes, run the existing async-geometry stress suite on the same runtime source state and close upload-budget, stale-generation, no-wait, P99, zero-allocation and arena-pressure gates from measured results.
3. Replace managed scheduler/cache structures only where the zero-allocation gate demonstrates post-warmup growth; do not rewrite already-stable collections speculatively.
4. Profile CPU indirect draw submission after active-slot visibility/fixed staging; move to GPU-driven draw compaction only if it is still a meaningful frame cost.
5. Once the runtime gates pass on one source SHA, mark acceptance complete and prepare the branch for merge.
