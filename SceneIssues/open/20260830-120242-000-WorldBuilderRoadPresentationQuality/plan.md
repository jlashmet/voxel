# Plan

## Acceptance
Improve the authoritative `WorldRoadNetwork` presentation without changing route/topology authority: coherent curved/diagonal edges; bounded formed cross-sections/shoulders/cut-fill; deterministic shared terrain-surface wear; topology-aware junctions; stable chunk/LOD continuity; vegetation/material/collision/destruction semantics preserved; costs within existing budgets; and exact built-player AAA validation in `KentridgePlayableSlice`.

## Current architecture / ownership
`WorldRoadProfile` + `WorldRoadResolver` own semantic route/profile intent. `WorldRoadInfluence` and `WorldRoadNetworkVoxelCatalogue` currently realize that intent as the union of independent straight resolved segments. Each bounded piece emits one generic `EmitTerrainCorridor`; `TerrainCorridorRasteriser` evaluates integer closest-point distance, grades the voxel column, and stores the same 0..31 scalar as road/terrain material-blend coverage. `SmoothSurfaceVertex` remains 32 bytes and `SmoothSurface.shader` consumes generic packed material/style/coverage; it has no road centerline, curve, or topology context. Kentridge and TopDown are existing independent network consumers.

## Root cause / falsified hypothesis
The geometry defect originates before SmoothSurface: semantic influence and physical lowering are both unions of segment-local straight corridor fields, so turns and true junctions cannot receive continuous tangent or topology-aware shaping. The rasterizer also has only core/outer radius, cut/fill, edge noise, material and seed, so it cannot express crown/camber, structured shoulders, drainage/fill support, or road-use wear. SmoothSurface reconstructs and shades the resulting generic voxel/material payload; it can preserve continuous blend coverage but cannot recover missing curve/topology/cross-section semantics. This falsifies the hypothesis that shader-only work can solve the primary edge/junction defects.

## Selected approach
Extend the existing semantic/profile -> corridor -> packed-surface production path, not route authority or rendering ownership. Add the smallest reusable presentation contract needed for cross-section/wear, and pass continuity/topology information from the resolved network into bounded corridor realization. Preserve existing generic terrain material blending and 32-byte vertex/storage contracts where possible. Junction shaping must be derived from semantic network connectivity, never proximity alone. Prove reuse with the existing TopDown consumer/fixture.

## Blast radius / cost expectations
WorldBuilder road API/lowering, generic terrain-corridor sampling/rasterization, packed terrain surface response, and focused regressions are in scope. Avoid storage-format or `SmoothSurfaceVertex` growth. Keep one bounded corridor primitive per physical piece where practical and measure definitions, voxel writes, vertex/material, world-build and streaming/LOD costs against existing budgets.

## Current commit
Feature head before implementation: `752dc4b5ebed093cd106a47ca5bc9b31c1390baf`; includes then-current `origin/master` `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470`.

## Remaining gates
Implement reusable presentation semantics and production lowering; independent reuse proof; focused deterministic regressions; budget measurement; merge current master as required; exact built-player curve/shoulder/junction/non-flat/far-field/traversal evidence; human `production-quality` review; exact-SHA targeted CI via `ci-test/fixes/agent-3`; then direct open -> closed metadata/bookkeeping and non-force promotion to master.
