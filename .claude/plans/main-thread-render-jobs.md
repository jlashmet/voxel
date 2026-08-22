# Main-thread render admission reduction

**Branch:** `agent/main-thread-render-jobs-v2`
**Current base:** contains current `master` through `e30b5562`
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
- [x] Merge the nine post-PR #127 commits through PR #128 and fast-forward the feature branch to merged `master` commit `e30b5562ffc9347ea657c0055505e00b8c3dd135` before resuming work.
- [x] Preserve `master`'s single-test policy: `group: single-test-${{ github.ref }}`, `cancel-in-progress: true`, job timeout 5 minutes, Unity invocation ceiling 4 minutes.
- [x] Adopt exactly one reused `ci-test/agent/main-thread-render-jobs-v2` branch for targeted validation.
- [x] Restore renderer-specific CI behavior compatible with that policy: SmallVoxelShowcase bake exclusions, eight-worker traversal routing, and failure details.
- [x] Update source-contract tests for the current CI policy (`39f637192b3d2fd89a01cedda1f9268a0ba5ff09`).
- [x] Confirm feature branch is based on current merged master before the new visual-regression fixes.

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
- [x] Align the timing gate with the documented authority boundary: keep PlayMode for coverage/blocking plus percentile sanity (`07354a6b`), and move the isolated `<33.34 ms` single-frame acceptance plus walking p95/p99, missing/reappearance, lease-failure, and fallback-safety checks to the real-player traversal (`0550e402`). Guard that split in `ShowcaseTraversalFallbackContractTests` (`4c5a2da4`).
- [x] Run the focused EditMode source contract after the authoritative-player gate change. Run `32580810700` is green.
- [x] Diagnose final-gate attempt `32581038603` instead of merging through a failed production-player assertion. The eight-worker PlayMode traversal passed in 79 seconds, but the visible player failed the authoritative validator: worst walking-window p95 `19.39 ms`, max `47.91 ms`, and `SURFACE missingMax=7`; `leaseFail=0` and `reappeared=0` remained clean.
- [x] Compare the failed visible run against the earlier good `32579464263` run and identify a harness nondeterminism: `AutoWalk` inherits mutable pre-automation mouse yaw, so nominally identical launches take materially different routes and render different chunk loads. Do not weaken the performance/coverage thresholds to accommodate different paths.
- [x] Make command-line `AutoWalk` derive a deterministic clockwise tangent from the player/landmark world geometry, while still feeding ordinary forward movement through `CharacterMotor` and the normal streaming path. A focused source contract now guards this invariant.
- [ ] Re-run the corrected final full traversal request and require both the PlayMode correctness gate and authoritative real-player performance/coverage gate to pass with `ci/single-test=success`.
- [ ] Review final diff against `CLAUDE.md`, the rendering specs, and current `master`.

## Terrain fidelity follow-up

- [x] Confirm actual serialized `VoxelShowcase` near-terrain LOD configuration: LOD is enabled, but `m_DetailBandScale=0.6`, so the normal 96 m finest band is only 57.6 m before step-2/4/8 terrain begins.
- [x] Trace the authoritative moat/subtractive-terrain authoring path and compare it with `VoxelFarTerrain.SampleTerrainHeight`; determine exactly why the far height-field proxy can cover the moat. `CastleSiteAuthoring` lowers/empties the analytic `TerrainQuery` surface for the crag/cliff, river gorge, and optional moat; `FarTerrainHeightJob` samples only that untouched analytic terrain, while the old `FarFieldStructureStore`/`VoxelFarTerrain` path preserved only tall positive landmark tops.
- [x] Preserve fallback coverage during incomplete near-field convergence while preventing the far proxy from visibly filling authored subtractive terrain. `FarFieldStructureStore` now captures authoritative lowered terrain and `VoxelFarTerrain` applies it before near-footprint structure suppression.
- [ ] Evaluate finest-LOD presentation separately from fallback correctness against the repository's documented performance/device budgets; do not blindly expand 57.6 m to 96 m.
- [x] Add focused source regression coverage for the authored subtractive-terrain fallback invariant.
- [ ] Run focused Unity CI on the reused `ci-test/agent/main-thread-render-jobs-v2` branch and keep the job under five minutes.
- [ ] Run the corrected exact full `VoxelShowcase` PlayMode + real-player path; inspect screenshots for moat/far-proxy fidelity and require no coverage, fallback, lease, reappearance, blocking-completion, or water-admission regression.

