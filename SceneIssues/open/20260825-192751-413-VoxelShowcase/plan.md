# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The only capture is `screenshot-001.png`; its only marked circle covers the top-left FPS/surface telemetry at the saved `Showcase Camera` pose. Replay ties the complaint to moving-player rendering/streaming cost. Acceptance is the unchanged production traversal regression plus 45 s saved-pose replay: p95 < 18 ms, p99 < 25 ms, zero frame-path blocking completions, streamed-region movement proven, visible solids every frame, and near/far gap <= 5 cm.

## Competing hypotheses / evidence
**Exact near-ring snapshot overhead — supported but insufficient alone.** Profiling attributed a sampled ~70.83/70.86 ms worker overrun to `Voxel.Surface.Snapshot`. Inlining the existing clear/map/compact metadata bodies only for bounded step-1/2 exact grids reduced snapshot p95 to ~1.76 ms, but exact traversal remained above budget.

**Draw/upload/GPU/GC and admission — disfavored/rejected.** Settled replay and stage attribution keep upload/GC small and stationary draw throughput much faster. Two admission variants either lost visible geometry or still missed the unchanged 18/25 ms tail limits.

**Cross-frame converging visibility reuse — rejected as closing fix.** Exact request `agent-2-192751-final-bounded-visibility-reuse-v2-20260827-1219` preserved coverage but failed at p95 23.40 ms. Historical profiling already ruled out inline frustum math and cadence-throttled 360-degree demand as fixes.

**Duplicate same-frame visibility — selected fix.** The behavioral regression advances the player, yields one player frame, then explicitly renders the same camera before stopping its timer. `VoxelSurfaceScheduler.Prepare` advances world/change/build state once per `Time.frameCount`; a second Prepare in that frame only reruns visibility. During convergence that is another ~4–6 ms full slot/LOD sweep even when camera identity and pose are unchanged. `VoxelRenderPass` now reuses the already-prepared scheduler result only for the same camera instance, exact position/rotation, and same frame. A different camera or changed same-frame pose still invokes scheduler visibility.

## Regression / blast radius / cost
`ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` moves 0.5 m per rendered frame for 420 frames across >=4 region boundaries and asserts visible solids, <=5 cm fallback coverage while near geometry is incomplete, zero blocking completions, streaming activity, and the unchanged 18/25 ms tails.

Rendering only. No cross-frame reuse is added; no clipmap/prefetch, Storage, gameplay/collision authority, build admission, publication, geometry semantics, or thresholds change. Draw staging/submission still executes for every render; only an identical second preparation in one Unity frame is suppressed.

- [x] Inspect the sole marked region and saved-pose evidence.
- [x] Discriminate snapshot, draw/upload/GPU/GC, admission, and visibility hypotheses.
- [x] Retain the moving traversal behavioral regression.
- [x] Implement bounded snapshot work and same-frame identical-view preparation reuse.
- [ ] Green exact-SHA targeted CI plus 45 s saved-pose replay.
- [ ] Commit `verification-final.png`, complete pending metadata, close, merge latest master, and non-force advance master.
