# Tasks

## Investigation
- [x] Read `AGENTS.md`, `SceneIssues/feature-readme.md`, and canonical `SceneIssues/README.md`; keep `plan.md` and `tasks.md` separate.
- [x] Resume `fixes/agent-1`, confirm `ci-test/fixes/agent-1` is the only CI transport, and merge current master before substantial work.
- [x] Trace voxel authoring/storage, material palette, showcase composition, collision/edit, and player-capture owners; canonical replay seam is `IStructureAuthoringSession` + `StructuresComposition.CreateAuthoringSession`.
- [x] Confirm no existing arbitrary mesh-to-voxel production importer should own this conversion.
- [x] Record architecture hypotheses/discriminator and blast-radius/cost boundaries in `plan.md`.
- [x] Resolve live VoxelShowcase input ownership: structure selection must be explicit so ordinary movement/brush controls are unchanged outside placement mode.
- [x] Reject earlier source candidates that fail anatomy/detail or cannot be transferred without lowering acceptance; retain evidence in `verification-source-selection.txt`.
- [x] Inspect user upload `mountain_dragon_supported.zip`: one valid binary CHITUBOX STL, 107,207,684 bytes, 2,144,152 triangles, SHA-256 `a01f600705a6daf79a8828474f227251a5680d4bb8bad4aa46659f9e06cf53d6`.
- [x] Separate print-support geometry from the uploaded source: dominant dragon/scenic-base component is 1,763,914 triangles; support-free STL SHA-256 `e6a0a8bee6a08193db1eb09afea5003d3a502d3c097cf061a165ecc9bb637813`.
- [x] Produce deterministic conventional support-free OBJ derivation candidate via 0.5-unit vertex clustering: 13,431 vertices / 29,734 triangles / SHA-256 `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`; do not treat this as voxels or hand-authored replacement geometry.
- [x] Record exact upload/cleanup/derivation hashes, dimensions, and material limitation in `verification-uploaded-source.txt`.
- [ ] Vendor the deterministic OBJ source/reconstructable archive into this repository without changing geometry. BLOCKED: only transfer parts `part00`-`part07` (160,000 base64 bytes total) are present on the branch; the remaining exact derived payload is not retained in repository or recoverable prior context. Do not fabricate or regenerate different geometry; resume exact lossless transfer when the verified payload is available.
- [ ] Complete exact third-party provenance: source URL, author, and named license/permission text are still unavailable. User states the model was free and able to be used; record that statement but do not invent missing attribution/license fields or close provenance acceptance without them.
- [x] Verify source candidate is detailed/non-voxel-native and contains readable head, body, wings, limbs/feet, long curved tail, secondary surface detail, and scenic base after support removal.

## Behavior-first regressions
- [x] Add importer contract tests before production code.
- [x] Cover conservative curved surface coverage, solid closed-interior fill, deterministic output/material ownership, transforms including mirrored/non-uniform scale, thin features, malformed/non-finite/oversized input, topology policy, codec round-trip, and canonical authoring replay.
- [x] Cover independent non-dragon reuse through `MeshVoxelizationReuseTests.IndependentBoxFixture_UsesImporterCodecAndCanonicalAuthoringPath`.
- [x] Cover one-shot structure selection plus reusable input ownership through `StructurePlacementInputRouterTests`.
- [x] Cover reusable fidelity/cost instrumentation for surface extraction, connectedness/material/brick counts, symmetric p95 distance, fixed-view silhouette IoU, and transformed mesh→bake measurement.
- [ ] Add dragon-specific production regression proving required anatomical regions are non-empty/spatially plausible through produced bake data, not source-string/count-only assertions. BLOCKED on exact source reconstruction + baked artifact.

