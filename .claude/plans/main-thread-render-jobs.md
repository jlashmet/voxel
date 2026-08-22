# Main-thread render admission reduction

**Branch:** `agent/main-thread-render-jobs-v2`
**Started from:** `master` at `9422c90964b215b7bf9d1f075e83573e2ec9eaa5`
**Related plan:** `.claude/plans/rendering-garbled.md`, especially the measured renderer-performance work in section E/G

## Goal

Reduce player-frame CPU work that occurs when newly resident voxel regions become renderable, without moving Unity/GPU publication onto unsafe threads, weakening snapshot/halo correctness, or hiding work by reducing visible coverage.

The authoritative region surface classification and compaction are already Unity Jobs/Burst work. Measure the remaining main-thread handoff and streaming transients directly, remove proven redundant work first, and only move additional staging off-thread when measurements identify the real bottleneck.

## Constraints

- Preserve exact chunk ownership, including negative coordinates and chunk-border cases.
- Surface discovery is admission, not voxel mutation; it must not invalidate already-known geometry.
- Do not synchronously `Complete()` geometry jobs on the player frame.
- Do not reduce render distance, LOD coverage, or visible chunk count to improve timings.
- Unity/GPU resource mutation remains on the safe publication path; only pure/native staging is eligible for Burst work.
- Use real-player measurement for performance conclusions.
- For iterative stutter work, prefer the built `SmallVoxelShowcase` player. It exercises the production renderer/job path without the heavyweight full-showcase bake. Reserve full `VoxelShowcase` traversal for final coverage/fallback acceptance after a candidate has already won the small-player gate.

## Current branch state

- [x] Canonicalize discovered surface bricks to the owning render chunk.
- [x] Partition each discovery publication batch by the exact solid-render shard that owns it.
- [x] Stop making every worker rescan every discovery record and reject non-owned chunks.
- [x] Reuse scheduler-owned shard buckets rather than allocating per publication.
- [x] Add regression coverage for positive/negative chunk ownership and shard routing.
- [x] Remove duplicate ownership math in the router (`863a3b84`).
- [x] Collapse repeated surface-brick records to one admission per unique owning chunk.
- [x] Return the unique routed-chunk count from `PartitionByOwningShard`.
- [x] Add a representative flat-terrain fanout regression: 512 step-1 surface bricks collapse to 8 unique render-chunk admissions.
- [x] Run the latest flat-terrain unique-chunk regression (`32540983107`): Unity returned 0 and exactly one requested test executed; only artifact upload failed afterward because of quota.
- [x] Route the traversal performance test through eight Unity job workers while leaving ordinary focused tests at one worker.
- [x] Map the traversal filter to the real-player autowalk harness and print performance telemetry directly into Actions logs.
- [x] Configure single-test Actions concurrency (`6a9aadd7`) so an active Unity run finishes, but a newer request replaces an older pending request.
- [x] Add fast built-player `SmallVoxelShowcase` moving profiles (`e313c381`, guarded by `3eaf062c`) at converging ceilings 12 and 8: 90 s total, autowalk after 20 s, same telemetry.
- [x] Make those exact small-player PlayMode profiles skip the heavyweight `VoxelShowcase` startup-world bake (`5b6a1c3f`).
- [ ] Get a fully green push-triggered CI status. Unity tests/player captures pass, but `actions/upload-artifact` still marks workflows red because repository artifact storage is full.

## Measurement and validation history

- [x] Original v2 traversal diagnostic (`32539453488`) proved the one-worker PlayMode setup could not reach movement: `known=6323 resident=173 dirty=1915 visible=173 missing=534 jobs=12` at the end of the convergence gate.
- [x] Compare the same real-player scene/path/hardware against `master` with isolated `SmallVoxelShowcase` runs.
  - Master run `32540389129`: final-20-second mean p50 `2.282 ms`, worst p50 `3.19 ms`, worst frame `37.99 ms`, `missingVisible=0`.
  - Feature run `32543006178`: final-20-second mean p50 `1.790 ms`, worst p50 `2.30 ms`, worst frame `22.21 ms`, `missingVisible=0`.
  - Routing/dedup therefore remains a measured win: mean p50 improved about 22%, worst p50 about 28%, and worst observed settled-tail frame about 42%.
