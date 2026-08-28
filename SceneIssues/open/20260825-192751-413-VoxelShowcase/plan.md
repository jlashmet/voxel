# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The sole marked region is the top-left FPS/surface telemetry at the saved Showcase camera. Acceptance is the production 420-frame/~210 m traversal: solids visible every moving frame, <=5 cm far fallback while near coverage is incomplete, zero frame-path blocking completions, >=4 streamed regions, p95 <18 ms and p99 <25 ms, plus the 45 s real-player replay with intact geometry.

## Evidence / discriminators
1. **CPU preparation is the throughput pressure, but not the coverage regression.** Profiling measured scheduler/admission/worker prep near 9.16/6.39/5.21 ms versus ~0.16 ms upload. CPU source `1ddb80f...` retained visible solids for all 420 moving frames and failed only p95/p99.
2. **Legacy GPU-v1 safely accelerates production.** Falsified twice: runs `33125988697` and `33131454442` lost every visible solid at frame 8; GPU-v1 remains explicit-experiment only.
3. **Camera-priority discovery fixes the movement hole.** Falsified by exact run `33140352114`: source `9d689afe...` restored bounded priority discovery yet lost every visible solid at moving frame 15, versus frame 16 without it. Replay later converged around 310-480 FPS. The priority experiment is removed exactly.
4. **Cache residency / toroidal slot retirement causes the loss.** Falsified by source differential: `CpuTransvoxelChunkCache.cs` and `SurfaceChunkSlotGrid.cs` are byte-for-byte identical between coverage-safe `1ddb80f...` and the failing modern CPU branch.
5. **Frustum-blind LOD completion can retire drawable fallback.** Supported by source inspection. `SurfaceLodVisibilitySelector` documents that off-frustum children must not retire coarse fallback, but `IsCurrentViewComplete` ignored `inFrustum`. The selector can therefore expand a parent into logically complete children that have no drawable entry in the current view, matching the observed all-draw collapse during camera motion.

## Selected correction / regressions
Restore `VoxelSurfaceScheduler.cs` byte-for-byte to the current master blob, removing the rejected priority experiment. Require ring ownership **and current-frustum membership** plus current-ready/current-known-empty publication proof before a fine child participates in atomic LOD handoff.

`SurfaceLodVisibilitySelectorTests.CurrentViewCompletionRequiresOwnedVisiblePublishedProof` is the focused invariant regression. `ShowcaseGpuMigrationTests.MovingShowcaseKeepsLegacyGpuV1OffAndPreservesCoverage` remains the behavioral 420-frame production traversal and final targeted CI gate; thresholds and coverage assertions are unchanged.

## Blast radius / cost
Rendering visibility handoff only. Storage/gameplay authority, cache residency, clipmap slots, meshing, geometry/material semantics, upload, shaders, arenas, GPU-v1 quarantine, worker counts, and performance thresholds are unchanged. The tradeoff is bounded retention of an already-drawable coarse parent at frustum boundaries until its visible finer children have published proof, preventing a transient hole without adding a scan or allocation.