## Implementation
- [x] Add reusable semantic/config-driven transformed triangle mesh→voxel API/configuration with bounded cost, fill/topology/material/thin-feature policy.
- [x] Conservatively rasterize triangles, bounded-fill closed interiors, preserve deterministic surface material ownership, and support mirrored/non-uniform transforms.
- [x] Add deterministic sparse baked-cell codec/artifact and replay through canonical `IStructureAuthoringSession`.
- [x] Add generic Editor-only Unity hierarchy/skinned-mesh adapter with deterministic submesh mapping.
- [x] Add reusable bounded offline bake-analysis/fidelity metrics.
- [x] Add isolated one-shot structure-selection state and control-consumption router.
- [x] Add source reconstruction/import for the committed split OBJ archive in Editor-only/source-specific tooling; reconstruction fails closed on missing/non-contiguous parts or gzip/OBJ hash mismatch, and ordinary runtime never reads the source archive.
- [ ] Add source-specific bake configuration and generate/commit the baked dragon artifact within structure bounds X/Z<=127 and Y<=511. Configuration is implemented by `MountainDragonVoxelBakePolicy`/`MountainDragonAuthoringPolicy`; artifact generation is BLOCKED on the missing exact archive parts.
- [x] Apply deterministic semantic showcase palette mapping. The source has no standard material/color regions, so composition maps unmaterialed source/interior cells to canonical `DarkStone`; this is explicit composition mapping, not source-color preservation.
- [ ] Instantiate baked dragon through normal `ShowcaseWorld`/WorldBuilder voxel authoring so rendering/collision/edit/destruction share canonical storage. BLOCKED on baked artifact.
- [ ] Wire explicit VoxelShowcase selection mode: scroll selects while active and Space commits once; ordinary controls unchanged outside mode. Current tool blocker: live 58 KB `Assets/Game/Composition/Showcase/SceneRuntime/VoxelShowcase.cs` is only writable as a whole while connector reads truncate it; do not risk unrelated loss or create parallel/reflection input authority.
- [ ] Add labeled `Mesh -> Voxels` comparison area with matched pose/scale/orientation/ground/lighting; source mesh is presentation-only with no collider/gameplay authority. BLOCKED on reconstructed source + baked artifact.
- [ ] Add durable capture support for front, side, rear, front 3/4, rear 3/4, elevated/top 3/4, plus head/horns, wing, feet/claws, and tail closeups. Semantic 10-view capture contract + coverage test are implemented; built-player integration and real evidence remain required.
- [ ] Emit deterministic source triangle count, voxel resolution, authored voxel count, sparse brick/chunk count, voxelization duration, serialized size, resident/runtime placement/build cost. Final values BLOCKED on baked artifact.
- [ ] Add destruction/world-truth validation instance proving voxel edits affect rendering/collision without source-mesh shell/collider fallback. BLOCKED on baked artifact.

## Reusability review
- [x] Keep shared `MeshVoxelization` and metrics/config completely mesh-agnostic; no dragon names/anatomy/source IDs/showcase controls in engine APIs.
- [x] Keep source selection, source reconstruction, dragon bake configuration, palette choices, comparison staging, and placement controls in Editor/game/showcase composition.
- [x] Prove a second non-dragon fixture uses the same importer/codec/canonical authoring path without dragon branches.

## Dragon artifact acceptance
- [ ] Downloaded/user-provided third-party source is legitimately redistributable and exact provenance/checksums/required attribution are committed.
- [x] Candidate source is detailed/curved/non-voxel-native and genuinely exercises wing/head/limb/tail silhouette fidelity (>20k triangles after deterministic support-free derivation).
- [ ] Generated structure is volumetric, sparse, bounded, and preserves recognizable head/body/wings/limbs/feet/tail/secondary detail.
- [ ] Source and voxel exhibit use same effective transform/pose; required material/color acceptance is explicitly and correctly resolved for an STL source with no standard material regions.
- [ ] Human review confirms voxelized result is unmistakably this exact source model, not merely a generic dragon.

## Validation / cost
- [ ] Exact-SHA final CI compiles/passes focused importer/codec/authoring/showcase + dragon-specific regressions.
- [ ] Record import/voxelization time, occupied voxel count, sparse brick/chunk count, serialized size, runtime resident/storage impact, and incremental render/world-build cost within repository budgets.
- [x] Verify ordinary runtime does not execute mesh voxelization and no `MeshCollider`/source-mesh gameplay fallback exists; recorded in `verification-regression-coverage.txt`.
- [x] Feature blast radius remains confined to mesh-import editor/runtime, showcase mesh-structure/selection helpers, focused tests/assets/evidence, and this issue folder.
- [x] Merge current `origin/master` `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` into feature at `75d2a5e8f783c836f1ecb4c0aa58c714d444d64c`; delta was only shared SceneIssues workflow guidance.
- [ ] Re-run final feature diff review after source/showcase/evidence implementation.
- [ ] Refresh/merge current `origin/master` immediately before final CI if advanced.
- [ ] Issue one final exact-source request only through `ci-test/fixes/agent-1`; never replace queued/running CI.
- [ ] Obtain green focused regression plus exact built-player `VoxelShowcase` validation and inspect all required views directly.
- [ ] Classify built-player visual evidence `production-quality`; lower classification fails.
- [ ] Confirm built-app destruction/world truth, one-shot spawn, metrics, and no startup/runtime exceptions.

## Promotion / closure
- [ ] Complete metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`, `resolvedUtc`) only after every required gate passes.
- [ ] Move only this assignment directly `open` → `closed` after green exact-SHA gates and human visual acceptance.
- [ ] Merge latest `origin/master` into `fixes/agent-1`, push feature head, then push that exact head to `origin/master` non-force; fetch/merge/retry if master advances.
