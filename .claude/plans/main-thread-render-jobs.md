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
- Validate on the real `VoxelShowcase` moving-player path, not only a synthetic microbenchmark.

## Current branch state

- [x] Canonicalize discovered surface bricks to the owning render chunk.
- [x] Partition each discovery publication batch by the exact solid-render shard that owns it.
- [x] Stop making every worker rescan every discovery record and reject non-owned chunks.
- [x] Reuse scheduler-owned shard buckets rather than allocating per publication.
- [x] Add regression coverage for positive/negative chunk ownership and exact-once shard routing.
- [x] Remove the accidental unrelated index-buffer type change from the optimization diff.
- [x] Run the focused routing regression in Unity (`32537445817`): Unity returned 0 and exactly one requested test executed successfully.
- [ ] Get a fully green push-triggered CI status. The focused test's workflow is red only because `actions/upload-artifact` hit the repository artifact-storage quota after the Unity test passed.
- [x] Remove duplicate ownership math in the router (`863a3b84`): each discovery record now derives its chunk once, then uses that same chunk for shard selection and canonical-brick construction instead of repeating three floor divisions.
- [ ] Re-run the focused routing regression against `863a3b84` (queued as `ci-test/main-thread-render-jobs-v2-routing2`).

## Measurement and validation

- [x] Attempt `ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` on the branch in run `32537620759`. The showcase bake passed, but Unity returned status 2 from the requested PlayMode test after 53 s.
- [ ] Recover the exact traversal assertion. The normal workflow writes it to `single.xml`/`single.log`, but artifact quota prevented those files from being uploaded. A temporary CI-only diagnostic rerun is queued to print the assertion directly into the Actions log.
- [ ] Record moving p95/p99/max frame time and verify zero `FramePathBlockingCompletionViolations` and continuous near/far coverage if the test reaches its measurement window.
- [ ] Compare against current `master` under the same test before attributing any performance change to shard routing. A master baseline request is queued on `ci-test/main-thread-render-jobs-v2-master-baseline`.
- [ ] Add a distinct low-overhead timing for the main-thread discovery-admission handoff if existing telemetry cannot isolate it from scheduler Prepare.
- [ ] Record discovered/routed record counts beside that timing so empty frames do not dilute the result.

### Existing performance evidence

The broader rendering investigation already measured a settled real-player run where scheduler `Prepare` averaged about 2.12 ms: visibility alone averaged about 1.92 ms, worker admission about 0.20 ms, and discovery/invalidation were effectively zero. That makes off-thread cache admission a conditional optimization, not the default next step. The moving-region measurement above must show a material transient cost before this branch expands into native/Burst staging.

The visibility path is already known to spend most of its time walking active chunk coordinates and consulting managed state (`_known`, desired versions, ready entries, empty versions). The frustum primitive itself was experimentally replaced earlier with no measurable improvement. If admission proves negligible, the next main-thread target should therefore be visibility-state lookup/selection rather than another culling-math rewrite.

## Next optimization gate

Do not jobify the managed cache blindly. `CpuTransvoxelChunkCache` still owns managed dictionaries, hash sets, queues, entry lifetimes, and clipmap-slot admission. After the measurement above:

- [ ] If shard routing makes discovery admission negligible, stop here and move to the measured visibility-state lookup hotspot.
- [ ] If admission remains material, eliminate redundant coordinate work first: consider handing each shard canonical chunk coordinates directly instead of canonical brick coordinates that `DiscoverSurfaceBricks` converts back to chunks.
- [ ] If a material residual remains after that, stage/deduplicate candidate chunk coordinates in native containers/Burst, then perform only the bounded managed-cache commit on the main thread.
- [ ] Keep the commit step allocation-free and bounded by an explicit per-frame publication budget.

## Acceptance

- [x] Focused ownership/routing behavior proven by an executed Unity test; full CI-green status remains infrastructure-blocked by artifact quota.
- [ ] Moving traversal behavior characterized on the real showcase.
- [ ] No new synchronous completion violations.
- [ ] No geometry holes or near/far fallback regression.
- [ ] Measured main-thread improvement versus `master`, or documented evidence that this path is no longer worth optimizing.
- [ ] Update `.claude/plans/rendering-garbled.md` if this work changes the broader renderer bottleneck conclusion.
