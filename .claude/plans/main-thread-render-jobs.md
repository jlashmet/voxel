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
- Use the real-player harness for performance conclusions. The single-test workflow keeps one Unity job worker for ordinary correctness tests, but `ShowcaseTraversalPerformanceTests.*` now gets a bounded eight-worker pool so its convergence precondition is representative enough to execute. Real-player timing remains authoritative.

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

## Measurement and validation

- [x] Run a diagnostic `ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` on the original v2 implementation (`32539453488`) with failure XML printed directly into Actions logs.
- [x] Identify the exact failure: the test never reached movement. Its 1,200-frame pre-traversal gate ended at `known=6323 resident=173 dirty=1915 visible=173 missing=534 jobs=12`, so no p95/p99/max frame result exists for that run.
- [x] Identify the validation defect behind that run: the generic single-test path forced one Unity job worker while the renderer intentionally allows many converging builds. That setup was useful for ordinary correctness tests but not this traversal gate.
- [x] Correct the traversal assertion path to eight workers without changing the renderer's production worker policy.
- [x] Make the same targeted traversal request automatically run the existing standalone `VoxelShowcase` player/autowalk harness afterward; the player remains the authoritative performance measurement.
- [ ] Run the corrected traversal + real-player profile on the current renderer source. Request: `ci-test/main-thread-render-jobs-v2-latest` at `52e16329961f26bd6fc876ceb91c8f7c36f2d46d` (later feature commits are validation comments/contracts only). The request still has no Actions status, so do not treat it as queued or executed.
- [x] Compare the same real-player scene, motion window and hardware against `master` with isolated `SmallVoxelShowcase` A/B runs.
  - Master: run `32540389129`, 150 s player, autowalk after 60 s, 15 screenshots, success. Settled final-20-second window: mean p50 `2.282 ms`, worst p50 `3.19 ms`, worst frame `37.99 ms`, `missingVisible=0`.
  - Feature: run `32543006178`, same scene/window/hardware, 15 screenshots, success. Settled final-20-second window: mean p50 `1.790 ms`, worst p50 `2.30 ms`, worst frame `22.21 ms`, `missingVisible=0`.
  - Delta: mean p50 improved about 22%, worst p50 about 28%, and worst observed settled-tail frame about 42%. This is a real moving-player win rather than a steady-state-only microbenchmark.
- [x] Record the key residual from the A/B: the feature still has periodic worker-prepare spikes. One sample reached `prepare=20.42 ms`, with `admit=16.45 ms`, while discovery itself was only `0.10 ms` and visibility `0.64 ms`.
- [ ] Record discovered-brick versus unique-chunk admission fanout in real-player telemetry if another routing iteration is needed.
- [ ] Verify no coverage/fallback regression with the corrected traversal assertion independently of the successful `missingVisible=0` real-player samples.

### Existing performance evidence

The broader rendering investigation already measured a settled real-player run where scheduler `Prepare` averaged about 2.12 ms: visibility alone averaged about 1.92 ms, worker admission about 0.20 ms, and discovery/invalidation were effectively zero. That remains the steady-state picture.

The moving A/B changes the transient conclusion: discovery routing/dedup is worthwhile because it materially improves moving-player frame behavior. It does not finish the work. In the feature run, discovery is negligible even on a spike (`0.10 ms`), while `admit` still reaches `16.45 ms`. `admit` is the scheduler's timed loop over `CpuTransvoxelChunkCache.Prepare`, not the surface-discovery router itself. The next investigation therefore belongs inside worker `Prepare()` rather than another round of shard-fanout jobification.

The visibility path is still a separate known steady-state hotspot: active chunk coordinates consult managed state (`_known`, desired versions, ready entries, empty versions), and replacing the frustum primitive previously produced no measurable win. Keep that work separate from the transient admission spikes measured here.

## Next optimization gate

Do not jobify the managed cache blindly. The A/B proves the routing optimization is useful, but it also proves the remaining transient is elsewhere.

- [x] Keep the unique shard routing/dedup optimization; the real-player A/B measured a material improvement.
- [x] Stop treating discovery fanout as the dominant residual: feature spike discovery was `0.10 ms` while worker admission was `16.45 ms`.
- [ ] Split `CpuTransvoxelChunkCache.Prepare()` spike cost into its already-instrumented sections: rule sync, residency prune, capacity, build selection, snapshot/append/profile/transition work.
- [ ] Fix the first section proven to violate the scheduler's intended frame budget. Note that rule sync, clipmap/residency pruning and capacity enforcement currently execute before the worker computes its `deadline`; if one of those is the spike source, make it resumable/budget-aware rather than merely moving managed mutation to a job.
- [ ] If the residual instead comes from pure snapshot/coordinate processing, move only that pure/native portion to Burst/Jobs and leave managed dictionaries, queues, entry lifetimes and GPU publication on the main thread.
- [ ] Keep the commit/publication path allocation-free and bounded by an explicit per-frame budget.
- [ ] Re-run the exact same real-player A/B after the next change; do not infer success from editor timing.

## Acceptance

- [x] Original ownership/routing behavior proven by an executed Unity test; full CI-green status remains infrastructure-blocked by artifact quota.
- [x] Latest unique-chunk/fanout regression executed successfully in Unity.
- [ ] Corrected traversal assertion reaches movement without a coverage/fallback regression.
- [x] Real-player moving/streaming behavior characterized against master.
- [ ] No new synchronous completion violations.
- [ ] No geometry holes or near/far fallback regression.
- [x] Measured main-thread/frame-time improvement versus `master` in the isolated real-player A/B.
- [ ] Identify and reduce the remaining worker-`Prepare()` transient without regressing the measured A/B.
- [ ] Update `.claude/plans/rendering-garbled.md` if this work changes the broader renderer bottleneck conclusion.
