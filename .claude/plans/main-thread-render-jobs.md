# Main-thread render admission reduction

**Branch:** `agent/main-thread-render-jobs-v2`
**Current base:** merged current `master` through `b496f3a0bc600c9532222e10cd60a62a244ecfa3`
**Related plan:** `.claude/plans/rendering-garbled.md`

## Goal

Reduce player-frame CPU work when newly resident voxel regions become renderable without moving Unity/GPU publication onto unsafe threads, weakening snapshot/halo correctness, or hiding work by reducing visible coverage.

Authoritative surface classification/compaction already runs as Jobs/Burst work. Measure the remaining main-thread handoff directly, remove only proven redundant work, and move additional staging off-thread only when evidence identifies a safe and useful boundary.

## Constraints

- Preserve exact chunk ownership, including negative coordinates and chunk-border cases.
- Surface discovery is admission, not voxel mutation; it must not invalidate already-known geometry.
- Do not synchronously `Complete()` geometry jobs on the player frame.
- Do not reduce render distance, LOD coverage, or visible chunk count to improve timings.
- Unity/GPU resource mutation remains on the safe publication path; only pure/native staging is eligible for Burst work.
- Use real-player measurements for performance conclusions.
- Use built `SmallVoxelShowcase` as the iterative stutter gate. Reserve full `VoxelShowcase` traversal for final coverage/fallback acceptance after a candidate wins the small-player gate.
- Follow current repository CI policy: exactly one reused `ci-test/agent/main-thread-render-jobs-v2` request branch, latest request wins, and every single-test workflow must complete in under five minutes once started.

## Completed implementation

- [x] Canonicalize discovered surface bricks to the owning render chunk.
- [x] Partition each discovery publication batch by the exact solid-render shard that owns it.
- [x] Stop every worker rescanning every discovery record and rejecting non-owned chunks.
- [x] Reuse scheduler-owned shard buckets instead of allocating per publication.
- [x] Add positive/negative ownership and shard-routing regression coverage.
- [x] Collapse repeated surface-brick records to one admission per unique owning chunk.
- [x] Add representative flat-terrain fanout regression: 512 step-1 surface bricks collapse to 8 unique render-chunk admissions.
- [x] Keep the routing/dedup optimization after real-player comparison showed a measurable win.
- [x] Establish `SmallVoxelShowcase` moving profiles with production converging ceiling 12 and A/B ceiling 8.
- [x] Route the expensive traversal diagnostic through eight Unity job workers while ordinary focused tests remain at one.
- [x] Make the two exact SmallVoxelShowcase benchmark profiles skip the unrelated full-`VoxelShowcase` startup bake.
- [x] Add low-frequency main-thread phase, GC, job-count, coverage and arena diagnostics to player logs.
- [x] Add allocation-free direct solid-arena upload telemetry around `BeginWrite` / native copy / `EndWrite` for vertex, index and indirect-args buffers.
- [x] Retain the worst direct arena-upload frame per one-second diagnostic window, including wall time, call count, bytes and source frame.

## Evidence and rejected hypotheses

- Flat-terrain unique-chunk regression run `32540983107` executed the requested Unity test successfully; artifact upload failed afterward because repository artifact storage was full.
- Original one-worker traversal diagnostic `32539453488` could not reach movement (`known=6323 resident=173 dirty=1915 visible=173 missing=534 jobs=12`), so it was not a valid performance gate.
- Same-scene/path/hardware SmallVoxelShowcase comparison:
  - master run `32540389129`: final-20-second mean p50 `2.282 ms`, worst p50 `3.19 ms`, worst frame `37.99 ms`, `missingVisible=0`.
  - feature run `32543006178`: final-20-second mean p50 `1.790 ms`, worst p50 `2.30 ms`, worst frame `22.21 ms`, `missingVisible=0`.
  - Routing/dedup therefore improved mean p50 about 22%, worst p50 about 28%, and worst settled-tail frame about 42%.
