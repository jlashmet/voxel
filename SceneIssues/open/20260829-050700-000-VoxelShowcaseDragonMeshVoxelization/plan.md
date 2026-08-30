# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance
Build a reusable editor-side mesh→voxel authoring pipeline that consumes ordinary Unity triangle meshes, preserves conservative triangle coverage, fills intended interiors, handles transforms/material regions/thin features, and bakes canonical sparse voxel data used by normal rendering/collision/destruction. Prove it with a legitimately redistributable detailed curved dragon and a side-by-side built `VoxelShowcase` comparison plus geometric/silhouette metrics and destruction/world-truth evidence.

## Initial hypotheses
A. Existing voxel stamp/structure authoring and sparse storage already provide most of the bake/runtime path; the missing piece is a reusable triangle rasterization/interior-fill baker plus showcase presentation.
B. Existing mesh/SDF utilities already rasterize triangle meshes, and the smallest correct solution is to generalize/reuse them rather than add a new conversion stack.

The first discriminator is architecture/source tracing: search current mesh import, voxel stamp serialization, structure placement, material mapping, showcase composition, collision/edit APIs, and any prior dragon/SDF work. Separately verify a redistributable detailed source asset and record license/metadata before committing it.

## Constraints
Authoritative output must be deterministic CPU-side discrete voxel occupancy/material state. The source mesh is authoring/reference only; runtime gameplay must not depend on MeshCollider/GPU/SDF/triangle truth. Expensive voxelization is offline/editor-side. Preserve current world voxel semantics and budgets.

## Required validation
Focused production regressions must cover curved closed geometry surface coverage/interior fill, deterministic repeat conversion, transforms/orientation, material mapping, thin features, dragon anatomy plausibility, normal WorldBuilder/sparse placement, and canonical edit/collision behavior. Final exact-SHA CI must be PlayMode with this SceneIssue so the real-player `VoxelShowcase` is built and launched. Human review must inspect matched front/side/rear/three-quarter/top comparisons and head/wing/feet/tail close-ups, plus surface-distance and silhouette-IoU evidence and runtime/import cost.

## Current state
Branch `fixes/agent-1` starts from current `origin/master` `e95324aeaef619cb49d84bf2b07f770184bead81`. `SceneIssues/feature-readme.md` does not exist at this revision; canonical `SceneIssues/README.md` is the available workflow authority. No product implementation has been changed yet.
