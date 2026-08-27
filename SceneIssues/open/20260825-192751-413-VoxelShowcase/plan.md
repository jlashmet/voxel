# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The single marked region is the top-left FPS/surface telemetry at the saved showcase pose; replay reproduces that HUD and ties the complaint to moving-player rendering cost. Acceptance remains the unchanged production traversal regression plus saved-pose RealPlayer replay: p95 <= 18 ms, p99 <= 25 ms, no frame-path blocking completions, streamed-region movement proven, visible solids every frame, and near/far gap <= 5 cm.

## Competing hypotheses / evidence
**Exact-snapshot preparation — supported.** Per-frame profiling attributes the dominant synchronous worker overrun to `Voxel.Surface.Snapshot`; one sampled worker spent ~70.83/70.86 ms there. Source audit isolates `ScheduleExactMetadataSnapshot` fan-out before its deadline check. A test-only 12->8 build ceiling reduced snapshot/worker spikes but did not close acceptance.

**Draw/upload/GPU/GC — disfavored.** Settled replay and stage attribution show upload/GC small and stationary draw throughput much faster than traversal. Initial camera/LOD discovery was a separate coverage defect and remains protected by the traversal assertions.

**Admission tuning — rejected.** A completion-count ramp lost every visible draw at traversal frame 5. A monotonic ramp preserved coverage but failed at p95 20.73 ms / p99 25.10 ms and reduced saved-pose replay to ~97–119 FPS; production admission was restored.

## Selected fix / discriminator
For the two small GPU-capable exact near rings, execute the existing metadata clear/map/compact job bodies inline instead of scheduling clear + per-region fan-out + compaction. Step 1 has 10^3 padded entries and step 2 has 18^3; step 4 (34^3) and step 8 (66^3) retain the existing asynchronous Burst pipeline. All Storage pin/version validation, classification, GPU/CPU fallback, geometry publication, and LOD semantics remain unchanged.

Falsifier: unchanged exact-SHA traversal still exceeds p95 18 ms or p99 25 ms, loses visible solids/fallback coverage, reports blocking completion, or replay regresses. If falsified, revert this bounded scheduling change and proceed to the documented GPU-v2 snapshot-bypass experiment rather than further admission tuning.

## Blast radius / cost
Rendering only. Worst synchronous metadata work is bounded to the 5832-entry step-2 grid plus compaction; coarse snapshots cannot become main-thread scans. No gameplay/collision authority, storage/GPU format, geometry semantics, allocations, concurrency defaults, or acceptance thresholds change.

- [x] Inspect the one marked region and replay its saved pose.
- [x] Add/retain behavioral traversal regression and stage profiling evidence.
- [x] Reject two scheduling variants with exact-SHA CI evidence and restore production admission.
- [x] Implement bounded near-ring exact-metadata scheduling discriminator.
- [ ] Green exact-SHA traversal + 45 s saved-pose replay.
- [ ] Commit `verification-final.png`; complete pending metadata and move open -> pending.
- [ ] Close capture, merge latest master, and non-force advance master under task authorization.