- Residual moving transient remained: representative windows reached roughly `prepare=20.42 ms`, `admit=16.45 ms`, while discovery was about `0.10 ms` and visibility about `0.64 ms`.
- Per-worker `CpuTransvoxelChunkCache.Prepare()` subsections remained sub-millisecond while frames reached 20-50 ms; do not revisit worker extraction preparation without contradictory evidence.
- Build concurrency A/B (`32548632231` at 12 vs `32549914390` at 8) did not remove repeated ~20-22 ms frames; do not lower production concurrency as a stutter fix.
- GC/allocation pass `32552157183` showed collection-count deltas in both clean and hitch windows; collection counts are not causal evidence here.
- Marker-correlation run `32553044708` showed actual `GC.Collect` around only `0.38-0.85 ms`, ruling managed GC pause out for the ~20 ms transient.
- Custom `ProfilerRecorder` names for `Voxel.Surface.*` return zero in the standalone player, so renderer phase correlation must use direct scheduler/primitive telemetry.
- The same marker-correlation run caught a representative late hitch with approximately `prepare=19.36 ms`, `admit=15.88 ms`, `visible=0.39 ms`, `missingVisible=0`.
- Arena-pressure relief is ruled out for that event: `leaseFail=0` and the solid arena was only ~14% occupied.
- Three solid leases published in that frame, but the run's existing direct `UploadTiming` proves full per-publication work was small: `upload[max=0.964 ms]`. `TryPublishPending` times the entire `Entry.AdvanceUpload`, including arena acquisition/bookkeeping and `BeginWrite`/copy/`EndWrite`, not just the raw buffer write.
- The scheduler permits at most four solid upload workers per frame; therefore even the conservative upper bound from that run is under ~3.9 ms for all solid publications, and the representative three-publication frame is bounded under ~2.9 ms. Solid publication cannot account for `admit=15.88 ms`.
- The scheduler's aggregate solid worker/admission+publication timing also stayed small in the late windows (`worker[max]` about `1.294 ms` in the final printed window). This independently points to work after the solid phase.
- Raw arena writes are a strict subset of the already-bounded `Entry.AdvanceUpload`; the arena-write probe is confirmatory but is no longer required to rule solid publication out as the 16 ms source.
- Same-frame phase run `32574092199` identifies the remaining transient conclusively. Requested Unity test and real-player capture both succeeded; the workflow failed only afterward because `actions/upload-artifact` hit repository storage quota.
- Representative late hitch frames from that run are:
  - frame `35821`: `total=15.574 ms`, `solid=0.139`, `relief=0.000`, `water=15.435`, `schedule=0.000`.
  - frame `36674`: `total=15.598 ms`, `solid=0.142`, `relief=0.000`, `water=15.456`, `schedule=0.000`.
  - frame `45119`: `total=15.628 ms`, `solid=0.136`, `relief=0.000`, `water=15.491`, `schedule=0.000`.
  - frame `48221`: `total=15.631 ms`, `solid=0.137`, `relief=0.000`, `water=15.494`, `schedule=0.000`.
- Normal admission windows in the same run were about `0.12-0.16 ms`, with water at approximately zero. `JobHandle.ScheduleBatchedJobs()` stayed `0.000-0.001 ms`; it is ruled out.
- Hitch `RINGS` rows also show `discover=0.10 ms` immediately before `admit≈15.4-15.6 ms`, while `leaseFail=0`, pointing specifically at the discovery-to-water handoff rather than water arena pressure.

## 2026-08-22 master integration / CI compatibility

- [x] Merge current `master` into the feature branch, most recently through `b496f3a0bc600c9532222e10cd60a62a244ecfa3` in merge commit `5ca49069aa1c53f5ac378e113628bc6081ddfefb`.
- [x] Preserve `master`'s single-test policy: `group: single-test-${{ github.ref }}`, `cancel-in-progress: true`, job timeout 5 minutes, Unity invocation ceiling 4 minutes.
- [x] Adopt the current branch-discipline policy: exactly one reused `ci-test/agent/main-thread-render-jobs-v2` branch for every targeted validation iteration.
- [x] Restore only renderer-specific behavior compatible with that policy: SmallVoxelShowcase bake exclusions, eight-worker traversal routing, and failure details (`b74c3dc761a0dcda9d5b85749c4464255c040790`).
- [x] Update source-contract tests so they enforce the new CI policy instead of the superseded global-queue behavior (`39f637192b3d2fd89a01cedda1f9268a0ba5ff09`).
- [x] Confirm branch is ahead of current master and `behind_by=0`.
- [x] Treat the older arena-write request `81396a37c2eb7d13d664e47c640b30a7759c056d` (`arena-upload-20260822-0532`) as superseded. It remained queued/not started, and existing run `32553044708` already bounds full solid publication below the hitch.

## Current fix gate

