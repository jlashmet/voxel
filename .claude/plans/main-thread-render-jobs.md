# Main-thread render admission reduction

**Branch:** `agent/main-thread-render-jobs-v2`
**Current base:** merged current `master` through `b496f3a0bc600c9532222e10cd60a62a244ecfa3`
**Related plan:** `.claude/plans/rendering-garbled.md`

## Goal

Reduce player-frame CPU work when newly resident voxel regions become renderable without moving Unity/GPU publication onto unsafe threads, weakening snapshot/halo correctness, or hiding work by reducing visible coverage.

Authoritative surface classification/compaction already runs as Jobs/Burst work. Measure remaining main-thread handoff directly, remove only proven redundant work, and move additional staging off-thread only when evidence identifies a safe and useful boundary.

## Constraints

- Preserve exact chunk ownership, including negative coordinates and chunk-border cases.
- Surface discovery is admission, not voxel mutation; it must not invalidate already-known geometry.
- Do not synchronously `Complete()` geometry jobs on the player frame.
- Do not reduce render distance, LOD coverage, or visible chunk count to improve timings.
- Unity/GPU resource mutation remains on the safe publication path; only pure/native staging is eligible for Burst work.
- Use real-player measurements for performance conclusions.
- Use built `SmallVoxelShowcase` as the iterative stutter gate. Reserve full `VoxelShowcase` traversal for final coverage/fallback acceptance after a candidate wins the small-player gate.
- Follow current repository CI policy: exactly one reused `ci-test/agent/main-thread-render-jobs-v2` request branch, latest request wins, and every single-test workflow must complete in under five minutes once started.
- Full-scene acceptance must validate the current baked `VoxelShowcase` world. Do not substitute a stale checked-in startup-world blob merely to shorten CI.

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
- [x] Make only the two exact `SmallVoxelShowcase` benchmark profiles skip the unrelated full-`VoxelShowcase` startup bake.
- [x] Add low-frequency main-thread phase, GC, job-count, coverage and arena diagnostics to player logs.
- [x] Add allocation-free direct solid-arena upload telemetry around `BeginWrite` / native copy / `EndWrite` for vertex, index and indirect-args buffers.
- [x] Retain the worst direct arena-upload frame per one-second diagnostic window, including wall time, call count, bytes and source frame.
- [x] Preserve immediate `_changedWaterBricks` mutation invalidation while routing only initial/streaming `_discoveredSurfaceBricks` through bounded water classification.
- [x] Deduplicate pending water discovery and drain a fixed 32 classifications per frame through `WaterSurfaceDiscoveryAdmission`.

## Performance evidence and rejected hypotheses

- Flat-terrain unique-chunk regression run `32540983107` executed successfully in Unity.
- Same-scene/path/hardware `SmallVoxelShowcase` comparison:
  - master `32540389129`: final-20-second mean p50 `2.282 ms`, worst p50 `3.19 ms`, worst frame `37.99 ms`, `missingVisible=0`.
  - feature `32543006178`: final-20-second mean p50 `1.790 ms`, worst p50 `2.30 ms`, worst frame `22.21 ms`, `missingVisible=0`.
  - Routing/dedup improved mean p50 about 22%, worst p50 about 28%, and worst settled-tail frame about 42%.
- Build concurrency A/B (`32548632231` at 12 vs `32549914390` at 8) did not remove the repeated ~20-22 ms frames; do not lower production concurrency as a stutter fix.
- GC/allocation runs (`32552157183`, `32553044708`) rule managed GC pause out for the ~20 ms transient.
- Solid publication is ruled out: direct full-publication timing in `32553044708` stayed below the admission hitch, and arena pressure was absent (`leaseFail=0`, arena ~14% occupied).
- Same-frame phase run `32574092199` identifies the remaining transient conclusively. Representative late frames put essentially all of the ~15.5 ms admission spike in the water block while `ScheduleBatchedJobs()` stayed ~0 ms.
- The proven cause was synchronous water-material classification of every newly discovered surface brick before the already-budgeted water build.
- Candidate fix is bounded, deduplicated discovery classification while authoritative mutation invalidation stays immediate.
- Fast acceptance run `32575648213` passed the requested Unity test and 90-second real-player capture. Worst water admission fell from `15.494 ms` pre-fix to `1.226 ms`; worst total admission was `1.687 ms`. `leaseFail=0`, `reappeared=0`, and coverage remained equivalent.

## 2026-08-22 master integration / CI compatibility

- [x] Merge current `master` into the feature branch, most recently through `b496f3a0bc600c9532222e10cd60a62a244ecfa3` in merge commit `5ca49069aa1c53f5ac378e113628bc6081ddfefb`.
- [x] Preserve `master`'s single-test policy: `group: single-test-${{ github.ref }}`, `cancel-in-progress: true`, job timeout 5 minutes, Unity invocation ceiling 4 minutes.
- [x] Adopt exactly one reused `ci-test/agent/main-thread-render-jobs-v2` branch for targeted validation.
- [x] Restore renderer-specific CI behavior compatible with that policy: SmallVoxelShowcase bake exclusions, eight-worker traversal routing, and failure details.
- [x] Update source-contract tests for the current CI policy (`39f637192b3d2fd89a01cedda1f9268a0ba5ff09`).
- [x] Confirm feature branch is ahead of current master and `behind_by=0` at the integration gate.

## Validation after the first proven fix

