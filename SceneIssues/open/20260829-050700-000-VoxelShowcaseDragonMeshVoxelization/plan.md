# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
Ship a reusable conventional triangle-mesh → canonical sparse-voxel authoring path, proved on the recovered detailed Dragon. Runtime truth is discrete authored voxels only; source triangles are Editor/presentation input. Shared importer/codec/replay/metrics stay mesh-agnostic under Structures; Dragon/source/palette/comparison policy stays in Showcase composition. Independent non-dragon reuse is proven.

## Material results
Source identity is pinned: ZIP SHA `f48cab5ab5b7edf6a84cc7bf14797c73d0ac61bf597ef76a587589a4522aeb0f`; contained STL SHA `a01f600705a6daf79a8828474f227251a5680d4bb8bad4aa46659f9e06cf53d6`; deterministic support-free OBJ has 29,734 triangles, SHA `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`. Commercial-use permission is recorded. Exact upstream URL, creator, and named license text remain unavailable; provenance remains an external blocker rather than an inferred substitution.

The pinned bake has 98,100 cells, 594 sparse bricks, 99×107×107 bounds, canonical SHA `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`. Exact run `33490519425` passed focused/module/built-player validation and canonical destruction/collision truth. Exact matte run `33520438248` also passed, but direct human review showed the Dragon still read as rounded because canonical DarkStone reconstruction is `SurfaceStyles.Smooth`.

The demonstrated visual defect is now addressed at the correct semantic boundary: reusable `PlaceBakedMeshStructure` accepts an optional reconstruction style and uses canonical `IStructureAuthoringSession.SetStyled`; default callers retain material-default behavior. `PlaceMountainDragon` alone requests `SurfaceStyles.Cubic`, while the validation scene alone keeps Dragon roughness at `1.0` / smoothness `0.0`. No global DarkStone or renderer default is changed. The built-player scenario requires `surface_style=7` plus the matte values.

## Remaining gates
1. Run exact-SHA focused/module built-player validation for cubic Dragon placement and directly inspect the fresh engine captures/no-exception evidence.
2. Provenance blocker: obtain exact upstream URL, creator, and named license/permission text for the currently baked source without inference.
3. Once provenance is resolved, merge current `origin/master`, run final exact-SHA gates only through `ci-test/fixes/agent-1`, inspect final captures, then close/promote non-force.
