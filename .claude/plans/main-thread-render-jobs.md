# Main-thread render admission reduction

**Branch:** `agent/main-thread-render-jobs-v2`
**Started from:** `master` at `9422c90964b215b7bf9d1f075e83573e2ec9eaa5`
**Related plan:** `.claude/plans/rendering-garbled.md`, especially the measured renderer-performance work in section E/G

## Goal

Reduce player-frame CPU work that occurs when newly resident voxel regions become renderable, without moving Unity/GPU publication onto unsafe threads, weakening snapshot/halo correctness, or hiding work by reducing visible coverage.

The authoritative region surface classification and compaction are already Unity Jobs/Burst work. The remaining handoff publishes discovered surface bricks into the managed solid-render caches on the main thread. Measure that handoff independently, remove proven redundant work first, and only move additional staging off-thread if measurements show a material residual.

## Constraints

- Preserve exact chunk ownership, including negative coordinates and chunk-border cases.
- Surface discovery is admission, not voxel mutation; it must not invalidate already-known geometry.
- Do not synchronously `Complete()` geometry jobs on the player frame.
- Do not reduce render distance, LOD coverage, or visible chunk count to improve timings.
- Unity/GPU resource mutation remains on the safe publication path; only pure/native staging is eligible for Burst work.
- Use real-player measurement for performance conclusions. For iterative stutter work, prefer the built `SmallVoxelShowcase` player because it exercises the production renderer/job path without the several-minute full-showcase world bake. Reserve the full `VoxelShowcase` traversal/bake for final coverage acceptance after a candidate has already won the small-player A/B.

## Current branch state

- [x] Canonicalize discovered surface bricks to the owning render chunk.
- [x] Partition each discovery publication batch by the exact solid-render shard that owns it.
- [x] Stop making every worker rescan every discovery record and reject non-owned chunks.
- [x] Reuse scheduler-owned shard buckets rather than allocating per publication.
- [x] Add regression coverage for positive/negative chunk ownership and shard routing.
- [x] Remove the accidental unrelated index-buffer type change from the optimization diff.
- [x] Run the original focused routing regression in Unity (`32537445817`): Unity returned 0 and exactly one requested test executed successfully.
- [ ] Get a fully green push-triggered CI status. Focused Unity tests pass, but `actions/upload-artifact` still marks their workflows red because repository artifact storage is full.
- [x] Remove duplicate ownership math in the router (`863a3b84`): each discovery record derives its chunk once and reuses it for shard selection and canonicalization.
- [x] Collapse repeated surface-brick records to one admission per unique owning chunk. Discovery is chunk-granular; duplicates previously repeated worker chunk math, shard hashing, clipmap/slot lookup and managed HashSet probes without changing state.
- [x] Change the routing regression to prove that duplicate bricks in one chunk emit that chunk exactly once while preserving all distinct positive/negative chunks.
- [x] Return the unique routed-chunk count from `PartitionByOwningShard`; existing scheduler callers may ignore it, while later telemetry can record discovered-brick versus actual-admission fanout without another API change.
- [x] Add a representative flat-terrain fanout regression: a 512-brick step-1 publication collapses to 8 unique render-chunk admissions before managed cache work.
- [x] Run the latest flat-terrain unique-chunk regression (`32540983107`): Unity returned 0 and exactly one requested test executed; only artifact upload failed afterward because of quota.
- [x] Give `ShowcaseTraversalPerformanceTests.*` eight Unity job workers in `tests-single.yml`; all ordinary single-test validation stays on one worker.
- [x] Map the continuous traversal filter to a real `VoxelShowcase` player/autowalk profile in `tools/showcase-player-capture.sh`, and print the warm FPS tail directly into the job log so artifact quota cannot hide timing output.
- [x] Guard both the traversal worker selection and traversal-to-player mapping in `StationaryRenderBenchmarkTests` source-contract coverage.
- [x] Configure single-test Actions concurrency (`6a9aadd7`) so the currently running Unity job is never killed, but a newer request replaces any older pending request waiting behind it.
- [x] Add fast built-player `SmallVoxelShowcase` moving profiles (`e313c381`, guarded by `3eaf062c`) at converging ceilings 12 and 8: 90 s total, autowalk after 20 s, same FPS/prepare/job-pressure/coverage logging.
- [x] Make those exact small-player PlayMode profiles skip the heavyweight `VoxelShowcase` startup-world bake (`5b6a1c3f`).

