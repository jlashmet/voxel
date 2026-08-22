# Main-thread render admission reduction

**Branch:** `agent/main-thread-render-jobs-v2`
**Current base:** merged current `master` through `96da31346004c5e59efb2ba34e2b29cf31bcce6b`
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
- Follow current repository CI policy: dedicated `ci-test/...` request branch, latest request wins per branch, and every single-test workflow must complete in under five minutes once started.

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

## 2026-08-22 master integration / CI compatibility

- [x] Merge current `master` into the feature branch: merge commit `86f4fccf31db55bc5aa70cc32f847cbfa7a9ca0b`.
- [x] Preserve `master`'s new single-test policy: `group: single-test-${{ github.ref }}`, `cancel-in-progress: true`, job timeout 5 minutes, Unity invocation ceiling 4 minutes.
- [x] Restore only renderer-specific behavior compatible with that policy: SmallVoxelShowcase bake exclusions, eight-worker traversal routing, and failure details (`b74c3dc761a0dcda9d5b85749c4464255c040790`).
- [x] Update source-contract tests so they enforce the new CI policy instead of the superseded global-queue behavior (`39f637192b3d2fd89a01cedda1f9268a0ba5ff09`).
- [x] Confirm branch is ahead of current master and `behind_by=0`.
- [x] Treat the older arena-write request `81396a37c2eb7d13d664e47c640b30a7759c056d` (`arena-upload-20260822-0532`) as superseded. It remained queued/not started, and existing run `32553044708` already bounds full solid publication below the hitch.

## Current diagnostic gate

The remaining transient is broad scheduler admission, but the pre-water solid portion is now bounded too low to explain it. In `VoxelSurfaceScheduler.Prepare`, the order after visibility is:

1. ring policy + solid worker admission/build progress;
2. solid pending-publication processing;
3. optional arena-pressure relief;
4. water invalidation/prepare/publication/relief;
5. `JobHandle.ScheduleBatchedJobs()`;
6. record `LastAdmissionMs`.

For the representative hitch, steps 1-2 are bounded small and step 3 did not run. The current probe distinguishes steps 4 and 5 without losing same-frame correlation.

- [x] Rule full solid publication out using existing `UploadTiming` from run `32553044708`; do not optimize arena allocation/copy code based on the three lease changes alone.
- [x] Add allocation-free current-frame timing for solid admission, arena-relief bookkeeping, the complete water block, and `JobHandle.ScheduleBatchedJobs()` (`ed54f644afecde44102368b792a0fd2b2d0ad818`).
- [x] Copy that snapshot through the read-only Composition boundary as primitives (`e3cc50f0cfe528bf6f8780a323a62a3244471a90`).
- [x] Retain the same frame's phase values whenever it is the worst admission frame in the one-second report window and print `total`, `solid`, `relief`, `water`, `schedule`, and residual (`d2e02fce45968d5bb548d4d0c92ab0e46c78c027`).
- [x] Add source-contract guards for the timing boundaries and same-frame sparse diagnostic (`6791f6daee630ded5535f9741020ca51a4990013`).
- [x] Verify the scheduler instrumentation diff before promotion: 45 additions, 0 deletions. Overall diagnostic diff remains limited to scheduler, Composition, harness, and source-contract test.
- [ ] Run the production-policy `SmallVoxelShowcaseMovingBuild12` gate with the same-frame admission split and classify the ~20 ms hitch as water, batched-job scheduling, or an explicitly measured residual.
- [ ] Record the CI run and classification here before changing production behavior.
- [ ] Fix only the first measured culprit. Do not change renderer concurrency, coverage, or publication policy speculatively.

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
- [x] Concurrency, GC, arena pressure, and solid-publication hypotheses rejected with direct measurements.
- [ ] Identify whether water or `ScheduleBatchedJobs()` owns the remaining ~20 ms transient.
- [ ] Reduce it without reducing render distance/coverage or introducing synchronous job completion.
- [ ] No geometry holes or near/far fallback regression.
- [ ] Final corrected full traversal reaches movement and passes coverage acceptance.
- [ ] Relevant CI status is green, or a concrete external CI blocker is documented with successful Unity/player evidence separated from the failing infrastructure step.
