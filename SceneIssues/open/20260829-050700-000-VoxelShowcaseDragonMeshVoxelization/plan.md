# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
`VoxelShowcase` needs a reusable **authoring-time** conventional triangle-mesh → canonical voxel path, proved on a downloaded redistributable detailed curved winged dragon. Runtime must load/replay only baked sparse cells through normal voxel storage so rendering, collision, edits, and destruction share world truth. Matched source/voxel built-player evidence must be judged `production-quality`.

## Architecture / discriminator
Hypothesis A: `IStructureAuthoringSession` and normal structure storage remain authoritative; the missing capability is deterministic offline conversion plus composition wiring. Hypothesis B: an existing arbitrary mesh/SDF importer should be generalized. Result: **A supported; B rejected.** No existing arbitrary production importer owns this conversion.

Generic implementation remains under `Structures/Runtime/MeshImport`: transformed triangles → conservative surface coverage → bounded closed-interior fill → deterministic material ownership → ordered sparse bake/codec → canonical authoring replay. Unity hierarchy/skinned adaptation is editor-only. Shared importer/metrics APIs contain no dragon/source/showcase policy. Independent non-dragon reuse is covered by `MeshVoxelizationReuseTests.IndependentBoxFixture_UsesImporterCodecAndCanonicalAuthoringPath`. Runtime-authority review confirms showcase placement consumes only `BakedVoxelStructure`, the Unity source adapter is Editor-only, and no `MeshCollider` fallback is present.

The selection seam is input-device independent: `StructurePlacementSelection` owns one-shot selection/commit state and `StructurePlacementInputRouter` owns control-consumption policy. Remaining live `VoxelShowcase` call-site wiring is not claimed complete: the connected GitHub writer only supports whole-file replacement while its read response truncates the 58 KB scene owner, so rewriting that file here would risk unrelated code loss. Do not bypass this with reflection or a second competing input controller.

## Source / current discriminator
The previous Delatronic source-transfer blocker is superseded by the user-provided `mountain_dragon_supported.zip`. Exact upload, cleanup, derivation hashes and geometry counts are recorded in `verification-uploaded-source.txt`.

The uploaded binary STL is a 2,144,152-triangle CHITUBOX supported print export. Connectivity cleanup retains the dominant 1,763,914-triangle dragon/scenic-base component and excludes separate print supports. To fit repository transport without changing the source into voxels, deterministically vertex-cluster the support-free geometry at 0.5 source units, preserving a conventional OBJ with 13,431 vertices / 29,734 triangles and SHA-256 `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`. This remains above the issue's practical detail floor; built-player and fidelity gates still decide whether the derivation is acceptable.

Two provenance/material constraints remain and must not be papered over: the user states the model was free and usable, but no source URL/author/named license text is available, so exact third-party attribution remains an external prerequisite for final provenance acceptance; STL has no standard source color/material regions, so any showcase palette is deterministic composition mapping, not preservation of absent source materials.

## Blast radius / current state / gates
Scope is mesh-import editor/runtime, VoxelShowcase composition, focused tests/assets/evidence, and this issue folder. Offline conversion is bounded; ordinary runtime does not voxelize triangles or use `MeshCollider`/source-mesh gameplay truth. Branch merged current `origin/master` `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` at merge commit `75d2a5e8f783c836f1ecb4c0aa58c714d444d64c`; master changes were only the shared SceneIssues workflow guides.

Next non-blocked work: vendor/reconstruct the deterministic OBJ source within connector limits, add source-specific bake configuration and dragon-anatomy regression, then stage canonical baked placement/comparison. Remaining gates include exact attribution/license metadata, final live VoxelShowcase input wiring, comparison/destruction staging, metrics/cost evidence, exact built-player views and production-quality visual review. Only then refresh master again and issue the single final exact-SHA request via `ci-test/fixes/agent-1`; after green validation move this assignment directly `open` → `closed` and non-force promote the exact feature head.
