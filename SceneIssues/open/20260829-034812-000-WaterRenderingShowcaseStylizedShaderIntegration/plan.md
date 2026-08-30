# Plan

## Observed behavior

`WaterRenderingShowcase` has no capture frames; the assignment, package assets, and `WaterfallReference.shader` define the target. Production already has one shared liquid topology/render path: voxel liquid faces are extracted by `CpuWaterSurfaceChunkCache`/`WaterBrickMeshBatchJob`, then rendered by `VoxelRenderPass` with `Hidden/VoxelEngine/WaterSurface`. The defect is architectural and visual: engine extraction hard-codes game IDs 11/16, while the water shader hard-codes ID 16 as a special cascade and otherwise exposes only one generic animated-water look. It lacks the imported package's reusable depth fade, shallow/deep response, contact/surface foam, normal/detail motion, refraction semantics, and distinct directional river profile.

## Hypotheses and discriminator

1. **One production water seam exists.** Falsified if normal scenes/builders bind independent water materials. **Result: supported.** Visible water chunks all use the same renderer-owned material and retain only opaque material ID per vertex.
2. **The imported Stylized Water Shader contains presentation semantics not represented by production.** Falsified if equivalent profile/depth/foam/flow inputs already exist. **Result: supported.** The package material exposes deep/shallow color, depth fade, intersection/surface foam, normals, refraction, wave direction/speed/length/steepness; production does not.
3. **A waterfall can be represented as merely faster/rotated lake water.** **Result: rejected.** `WaterfallReference.shader` requires downward streaking, turbulent breakup, aeration, lip/edge/base-impact foam, and mist/spray cues.

## Selected fix

Evolve the existing shared path, not add scene forks: introduce renderer-owned, semantic water-presentation rows selected by the existing per-vertex material index; derive liquid classification from those rows instead of engine-known game IDs; install game-owned still/river/waterfall profile mappings through composition; adapt the package's color/depth/foam/detail/refraction/wave behavior and the waterfall reference semantics into the shared water shader. Preserve authoritative voxel simulation, discovery, meshing, edits, collision/swimming/buoyancy behavior, batching/culling, and normal scene authoring.

## Blast radius / cost

Touch only shared material presentation, water extraction classification, water rendering/binding, game composition/profile definitions, focused regressions, and assigned showcase evidence. Keep one shared shader/material and bounded 32-row constant data; no per-body materials or new draw architecture. Measure water draw count/overdraw and avoid added CPU allocations or unbounded turbulence/foam work.

## Remaining gates

Implement + focused production-path regressions; validate showcase and portability bodies; exact-SHA targeted CI; exact-SHA built-player `WaterRenderingShowcase`, `VoxelShowcase`, and a second normal water scene; durable time-separated/wide/near evidence; visual/cost review; pending/closed metadata and non-force master promotion.