## Measurement and validation

- [x] Run a diagnostic `ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` on the original v2 implementation (`32539453488`) with failure XML printed directly into Actions logs.
- [x] Identify the exact failure: the test never reached movement. Its 1,200-frame pre-traversal gate ended at `known=6323 resident=173 dirty=1915 visible=173 missing=534 jobs=12`, so no p95/p99/max frame result exists for that run.
- [x] Identify the validation defect behind that run: the generic single-test path forced one Unity job worker while the renderer intentionally allows many converging builds. That setup was useful for ordinary correctness tests but not this traversal gate.
- [x] Correct the traversal assertion path to eight workers without changing the renderer's production worker policy.
- [x] Make the same targeted traversal request automatically run the existing standalone `VoxelShowcase` player/autowalk harness afterward; the player remains the authoritative full-scene performance measurement.
- [x] Compare the same real-player scene, motion window and hardware against `master` with isolated `SmallVoxelShowcase` A/B runs.
  - Master: run `32540389129`, 150 s player, autowalk after 60 s, 15 screenshots, success. Settled final-20-second window: mean p50 `2.282 ms`, worst p50 `3.19 ms`, worst frame `37.99 ms`, `missingVisible=0`.
  - Feature: run `32543006178`, same scene/window/hardware, 15 screenshots, success. Settled final-20-second window: mean p50 `1.790 ms`, worst p50 `2.30 ms`, worst frame `22.21 ms`, `missingVisible=0`.
  - Delta: mean p50 improved about 22%, worst p50 about 28%, and worst observed settled-tail frame about 42%. This is a real moving-player win rather than a steady-state-only microbenchmark.
- [x] Record the key residual from the A/B: the feature still has periodic worker-prepare spikes. One sample reached `prepare=20.42 ms`, with `admit=16.45 ms`, while discovery itself was only `0.10 ms` and visibility `0.64 ms`.
- [x] Expose the renderer's existing worker-prepare timing windows through the Composition diagnostics boundary as primitive values only; do not add new stopwatch work to the renderer frame path.
- [x] Sample those timing windows sparsely in the standalone player and print `PREPARESECTIONS` directly into the Actions log so artifact-quota failures cannot hide the diagnostic.
- [x] Run the phase diagnostic in the real `VoxelShowcase` player (`32545603132`). The player completes 150 s and captures 14 screenshots; the workflow is red because the traversal assertion still fails and artifact upload quota is full.
- [x] Rule out the instrumented managed `CpuTransvoxelChunkCache.Prepare()` sections as the 20–50 ms late-run source. At t=130–150 s, worker `Prepare` max is only `0.181–0.364 ms`; capacity max is `0.000 ms`, selection max `0.011 ms`, residency max `0.044 ms`, while FPS windows still contain `26.99–50.35 ms` frames and one separate `956.74 ms` outlier.
- [x] Make the build-concurrency A/B isolate exactly one policy variable. `SurfaceBuildConcurrencyHarness` applies `SetVoxelBuildConcurrency(converging, 0)`, so the experiment changes only production convergence `12 -> 8` and preserves the production converged ceiling at zero.
- [x] Add direct active-job pressure to the sparse player diagnostic (`c167ccc9`, guarded by `ea3030c6`): `PREPARESECTIONS` reports `RunningSolidJobs` and `MissingVisibleSolidChunks` alongside the timing windows.
- [x] Preserve failed traversal assertions in the Actions log (`532f3ac8`, guarded by `f4fd0e7b`) so artifact quota cannot hide the assertion reason.
- [x] Correct an instrumentation interpretation before changing production policy: renderer-wide `LastAdmissionMs` starts before the worker loop but ends after solid publication, arena-pressure relief, water prepare/publication, and `JobHandle.ScheduleBatchedJobs()`. Therefore `admit=16 ms` with sub-ms worker `Prepare()` does not by itself prove job starvation; the build-concurrency A/B remains the next causal test, and a negative result points to the post-worker admission remainder.
- [ ] Run fast `SmallVoxelShowcaseMovingBuild12` real-player baseline on the current feature head.
- [ ] Run fast `SmallVoxelShowcaseMovingBuild8` candidate on the same feature head/hardware/path.
- [ ] Compare 12 vs 8 using moving p50/p95/p99/max, `PREPARESECTIONS jobs=... missing=...`, `RINGS prepare[...]`, and `SURFACE missingMax`; accept 8 only if tail latency improves without worse visible coverage.
- [ ] If the small-player A/B proves lower convergence concurrency, implement the production policy and re-run the same small-player gate.
- [ ] Only after a candidate wins the small-player gate, run the expensive corrected full `VoxelShowcase` traversal + real-player profile for final coverage/fallback acceptance.
- [ ] Record discovered-brick versus unique-chunk admission fanout in real-player telemetry if another routing iteration is needed.

