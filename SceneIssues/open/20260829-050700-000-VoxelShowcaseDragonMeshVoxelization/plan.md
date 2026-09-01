# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
Ship a reusable conventional triangle-mesh → canonical sparse-voxel authoring path, proved on the recovered detailed Dragon. Runtime truth is discrete authored voxels only; source triangles are Editor/presentation input. Shared importer/codec/replay/metrics stay mesh-agnostic under Structures; Dragon/source/palette/comparison policy stays in Showcase composition. Independent non-dragon reuse is proven.

## Material results
Source identity is pinned: ZIP SHA `f48cab5ab5b7edf6a84cc7bf14797c73d0ac61bf597ef76a587589a4522aeb0f`; contained STL SHA `a01f600705a6daf79a8828474f227251a5680d4bb8bad4aa46659f9e06cf53d6`; deterministic support-free OBJ has 29,734 triangles, SHA `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`. Commercial-use permission is recorded. Exact upstream URL, creator, and named license text remain unavailable: prior conversation/Library retrieval plus exact-hash/filename public searches found no match. The historical Delatronic/BlendSwap Dragon is now publicly retrievable with explicit CC-BY attribution, but it is different geometry; its license cannot be applied to the uploaded source, and substituting it would invalidate the exact-source bake/fidelity/visual/destruction/cost evidence and require full revalidation. Provenance therefore remains an external blocker rather than an inferred substitution.

The pinned bake has 98,100 cells, 594 sparse bricks, 99×107×107 bounds, canonical SHA `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`. Fidelity and ten-view built visual acceptance passed earlier exact runs.

Exact targeted run `33490519425` passed focused tests, automatic module validation, Dragon/Kentridge built players, top-level replay, previews, and final status. The Dragon player placed all 98,100 voxels through normal ShowcaseWorld authoring, recorded 60.122 ms placement and 35,656,896 / 4,999,999,488 storage bytes without pressure, then changed a torso target from material 6→0 via canonical destruction; collision changed blocked→unblocked and source colliders were zero. Durable values are in `verification-ci-33490519425.txt`.

VoxelShowcase placement composition is repaired by `b4b87cefcc1174d9c43cebc35b62d3eb62cc2def`: it loads `MountainDragonBakedArtifact` rather than an optional inspector asset. One-shot control ownership is covered by the production router tests; the issue does not require a new synthetic-keyboard harness.

## Remaining gates
1. Provenance blocker: obtain exact upstream URL, creator, and named license/permission text for the currently baked uploaded source without inference.
2. Once that blocker is resolved, merge current `origin/master` into `fixes/agent-1`; current branches are substantially diverged, so final validation must run after the merge.
3. Run final exact-SHA focused/module-player gates only through `ci-test/fixes/agent-1` and directly inspect final captures/no-exception evidence.
4. Only then set fixed metadata, move this assignment `open` → `closed`, fetch/merge any newly advanced master, and non-force promote the exact feature head to `origin/master`.
