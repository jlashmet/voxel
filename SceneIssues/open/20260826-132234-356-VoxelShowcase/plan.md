# Plan — VoxelShowcase Dirt/grass seam

## Observed / acceptance
Saved camera has two marked Dirt/Moss joins. Exact replay from source `f640c44f5d2038a2efe62cde23ea9898544a0d97` (targeted run `33102166380`) is resident and leaves the lower mark clean, but direct inspection still shows the upper mark as a raised rectangular green tongue. Acceptance: both marks read as continuous Dirt/grass joins at the saved pose.

## Hypotheses / discriminators
1. **Streaming/LOD or stale bake.** Falsified: the fresh exact replay is resident and the bake cache fingerprints `Assets/Game/WorldBuilder`.
2. **Material ownership only.** Falsified by the green CI source: the upper terrace already reports Dirt in the 82.8–84.8 m mismatch and the added Dirt repaint did not change the visible upper tongue.
3. **Civic south-west shoulder geometry.** Survives. The upper mark projects into the civic summit south-west shoulder. Production builds the entire ~61 m civic south edge from one centreline terrain sample, while the adjacent upper west edge is locally sampled; that can flatten this corner into the rectangular shelf visible in replay.

## Selected fix / regression
At existing correction precedence 16, re-realize only the civic south-west 72 dm × 72 dm shoulder as six 12 dm-wide Z ramps. Each strip samples the production `TerrainQuery` at its own outer-edge midpoint and joins that height to the unchanged civic core. Remove the superseded upper-patch Dirt repaint. The PlayMode regression parses the production correction program and requires all six ramp outer elevations to equal their local terrain samples, while retaining civic paving and a bounded primitive budget.

## Blast radius / cost
Only the civic summit south-west shoulder changes height/occupancy; cores, roads, market taper, other districts/captures, and generic rasterization are unchanged. The correction grows from 2 to at most 14 primitives for this patch (budget 16), generated once during world build; no per-frame work is added.

## Verification gate
Keep open until exact-SHA targeted CI passes `SceneIssue20260826132234356CivicSouthWestShoulderFollowsLocalTerrainProfile`, player compilation succeeds, and a fresh saved-camera replay is directly inspected at both marked circles. Only a clean replay becomes `verification-final.png`.
