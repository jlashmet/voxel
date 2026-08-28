# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
The sole capture has one marked region: the top-left FPS/surface telemetry at the saved `Showcase Camera` pose. The report is moving-player stutter, slow convergence, and transient missing geometry. Final gates are a 420-frame production traversal (visible solids every moving frame, <=5 cm far fallback while near coverage is incomplete, zero frame-path blocking completions, >=4 streamed regions, p95 <18 ms / p99 <25 ms) plus a 45 s replay at the captured pose with intact geometry.

## Competing hypotheses / evidence
1. **CPU near-ring preparation dominates traversal.** Eight-worker profiling measured scheduler/admission/worker preparation around 9.16/6.39/5.21 ms and snapshot spikes while upload was ~0.16 ms. This remains the long-term architecture pressure.
2. **The existing GPU-v1 cutover is a safe production acceleration.** Falsified twice. Source `2d4c0a0` (run `33125988697`) lost every visible solid draw at traversal frame 8. After GPU-side semantic classification plus CPU fallback, exact source `0c4a5b1` (run `33131454442`) failed identically at frame 8; its replay spent long intervals at single-digit/low-double-digit FPS and showed incomplete flat terrain before late castle convergence.
3. **LOD selection itself drops the parent.** Source inspection rejects this as primary cause: `SurfaceLodVisibilitySelector` retains a drawable parent until every direct child is current-complete, and entries remain drawable while replacements build.

## Selected fix / regression
Keep the optimized asynchronous CPU surface renderer as production behavior and quarantine legacy per-worker GPU-v1 behind `VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1`; `VOXEL_DISABLE_GPU_CUTOVER=1` still wins. GPU-v1 remains available for diagnostics/GPU-v2 development, but cannot silently become the player renderer.

`ShowcaseGpuMigrationTests.MovingShowcaseKeepsLegacyGpuV1OffAndPreservesCoverage` is the focused behavioral regression. It runs the same 420-frame/210 m production traversal, asserts fallback coverage, streamed-region work, zero blocking completions, zero GPU-v1 backend/build activity, and the unchanged p95/p99 budgets.

## Blast radius / cost
The guard changes only production backend selection before scene startup. Storage, voxel meaning, collision/gameplay authority, world generation, LOD hierarchy, CPU meshing, geometry arena publication, and GPU-v1 implementation are unchanged. It removes per-worker GPU mirrors and per-chunk count/readback/write serialization from production. GPU-v2 remains the documented follow-up: one shared persistent mirror plus batched GPU allocation/indirect generation.

Current source: `6e5e7a9213e708009da36a2f13d92881469e8de5`. Remaining gate: one exact-SHA targeted PlayMode run of the regression plus 45 s replay; only green evidence may promote the capture to pending/closed.
