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

## Validation gates
- Focused regressions cover semantic→resolved→physical traceability, determinism, grade/cut-fill, blocked routes, shared influence, continuous shoulders, vegetation recovery, seam continuity, consumer access, and Kentridge connectivity.
- Exact-SHA targeted CI is green.
- Exact-SHA built application launches `Assets/Scenes/KentridgePlayableSlice.unity`; durable evidence proves endpoint connection, player traversal, natural sloped shoulders, vegetation recovery, and no medium/far chunk/LOD seams.
- Measure route/world-build cost, voxel/brick work, primitive/GameObject count, memory, CPU/GPU impact, and streaming/LOD behavior without weakening budgets.

Current source SHA at investigation start: `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c`.
