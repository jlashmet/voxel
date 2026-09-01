# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
Ship a reusable conventional triangle-mesh → canonical sparse-voxel authoring path, proved on the recovered detailed Dragon. Runtime truth is discrete authored voxels only; source triangles are Editor/presentation input. Shared importer/codec/replay/metrics stay mesh-agnostic under Structures; Dragon/source/palette/comparison policy stays in Showcase composition. Independent non-dragon reuse is proven.

## Material results
Source identity is pinned: ZIP SHA `f48cab5ab5b7edf6a84cc7bf14797c73d0ac61bf597ef76a587589a4522aeb0f`; contained STL SHA `a01f600705a6daf79a8828474f227251a5680d4bb8bad4aa46659f9e06cf53d6`; deterministic support-free OBJ has 29,734 triangles, SHA `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`. Commercial-use permission is recorded. Exact upstream URL, creator, and named license text remain unavailable; provenance remains an external blocker rather than an inferred substitution.

The pinned bake has 98,100 cells, 594 sparse bricks, 99×107×107 bounds, canonical SHA `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`. Exact run `33490519425` passed focused/module/built-player validation and canonical destruction/collision truth. User review then demonstrated that matte shading alone still read as rounded because DarkStone reconstructed with `SurfaceStyles.Smooth`.

The visual defect is fixed at the semantic authoring boundary: reusable `PlaceBakedMeshStructure` accepts an optional reconstruction style through canonical `IStructureAuthoringSession.SetStyled`; default callers are unchanged, while `PlaceMountainDragon` requests `SurfaceStyles.Cubic`. Validation keeps Dragon roughness `1.0` / smoothness `0.0` without changing global DarkStone.

Exact run `33523442519` passed. Its built player logged `MOUNTAIN_DRAGON_BLOCK_PRESENTATION material=6 surface_style=7 roughness=1.0 smoothness=0.0`, wrote all 98,100 voxels in 44.033 ms, retained destruction/collision truth, and produced all required captures. Direct inspection of the fresh head/horns, wing, torso/base, and tail views confirms the authoritative right-side Dragon now uses discrete stepped cubic/block faces rather than the prior rounded surface.

## Remaining gates
1. Provenance blocker: obtain exact upstream URL, creator, and named license/permission text for the currently baked source without inference.
2. Once provenance is resolved, merge current `origin/master`, run final exact-SHA focused/module-player gates only through `ci-test/fixes/agent-1`, inspect final captures, then close/promote non-force.