### Existing performance evidence

The broader rendering investigation already measured a settled real-player run where scheduler `Prepare` averaged about 2.12 ms: visibility alone averaged about 1.92 ms, worker admission about 0.20 ms, and discovery/invalidation were effectively zero. That remains the steady-state picture.

The moving A/B changes the transient conclusion: discovery routing/dedup is worthwhile because it materially improves moving-player frame behavior. It does not finish the work. In the feature run, discovery is negligible even on a spike (`0.10 ms`), while renderer-wide `admit` still reaches `16.45 ms`.

The phase diagnostic rules out the measured worker sub-sections, but source inspection shows `admit` is broader than that worker loop. The next cheap causal experiment is therefore the real built `SmallVoxelShowcase` at convergence ceilings 12 and 8. Do not change the production ceiling until that A/B separates job pressure from post-worker admission work.

The visibility path is still a separate known steady-state hotspot: active chunk coordinates consult managed state (`_known`, desired versions, ready entries, empty versions), and replacing the frustum primitive previously produced no measurable win. Keep that work separate from the transient spikes measured here.

## Next optimization gate

Do not jobify the managed cache blindly. The routing optimization is already proven useful; now isolate the residual with the cheapest representative real-player test.

- [x] Keep the unique shard routing/dedup optimization; the real-player A/B measured a material improvement.
- [x] Add low-perturbation diagnostics for worker sections and job pressure.
- [x] Make pending CI requests replace stale pending requests without cancelling the active Unity run.
- [x] Establish `SmallVoxelShowcase` built-player motion as the fast iteration gate before full-world traversal.
- [ ] A/B the production converging-build ceiling (`12`) against `8` in `SmallVoxelShowcase` first.
- [ ] If build concurrency is proven causal, replace the fixed high converging ceiling with a policy that reserves CPU for the player/main thread while still prioritizing visible missing chunks.
- [ ] If concurrency does not explain the residual, split the post-worker admission remainder (solid publication, arena relief, water, batched-job scheduling) before changing renderer behavior.
- [ ] Keep the commit/publication path allocation-free and bounded by an explicit per-frame budget.
- [ ] Re-run the exact same small-player A/B after the next production change, then use full VoxelShowcase only for final acceptance.

## Acceptance

- [x] Original ownership/routing behavior proven by an executed Unity test; full CI-green status remains infrastructure-blocked by artifact quota.
- [x] Latest unique-chunk/fanout regression executed successfully in Unity.
- [x] Real-player moving/streaming behavior characterized against master.
- [ ] Fast SmallVoxelShowcase 12-vs-8 causal A/B completed on the same source/hardware/path.
- [ ] Corrected full traversal assertion reaches movement without a coverage/fallback regression after the winning candidate exists.
- [ ] No new synchronous completion violations.
- [ ] No geometry holes or near/far fallback regression.
- [x] Measured main-thread/frame-time improvement versus `master` in the isolated real-player A/B.
- [ ] Identify and reduce the remaining streaming transient without regressing the measured A/B.
- [ ] Update `.claude/plans/rendering-garbled.md` if this work changes the broader renderer bottleneck conclusion.
