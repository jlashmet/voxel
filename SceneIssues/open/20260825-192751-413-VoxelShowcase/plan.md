# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The only capture is `screenshot-001.png`; its only marked circle covers the top-left FPS/surface telemetry at the saved `Showcase Camera` pose. The report requires materially faster traversal/loading without missing geometry. Acceptance is the unchanged moving-player regression plus saved-pose replay: p95 < 18 ms, p99 < 25 ms, zero frame-path blocking completions, streamed-region movement proven, visible solids every frame, and near/far gap <= 5 cm.

## Competing hypotheses / evidence
**Exact near-ring snapshot overhead — supported but insufficient alone.** Profiling attributed a sampled ~70.83/70.86 ms worker overrun to `Voxel.Surface.Snapshot`. Inlining the existing clear/map/compact metadata bodies only for bounded step-1/2 exact grids reduced snapshot p95 to ~1.76 ms. Exact-SHA CI still failed traversal at ~20.16 ms p95 / ~28.33 ms p99, falsifying snapshot-only as the complete fix.

**Draw/upload/GPU/GC — disfavored.** Stage attribution and settled replay keep upload/GC small; stationary draw throughput is much faster than moving traversal. Two admission variants were rejected: one lost all visible draw by frame 5; the monotonic variant preserved coverage but still failed at p95 20.73 / p99 25.10 and hurt saved-pose FPS.

**Repeated visibility traversal while coverage is converging — selected final discriminator.** Runtime telemetry leaves visibility at ~4–6 ms while checking ~6,953 slot-bounded candidates. The final bounded reuse path is permitted only when demand/ready-set versions are unchanged, visible solids already exist, missing visibility is still converging, camera translation <= 0.75 m, rotation <= 2 degrees, and the reuse occurs immediately after a full pass. The following frame must run full visibility again. Stationary identical-pose reuse is also version-gated. Any demand/readiness change invalidates reuse.

## Regression / blast radius / cost
`PlayerTraversal_StreamingAcrossRegionBoundaries_StaysWithinFrameBudget` moves the camera every rendered frame across >=4 region boundaries and asserts visible solids, <=5 cm fallback gap while near coverage is incomplete, zero blocking completions, and the original 18/25 ms tails. This directly exercises the reuse risk through production scheduling/visibility code.

Rendering only. No gameplay/collision authority, Storage format, geometry semantics, acceptance thresholds, allocations, or build-admission policy change. Snapshot inlining is bounded to step-1/2 metadata; visibility reuse is one frame maximum during small-motion convergence and is invalidated by authoritative demand/readiness changes.

- [x] Inspect the sole marked region and saved pose evidence.
- [x] Discriminate snapshot, draw/upload/GPU/GC, admission, and visibility hypotheses.
- [x] Retain/add moving traversal behavioral coverage.
- [x] Implement bounded near-ring snapshot work and version-gated visibility reuse.
- [ ] Green exact-SHA targeted CI plus saved-pose replay.
- [ ] Commit `verification-final.png`, complete pending metadata, close, merge latest master, and non-force advance master.
