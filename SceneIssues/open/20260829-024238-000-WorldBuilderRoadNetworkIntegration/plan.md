# Plan

## Observed gap / acceptance
- `TopDownWorldLayout`/Mounting Force connectivity and Kentridge physical Dirt roads exist, but no proven shared contract carries one semantic road through route resolution, voxel grading, surface transition, vegetation, and travel/navigation consumers.
- Acceptance requires deterministic terrain-aware routing, one shared road influence, continuous Dirt↔local-terrain shoulders, ecology falloff, Kentridge migration, semantic consumer access, built `KentridgePlayableSlice` visual proof, and measured blast radius/cost.

## Competing hypotheses
1. **Composition gap:** current road generation is already reusable enough; the missing work is wiring macro connectivity/profile data into the existing production road primitive plus exposing resolved semantics to consumers.
2. **Representation gap:** current Kentridge roads are fundamentally scene/local strip geometry, so a compact route/influence model and shared terrain-coverage path must be introduced before macro integration can be correct.

**Next discriminator:** trace current owners for macro layout, WorldBuilder composition, roads/streets, voxel density/materials, vegetation, and streaming; compare them with historical commits `336cb6e63e19bc6039f3f89bb4d2056e2d0efb60` and `8cd28a5ea7133a4012a17112375f70384bee79ec`. Hypothesis 1 is falsified if road deformation/presentation is encoded as Kentridge-specific primitives or independent subsystem approximations.

## Selected design constraints
- Logical endpoint connectivity remains separate from resolved geometry.
- Road profiles carry width/shoulder/material/grade/cut-fill/edge/ecology/traversal intent.
- Deterministic resolver produces compact resolved centerline data and rejects/reroutes invalid crossings.
- One chunk-safe analytic influence drives grading, surface coverage, and vegetation falloff.
- Reuse/generalize terrain material/coating machinery; preserve exposed-top/coarse-slope safety and avoid a road-only shader or dense splat stack.
- Preserve resolved road data for navigation/map/travel/NPC consumers.

## Validation gates
- Focused behavioral regression covers semantic→resolved→physical traceability, determinism, grade/cut-fill, blocked route handling, shared influence, monotonic shoulder coverage, vegetation recovery, seam continuity, consumer access, and Kentridge connectivity.
- Exact-SHA targeted CI is green.
- Exact-SHA built-application harness launches `Assets/Scenes/KentridgePlayableSlice.unity` and durable evidence proves endpoint connection, player traversal, natural shoulders on slope, vegetation recovery, and no medium/far chunk/LOD seams.
- Measure route/world-build cost, voxel/brick work, primitive/GameObject count, memory, CPU/GPU impact, and streaming/LOD behavior without weakening budgets.

Current source SHA: `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c`.
