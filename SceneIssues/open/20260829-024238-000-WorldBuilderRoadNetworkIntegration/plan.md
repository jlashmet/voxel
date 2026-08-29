# Plan

## Observed gap / acceptance
- `TopDownWorldLayout`/Mounting Force connectivity and Kentridge physical Dirt roads exist, but no proven shared contract carries one semantic road through route resolution, voxel grading, surface transition, vegetation, and travel/navigation consumers.
- Acceptance requires deterministic terrain-aware routing, one shared road influence, continuous Dirt↔local-terrain shoulders, ecology falloff, Kentridge migration, semantic consumer access, built `KentridgePlayableSlice` visual proof, and measured blast radius/cost.

## Competing hypotheses
1. **Composition gap:** current road generation is reusable enough; only macro connectivity/profile wiring and semantic exposure are missing.
2. **Representation gap:** current Kentridge physical roads are local strip geometry, so a compact route/influence model and shared terrain-coverage path are required.

## Discriminator result
**Representation gap selected.** Current Kentridge planning already emits semantic `PlannedRoute` data (including diagonal legs), while `KentridgeTownSurfaceCatalogue` still realizes legacy `Streets` through Kentridge-specific axis-aligned carve/fill/ramp programs and five fixed grassy shoulder bands per side. Historical commit `336cb6e63e19bc6039f3f89bb4d2056e2d0efb60` confirms the ten-strip shoulder representation. Commit `8cd28a5ea7133a4012a17112375f70384bee79ec` establishes the coarse-LOD invariant that exposed +Y cap material must remain preferred on layered terrain. The shared capability therefore needs semantic road/profile + deterministic resolved geometry + one analytic influence, with a generic diagonal-to-voxel realization path and no restoration of stale package paths.

## Selected design constraints
- Logical endpoint connectivity remains separate from resolved geometry.
- Road profiles carry width/shoulder/material/grade/cut-fill/edge/ecology/traversal intent.
- Deterministic resolver produces compact resolved centerline data and explicitly rejects/reroutes invalid crossings.
- One chunk-safe analytic influence drives grading, surface coverage, and vegetation falloff.
- Regional ecology remains authoritative; road influence is only a local modifier.
- Reuse/generalize terrain material/coating machinery; preserve exposed-top/coarse-slope safety and avoid a road-only shader or dense splat stack.
- Preserve semantic/resolved road data for navigation/map/travel/NPC consumers.

## Blast-radius decision before implementation
- Assembly ownership stays acyclic: reusable semantic/profile/resolved-route/influence math belongs in engine-free `Game.WorldBuilder.Api`; `MountingForce.WorldGen.Core` remains zero-dependency; the existing voxel assembly adapts both modern `SettlementPlan.Routes`/legacy `Streets` and macro `TopDownWorldRouteSpec` into the shared road representation.
- Voxel storage and surface vertex stride do **not** need expansion. `VoxelSurfaceSemantics.Detail` already stores a 5-bit scalar, `TransvoxelTopologyJob.Pack` preserves the complete high semantics byte, and `SmoothSurface.shader` already decodes that scalar as `surfaceDetail`.
- The current shape bytecode exposes style/coating but not varying detail, while the rasteriser already owns clipped, deterministic column edits. Add a new backward-compatible generic terrain-corridor opcode/mode rather than changing existing emit operand counts. A corridor primitive carries resolved A/B elevations plus core/outer radii; rasterisation computes distance once per column, grades toward the resolved elevation, and derives the same 0..31 influence scalar for surface presentation. This is one compact primitive per resolved segment instead of the legacy eleven-strip shoulder stack.
- Generalize coating presentation rather than adding a road shader branch: a coating row may optionally reference a configured secondary base-material presentation row and multiply its blend by the authored 5-bit detail scalar. The secondary material path reuses full material albedo/texture/normal/roughness response; existing coatings remain unchanged unless explicitly configured for detail-driven material coverage.
- Physical fill in a graded transition preserves the sampled local terrain base material where possible. Road identity remains authoritative in the semantic/resolved road profile, while the renderer presents the road material as secondary coverage; this avoids smearing a grass base material down exposed slopes and keeps the existing exposed-top/slope-material selection invariant intact.
- `TopDownWorldVoxelCatalogue` and Kentridge surface realization must both emit the shared corridor primitive. `RegionCorridorCatalogue` may retain river/bridge crossing geometry, but road semantics/centerline cannot remain a second fixed-axis road solver.

## Cost expectations to verify
- Primitive count should fall materially from the old Kentridge representation: legacy road pieces budget one carve plus Dirt core plus ten shoulder strips (and ramps) versus one shared corridor primitive per resolved segment plus plaza/crossing geometry.
- No per-segment GameObjects, dense world masks, voxel-cell widening, or surface-vertex stride growth are expected.
- Route resolution and influence queries operate on compact polylines and deterministic integer/fixed-point math; final validation must measure resolver/world-build time, voxels/brick mutations, primitive count, resident memory, CPU/GPU, and LOD/streaming impact rather than relying on this expectation.

## Validation gates
- Focused regressions cover semantic→resolved→physical traceability, determinism, grade/cut-fill, blocked routes, shared influence, continuous shoulders, vegetation recovery, seam continuity, consumer access, and Kentridge connectivity.
- Exact-SHA targeted CI is green.
- Exact-SHA built application launches `Assets/Scenes/KentridgePlayableSlice.unity`; durable evidence proves endpoint connection, player traversal, natural sloped shoulders, vegetation recovery, and no medium/far chunk/LOD seams.
- Measure route/world-build cost, voxel/brick work, primitive/GameObject count, memory, CPU/GPU impact, and streaming/LOD behavior without weakening budgets.

Investigation-start source SHA: `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c`.
Implementation planning head before source edits: `c78b13f15880115b36fb846121f091081f92ee22`.
