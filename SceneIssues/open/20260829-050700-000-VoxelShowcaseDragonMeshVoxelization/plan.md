# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
`VoxelShowcase` needs a reusable **authoring-time** conventional triangle-mesh → canonical voxel path, proved on a legitimately redistributable detailed curved winged dragon. Runtime must load/replay only baked sparse cells through normal voxel storage so rendering, collision, edits, and destruction share world truth. Matched source/voxel built-player evidence must be judged `production-quality`.

## Architecture / discriminator
Hypothesis A: `IStructureAuthoringSession` and normal structure storage remain authoritative; the missing capability is deterministic offline conversion plus composition wiring. Hypothesis B: an existing arbitrary mesh/SDF importer should be generalized. Result: **A supported; B rejected.** Generic implementation remains under `Structures/Runtime/MeshImport`: transformed triangles → conservative surface coverage → bounded closed-interior fill → deterministic material ownership → ordered sparse bake/codec → canonical authoring replay. Unity hierarchy/skinned adaptation is editor-only. Shared importer/metrics APIs contain no dragon/source/showcase policy, and independent non-dragon reuse is covered.

Input ownership is also separated: `StructurePlacementSelection` owns one-shot selection/commit state and `StructurePlacementInputRouter` owns control-consumption policy. Live `VoxelShowcase` call-site wiring remains incomplete because the connected whole-file writer cannot safely edit the 58 KB scene owner from truncated reads; do not bypass this with reflection or a competing controller.

## Source / blockers
The user-provided `mountain_dragon_supported.zip` contains a 2,144,152-triangle CHITUBOX STL. Connectivity cleanup retained a 1,763,914-triangle dragon/scenic-base component. Deterministic 0.5-unit vertex clustering produced a conventional OBJ with 13,431 vertices / 29,734 triangles, 860,349 bytes, SHA-256 `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`, gzip SHA-256 `fd2f8253fcf5bc32b275640448511f59d20dcc7d01c307f99124b224431892d4`.

All 15 contiguous split transfer parts `part00`–`part14` are now committed. The Editor-only reconstruction adapter pins the gzip hash, OBJ hash, and exact byte count and fails closed on missing/non-contiguous/changed data; an EditMode reconstruction regression now executes that contract. Keep the transfer-integrity task open until an exact-SHA test run proves the committed payload reconstructs to those pinned values; do not infer integrity from filenames alone. Runtime never reads source triangles.

Exact provenance is independently blocked: the user states the model was free and usable, but source URL, author, and named license/permission text are unavailable. Do not close licensing acceptance without them.

The STL/derived OBJ has no standard source material regions. Composition now deterministically maps unmaterialed source/interior cells to canonical `DarkStone` via `MountainDragonPalettePolicy`/`MountainDragonAuthoringPolicy`; this is explicit showcase palette mapping, not preservation of absent source color. Existing/future real source material IDs pass through unchanged.

## Current state / next work / gates
The shared importer now has explicit `VoxelShellFill` semantics independently covered by non-dragon topology regressions: topologically open input may fill only when conservative rasterization itself encloses voxel-space cells, while genuinely open rasters remain unfilled and `Reject`/`SurfaceOnly` semantics remain unchanged. The dragon composition explicitly opts into that policy at `0.30` source units/voxel with X/Z <=127, Y <=511 and no global thin-feature dilation. Actual artifact generation, dragon-anatomy regression, placement, comparison, metrics, destruction validation, and visual review remain pending exact archive verification and the relevant implementation/evidence work; provenance remains an independent closure blocker.

Branch was last merged with `origin/master` `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470`; final promotion must refresh master again. Final gates remain: exact provenance, exact baked artifact, live VoxelShowcase selection/comparison wiring, quantitative metrics/cost, built-player destruction/world truth, all required matched captures, and direct `production-quality` human visual review. Only after all gates pass should master be refreshed, the exact-SHA request be issued through `ci-test/fixes/agent-1`, the issue move `open` → `closed`, and the exact feature head be promoted non-force.