- [x] Record the remaining transient: feature run still reached `prepare=20.42 ms`, `admit=16.45 ms`, with discovery only `0.10 ms` and visibility `0.64 ms`.
- [x] Expose existing worker-prepare timing windows through Composition as primitive diagnostics; do not add new stopwatch work inside the worker pipeline.
- [x] Print sparse `PREPARESECTIONS` lines directly into Actions logs so artifact-quota failures cannot hide them.
- [x] Run the phase diagnostic in the real player (`32545603132`).
- [x] Rule out the instrumented `CpuTransvoxelChunkCache.Prepare()` subsections as the source of late 20–50 ms frames: worker `Prepare` remained sub-millisecond while frame maxima reached 25–50 ms.
- [x] Add live `RunningSolidJobs` and `MissingVisibleSolidChunks` to the sparse player diagnostic (`c167ccc9`, guarded by `ea3030c6`).
- [x] Correct the interpretation of scheduler `LastAdmissionMs`: it begins before worker admission but ends only after solid publication, arena-pressure relief, water prepare/publication, and `JobHandle.ScheduleBatchedJobs()`. Therefore an `admit` spike is broader than `CpuTransvoxelChunkCache.Prepare()`.
- [x] Make the convergence-concurrency A/B isolate one variable: `SurfaceBuildConcurrencyHarness` applies `SetVoxelBuildConcurrency(converging, 0)`, preserving the production converged ceiling of zero.
- [x] Run fast `SmallVoxelShowcaseMovingBuild12` baseline (`32548632231`). The PlayMode test and 90-second player both succeeded; workflow red only from artifact quota. Late motion still had repeated ~20–21 ms frames. Coverage remained healthy (`leaseFail=0`, normally `missingVisible=0`).
- [x] Run fast `SmallVoxelShowcaseMovingBuild8` candidate (`32549914390`) on the same path/hardware. The PlayMode test and player both succeeded; workflow red only from artifact quota.
- [x] Compare 12 vs 8 and reject lower convergence concurrency as the fix. Build-8 still produced repeated ~20–22 ms frames and renderer-wide spikes including `prepare=17.78 ms / admit=14.40 ms` and `prepare=19.23 ms / admit=15.60 ms`, both with `missingVisible=0`. Lowering the ceiling did not remove the transient and must not be promoted to production.
- [x] Run the first GC/allocation correlation pass (`32552157183`). The requested PlayMode source-contract test and 90-second real player both succeeded; the workflow failed only when artifact upload hit the existing quota. Late movement still showed repeated ~20 ms frames (`20.59`, `20.91`, `19.82`, `20.31`, `20.51 ms`).
- [x] Collection-count deltas do not discriminate those hitches. The player reports roughly `+9..+12` collections in every one-second window for all three generations, including both clean and ~20 ms windows, while renderer frame-path managed allocation remained `0`. A collection-count increment by itself is therefore not causal evidence on this Unity incremental-GC configuration.
- [x] Add per-frame ProfilerRecorder maxima (`4e879ca0`, guarded by `4797c41a`) for `Voxel.Surface.SchedulerPrepare`, broad `Voxel.Surface.WorkerAdmission`, aggregate worker prepare, solid upload, and `GC.Collect`. Unlike the old once-per-second RINGS sample, these maxima retain a transient that occurs anywhere in the FPS window.

## Current diagnostic plan

The concurrency A/B was negative. Do not change production concurrency. The next run must distinguish two remaining classes of cause before changing renderer behavior:

1. **Managed allocation / GC pause**
   - [x] Add low-perturbation GC diagnostics to the fast player measurement: per-window `GC.CollectionCount` deltas and managed allocation volume (`a93a1a62`, guarded by `0e8236bd`).
   - [x] Sample counters once per one-second FPS window rather than per-frame logging. Collection counters cover the whole managed process; allocated-byte deltas are sampled on the main thread before the diagnostic formats/logs its own line, then baselines are reset after logging.
   - [x] Correlate collection-count changes with ~20 ms windows and reject the count itself as a discriminator: collections occur continuously in clean and hitch windows alike.
   - [ ] Correlate actual per-frame `GC.Collect` marker time with hitch windows. If GC wall time rises with the hitch, allocation/collection remains a candidate; otherwise move on.

