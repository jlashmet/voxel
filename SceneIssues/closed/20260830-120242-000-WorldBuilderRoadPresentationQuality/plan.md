# Plan

## Acceptance
Improve authoritative `WorldRoadNetwork` presentation without changing route/topology authority: coherent curved/diagonal edges; formed carriageway/shoulders and bounded cut/fill; deterministic shared wear; topology-aware junctions; stable chunk/LOD continuity; preserved terrain/vegetation/collision/destruction semantics and budgets; and production-quality exact built-player validation in `KentridgePlayableSlice`.

## Root causes and corrections
Earlier Kentridge fixes removed the plot-frontage overwrite/trench. Valid player-height run `33416931897` then exposed a reusable physical road defect: internal curve/junction pieces produced repeated endpoint bands/ridges. Minimal generic run `33421466465` proved the same physical surface changed from top 16 to 15 solely by reversing incident `TerrainCorridor` piece order.

The first correction uses a continuous corridor arbitration stage in `FeatureRegionBuild`: contiguous corridor pieces remain bounded storage/execution partitions, but each x/z column chooses one physical corridor candidate before mutation. No endpoint fades, route rewrites, Kentridge coordinates, persisted vertex growth, or `EmitTerrainCorridor` width growth were added. Post-master run `33430943763` attempt 2 passed the continuous-field regressions, repository-derived spatial-reservation tests, and both standalone players after attempt 1 hit a proven Unity SIGSEGV infrastructure failure.

Fresh Kentridge captures then separated a second defect: long crown/shoulder terraces remained after piece-boundary repetition was removed. Exact discriminator run `33431993454` on feature SHA `b328f169333d802857425359ce981bdd6fab70e5` kept the partition/junction invariants green while `PhysicalCrossSectionMustNotTurnOneDecimetreCrownIntoMultiVoxelTerrace` failed with an adjacent-column target-height jump of 4 voxels versus an allowed maximum of 1. This proved whole-decimetre physical cross-section rounding as an independent cause.

Production commit `4670c82dc2b2f8e4e036d6c7dc13f70c864df364` keeps semantic centreline, closest-point, edge variation, coverage, wear, vegetation and authored dm contracts unchanged, while evaluating physical crown/shoulder vertical interpolation in voxel units before forming `TargetHeightVoxels`. No route data, road-specific vocabulary, primitive, persisted vertex, or program-width growth was introduced.

## Validation evidence
Focused correction run `33434127106` passed the voxel-precision continuous-field suite and repository-derived automatic module/player validation.

Broader exact-head run `33439981648` on feature SHA `67eaee7d00efc995962ef890331625f1108d0a12` passed all 10 `WorldRoadPresentationRegressionTests`, including generic formed crown/shoulders/wear, topology-aware junctions, Trail/custom-profile reuse, sloped diagonal/chunk independence, vegetation suppression, resolved-topology authority, and existing primitive/vertex storage budgets. Repository-derived automatic validation also passed: `SpatialReservationTests` 9.41s, production integration 6.33s, reusability 6.28s, structural socket integration 6.76s, Kentridge standalone player 91.72s, spatial-reservations standalone player 30.55s, total 151.06s.

Fresh exact Kentridge player-height captures at approximately `t=64s` and `t=74s` show the required production path during CharacterMotor traversal: a coherent curved/diagonal formed road, both terrain-shaped shoulders, a real topology-resolved junction, non-flat crown/shoulder cross-section, medium/far connected-road continuity, vegetation recovery immediately outside the road influence, and the previously demonstrated in-town frontage overwrite/trench remaining solved. The former repeated piece-end bands and scale-4 lateral terraces are absent. Remaining one-voxel stepping matches the authoritative voxel resolution rather than a multi-voxel presentation defect.

Budget evidence remains within existing contracts: the regression suite explicitly protects primitive/vertex storage budgets; the corrections add no persisted/program fields or extra route/corridor authority; automatic production validation completed in 151.06s and the Kentridge player remained responsive after load, with late captured frames generally around 290-370 FPS. No budget was weakened.

## Remaining gates
1. Review final branch diff for assignment-only scope and supported metadata; keep `.github/test-request.json` off the feature branch.
2. Refresh current `origin/master`. If it advanced, merge it into `fixes/agent-3` and rerun exact affected gates/player evidence.
3. Because this evidence documentation changes the feature SHA, run one final exact-head road regression plus repository-derived automatic production validation after the master refresh/merge decision. Do not replace queued/running CI.
4. After that exact implementation head is green, set closure metadata, move this assignment directly `open/` -> `closed/`, verify master has not advanced, and push the exact feature head to `origin/master` non-force.