- [x] Re-run the exact production-policy `SmallVoxelShowcase` path and require materially reduced tail spikes with equivalent coverage. Proven by `32575648213`.
- [x] Run the relevant focused Unity regression under the under-five-minute single-test policy. `SmallVoxelShowcaseMovingBuild12` executed successfully in 65 seconds in `32575648213`.
- [x] Diagnose first full `VoxelShowcase` acceptance attempt `32575925066` before changing renderer code. Eight-worker PlayMode expired at the 1200-frame *full near-convergence* precondition with `missing=40`; the accompanying freshly baked 150-second standalone player did reach movement.
- [x] Validate near/far fallback in the freshly baked standalone run `32575925066`: every sampled incomplete-near diagnostic kept `HoleRadiusMetres=0`; settled/moving tail had `missingVisible=0`, `leaseFail=0`, `reappeared=0`. Walking screenshots show continuous ground coverage.
- [x] Test the hypothesis that extending the PlayMode precondition to 1800 frames would make full near convergence a viable traversal prerequisite. **Rejected by `32577366383`**: it still expired before movement at `missing=30` after 1800 frames. The traversal gate must test fallback-safe movement rather than require the Editor path to reach zero missing geometry first.
- [x] Test the hypothesis that the full traversal's ~198-second startup bake was redundant. **Rejected by direct A/B.** Fresh-bake run `32575925066` stayed at `missingVisible=0`, `leaseFail=0` during movement; skip-bake run `32577366383` accumulated `leaseFail>3000` with persistent `missingVisible≈50-80` after movement.
- [x] Prove why the skip-bake path differs: the fresh current bake reports `199 regions, 10.8 MiB, seed 0x5EED1234`; the checked-in `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` is ~37.35 MB. The checked-in startup world is stale and cannot be used as an equivalent cached bake.
- [x] Restore the full traversal PlayMode precondition to a **fallback-safe visibility gate** (`9e69d1e7`): require visible near geometry and require the far-field hole to stay closed whenever near coverage is incomplete, then begin movement. Do not wait for `MissingVisibleSolidChunks==0` before exercising traversal.
- [x] Revert the invalid 1800-frame contract and exact full-traversal bake exclusion/source-contract assertions introduced by `8fe5c939`, `412c917c`, and `b5661592` (`22a2b48b`).
- [x] Add a focused EditMode source contract for the fallback-safe traversal gate (`62b6d853`); run `32578234246` is green.
- [x] Add a one-shot PlayMode bake-artifact bridge (`b62d7156`) that, after the normal fresh bake, copies the generated startup-world bytes into `Artifacts/SingleTest` and records their SHA-256. Remove it after the refreshed binary is committed.
- [x] Capture the deterministic current full-showcase startup world in under-five-minute CI. Run `32578386963` completed in about 3m40s: the bake took 199s, the one-shot PlayMode capture test took 11s, and the artifact contains `ShowcaseWorld.bytes` at `11,291,590` bytes (10.77 MiB), SHA-256 `0a76b072d6634bcc5e833827efb51760c861fbe388d8bb2a5cbf62d9ef1956c4`.
- [x] Publish that generated binary onto the existing CI branch, attach its exact Git blob `123558b8775a3d7b67c26c6e2bf6dc4e66fffda8` to the feature branch, and remove the temporary bake-artifact bridge (`55879a8e`). No binary reinterpretation occurred.
- [x] Restore a bake-free exact full-traversal request now that the checked-in startup world is current (`d4b1fb94`), and guard the fallback-safe + bake-free contract without reintroducing the rejected 1800-frame assumption (`c5e51fdc`).
- [x] Re-run the corrected full `VoxelShowcase` PlayMode traversal for correctness in `32579464263`. It reached and completed all 420 movement frames, crossed the required streaming boundaries, kept synchronous-completion violations at zero, and never opened the far-field hole while near coverage was incomplete. Its timing distribution passed p95 (`17.91 ms < 18`) and p99 (`22.04 ms < 25`) but one Editor-only frame hit `46.75 ms`, so the current PlayMode single-frame timing assertion failed after correctness had already passed.
- [x] Re-run the 150-second visible standalone traversal from the same refreshed checked-in world in `32579464263`. During the walking window, worst FPSLOG frame was `28.11 ms` (<33.34 ms); `SURFACE` stayed `missingMax=0`, `reappeared=0`; every sampled `RINGS` line kept `leaseFail=0`; every `FAR ... coverage=False` sample kept `hole=0`; 14 screenshots were captured and the walking frames show continuous ground with no exposed world/sky hole.
- [ ] Align the timing gate with the documented authority boundary: keep PlayMode for coverage/blocking plus percentile sanity, but move the isolated <33.34 ms single-frame acceptance to the real-player traversal. Add real-player assertions for walking p95/p99/max, post-convergence missing/reappearance, lease failures, and `coverage=False => hole=0` so production evidence can fail CI directly.
- [ ] Re-run the corrected final full traversal request and require both the PlayMode correctness gate and authoritative real-player performance/coverage gate to pass with `ci/single-test=success`.
- [ ] Review final diff against `CLAUDE.md`, the rendering specs, and current `master`.

## Acceptance

- [x] Ownership/routing behavior has focused regression coverage.
- [x] Unique-chunk/fanout regression executed successfully in Unity.
- [x] Real-player moving/streaming behavior characterized against master.
- [x] Routing/dedup main-thread improvement measured versus master.
- [x] Concurrency, GC, solid-publication, and `ScheduleBatchedJobs()` hypotheses rejected with direct measurements.
- [x] Identify water admission as the remaining ~20 ms transient owner.
- [x] Reduce it without reducing render distance/coverage or introducing synchronous job completion. `32575648213` reduces the measured water spike by about 92% while preserving the production SmallVoxel coverage signals.
- [x] No geometry holes or near/far fallback regression in the corrected full-scene production evidence (`32579464263`).
- [ ] Final corrected full traversal reaches movement and passes both correctness and authoritative performance acceptance.
- [ ] Relevant CI status is green, or a concrete external CI blocker is documented with successful Unity/player evidence separated from the failing infrastructure step.