## 2026-08-22 visual regression follow-up

Reported visible regressions after the renderer/main-thread work was integrated:

- castle and Kentridge walls can appear with whole rectangular sections missing;
- elevated/flying views can expose pale blue square gaps in terrain;
- near terrain kept the improved texture treatment but became nearly flat;
- natural dirt/grass boundaries form conspicuous closed rings / "crop circles".

Evidence and diagnosis:

- [x] Inspect real-player artifact `32581038603`. During movement the Kentridge/castle geometry is missing in chunk-sized rectangular sections rather than losing only back-facing triangles.
- [x] Correlate those screenshots with player diagnostics: movement raises `missingVisible` into the hundreds while `leaseFail=0` and `reappeared=0`, so the wall loss is unpublished newly visible chunks, not arena eviction or face culling.
- [x] Trace the scheduler configuration. Production used `SurfaceMaxConcurrentBuildsConverged=0`, and `SurfaceBuildConcurrencyHarness` explicitly forced `converged=0`; once the current frustum converged, all nearby off-screen surface building stopped. Turning, walking, or flying then exposed geometry before convergence workers could catch up. This also matches the historical elevated-view sky-square failure mode.
- [x] Restore one bounded steady-state prefetch build in production and in the traversal harness. Do not increase the visible-convergence ceiling or reduce coverage/LOD.
- [x] Trace near-terrain flattening to `bfe02ec4`: the styling change reduced the 51.2 m valley octave from amplitude 70 to 18 and removed every other valley octave.
- [x] Restore landscape-scale relief without reintroducing the old metre-scale corrugation: keep the 51.2 m octave at 70 and restore the 12.8 m octave at 24; continue omitting the former 3.2 m / 1.6 m layers.
- [x] Trace the "crop circles" to the generic low-surface material split at `TerrainQuery.BaseHeight`: generated ground below the cutoff was Dirt and above it Grass, so the boundary literally followed closed elevation contours.
- [x] Keep the newer stylized terrain presentation but make naturally generated valley surface Grass on both sides of the generic height split. Dirt remains subsurface and remains available to authored roads, fields, banks and excavations.
- [x] Add focused regression guards for non-zero converged prefetch, traversal-harness parity, broad-but-not-player-scale terrain relief, and continuous natural ground cover.
- [ ] Run the focused source/terrain Unity regressions on the reused CI branch.
- [ ] Re-bake the current `VoxelShowcase` startup world because the canonical terrain-height function changed; publish the exact generated binary before relying on the bake-free final traversal gate.
- [ ] Run the exact full PlayMode + real-player traversal and inspect elevated/walking screenshots. Require `missingVisible=0` during accepted movement, no pale blue square gaps, complete castle/Kentridge walls, and natural-looking terrain relief/material boundaries.
- [ ] Confirm the one-worker steady prefetch does not regress settled-frame performance or arena pressure.

## Acceptance

- [x] Ownership/routing behavior has focused regression coverage.
- [x] Unique-chunk/fanout regression executed successfully in Unity.
- [x] Real-player moving/streaming behavior characterized against master.
- [x] Routing/dedup main-thread improvement measured versus master.
- [x] Concurrency, GC, solid-publication, and `ScheduleBatchedJobs()` hypotheses rejected with direct measurements.
- [x] Identify water admission as the remaining ~20 ms transient owner.
- [x] Reduce it without reducing render distance/coverage or introducing synchronous job completion. `32575648213` reduces the measured water spike by about 92% while preserving the production SmallVoxel coverage signals.
- [x] No geometry holes or near/far fallback regression in the corrected full-scene production evidence (`32579464263`).
- [ ] Final corrected full traversal reaches movement and passes both correctness and authoritative performance acceptance with the new visual-regression fixes.
- [ ] Relevant CI status is green, or a concrete external CI blocker is documented with successful Unity/player evidence separated from the failing infrastructure step.
