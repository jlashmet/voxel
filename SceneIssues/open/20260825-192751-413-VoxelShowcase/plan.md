# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The sole marked region is the top-left FPS/surface telemetry at the saved Showcase camera. Acceptance is the production 420-frame/~210 m traversal: solids visible every moving frame, <=5 cm far fallback while near coverage is incomplete, zero frame-path blocking completions, >=4 streamed regions, p95 <18 ms and p99 <25 ms, plus the 45 s real-player replay with intact geometry.

## Evidence / discriminators
1. **CPU prep is the long-term throughput pressure.** Profiling measured scheduler/admission/worker prep near 9.16/6.39/5.21 ms versus ~0.16 ms upload. Older CPU source `1ddb80f...` nevertheless retained coverage for all 420 moving frames, failing only p95/p99.
2. **Legacy GPU-v1 safely accelerates production.** Falsified twice: runs `33125988697` and `33131454442` lost every visible solid at frame 8; the latter replay also stalled catastrophically. GPU-v1 remains explicit-experiment only.
3. **Frame-count warmup represented convergence.** Falsified by `33132712687`; the formal warmup is now wall-clock bounded and moving assertions are unchanged.
4. **LOD handoff alone caused the movement hole.** Partially supported: `33136824454` failed at moving frame 5; requiring current-ready/current-known-empty child proof moved the next exact failure to frame 16, but did not eliminate it.
5. **Camera-local discovery lost priority behind the general sweep.** Supported by exact run `33138524485` on `eabca84f...`: moving frame 16 had zero visible solids while the same source's 45 s replay later converged around 300–400 FPS. The coverage-safe CPU control `1ddb80f...` had a dedicated camera-priority discovery FIFO. Commit `2d653001...` later removed that FIFO while retaining camera-local region identification, so motion could enqueue newly exposed regions behind cold-start/prefetch work.

## Selected correction / regressions
Keep GPU-v1 quarantined and retain the current LOD publication-proof rule. Restore only bounded discovery ordering: resident regions in the camera's 3x3x3 neighborhood on clipmap movement enter a deduplicated priority FIFO ahead of ordinary discovery; active/retried priority work preserves priority.

`SurfaceLodVisibilitySelectorTests.CurrentViewCompletionRequiresRingOwnershipAndPublishedProof` covers atomic handoff. `ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` is the behavioral region-crossing regression for discovery/coverage/frame time. `ShowcaseGpuMigrationTests.MovingShowcaseKeepsLegacyGpuV1OffAndPreservesCoverage` keeps production off the unsafe GPU-v1 backend.

## Blast radius / cost
Runtime change is discovery ordering only: no storage/gameplay authority, voxel semantics, mesher, upload, shader, arena, or LOD-band change. At most 27 resident camera-neighbor regions are priority candidates per clipmap move, deduplicated by existing/new hash sets; background discovery may be delayed behind current-view demand. No new per-frame scan is introduced.
