# Plan

## Acceptance
Improve the authoritative `WorldRoadNetwork` presentation without changing route/topology authority: coherent curved/diagonal edges; bounded formed cross-sections/shoulders/cut-fill; deterministic shared terrain-surface wear; topology-aware junctions; stable chunk/LOD continuity; vegetation/material/collision/destruction semantics preserved; costs within existing budgets; and exact built-player AAA validation in `KentridgePlayableSlice`.

## Current architecture / ownership
`WorldRoadProfile` + `WorldRoadResolver` own semantic route/profile intent. `WorldRoadPresentationPath` is a deterministic presentation-only refinement; `WorldRoadInfluence` and `WorldRoadNetworkVoxelCatalogue` consume it while leaving resolved route points unchanged. Each bounded piece still emits one generic `EmitTerrainCorridor`; `TerrainCorridorRasteriser` evaluates the shared continuous influence, bounded crown/shoulder profile and deterministic surface-detail wear into voxel-authoritative terrain. `SmoothSurfaceVertex` remains 32 bytes and `SmoothSurface.shader` consumes the existing generic packed material/style/coverage channels. Kentridge and TopDown remain independent network consumers.

## Root cause / falsified hypothesis
The original geometry defect originated before SmoothSurface: semantic influence and physical lowering were unions of segment-local straight corridor fields, so turns and true junctions could not receive continuous tangent or topology-aware shaping. The rasterizer also lacked crown/shoulder/wear response. SmoothSurface only reconstructs and shades the resulting generic voxel/material payload, so shader-only work could not recover those missing semantics.

A follow-up architecture pass found one correctness gap in the first implementation: `WorldRoadPresentationPath` supported explicit junction preservation, but production network sampling and voxel lowering called its route-only overload, so an interior point shared by another route could still be rounded away. The production path now passes the network's exact resolved-vertex junction set into semantic influence, local-frame sampling, clearance sampling, and physical piece lowering. Nearby overlap still cannot invent topology.

## Selected approach
Keep the existing semantic/profile -> presentation path -> bounded corridor -> packed-surface production path. Curve refinement and cross-section shaping are reusable deterministic semantics, not Kentridge geometry. Junction behavior comes only from resolved network connectivity. Generic corridor wear stays in the existing persisted surface-detail channel; no road-only shader/material-instance island or vertex/storage growth is introduced. Reuse is exercised by generic trail/network fixtures and remains available to the existing TopDown road consumer.

## Blast radius / cost expectations
WorldBuilder road API/lowering, generic terrain-corridor sampling/rasterization, packed terrain surface response, and focused regressions are in scope. Storage format and `SmoothSurfaceVertex` remain unchanged. Keep one bounded corridor primitive per physical piece where practical and measure definitions, voxel writes, vertex/material, world-build and streaming/LOD costs against existing budgets.

## Current commit
Feature head after topology-aware production-path correction: `c8fb85c8970dbebd82a48996198594094d452346`; branch base remains current with `origin/master` `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` as of this update.

## Remaining gates
Finish focused deterministic regression coverage for sloped/both-side terrain integration, vegetation/non-road behavior, persistence/chunk/LOD invariants and budgets; merge current master as required; exact built-player curve/shoulder/junction/non-flat/far-field/traversal evidence; human `production-quality` review; exact-SHA targeted CI via `ci-test/fixes/agent-3`; then direct open -> closed metadata/bookkeeping and non-force promotion to master.
