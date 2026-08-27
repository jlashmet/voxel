# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The single marked region is the top-left FPS/surface telemetry at the saved showcase pose; the capture reports sub-100 FPS while walking, slow fill, and transient missing geometry. Replay at that pose reproduces the marked HUD and ties the complaint to runtime performance. Acceptance remains the unchanged production-path traversal regression plus saved-pose RealPlayer replay: p95 <= 18 ms, p99 <= 25 ms, no frame-path blocking completions, streamed-region movement proven, visible solids present every frame, and near/far coverage gap <= 5 cm.

## Competing hypotheses / evidence
**Movement rebuild / CPU exact snapshot — supported.** Per-frame profiling attributes the dominant synchronous worker overrun to `Voxel.Surface.Snapshot`; one observed worker spent ~70.83/70.86 ms there. A test-only 12->8 build ceiling reduced snapshot/worker spikes but still failed acceptance.

**Draw/upload/GPU/GC — disfavored.** Earlier settled replay and stage attribution show upload/GC small and draw-only steady state much faster than traversal. Initial camera/LOD discovery was a separate coverage defect and remains protected by the traversal assertions.

**Admission tuning — rejected as closing fix.** A completion-count ramp reduced burst pressure but lost every visible solid draw at movement frame 5. An explicit monotonic ramp preserved coverage but failed at p95 20.73 ms / p99 25.10 ms and cut the saved-pose replay to roughly 97–119 FPS. `a737f6aadea0bedef2dc5afe3394b238bb6fddca` restores pre-ramp production admission.

## Selected next discriminator
Implement the narrow GPU-v2 slice in `gpu-v2-next-step.md`: for supported plain continuous near-field terrain, feed the existing GPU Transvoxel backend from a persistent GPU brick mirror and bypass the normal CPU exact-snapshot preparation boundary. CPU voxel storage remains authoritative and unsupported chunks fall back to the existing path.

Falsifier: the prototype does not materially remove `Voxel.Surface.Snapshot` cost / reduce newly requested render-chunk CPU cost, or it introduces visible holes, stale-version publication, geometry readback, or player-frame GPU waits.

## Blast radius / cost
Keep the first slice render-only and feature-gated to supported step-1 terrain. No collision/gameplay authority moves to GPU, no acceptance threshold changes, and no unsupported authored feature loses the existing fallback.

- [x] Inspect the one marked region and replay its saved pose.
- [x] Add/retain behavioral traversal regression and stage profiling evidence.
- [x] Reject two scheduling variants with exact-SHA CI evidence and restore production admission.
- [ ] Build and measure GPU-v2 exact-snapshot bypass.
- [ ] Green exact-SHA traversal + 45 s saved-pose replay.
- [ ] Commit `verification-final.png`; complete pending metadata and move open -> pending.
- [ ] Close capture, merge latest master, and non-force advance master under task authorization.
