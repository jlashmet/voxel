# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
`VoxelShowcase` needs a reusable **authoring-time** conventional triangle-mesh → canonical voxel path, proved on a downloaded redistributable detailed curved winged dragon. Runtime must load/replay only baked sparse cells through normal voxel storage so rendering, collision, edits, and destruction share world truth. Matched source/voxel built-player evidence must be judged `production-quality`.

## Architecture / discriminator
Hypothesis A: `IStructureAuthoringSession` and normal structure storage remain authoritative; the missing capability is deterministic offline conversion plus composition wiring. Hypothesis B: an existing arbitrary mesh/SDF importer should be generalized. Result: **A supported; B rejected.** No existing arbitrary production importer owns this conversion.

Generic implementation remains under `Structures/Runtime/MeshImport`: transformed triangles → conservative surface coverage → bounded closed-interior fill → deterministic material ownership → ordered sparse bake/codec → canonical authoring replay. Unity hierarchy/skinned adaptation is editor-only. Shared importer/metrics APIs contain no dragon/source/showcase policy. Independent non-dragon reuse is covered by `MeshVoxelizationReuseTests.IndependentBoxFixture_UsesImporterCodecAndCanonicalAuthoringPath`. Runtime-authority review now also confirms showcase placement consumes only `BakedVoxelStructure`, the Unity source adapter is Editor-only, and no `MeshCollider` fallback is present.

## Source / blocker
Selected source: Delatronic **Dragon**, Blend Swap 15891 (historic 80766), CC BY 3.0 as recorded by independent Bitterli/Microsoft/GLTF redistribution mirrors. Intended exact PLY payload hashes and provenance are recorded in `verification-source-selection.txt`.

The next required task remains blocked on physical source transfer. The connected GitHub API cannot losslessly return the two multi-megabyte primary PLY binaries; the direct downloader cannot reach the raw object through the permitted network path; and a fresh format discriminator confirms the mirror is `binary_little_endian`, so it cannot be reconstructed from line-ranged UTF-8 reads. Lower-detail/wingless substitutes fail acceptance and must not be used. Continue only independent required work while this remains blocked.

## Blast radius / current state / gates
Scope is mesh-import editor/runtime, VoxelShowcase composition, focused tests/assets/evidence, and this issue folder. Offline conversion is bounded; ordinary runtime does not voxelize triangles or use `MeshCollider`/source-mesh gameplay truth. Branch includes current `origin/master` `5f07db5cd7677e84f617deb61c5b03a4b896159c` via merge commit `76ecb118cd93010a0169e270822d769e46804123`.

Remaining gates require the real source bytes, provenance/license, dragon-specific bake/anatomy/material regressions, comparison/destruction/showcase wiring, metrics/cost evidence, exact built-player views and production-quality visual review. Only then refresh master again and issue the single final exact-SHA PlayMode request via `ci-test/fixes/agent-1`; after green validation complete pending/closed metadata and non-force promote the exact feature head.