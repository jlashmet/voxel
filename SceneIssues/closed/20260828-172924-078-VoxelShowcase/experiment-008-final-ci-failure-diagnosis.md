# Experiment 008 — final CI failure diagnosis

## Question
Did exact-SHA run `33257610930` fail because the startup fallback repair is still wrong, or because the focused PlayMode regression observed the wrong synchronization/object state?

## Evidence at source `0aee2ebb70584af7228b381c70c873f28a01b216`
- Focused PlayMode job `99113954357` compiled and entered the test successfully; it failed in 0.071 s at `CastleLowerRiverWaterRepairPlayModeTests.cs:208` because `FarTerrainRing1.triangles.Length` was 0. This is a test/product assertion failure, not CI infrastructure.
- The same run built and launched `Assets/Scenes/VoxelShowcase.unity` for 60 s with zero player-harness assertion failures.
- Exact-player captures at 15.8 s and 25.8 s place all five original marked centers on the same flat diagonal startup fallback polygon.
- At 35.8 s that diagonal fallback polygon is gone at all five marked centers, and it remains gone at 45.8 s and 55.8 s. This temporal handoff falsifies a persistent grass/material explanation for the marked polygon and confirms it is transient far-terrain fallback ownership.

## Discriminator
The failing test completed the real `_heightJobHandle` but then assumed one coroutine yield had published it and selected `FarTerrainRing1` through global `Resources.FindObjectsOfTypeAll<Mesh>()` name lookup. Production publication instead owns state and meshes per `VoxelFarTerrain`: `LateUpdate` marks `_ringHeightValid[ring]`, rebuilds `_ringMeshes[ring]`, then advances the startup-fallback coverage boundary.

## Action
Commit `2dbe54cfea365a9210bbdff53abe841a77671970` changes only the regression. After completing the real ring-1 job it allows at most three player-loop turns for production `LateUpdate`, asserts the component's exact `_ringHeightValid[1]`, and inspects that same component's ring-1 and fallback meshes. The worker is already complete before this bounded wait, so worker throughput is no longer a variable. No production code or runtime cost changed.

## Verdict / next gate
The production visual fix remains supported by the exact built-player artifact, but the final focused exact-SHA gate is red and the corrected test is now on a newer feature SHA. Per assignment constraints, no extra CI transport or replacement request was created. Keep the issue `open` until a workflow-authorized exact-SHA targeted run and exact-head built-player run are green.