The measured culprit is the water block. `VoxelSurfaceScheduler.Prepare` handed every newly published solid-surface discovery brick directly to `_water.InvalidateSurfaceBricks(storage, _discoveredSurfaceBricks)`. That method synchronously reloads/material-scans every brick to decide whether it contains water/cascade material before the separately budgeted water build starts. A surface-discovery publication can contain up to 512 bricks, so this unbudgeted scan can consume the full ~15.5 ms hitch even though `_water.Prepare(..., WaterBuildBudgetMs)` itself is time-bounded and water meshing is job-based.

The candidate fix keeps authoritative mutation invalidation immediate and amortizes only initial/streaming presentation discovery. A scheduler-owned `WaterSurfaceDiscoveryAdmission` deduplicates discovered bricks in a FIFO and drains at most 32 classifications per `Prepare`, continuing to drain on later frames even when no new discovery batch arrives.

- [x] Rule full solid publication out using existing `UploadTiming` from run `32553044708`; do not optimize arena allocation/copy code based on the three lease changes alone.
- [x] Add allocation-free current-frame timing for solid admission, arena-relief bookkeeping, the complete water block, and `JobHandle.ScheduleBatchedJobs()` (`ed54f644afecde44102368b792a0fd2b2d0ad818`).
- [x] Copy that snapshot through the read-only Composition boundary as primitives (`e3cc50f0cfe528bf6f8780a323a62a3244471a90`).
- [x] Retain the same frame's phase values whenever it is the worst admission frame in the one-second report window and print `total`, `solid`, `relief`, `water`, `schedule`, and residual (`d2e02fce45968d5bb548d4d0c92ab0e46c78c027`).
- [x] Add source-contract guards for the timing boundaries and same-frame sparse diagnostic (`6791f6daee630ded5535f9741020ca51a4990013`).
- [x] Verify the scheduler instrumentation diff before promotion: 45 additions, 0 deletions. Overall diagnostic diff remains limited to scheduler, Composition, harness, and source-contract test.
- [x] Run production-policy `SmallVoxelShowcaseMovingBuild12`; run `32574092199` proves water owns essentially the entire transient and `ScheduleBatchedJobs()` does not.
- [x] Preserve immediate `_changedWaterBricks` mutation invalidation while routing initial/streaming `_discoveredSurfaceBricks` through bounded water classification (`cfaf93c4fa7fb69a9492439e95da59c9f9d06167`).
- [x] Deduplicate pending discovery and drain a fixed 32 bricks per frame through `WaterSurfaceDiscoveryAdmission` (`82337f2118901115942b17cb178fa17628199030`); add a source contract that preserves the mutation/discovery distinction (`23a5f95404719bf17fa4e74a5a36b690faa07458`).
- [x] Verify the final scheduler wiring diff is surgical: one helper field added and one discovery call replaced; no unrelated scheduler rewrite.
- [ ] Re-run the exact SmallVoxelShowcase gate and require the ~15.5 ms water admission spikes to disappear without increasing `missingVisible`, lease failures, or visible reappearance.

## Validation after the first proven fix

- [ ] Re-run the exact same production-policy SmallVoxelShowcase path and require materially reduced tail spikes with equivalent coverage (`missingVisible=0`, no new lease failures/holes).
- [ ] Run the relevant focused Unity regression(s) under the under-five-minute single-test policy.
- [ ] Only after the candidate wins the fast gate, run corrected full `VoxelShowcase` traversal + real-player profile for near/far coverage/fallback acceptance.
- [ ] Review final diff against `CLAUDE.md`, the rendering specs, and current `master`.

## Acceptance

- [x] Ownership/routing behavior has focused regression coverage.
- [x] Unique-chunk/fanout regression executed successfully in Unity.
- [x] Real-player moving/streaming behavior characterized against master.
- [x] Routing/dedup main-thread improvement measured versus master.
- [x] Concurrency, GC, arena pressure, solid-publication, and `ScheduleBatchedJobs()` hypotheses rejected with direct measurements.
- [x] Identify water admission as the remaining ~20 ms transient owner.
- [ ] Reduce it without reducing render distance/coverage or introducing synchronous job completion.
- [ ] No geometry holes or near/far fallback regression.
- [ ] Final corrected full traversal reaches movement and passes coverage acceptance.
- [ ] Relevant CI status is green, or a concrete external CI blocker is documented with successful Unity/player evidence separated from the failing infrastructure step.
