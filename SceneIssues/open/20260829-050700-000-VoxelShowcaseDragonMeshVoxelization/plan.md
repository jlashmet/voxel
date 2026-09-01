# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
Deliver a reusable conventional triangle-mesh → canonical sparse-voxel authoring path, proved on the recovered detailed curved Dragon. Runtime world truth must be only discrete authored voxels; source triangles are Editor/presentation input only. Shared importer/codec/replay/metrics remain mesh-agnostic under Structures; Dragon/source/palette/comparison policy remains Showcase composition. Independent non-dragon reuse is already covered.

Built visual proof must use a module-owned standalone-player fixture plus the required matched `Mesh -> Voxels` exhibit/captures. Top-level VoxelShowcase is integration evidence, not the feature fixture.

## Proven state / blockers
Recovered source: zip SHA `f48cab5ab5b7edf6a84cc7bf14797c73d0ac61bf597ef76a587589a4522aeb0f`; contained STL 2,144,152 triangles; deterministic support-free OBJ 29,734 triangles / SHA `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`. Exact reconstruction passed CI `33451165954`. Commercial-use permission is recorded, but original upstream URL, author, and named license text remain unavailable external acceptance blockers; do not invent them.

Exact bake generation passed CI `33451568424`: 98,100 cells, 99×107×107, 0.30 source units/voxel, 594 sparse bricks, canonical SHA `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`. Runtime transport SHA `758612c8b63316e3757a7695bfdb07f99ee5709f3706c504688d657017ecc961` was repaired without changing canonical identity. Anatomy/fail-closed regressions exist. Pinned-source fidelity regression enforces symmetric p95 <= 1.5 voxels and front/side/top IoU >= 0.90 but still needs exact-SHA validation.

## Current work
`ShowcaseWorld.PlaceMountainDragon` already loads the pinned artifact and replays through normal `IStructureAuthoringSession`. The placement regression’s first CI run `33466699310` failed before reaching Dragon placement because an arbitrary test seed exhausted unrelated Kentridge planning; feature commit `5b93fd04447c2bc7d955b6366e74efa73397cbe1` switched to the shipped Showcase seed.

Current master now contains the repository module-validation schema. A module-owned Dragon fixture is committed under `Assets/Game/Composition/Showcase/Validation/MountainDragonVoxelization/`: it uses the pinned bake, normal ShowcaseWorld storage, production rendering/material rules, deterministic terrain contact, and logs placement/runtime memory. Its built-player result is not yet validated and it does not substitute for the still-missing source-vs-voxel comparison.

A CI transport write accidentally parented the previous CI commit rather than the feature SHA; run `33469292081` is queued. Leave it untouched. Once complete, issue a fresh request parented to the then-current feature head.

## Next gates
1. Green exact-SHA world-placement regression, then pinned-source fidelity regression.
2. Repair VoxelShowcase placement mode to consume `MountainDragonBakedArtifact` in Showcase composition rather than unassigned inspector text/generic codec.
3. Build matched source-mesh/voxel exhibit and ten required views; inspect built-player output at production-quality bar.
4. Prove destruction/collision/rendering share edited voxel truth and record runtime cost/blast radius.
5. Resolve upstream provenance blocker, then merge current master immediately before final exact-SHA gates and close only after every checkbox passes.
