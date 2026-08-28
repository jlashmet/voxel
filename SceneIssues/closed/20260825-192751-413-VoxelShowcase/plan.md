# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The sole marked region is the top-left FPS/surface telemetry at the saved Showcase camera. The capture reports <100 FPS, long surface load, and geometry appearing only after meshing. Acceptance: 210 m at production movement speed crossing >=4 regions, visible voxel draw every moving frame, <=5 cm far fallback while near coverage is incomplete, zero frame-path blocking completions, p95 <18 ms and p99 <25 ms, plus the assigned 45 s saved-pose real-player replay.

## Evidence / competing hypotheses
1. **CPU exact-snapshot preparation owns the measured tail. Supported.** Profiling measured worker snapshot p95 4.50 ms and 70.83 ms of a 70.86 ms worst worker frame. Bounded inline metadata for the two small near grids reduced snapshot p95 to ~1.76 ms; step-4/8 grids remain asynchronous.
2. **Legacy GPU-v1 is a safe accelerator. Falsified.** Exact runs `33125988697` and `33131454442` lost every visible solid. Production keeps that cutover disabled unless explicitly opted into diagnostically.
3. **Cross-ring aggregation deletes valid moving geometry. Falsified.** Run `33143795979` failed with `physicalTotal=0`: 43 frustum candidates, zero ready/empty entries, 3224 known chunks and only 39 resident meshes. Loss occurred before aggregation.
4. **The original fixed 0.5 m/frame gate models production movement. Falsified.** It demands ~28 m/s even at the 18 ms budget and scales faster with FPS, above the scene's 18 m/s fly speed.

## Selected fix / verification
The final regression keeps the full 210 m distance, >=4 region crossings, coverage/fallback/GPU-safety assertions, and unchanged 18/25 ms budgets, but advances by elapsed time at `m_FlySpeed`; 0.5 m is only a per-frame displacement cap. Exact transport `b2c59e44594fbb6c77d386287c657f00d0bda026`, run `33155534318`, passed: 1533 moving frames, 210.0 m, p95 10.914 ms, p99 12.468 ms. Its 45 s assigned-pose replay succeeded; final rendered evidence kept castle/terrain present and converged to zero missing surface chunks.

## Blast radius / cost
Runtime optimization is limited to bounded near-ring snapshot scheduling; storage/gameplay authority, topology, coarse-ring jobs, frame budgets, and GPU-v1 safety are unchanged. Tested source: `cb025bdb5b0d4b9e07d8d5f50fa09a2a21fed4c8`. Remaining work is bookkeeping and merge only.