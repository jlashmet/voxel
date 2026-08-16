# Async Geometry Pipeline Refactor

**Branch:** `refactor/async-geometry-pipeline`  
**Status:** In progress — runtime acceptance, allocation proof, and coarse-LOD follow-up  
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
- [x] Schedule transition-cell meshing instead of calling `TransitionMeshJob.Run()` inline.
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

### Workspace and allocation control

- [x] Cache the flattened worker array instead of allocating it when metrics are read.
- [x] Scale build workspace counts by LOD: 8 / 8 / 4 / 2 for source steps 1 / 2 / 4 / 8.
- [x] Disable formatted per-frame surface diagnostic strings by default; structured metrics remain always available.
- [x] Pool/reuse managed `Entry` objects so churn after residency eviction does not allocate.
- [x] Introduce persistent `SurfaceChunkSlot` identities with generation tokens; stale builds validate the slot before publication.
- [x] Split persistent surface chunk/slot state from reusable geometry build workspaces.
- [x] Share immutable regular/transition Transvoxel lookup tables across all solid workers; keep writable face scratch per workspace.
- [ ] Remove/replace remaining managed collections from steady-state scheduler/cache maintenance where profiling proves they grow after warmup.

### Visibility and clipmap residency

- [x] Replace `CollectVisible` scans of all known chunks with bounded ring/clipmap coordinate traversal.
- [x] Make the camera-centred clipmap window the render-residency admission boundary; retire out-of-window chunks incrementally.
- [x] Introduce fixed/toroidal `SurfaceChunkSlot` residency per LOD ring with slot generation IDs.
- [x] Recycle only newly exposed clipmap edges when the camera crosses a chunk boundary.
  - [x] Retire outgoing slabs incrementally rather than scanning lifetime residency.
  - [x] Rediscover/readmit newly exposed clipmap regions incrementally rather than rescanning the full window.
- [ ] Move visibility/culling to batched/GPU-driven draw compaction after slot ownership is stable.

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
- [ ] Replace the temporary exact step-8 fallback with a feature-preserving render LOD representation (surface-aware/SDF/min-max/HLOD).
- [x] Add a pixel/silhouette architectural regression so a grey blob cannot satisfy metric-only assertions.

### Water parity

- [x] Apply the same async snapshot/result/publication contract to water geometry.
  - [x] Bound water dirty selection, brick traversal, region invalidation, residency pruning, arena pressure, and GPU publication.
  - [x] Move water greedy mesh emission to owned immutable material snapshots + Burst jobs.
- [x] Give water bounded GPU publication and shared/pool-backed geometry ownership.

## Runtime acceptance gates

- [ ] Unity C# compile is clean on the branch.
- [ ] `GeometryPipelineArchitectureTests` pass.
- [ ] LOD PlayMode regression passes across steps 1 / 2 / 4 / 8.
- [ ] Add/run continuous-camera streaming stress coverage.
- [ ] Add/run continuous voxel edit/destruction stress coverage.
- [ ] Assert `LastFrameSolidUploadedBytes <= SolidUploadBudgetBytes` on every stressed frame.
- [ ] Assert stale results are discarded and old geometry remains visible until replacement publication.
  - Coverage: `AsyncGeometryStressTests.VisibleEditDuringRunningBuildRejectsStaleGeneration` injects a second edit while a solid geometry job is running and requires `RejectedStaleSolidBuilds` to advance without a visible hole.
- [ ] Assert no unfinished geometry job is synchronously completed on the frame path.
  - Instrumentation: `GeometryFrameJobCompletionGuard` refuses premature completion and increments `FramePathBlockingCompletionViolations`; the stress gate requires zero violations.
- [ ] Measure P99 main-thread geometry orchestration against the target budget.
  - Instrumentation: stressed `SchedulerPrepareTiming.P99Ms` is asserted against the merge-gate threshold.
- [ ] Verify zero steady-state managed allocation after warmup.
- [ ] Verify arena pressure causes backlog/convergence delay rather than frame spikes or visible holes.
  - Coverage: `AsyncGeometryStressTests.GeometryArenaPressureKeepsPublishedLeaseUntilReplacementConverges` fills a tiny fixed arena, proves the old lease stays live while replacement staging is blocked, then proves convergence after one unrelated lease is reclaimed.

## Current next slices

1. Complete the current self-hosted EditMode + Metal PlayMode checkpoint and repair any compile/runtime failures before marking runtime gates complete.
2. Add a warm steady-state allocation gate and remove any scheduler/cache managed collection growth it exposes.
3. Add an arena-pressure runtime gate that proves backlog/convergence delay without visible holes or frame spikes.
4. Replace the exact step-8 fallback with a feature-preserving coarse render representation.
5. Move visibility/culling toward batched/GPU-driven draw compaction now that per-ring slot ownership is fixed.
