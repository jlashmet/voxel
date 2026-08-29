# Experiment 007 — deterministic startup handoff regression

## Question
Did the prior final-CI failure prove the startup fallback handoff was still broken, or did the 300-frame polling loop merely fail to give Unity's single worker enough wall-clock time to finish ring 1?

## Runtime discriminator
The failed exact-SHA run for `1ab2f10111076c8799da488ca5fa8182e73d9c11` produced both the failing PlayMode result and a real `VoxelShowcase` player replay. The PlayMode test still had zero ring-1 indices after its fixed 300-frame loop. In the same run's player artifact, the flat teal/green fallback covers the capture diagonal at 15.6 s and 25.6 s, but is gone by 35.6 s and remains gone at 45.7 s and 55.7 s. The asynchronous handoff therefore does complete in the product; frame count was not a reliable proxy for worker progress on the one-worker CI host.

All five annotated capture centers were checked against that timeline at the original 1440x801 resolution: regions 1, 2, 3, 4, and 5 are all inside the same flat fallback polygon at 25.6 s (region 4 also has foreground branch geometry), and all five are released to authored scene geometry by 35.6 s. A static grass/water material error cannot explain that synchronized disappearance; fallback XZ ownership can.

## Regression change
`StartupFallbackDoesNotOverlapPublishedFirstOuterRing` still instantiates production `VoxelFarTerrain` at the captured camera and still requires a 350 m probe to be owned by ring 1 but not by the startup fallback. The only harness change is how it reaches the publication boundary: after the first `LateUpdate` synchronously builds ring 0 and schedules ring 1, the test reflects the existing private job state, asserts the scheduled job is ring 1, calls `Complete()` on that real `JobHandle`, and yields one frame so normal production `LateUpdate` consumes the completed job, publishes ring 1, and advances the fallback hole.

This removes host-speed dependence without manufacturing mesh data, invoking private mesh builders, changing production scheduling, or weakening the overlap assertion.

## Blast radius / cost
Test-only change in `Assets/Tests/PlayMode/CastleLowerRiverWaterRepairPlayModeTests.cs`; no runtime or scene code changed. CI should become faster and deterministic because it no longer burns up to 300 rendered frames waiting for worker throughput. Product cost remains the selected fix's existing bounded startup cost: the eight-triangle fallback is rebuilt only when contiguous outer coverage advances, with no new per-frame sampling or steady-state work.

## Final gate
Pending one fresh exact-SHA `scene-regression` request after refreshing `master`. Success requires the deterministic PlayMode regression to pass and the built `Assets/Scenes/VoxelShowcase.unity` replay to remain free of the marked fallback overdraw across all five regions.