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
- Three solid leases published in the same representative hitch frame, making solid publication the first remaining subsection to test.

## 2026-08-22 master integration / CI compatibility

- [x] Merge current `master` into the feature branch: merge commit `86f4fccf31db55bc5aa70cc32f847cbfa7a9ca0b`.
- [x] Preserve `master`'s new single-test policy: `group: single-test-${{ github.ref }}`, `cancel-in-progress: true`, job timeout 5 minutes, Unity invocation ceiling 4 minutes.
- [x] Restore only renderer-specific behavior compatible with that policy: SmallVoxelShowcase bake exclusions, eight-worker traversal routing, and failure details (`b74c3dc761a0dcda9d5b85749c4464255c040790`).
- [x] Update source-contract tests so they enforce the new CI policy instead of the superseded global-queue behavior (`39f637192b3d2fd89a01cedda1f9268a0ba5ff09`).
- [x] Confirm branch is ahead of current master and `behind_by=0`.
- [ ] Current authoritative arena-upload probe request: CI commit `81396a37c2eb7d13d664e47c640b30a7759c056d`, request id `arena-upload-20260822-0532`. As of the latest check it has no `ci/single-test` status yet, which under `AGENTS.md` means queued/not started.

## Current diagnostic gate

The remaining transient is broad scheduler admission, not discovery, visibility, per-worker preparation, convergence ceiling, GC pause, or arena-pressure relief. The scheduler order is now explicitly understood:

1. ring policy + solid worker admission/build progress;
2. solid pending-publication processing;
3. optional arena-pressure relief;
4. water invalidation/prepare/publication/relief;
5. `JobHandle.ScheduleBatchedJobs()`;
6. record `LastAdmissionMs`.

The direct solid-arena write probe measures the actual `BeginWrite` / copy / `EndWrite` portion inside step 2.

- [ ] Run production-policy `SmallVoxelShowcaseMovingBuild12` with direct arena-write telemetry.
- [ ] Correlate representative ~20 ms FPS/admission windows with `arenaUpload[ms=... calls=... bytes=... frame=...]` in `PREPARESECTIONS`.
- [ ] If arena writes account for most admission wall time, optimize publication without changing coverage or concurrency.
- [ ] If arena writes stay small, mark raw GPU writes ruled out and add only the minimum timing split needed to distinguish solid-publication bookkeeping from water work/publication and `ScheduleBatchedJobs()`.
- [ ] Update this plan with the measured result before changing production behavior.

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
- [x] Concurrency and GC hypotheses rejected with direct measurements.
- [ ] Identify the remaining ~20 ms transient with direct evidence.
- [ ] Reduce it without reducing render distance/coverage or introducing synchronous job completion.
- [ ] No geometry holes or near/far fallback regression.
- [ ] Final corrected full traversal reaches movement and passes coverage acceptance.
- [ ] Relevant CI status is green, or a concrete external CI blocker is documented with successful Unity/player evidence separated from the failing infrastructure step.
