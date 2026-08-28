# Plan — VoxelShowcase Dirt/grass seam

## Observed / acceptance
Saved camera has two marked Dirt/Moss joins. Fresh targeted replay after `31106c4042294c481d820e049c1f4e5f5b0f0c04` rebuilt `Game.WorldBuilder.Voxel.dll` and `ShowcaseWorld.bytes`: the lower mark is clean, while direct replay inspection still shows the upper mark as a raised rectangular green tongue. Acceptance: both marks read as continuous Dirt/grass joins at the saved pose.

## Hypotheses / discriminators
1. **Streaming/LOD or stale bake.** Falsified: the fresh replay rebuilt WorldBuilder code and the bake cache fingerprints `Assets/Game/WorldBuilder`.
2. **Material ownership only.** Falsified: Dirt repaint changed bytecode/material ownership but did not remove the upper tongue.
3. **Civic south-west shoulder geometry.** Supported after 3D localization. Projecting the upper marked circle through the saved camera to local natural height spans roughly X=91.0–93.8m/Z=28.6–30.4m. The district civic south shoulder is one full-width ramp using a centreline outer-height sample (~22.2m), while local southwest terrain is ~22.0m. The first geometric experiment repaired only X<92.0m, leaving most of the marked circle on the original centre-sampled south ramp.

## Selected fix / regression
At correction precedence 16, extend the local-profile repair only through the observed 9.6m southwest envelope (X=84.8–94.4m), using eight 1.2m Z-ramp strips whose outer elevations each come from production `TerrainQuery`. The PlayMode regression now builds the final `KentridgeCombinedVoxelCatalogue` and requires all eight ramp samples—including X=92.6m and 93.8m inside the marked circle—to meet local terrain, while retaining civic paving and bounded primitive cost.

## Blast radius / cost
Only an additional 2.4m × 7.2m slice of the civic south shoulder changes height/occupancy beyond the previous experiment; cores, roads, market taper, other districts/captures, and generic rasterization are unchanged. Two added strips cost four additional primitives, taking this patch from at most 14 to 18 primitives, generated once during world build; no per-frame work is added.

## Verification gate
Keep open until exact-SHA targeted CI passes `SceneIssue20260826132234356CivicSouthWestShoulderFollowsLocalTerrainProfile`, player compilation succeeds, and a fresh saved-camera replay is directly inspected at both marked circles. Only a clean replay becomes final verification evidence using the canonical `SceneIssues/README.md` filename/encoding.
