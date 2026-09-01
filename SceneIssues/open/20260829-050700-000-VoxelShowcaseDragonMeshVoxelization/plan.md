# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
`VoxelShowcase` needs a reusable authoring-time conventional triangle-mesh → canonical sparse-voxel path, proved on a legitimately redistributable detailed curved winged dragon. Ordinary runtime must consume only the baked sparse artifact through normal voxel storage so rendering, collision, edits, and destruction share one world truth. Built-player source/voxel evidence must be matched and directly judged `production-quality`.

## Architecture / discriminator
The supported ownership model is: generic mesh import/voxelization/codec/replay under Structures runtime APIs, source-specific adaptation and dragon policy in Showcase composition/Editor tooling, and final placement through `IStructureAuthoringSession`. No source triangles, dragon policy, source IDs, or showcase controls belong in shared engine APIs. Independent non-dragon fixtures prove the importer/codec/replay seam is reusable.

Input ownership remains isolated through `StructurePlacementSelection` and `StructurePlacementInputRouter`; placement consumes controls only while active and ordinary movement/brush behavior remains unchanged otherwise. The canonical VoxelShowcase wiring already compile-passed exact-parent targeted CI after fixing its demonstrated Structures runtime assembly reference.

Built-player feature proof must use a module-owned dragon validation scene/fixture that invokes production systems. Worldbuilding Gallery and the top-level showcase are integration/evidence consumers, not substitutes for the module-local feature fixture.

## Source state / blockers
The authoritative recovered upload is `mountain_dragon_supported.zip`, 50,524,579 bytes, SHA-256 `f48cab5ab5b7edf6a84cc7bf14797c73d0ac61bf597ef76a587589a4522aeb0f`. It contains one CHITUBOX binary STL, 107,207,684 bytes / 2,144,152 triangles / SHA-256 `a01f600705a6daf79a8828474f227251a5680d4bb8bad4aa46659f9e06cf53d6`.

The support-free deterministic derivation has now been reproduced exactly rather than approximately: OBJ 860,349 bytes / 29,734 triangles / SHA-256 `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`; deterministic gzip 352,348 bytes / SHA-256 `fd2f8253fcf5bc32b275640448511f59d20dcc7d01c307f99124b224431892d4`. The exact reconstructed archive is committed in logical whole/segmented transfer parts. `MountainDragonSourceArchive` validates contiguity, Base64 syntax, pinned gzip identity, bounded decompression, OBJ byte count, and pinned OBJ identity before exposing the generated Editor asset.

The old repository-transfer failures were correctly isolated to a corrupt/incomplete archive. After repairing those exact bytes and replacing the corruption-history diagnostic with behavioral integrity regressions, exact-SHA CI run `33451165954` passed `MountainDragonSourceArchiveTests.ReconstructObjBytes_CommittedArchiveMatchesPinnedIdentity` for feature SHA `5b26f2b00924de6ec33f0798d2351c6b7dbc3ed0`. Repository-byte transport is therefore no longer a blocker.

Commercial-use permission is recorded from the project owner in `verification-source-license.txt`. One acceptance blocker remains external: the original upstream source URL, original author/creator, and named upstream license/permission text have not been recovered. Do not invent those fields or weaken acceptance; continue independent implementation while recording this blocker.

The STL/OBJ has no standard material regions. Showcase composition deterministically maps unmaterialed source/interior cells to canonical `DarkStone`; this is explicit scene policy, not a claim of preserving absent source color.

## Bake / runtime plan
`MountainDragonAuthoringPolicy` explicitly selects the shared `VoxelShellFill` mode at 0.30 source units/voxel. That policy is independently covered by non-dragon tests proving a conservative voxel-space shell may fill while genuinely open rasters remain open; `Reject` and `SurfaceOnly` semantics are unchanged.

`MountainDragonBakeGenerator.GeneratePinnedBakeAndWriteArtifact()` is the production source-dependent path to use next. It reconstructs the pinned OBJ, adapts it through the generic Unity mesh source, runs the shared `MeshVoxelizer`, validates `MountainDragonVoxelBakePolicy`, analyzes metrics, round-trips/encodes the sparse artifact, and writes the `.mvx` plus deterministic metrics sidecar. Existing `MountainDragonBakeGenerationTests.GeneratePinnedBake_ProducesValidatedSparseArtifact` already exercises this path, so do not create a duplicate bake harness.

Exact-SHA CI run `33451568424` passed that generator and preserved the authoritative generated artifact: canonical MVX SHA-256 `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`, 1,073,295 serialized bytes, 99×107×107 bounds, and 98,100 authored voxels. The later runtime transport failure was isolated rather than treated as a voxelization failure. Re-encoding the green MVX through the production `MDVP1` layout and deterministic gzip level 9 / zero timestamp reproduces the already-pinned transport SHA-256 `758612c8b63316e3757a7695bfdb07f99ee5709f3706c504688d657017ecc961` exactly. The checked-in Base64 had one edit region (`f` where canonical transport has `Fv`), so the earlier one-symbol-insertion hypothesis could not succeed. Commit `da60731f20b829a0d25f25450a2b4bbaa0d504d9` replaces only the corrupted transport bytes and removes the intentionally-failing export diagnostic while retaining fail-closed and anatomical regressions.

Runtime placement already has the correct ownership seam: `ShowcaseWorld.PlaceBakedMeshStructure` accepts only a decoded sparse bake, prepares bounded regions, replays cells through `IStructureAuthoringSession`, publishes storage, and returns placement cost. It never consumes source triangles or a `MeshCollider`. After the authoritative bake is retrieved and committed, wire that artifact into the mountain-dragon layout/comparison and use the same placement path for rendering, collision, edits, destruction, and runtime cost evidence.

## Remaining acceptance work
With the exact bake transport repaired, continue from the produced bake data: validate the permanent anatomy/integrity regression through targeted CI, instantiate it through normal Showcase/WorldBuilder authoring, stage the labeled matched `Mesh -> Voxels` exhibit, and add the module-owned built-player validation scene using production systems. The semantic ten-view capture plan already enumerates front, side, rear, front 3/4, rear 3/4, top/elevated 3/4, head/horns, wing, feet/claws, and tail; only real built-player integration/evidence may satisfy those checkboxes.

Quantitative acceptance remains surface distance <= 1.5 voxels and silhouette IoU >= 0.90. Built-player evidence must also prove one-shot placement, no source-mesh gameplay authority, destruction/world truth, no startup/runtime exceptions, and measured import/voxelization/occupied-cell/sparse-brick/serialized/runtime placement/resident/render-world-build cost within repository budgets.

## CI / closure discipline
`ci-test/fixes/agent-1` is the only targeted-CI transport. Never replace a queued/running request; completed product failures must be fixed before another request and proven infrastructure failures may be retried unchanged. Each request commit must parent the exact feature SHA and differ only by `.github/test-request.json`.

Master has advanced since earlier integration and must be refreshed immediately before final exact-SHA acceptance gates. Do not close while upstream provenance, built-player comparison/destruction evidence, quantitative fidelity/cost, or human visual review remain incomplete. Only after every required checkbox passes should the issue move directly `open` → `closed`, metadata be finalized, current `origin/master` be merged into `fixes/agent-1`, and that exact feature head be pushed non-force to `origin/master` with fetch/merge/retry if master advances.
