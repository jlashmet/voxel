# Experiment 006 — minimal startup ring-handoff reproduction

## Why this experiment is required
Three product corrections have failed exact-scene acceptance, so the issue workflow requires a minimal reproduction before a fourth production change. This experiment does not change production behavior.

## Runtime/source discriminator
The 60-second exact-player replay showed the viewport-scale diagonal proxy at ~15.3 s and ~25.3 s, then no proxy from ~35.3 s onward once visible near/far coverage had settled. `VoxelFarTerrain.LateUpdate` publishes outer rings single-flight in increasing order, but `_startupFallbackRing` remains the outermost ring and is drawn every frame until that final ring's first authoritative job completes.

For the production 220 m inner radius and 96-sample clipmap:
- ring 0 spacing = 64 voxels = 6.4 m and half extent = 307.2 m;
- the startup fallback hole ends at about 300.8 m (`307.2 - 6.4`);
- ring 1 spacing = 128 voxels = 12.8 m, outer half extent = 614.4 m, and inner hole is about 294.4 m (`307.2 - 12.8`).

Therefore, after ring 1 publishes but before the outermost ring publishes, authoritative ring 1 and the flat startup fallback both own approximately 300.8–614.4 m of the same XZ footprint. A point 350 m east of the captured camera is inside both meshes. Because the fallback is flat at base height while authored receiving-water terrain can be lower, depth testing can show the fallback as the giant diagonal triangle seen in the exact replay.

## Minimal reproduction
Commit `f6d148a4f838c0b7a4141e931e9849f6977c3bbf` adds `CastleLowerRiverWaterRepairPlayModeTests.StartupFallbackDoesNotOverlapPublishedFirstOuterRing` without modifying production code. It instantiates production `VoxelFarTerrain` at the captured camera, waits only until ring 1 has published authoritative topology, confirms the outer fallback is still present, confirms a 350 m probe belongs to ring 1, and then requires the fallback not to cover that same probe.

On the current production ownership rules that final assertion is expected to fail: the fallback still covers the probe. The reproduction isolates the handoff defect without castle authoring, storage repair, material recapture, near-renderer publication, or screenshot heuristics.

## Competing hypotheses resolved
- **Ring-0-only overlap:** falsified; the prior ring-0 regression passed while the exact replay still failed, and this reproduction identifies a ring-1/fallback overlap window.
- **Steady-state terrain/material error:** falsified by clean settled frames after ~35 s and complete coverage.
- **Harness pre-pin artifact:** falsified by the ~25.3 s frame after exact camera pinning.
- **Shader displacement required to explain the triangle:** unnecessary for the reproduction; two simultaneously drawn meshes already share the same XZ region while one is an intentionally flat base-height proxy. The handoff invariant is violated before shader behavior is considered.

## Candidate correction and cost bound
When an outer ring publishes, advance the startup fallback's inner hole to the newly authoritative contiguous ring footprint instead of leaving it fixed at ring 0 until the outermost job completes. This requires at most one tiny eight-triangle fallback rebuild per outer-ring startup publication (four rebuilds for the current five-ring configuration), no terrain/storage resampling, no extra jobs, and no steady-state work. The fallback still covers unresolved horizon space until the outermost ring lands.
