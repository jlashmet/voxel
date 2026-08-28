# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
The sole capture has one marked region: the top-left FPS/surface telemetry at the saved `Showcase Camera` pose. The reported defect is moving-player stutter, slow convergence, and transient missing geometry. Final gates are a 420-frame/210 m production traversal (visible solids every moving frame, <=5 cm far fallback while near coverage is incomplete, zero frame-path blocking completions, >=4 streamed regions, p95 <18 ms / p99 <25 ms) plus a 45 s real-player replay at the captured pose with intact geometry.

## Competing hypotheses / evidence
1. **CPU near-ring preparation dominates traversal.** Eight-worker profiling measured scheduler/admission/worker preparation around 9.16/6.39/5.21 ms while upload was ~0.16 ms. This remains long-term architecture pressure.
2. **Legacy GPU-v1 is a safe production acceleration.** Falsified twice: run `33125988697` lost every visible solid draw at traversal frame 8; exact run `33131454442` failed identically after semantic classification/CPU fallback and its replay showed severe convergence stalls and incomplete terrain.
3. **LOD selection drops the drawable parent.** Source inspection rejects this as primary: `SurfaceLodVisibilitySelector` retains a drawable parent until direct children are current-complete.
4. **The rollback regression's 1200-frame startup gate represents a stable convergence budget.** Falsified by exact request `33132712687`: the Editor test consumed 1200 frames in only ~11-12 s after a 25.66 s scene build and failed before traversal (`visible=0`, `farHole=365.92m`). The same request's independent real-player replay reached stable coverage (`visible=517`, `dirty=0`, far hole closed) and then held roughly 300-500 FPS with no visible-drop events. The harness now uses a 30 s wall-clock startup budget; all moving-product assertions and frame-time limits are unchanged.

## Selected fix / regression
Production stays on the optimized asynchronous CPU surface renderer; legacy per-worker GPU-v1 is quarantined behind `VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1`, with `VOXEL_DISABLE_GPU_CUTOVER=1` as the stronger override. GPU-v1 remains available for explicit experiments, not silent player cutover.

`ShowcaseGpuMigrationTests.MovingShowcaseKeepsLegacyGpuV1OffAndPreservesCoverage` is the behavioral regression. It checks the 420-frame traversal, far fallback, streamed-region work, zero blocking completions, zero GPU-v1 backend/build activity, and unchanged p95/p99 budgets. Only its pre-traversal readiness timeout changed from frame-count to wall-clock time.

## Blast radius / cost
Production impact is limited to backend selection before scene startup. Storage, voxel meaning, gameplay/collision authority, world generation, LOD hierarchy, CPU meshing, and geometry publication are unchanged. Test-only warmup semantics now reflect elapsed time rather than Editor frame rate. GPU-v2 remains follow-up work: a shared persistent mirror plus batched allocation/indirect generation.

Remaining gate: one exact-SHA targeted PlayMode run of the focused regression plus 45 s replay. Only green exact-source evidence may promote this capture.
