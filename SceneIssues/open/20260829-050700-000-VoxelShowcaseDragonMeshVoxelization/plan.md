# Plan — VoxelShowcase Dragon Mesh Voxelization

## Observed behavior / acceptance
`VoxelShowcase` has no reusable production path from a conventional detailed triangle mesh to canonical voxel world data. This feature must add an **authoring-time** generic mesh→voxel importer, prove it on a downloaded licensed curved winged dragon, store/replay only baked sparse voxel cells at runtime, and present matched source/voxel comparison evidence. Built-player visual output must be judged `production-quality`.

## Hypotheses / discriminator
A. Existing `IStructureAuthoringSession`/structure storage is the correct runtime truth; missing work is deterministic authoring conversion plus showcase wiring.
B. An existing arbitrary mesh/SDF importer should be generalized instead.

Result: **A supported; B rejected.** No existing arbitrary production importer owns this conversion; collision/edit/render derive from canonical voxel storage.

## Implementation
Keep conversion additive under `Structures/Runtime/MeshImport`: transformed vertices → conservative triangle coverage → bounded exterior flood fill for closed meshes → deterministic material ownership → ordered sparse cells/codec → `IStructureAuthoringSession` replay. Preflight indices, finite source/transform, topology, coordinate encodability, and dense working-set limits before allocation. Unity hierarchy/skinned-mesh adaptation remains editor-only. Runtime must not voxelize triangles or use source-mesh/MeshCollider gameplay truth.

Existing tests cover curved fill/surface, determinism, transforms/mirroring, materials, thin sheets, malformed/oversized/non-finite input, topology policy, codec replay, canonical placement, and one-shot selection. Remaining regressions are dragon-artifact anatomy/metrics and built-player behavior.

## Source / discriminator
Selected: Delatronic **Dragon**, Blend Swap 15891 (historic 80766), CC BY 3.0. Bitterli redistribution is independently mirrored by Microsoft PBRT/PLY and `gkjohnson/3d-demo-data`; the latter states its Draco optimization preserves scene graph/geometry without simplification. `ErfanMo77/gltf-research-scenes` independently converts the same Blend Swap/Bitterli source, records 16 meshes / 831,812 scene triangles, and exposes uncompressed glTF metadata plus original PLYs.

Transfer discriminator: prefer the compact 1,651,276-byte `gkjohnson` `dragon.glb` if exact bytes can be vendored and decoded offline without adding runtime dependencies; otherwise use the Microsoft/Erfan source PLYs. Current connectors can verify/base64 binary objects, but cannot yet transfer multi-megabyte payloads losslessly into this repo. Do **not** substitute lower-detail geometry to fit tooling.

## Blast radius / gates
Scope stays mesh-import, this showcase, focused tests/assets/evidence, and this issue folder. No terrain/storage/global-scale/workflow/other-issue changes. Conversion cost is offline; runtime cost is sparse decode/replay only. After source+bake/showcase/metrics/destruction evidence are complete: refresh master, make the single exact-SHA request through `ci-test/fixes/agent-1`, inspect all required built-player views, require production-quality visual acceptance, complete pending bookkeeping, close, merge latest master, and non-force promote exact feature head.