# Main-thread render admission reduction

**Branch:** `agent/main-thread-render-jobs-v2`
**Started from:** `master` at `9422c90964b215b7bf9d1f075e83573e2ec9eaa5`
**Related plan:** `.claude/plans/rendering-garbled.md`, especially section G

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
- [ ] Focused EditMode routing regression is green in push-triggered CI.

## Measurement and validation

- [ ] Run `ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` on the branch. This crosses multiple streamed region boundaries and exercises discovery/publication while the player moves.
- [ ] Record moving p95/p99/max frame time and verify zero `FramePathBlockingCompletionViolations` and continuous near/far coverage.
- [ ] Compare against current `master` under the same test before attributing any performance change to shard routing.
- [ ] Add a distinct low-overhead timing for the main-thread discovery-admission handoff if existing telemetry cannot isolate it from scheduler Prepare.
- [ ] Record discovered/routed record counts beside that timing so empty frames do not dilute the result.

## Next optimization gate

Do not jobify the managed cache blindly. `CpuTransvoxelChunkCache` still owns managed dictionaries, hash sets, queues, entry lifetimes, and clipmap-slot admission. After the measurement above:

- [ ] If shard routing makes discovery admission negligible, stop here and move to the next measured main-thread hotspot.
- [ ] If admission remains material, eliminate redundant coordinate work first: consider handing each shard canonical chunk coordinates directly instead of canonical brick coordinates that `DiscoverSurfaceBricks` converts back to chunks.
- [ ] If a material residual remains after that, stage/deduplicate candidate chunk coordinates in native containers/Burst, then perform only the bounded managed-cache commit on the main thread.
- [ ] Keep the commit step allocation-free and bounded by an explicit per-frame publication budget.

## Acceptance

- [ ] Focused ownership/routing test green.
- [ ] Moving traversal gate green on the real showcase.
- [ ] No new synchronous completion violations.
- [ ] No geometry holes or near/far fallback regression.
- [ ] Measured main-thread improvement versus `master`, or documented evidence that this path is no longer worth optimizing.
- [ ] Update `.claude/plans/rendering-garbled.md` section G with the measured result if it changes the broader renderer bottleneck conclusion.
