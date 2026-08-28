# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The sole marked region is the top-left FPS/surface telemetry at the saved Showcase camera. The capture reports <100 FPS, long surface load, and geometry appearing only after meshing. Final acceptance is a 210 m production-speed traversal crossing >=4 regions with a visible voxel draw every frame, <=5 cm far fallback while near coverage is incomplete, zero frame-path blocking completions, p95 <18 ms and p99 <25 ms, plus the assigned 45 s real-player replay at the original pose.

## Evidence / competing hypotheses
1. **CPU exact-snapshot preparation owns the measured tail. Supported.** Per-frame profiling put worker snapshot p95 at 4.50 ms and the worst 70.86 ms worker frame at 70.83 ms snapshot. The bounded near-ring inline-metadata change cut snapshot p95 to ~1.76 ms without changing topology, storage authority, or coarse-ring scheduling.
2. **Legacy GPU-v1 is a safe production accelerator. Falsified.** Exact runs `33125988697` and `33131454442` lost every visible solid; production keeps that cutover explicit-experiment only.
3. **Cross-ring aggregation deletes valid geometry during motion. Falsified.** Exact run `33143795979` failed with `physicalTotal=0`: 43 frustum candidates but zero ready/empty entries, 3224 known chunks and only 39 resident meshes. The loss happened before aggregation.
4. **The failing movement gate represents production movement. Falsified.** Its fixed 0.5 m/frame step is ~28 m/s even at the 18 ms budget and scales upward with FPS, exceeding the scene's 18 m/s unsprinted fly speed.

## Selected final discriminator
`417e82f...` keeps the full 210 m distance, region crossings, fallback/visibility/GPU-safety assertions, and 18/25 ms budgets, but advances by elapsed time at the scene's actual `m_FlySpeed`; 0.5 m remains a maximum per-frame step. This tests the renderer under supported movement instead of a frame-rate-dependent teleport.

## Blast radius / cost / remaining gate
Production optimization remains limited to the two bounded near exact-metadata grids; larger step-4/8 grids stay asynchronous. GPU-v1 remains opt-in. The final test change alters pacing only, not distance or budgets. One exact-SHA targeted PlayMode run plus the 45 s captured-pose replay must be green; otherwise the issue stays open.