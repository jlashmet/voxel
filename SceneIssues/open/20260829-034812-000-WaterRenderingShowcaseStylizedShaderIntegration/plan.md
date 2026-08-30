# Plan

## Observed behavior

`WaterRenderingShowcase` has no capture frames; the assignment, package assets, and `WaterfallReference.shader` define the target. Production already has one shared liquid topology/render path: voxel liquid faces are extracted by `CpuWaterSurfaceChunkCache`/`WaterBrickMeshBatchJob`, then rendered by `VoxelRenderPass` with `Hidden/VoxelEngine/WaterSurface`. The defect is architectural and visual: engine extraction hard-codes game IDs 11/16, while the water shader hard-codes ID 16 as a special cascade and otherwise exposes only one generic animated-water look. It lacks the imported package's reusable depth fade, shallow/deep response, contact/surface foam, normal/detail motion, refraction semantics, and distinct directional river profile.

The resumed branch was refreshed from current `origin/master` in merge commit `20adc71ba46ac929136c7f95c042fcb62a62a2e0` with no water-path conflicts. Resume audit also found two concrete compile blockers left by the partial implementation: `GameMaterialRenderingTests` references a missing `WaterRenderingMaterialBinding`, and its canonical shader-path test references an absent `Assets/Game/Materials/Shaders/StylizedWater.shader`. These are required implementation work, not test expectations to delete or weaken.

## Hypotheses and discriminator

1. **One production water seam exists.** Falsified if normal scenes/builders bind independent water materials. **Result: supported.** Visible water chunks all use the same renderer-owned material and retain only opaque material ID per vertex.
2. **The imported Stylized Water Shader contains presentation semantics not represented by production.** Falsified if equivalent profile/depth/foam/flow inputs already exist. **Result: supported.** The package material exposes deep/shallow color, depth fade, intersection/surface foam, normals, refraction, wave direction/speed/length/steepness; production does not.
3. **A waterfall can be represented as merely faster/rotated lake water.** **Result: rejected.** `WaterfallReference.shader` requires downward streaking, turbulent breakup, aeration, lip/edge/base-impact foam, and mist/spray cues.
4. **The resumed branch is compile-ready and only needs validation.** **Result: rejected.** Focused tests currently reference a missing shared material binding type and a missing canonical shader asset. The implementation must restore those production artifacts before build/CI gates are meaningful.

## Selected fix

Evolve the existing shared path, not add scene forks: introduce renderer-owned, semantic water-presentation rows selected by the existing per-vertex material index; derive liquid classification from those rows instead of engine-known game IDs; install game-owned still/river/waterfall profile mappings through composition; adapt the package's color/depth/foam/detail/refraction/wave behavior and the waterfall reference semantics into the shared water shader. Preserve authoritative voxel simulation, discovery, meshing, edits, collision/swimming/buoyancy behavior, batching/culling, and normal scene authoring.

`CpuWaterSurfaceChunkCache` and the voxel meshing/extraction path remain the sole production geometry/deformation authority. The shader/binding layer may animate presentation details and profile flow but must not create a second scene-local mesh/deformation authority. Add the missing shared `WaterRenderingMaterialBinding` (or smallest equivalent) to create one reusable canonical water material per renderer lifecycle, apply deterministic profile/property data, and release it explicitly; do not instantiate materials per frame. Add/promote the missing player-build shader asset under `Assets/Game/Materials/Shaders/` with the exact shader name selected by composition and the smallest required Unity metadata/retention path.

## Blast radius / cost

Touch only shared material presentation, water extraction classification, water rendering/binding, game composition/profile definitions, focused regressions, and assigned showcase evidence. Keep one shared shader/material and bounded 32-row constant data; no per-body materials or new draw architecture. Measure water draw count/overdraw and avoid added CPU allocations or unbounded turbulence/foam work. Explicitly verify gameplay liquid consumers (swimming/buoyancy/collision/discovery/streaming/edits/diagnostics) still derive behavior from canonical liquid semantics rather than the presentation refactor.

## Remaining gates

Complete consumer tracing; repair missing binding/shader artifacts; implement profile-driven shared rendering/classification; add focused production-path/cache/lifecycle/waterfall regressions; validate showcase and portability bodies; run static/build gates; exact-SHA targeted CI; exact-SHA built-player `WaterRenderingShowcase`, `VoxelShowcase`, and a second normal water scene; durable time-separated/wide/near evidence; visual/cost review; pending/closed metadata and non-force master promotion.
