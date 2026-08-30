# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance
Build a reusable editor-side mesh→voxel authoring pipeline that consumes ordinary Unity triangle meshes, preserves conservative triangle coverage, fills intended interiors, handles transforms/material regions/thin features, and bakes canonical sparse voxel data used by normal rendering/collision/destruction. Prove it with a legitimately redistributable detailed curved dragon and a side-by-side built `VoxelShowcase` comparison plus geometric/silhouette metrics and destruction/world-truth evidence.

## Hypotheses and discriminator
A. Existing structure authoring/storage provides the canonical runtime path; the missing piece is a reusable triangle rasterizer/interior-fill baker plus showcase presentation.
B. Existing mesh/SDF utilities already rasterize triangle meshes and should be reused/generalized.

Discriminator result: **A is supported; B is rejected on this revision.** `Assets/Editor` contains only CI tooling, `Assets/ThirdParty` contains only placeholder humanoids, and no existing mesh/SDF voxelizer is present. The canonical application seam is `VoxelEngine.Structures.Api.IStructureAuthoringSession`, whose `Set`/bulk operations write opaque voxel material indices. WorldBuilder already maps semantic materials into that seam. Collision ray/AABB code consumes voxel occupancy rather than mesh truth, and edits are routed through voxel alteration APIs. Therefore the smallest design is: deterministic editor/build conversion → serialized sparse baked cells/materials → runtime replay through the normal structure-authoring/world path. No runtime mesh dependency or second collision truth is needed.

## Constraints / blast radius
Authoritative output must be deterministic CPU-side discrete voxel occupancy/material state. Source mesh is authoring/reference only; runtime gameplay must not depend on MeshCollider/GPU/SDF/triangle truth. Expensive voxelization is offline/editor-side. Keep changes additive: new mesh-bake code plus VoxelShowcase integration; do not alter terrain/building generation semantics, storage format, global collision, or edit behavior. Bound importer memory/time by configured voxel bounds and reject oversized/open-invalid inputs before dense flood-fill work.

## Required validation
Focused production regressions cover curved closed geometry surface/interior, deterministic repeat conversion, transforms/orientation/mirroring, material mapping, thin features, dragon anatomy plausibility, normal WorldBuilder placement/occupancy, and canonical edit/collision behavior. Final exact-SHA CI is one PlayMode request with this SceneIssue so the real-player `VoxelShowcase` is built/launched and captures all required comparison views. Human review also checks surface-distance p95 <= 1.5 voxels, primary silhouette IoU >= 0.90, and recorded import/runtime cost.

## Current state
Branch `fixes/agent-1` started from `origin/master` `e95324aeaef619cb49d84bf2b07f770184bead81`. `SceneIssues/feature-readme.md` is absent; canonical `SceneIssues/README.md` is the available workflow authority. No product implementation has changed yet; next gate is a compile-valid behavior regression against the planned importer contract, committed before implementation.
