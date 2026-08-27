# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The single marked region is the top-left FPS/surface telemetry at the saved showcase pose; the capture reports sub-100 FPS while walking, slow fill, and transient missing geometry. Replay at that pose reproduces the marked HUD and ties the complaint to runtime performance. Acceptance is the unchanged production-path traversal regression plus saved-pose RealPlayer replay: p95 <= 18 ms, p99 <= 25 ms, no frame-path blocking completions, streamed-region movement proven, visible solids present every frame, and near/far coverage gap <= 5 cm.

## Competing hypotheses / evidence
**H1 — movement rebuild/snapshot admission. Supported.** Settled saved-pose replay is fast relative to traversal, while moving exact-SHA runs miss the p95/p99 gate. Per-frame profiling attributes the dominant synchronous worker overrun to `Voxel.Surface.Snapshot`; one observed worker spent ~70.83/70.86 ms there. Broad 12->8 concurrency reduced snapshot spikes without passing acceptance, so concurrency amplifies the snapshot cost rather than fully explaining it.

**H2 — draw/upload/GPU/GC. Disfavored.** Settled replay and stage attribution show upload/GC small and draw-only steady state fast. Startup coverage was a separate defect already addressed by camera/LOD-aware initial discovery and remains protected by the traversal coverage assertions.

## Selected fix / falsifier
The first ramp (`36eeeace...`) used previous-frame `RunningSolidJobs + 1`; exact-SHA CI failed because movement frame 5 reached `VisibleSolidChunks == 0`. Short jobs can finish between frames, so that value is not monotonic ramp state and can repeatedly collapse toward one.

`203788c90ee0ab82d9fed5a1d9dfb317c0d039d8` keeps explicit render-pass ramp state instead: start with two convergence slots for presentation continuity, then expose one additional slot per rendered frame while previous metrics report missing visible solids. Reset the ramp on world teardown. Already-active builds and the configured maximum are unchanged.

Falsifier: unchanged traversal exceeds p95/p99, loses visible solids, reports synchronous completion, or opens the far-field coverage gap.

## Blast radius / cost
Shared solid-render admission only; no storage/geometry semantics, LOD bands, arena formats, upload budgets, frame-time thresholds, or per-frame allocations change. Cost is slower exposure of the configured convergence ceiling during missing coverage; the behavioral traversal directly measures unacceptable deferral.

- [ ] Green exact-SHA traversal + 45 s saved-pose replay.
- [ ] Commit `verification-final.png`; complete pending metadata and move open -> pending.
- [ ] Close capture, merge latest master, and non-force advance master under task authorization.
