# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The original capture remains authoritative: VoxelShowcase drops below 100 FPS while moving, takes too long to reach full visible surface coverage, and can show missing geometry while chunks are being meshed. The rendering path needs a material architecture change with GPU meshing/extraction as the intended direction, preserving correctness while pursuing roughly 1000 FPS of rendering headroom. CPU-only tuning is supporting work, not a valid final closure.

## Current state / leading hypothesis
Current `master` has a partial production GPU cutover for supported near rings, but GPU-candidate chunks still pay substantial CPU exact-snapshot/classification and dense brick-cache staging work before compute extraction. The leading hypothesis is that a persistent GPU brick mirror plus direct GPU classification/sampling will remove that per-chunk CPU preparation boundary and materially increase sustainable meshing throughput.

## Work
- Move GPU eligibility/classification and packed surface-semantic decoding onto the GPU for supported chunks.
- Remove the dense per-chunk CPU brick-cache staging walk; publish compact brick/version deltas into a persistent GPU mirror instead.
- Preserve GPU-resident generated geometry through draw with no geometry readback or player-frame GPU wait.
- Port planar/sharp/faceted semantics only with CPU↔GPU topology/normal parity coverage; unsupported cases must fall back to CPU without holes or stale publication.
- After the meshing migration, profile visibility/submission and move additional work GPU-side only if it remains material.

## Verification
A behavioral regression must prove GPU surface builds actually complete during the production path. Keep the 210 m production-speed traversal across >=4 regions, visible voxel coverage every moving frame, <=5 cm near/far fallback gap, zero frame-path blocking completions, and p95 <18 ms / p99 <25 ms. Record moving and stationary CPU/GPU telemetry showing what CPU work disappeared, and pursue the original ~1000 FPS steady-state goal. Do not close this issue until the production architecture materially removes CPU voxel meshing/preparation work and satisfies the original performance intent, or an evidence-backed alternative architecture does.
