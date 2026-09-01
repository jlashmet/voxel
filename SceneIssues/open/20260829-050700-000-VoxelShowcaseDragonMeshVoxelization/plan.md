# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
Ship a reusable conventional triangle-mesh → canonical sparse-voxel authoring path, proved on the recovered detailed Dragon. Runtime truth is discrete authored voxels only; source triangles are Editor/presentation input. Shared importer/codec/replay/metrics stay mesh-agnostic under Structures; Dragon/source/palette/comparison policy stays in Showcase composition. Independent non-dragon reuse is proven.

## Material results
Source identity is pinned: ZIP SHA `f48cab5ab5b7edf6a84cc7bf14797c73d0ac61bf597ef76a587589a4522aeb0f`; contained STL SHA `a01f600705a6daf79a8828474f227251a5680d4bb8bad4aa46659f9e06cf53d6`; deterministic support-free OBJ has 29,734 triangles, SHA `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`. Commercial-use permission is recorded. Exact upstream URL, creator, and named license text remain unavailable: prior conversation/Library retrieval plus exact-hash/filename public searches found no match. The historical Delatronic/BlendSwap Dragon is different geometry; its license cannot be applied to the uploaded source. Provenance remains an external blocker rather than an inferred substitution.

The pinned bake has 98,100 cells, 594 sparse bricks, 99×107×107 bounds, canonical SHA `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`. Fidelity and ten-view built visual acceptance passed earlier exact runs. Exact targeted run `33490519425` passed focused tests, automatic module validation, Dragon/Kentridge built players, top-level replay, previews, and final status; canonical destruction changed material 6→0 and collision blocked→unblocked with zero source colliders.

User review of the real built capture demonstrated excessive sheen on the authoritative Dragon. The validation composition now clones the production game material presentation and overrides only Dragon/DarkStone roughness to `1.0` (smoothness `0.0`) before the first rendered frame; shared renderer and global DarkStone defaults remain unchanged. The player scenario requires a durable matte-presentation marker.

## Remaining gates
1. Run an exact-SHA Dragon built-player validation for the matte correction and directly inspect captures/no-exception evidence.
2. Provenance blocker: obtain exact upstream URL, creator, and named license/permission text for the currently baked uploaded source without inference.
3. Once provenance is resolved, merge current `origin/master`, run final exact-SHA focused/module-player gates through `ci-test/fixes/agent-1`, inspect final captures, then close/promote non-force.
