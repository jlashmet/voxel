# Plan — Water Rendering Showcase Stylized Shader Integration

## Observed state / acceptance
- Ticket has no captures; `issue.json`, the supplied `Assets/Stylized Water Shader/` package, and `WaterfallReference.shader` define the target.
- The issue folder is missing the normal `repro.json`, `expected.json`, and `replay.json` contract files; restore those before capture/CI work.
- Production water must keep the existing voxel/world-authoring geometry/gameplay path while replacing its presentation globally with one reusable stylized renderer.
- Add built `WaterRenderingShowcase` through standard water authoring, covering still/deep, shoreline, river, waterfall, terrain/structure contact, near/wide and time-separated views. Also prove replacement in existing game scenes.

## Competing hypotheses / discriminator
1. Existing production water already has a single material-selection seam, so integration is primarily a shared renderer/profile upgrade. Falsified if water materials/shaders are instantiated or selected independently by scenes/builders.
2. The package Shader Graph assumptions cannot be driven by current voxel-water data, requiring new reusable profile/render semantics for flow, shoreline foam/depth, and waterfall behavior. Falsified if the standard production renderer already exposes equivalent body/profile inputs.
3. Waterfall quality can reuse the horizontal surface model with only orientation/speed. Expected false: the approved reference requires turbulence, aeration, edge/lip/base foam and mist semantics beyond rotation/speed.

## Next discriminator
Trace standard water authoring -> mesh/discovery -> renderer/material/shader binding and all current consumers. Inspect the package graph/support assets and `WaterfallReference.shader`, then identify the smallest engine-owned profile contract and renderer seam that can drive all required cases without scene-local materials/meshes.

## Constraints / blast radius
Preserve collision, buoyancy/swimming/wading, discovery, streaming, edits and diagnostics. No per-water-voxel objects or per-body unique materials. Retain culling/batching and device budgets; quantify CPU/GPU/memory/variants/draw-call impact. No workflow/package changes unless proven necessary.

## Remaining gates
Restore issue contract; implement shared production path + showcase; add production-path portability regressions; validate shader/build reliability; exact-SHA targeted CI; exact built showcase motion evidence; built existing-scene evidence (VoxelShowcase + another water scene if available); inspect visual quality/cost; then pending/closed bookkeeping and non-force master promotion.
