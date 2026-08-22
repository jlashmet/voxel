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
- Use the real-player harness for performance conclusions. The generic single-test workflow forces `-job-worker-count 1`, which is useful for correctness but is not representative of this job-heavy renderer.

## Current branch state

- [x] Canonicalize discovered surface bricks to the owning render chunk.
- [x] Partition each discovery publication batch by the exact solid-render shard that owns it.
- [x] Stop making every worker rescan every discovery record and reject non-owned chunks.
- [x] Reuse scheduler-owned shard buckets rather than allocating per publication.
- [x] Add regression coverage for positive/negative chunk ownership and shard routing.
- [x] Remove the accidental unrelated index-buffer type change from the optimization diff.
- [x] Run the original focused routing regression in Unity (`32537445817`): Unity returned 0 and exactly one requested test executed successfully.
- [ ] Get a fully green push-triggered CI status. The original focused test's workflow is red only because `actions/upload-artifact` hit the repository artifact-storage quota after the Unity test passed.
- [x] Remove duplicate ownership math in the router (`863a3b84`): each discovery record derives its chunk once and reuses it for shard selection and canonicalization.
- [x] Collapse repeated surface-brick records to one admission per unique owning chunk. Discovery is chunk-granular; duplicates previously repeated worker chunk math, shard hashing, clipmap/slot lookup and managed HashSet probes without changing state.
- [x] Change the routing regression to prove that duplicate bricks in one chunk emit that chunk exactly once while preserving all distinct positive/negative chunks.
- [x] Return the unique routed-chunk count from `PartitionByOwningShard`; existing scheduler callers may ignore it, while later telemetry can record discovered-brick versus actual-admission fanout without another API change.
- [x] Add a representative flat-terrain fanout regression: a 512-brick step-1 publication collapses to 8 unique render-chunk admissions before managed cache work.
- [ ] Run the latest unique-chunk/fanout regression on the current feature tip; queued single-test requests are still draining through the one self-hosted runner.

## Measurement and validation

- [x] Run a diagnostic `ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` on the original v2 implementation (`32539453488`) with failure XML printed directly into Actions logs.
- [x] Identify the exact failure: the test never reached movement. Its 1,200-frame pre-traversal gate ended at `known=6323 resident=173 dirty=1915 visible=173 missing=534 jobs=12`, so no p95/p99/max frame result exists for that run.
- [x] Recognize that this is not a valid performance comparison: the generic single-test workflow forces one Unity job worker while the renderer intentionally allows 12 converging builds. The repo's dedicated showcase-performance workflow explicitly uses a real player because Editor/PlayMode timing is not authoritative.
- [ ] Compare the same one-worker convergence behavior with `master` only to classify the timeout as existing versus branch-specific; a master request is already queued.
- [ ] Measure the feature branch with `tools/showcase-player-capture.sh` in a real macOS player, using the same scene/motion window and hardware as the master baseline.
- [ ] Record real-player frame timing plus discovered-brick versus unique-chunk admission fanout before attributing a performance change to this routing work.
- [ ] Verify no coverage/fallback regression independently of the timing result.

### Existing performance evidence

The broader rendering investigation already measured a settled real-player run where scheduler `Prepare` averaged about 2.12 ms: visibility alone averaged about 1.92 ms, worker admission about 0.20 ms, and discovery/invalidation were effectively zero. That makes off-thread cache admission a conditional optimization, not the default next step. A moving/streaming real-player measurement must show a material transient cost before this branch expands into native/Burst staging.

The visibility path is already known to spend most of its time walking active chunk coordinates and consulting managed state (`_known`, desired versions, ready entries, empty versions). The frustum primitive itself was experimentally replaced earlier with no measurable improvement. The slot-grid invariant also proves that the scheduler's active-slot traversal only yields coordinates acquired into `_known`, and `_known.Remove` retires that slot, so the worker's leading `_known.Contains` is redundant specifically on that path. If admission proves negligible, that managed visibility-state lookup path is the next target.

## Next optimization gate

Do not jobify the managed cache blindly. `CpuTransvoxelChunkCache` still owns managed dictionaries, hash sets, queues, entry lifetimes, and clipmap-slot admission. After the real-player measurement:

- [ ] If unique shard routing makes discovery admission negligible, stop here and move to the measured visibility-state lookup hotspot.
- [ ] If admission remains material, move pure coordinate/shard fanout into the existing Burst surface-compaction pipeline; keep only unique managed-cache commit on the player thread.
- [ ] If a material residual remains after that, stage/deduplicate candidate chunk coordinates in native containers/Burst, then perform only the bounded managed-cache commit on the main thread.
- [ ] Keep the commit step allocation-free and bounded by an explicit per-frame publication budget.

## Acceptance

- [x] Original ownership/routing behavior proven by an executed Unity test; full CI-green status remains infrastructure-blocked by artifact quota.
- [ ] Latest unique-chunk/fanout regression executed successfully.
- [ ] Real-player moving/streaming behavior characterized against master.
- [ ] No new synchronous completion violations.
- [ ] No geometry holes or near/far fallback regression.
- [ ] Measured main-thread improvement versus `master`, or documented evidence that this path is no longer worth optimizing.
- [ ] Update `.claude/plans/rendering-garbled.md` if this work changes the broader renderer bottleneck conclusion.
