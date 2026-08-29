# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote Kentridge organic roads into a reusable WorldBuilder contract: semantic intent/provenance first, deterministic terrain-aware resolution, one continuous influence for voxel grading/surface transition/ecology, reusable keep-clearance, and a bounded physical representation. Kentridge is the proving consumer; the generic module must not depend on Kentridge. Final proof is the built `VoxelShowcase` player with no new seam, wall, floating prop, startup, or runtime failure.

## Discrimination result
The original representation gap is resolved in the current branch. `Game.WorldBuilder.Api` now owns `WorldRoadProfile`, `WorldRoadIntent`, deterministic `WorldRoadResolver`, `ResolvedWorldRoad`, `WorldRoadInfluence`, and `WorldRoadNetwork`. Kentridge modern `SettlementPlan.Routes` and compatibility `Streets` both author that contract; macro `TopDownWorldRouteSpec` hard connections do too. Physical lowering is one bounded `EmitTerrainCorridor` primitive per piece, not the historical carve/core/ten-strip shoulder stack.

`TerrainCorridorRasteriser` evaluates distance, target height and 0..31 coverage per voxel column. It grades/fills/carves destructible voxels, preserves local material outside road coverage, and persists the same RoadInfluence detail used for presentation. Kentridge vegetation samples that same network scalar and deterministically thins existing regional ecology. Generic `TrySampleClearance` extends beyond the grading shoulder for later placement consumers.

## Terrain flags
The generic resolver supports Blocked/Water/Reserved/Pass flags and profile crossing policy. Current production `TerrainQuery` exposes height/slope only; Kentridge `PlannedRoute` and top-down route specs carry no terrain-classification map. `WorldRoadVoxelTerrain.FlagsAtDm` therefore correctly returns `None` and documents the missing authority rather than fabricating crossings. Regression fixtures exercise real barrier/crossing behavior through the generic interface.

## Regression state
The pre-merge targeted run failed before product execution because the new EditMode regression omitted the `MountingForce.WorldGen` namespace. That harness is repaired. The focused class now covers bounded one-corridor lowering, semantic↔physical influence equality, continuous shoulders, ecology recovery, production vegetation consumption, deterministic terrain rerouting/grade/cut-fill, crossing policy, and generic Kentridge keep-clearance. `KentridgeOrganicLayoutTests` retains named-landmark/diagonal/connectivity traceability.

## Blast radius / cost
No per-frame road generation, road GameObjects, dense world masks, storage-width changes, or surface-vertex stride changes. Road work occurs during deterministic planning/catalogue generation. Each bounded physical piece budgets one primitive; definitions remain under `FeatureBudget`. Final CI/player evidence must still quantify generated definitions/primitives, bake/build behavior, residency/runtime health, and visible LOD/streaming continuity.

## Remaining gates
Refresh/merge current master, run the single final exact-SHA request on `ci-test/fixes/agent-1`, inspect its focused regression and available artifacts, then run/verify repository-supported built-player `VoxelShowcase` evidence. Update tasks/metadata only from actual green gates; then promote open→pending→closed and non-force merge the exact feature head to master.