2. **Broad scheduler admission remainder**
   - [x] Add per-frame maxima for the existing scheduler/admission/worker/upload profiler markers so a hitch cannot be missed by a cheap one-second reporting frame.
   - [ ] First establish whether `SchedulerPrepare`/broad admission wall time actually rises in the same windows as the ~20 ms frame.
   - [ ] If it does, split the broad admission marker into the minimum useful main-thread sections: solid pending publication/upload, arena-pressure relief, water work/publication, and `JobHandle.ScheduleBatchedJobs()`.
   - [ ] Reuse profiler markers/timing state and avoid high-frequency string logging or managed allocations.
   - [ ] Print the per-frame section maxima with the sparse player trace.

3. **Fast real-player proof**
   - [ ] Run one production-policy (`converging=12`, `converged=0`) 90-second `SmallVoxelShowcase` autowalk after the new diagnostics compile/pass.
   - [ ] For each ~20 ms hitch, classify it as GC/allocation, a specific admission subsection, or neither.
   - [ ] Fix only the first proven source, then re-run the same SmallVoxelShowcase gate.
   - [ ] If neither GC nor scheduler/admission wall time explains the hitch, instrument Unity main/render-thread or presentation/GPU timing rather than guessing at renderer code.

4. **Final acceptance after a winning fix**
   - [ ] Re-run the same SmallVoxelShowcase baseline/candidate comparison and require materially reduced tail spikes with equivalent coverage.
   - [ ] Only then run the expensive corrected full `VoxelShowcase` traversal + real-player profile for final coverage/fallback acceptance.
   - [ ] Update `.claude/plans/rendering-garbled.md` if the proven bottleneck changes the broader renderer conclusion.

## Existing performance interpretation

The broader rendering investigation measured a settled real-player run where scheduler `Prepare` averaged about 2.12 ms: visibility about 1.92 ms, worker admission about 0.20 ms, and discovery/invalidation effectively zero. That remains the steady-state picture.

The moving-player routing/dedup optimization remains worthwhile and should stay. The residual transient is separate: discovery is negligible during spikes, worker `CpuTransvoxelChunkCache.Prepare()` subsections are sub-millisecond, lowering convergence concurrency from 12 to 8 did not remove the ~20 ms pattern, and raw GC collection counts are continuous rather than hitch-specific.

The next causal gate is now frame-accurate: compare FPS-window maxima against actual scheduler/admission and `GC.Collect` marker wall time. Only if scheduler wall time rises with the hitch do we spend another iteration splitting its post-worker remainder.

The visibility path remains a separate steady-state hotspot and should not distract from the transient investigation until the hitch source is identified.

## Next optimization gate

- [x] Keep the unique shard routing/dedup optimization; it has a measured real-player win.
- [x] Establish `SmallVoxelShowcase` as the fast real-player iteration gate.
- [x] A/B convergence ceiling 12 vs 8 and reject 8 as a stutter fix.
- [x] Correlate raw GC/allocation counters with hitch windows; collection counts are non-discriminating and renderer allocation is zero.
- [ ] Correlate actual GC pause and scheduler/admission marker wall time with hitch windows.
- [ ] Split post-worker admission wall time only if the broad scheduler/admission marker is actually present in the hitch.
- [ ] Fix the first measured culprit rather than changing renderer policy speculatively.
- [ ] Keep commit/publication work allocation-free and bounded by explicit per-frame budgets.
- [ ] Re-run the exact same small-player path after the fix, then full VoxelShowcase only for final acceptance.

## Acceptance

- [x] Ownership/routing behavior proven by focused Unity tests.
- [x] Unique-chunk/fanout regression executed successfully in Unity.
- [x] Real-player moving/streaming behavior characterized against master.
- [x] Fast SmallVoxelShowcase 12-vs-8 causal A/B completed and lower concurrency rejected.
- [ ] Identify the source of the remaining ~20 ms transient with direct evidence.
- [ ] Reduce that transient without reducing render distance/coverage or introducing synchronous job completion.
- [ ] No geometry holes or near/far fallback regression.
- [ ] Corrected full traversal assertion reaches movement after the winning candidate exists.
- [x] Measured main-thread/frame-time improvement versus `master` from the routing/dedup optimization.
