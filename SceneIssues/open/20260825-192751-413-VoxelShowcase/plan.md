# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The marked showcase capture reports sub-100 FPS while walking, slow surface fill, and missing geometry. Every marked region/pose has been replayed. Acceptance is the unchanged production-path traversal regression plus saved-pose RealPlayer replay: p95 <= 18 ms, p99 <= 25 ms, no frame-path blocking completions, streamed-region movement proven, visible solids present, and near/far coverage gap <= 5 cm.

## Competing hypotheses / evidence
**H1 — movement rebuild work.** Supported. Settled saved-pose replay is clean (~418 FPS; p95 8.70 ms; zero missing/reappeared/assertions), while moving exact-SHA CI failed at p95 18.57 ms / p99 28.01 ms. Stage profiling attributes the dominant synchronous overrun to `Voxel.Surface.Snapshot`; one observed worker spent ~70.83/70.86 ms there. Reducing broad convergence from 12 to 8 reduced snapshot spikes but did not pass acceptance, so concurrency amplifies rather than fully explains the cost.

**H2 — draw/upload/GPU/GC.** Disfavored by settled replay and stage A/B evidence: upload/GC are small and draw-only steady state is fast.

Startup coverage was a separate defect: `20a32987...` made initial discovery camera/LOD-aware and its exact-pose production regression passed. It remains required because traversal cannot be considered fixed by hiding geometry.

## Selected discriminator / fix
Production commit `36eeeace938f801063bd0aa6d57074c3bacaf9b2` smooths only **new** solid build admission: each rendered frame exposes at most one convergence slot beyond the previous frame's observed running solid jobs. Already-active builds continue and the configured steady-state ceiling is unchanged. This prevents multiple idle shards entering exact snapshot setup together without changing storage, extraction, LOD, arena, upload, or frame-time limits.

Falsifier: unchanged traversal still exceeds p95/p99 or opens the coverage assertion. If so, reject this scheduling fix and return to eliminating the CPU exact-snapshot boundary for GPU-supported near rings.

## Blast radius / remaining gate
Shared solid-render admission only; no allocation or geometry-semantic change. Cost is at most one additional exposed build slot per rendered frame during ramp-up; coverage regression measures unacceptable deferral.

- [ ] Green exact-SHA traversal + 45 s saved-pose replay.
- [ ] Commit `verification-final.png`; set pending metadata and move open -> pending.
- [ ] Under task authorization, close capture, merge latest master, and non-force advance master.
