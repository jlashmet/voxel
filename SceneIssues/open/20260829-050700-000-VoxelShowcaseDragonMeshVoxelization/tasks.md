# Tasks

## Investigation / source
- [x] Read `AGENTS.md`, `SceneIssues/feature-readme.md`, and canonical `SceneIssues/README.md`; keep `plan.md` and `tasks.md` separate.
- [x] Resume `fixes/agent-1`, use only `ci-test/fixes/agent-1` for targeted CI, and keep feature/CI transport deltas separate.
- [x] Trace canonical structure authoring/storage, rendering, collision/edit/destruction, palette, showcase composition, and input ownership.
- [x] Reject duplicate importer ownership; canonical replay seam is `IStructureAuthoringSession` through Structures composition.
- [x] Recover and verify `mountain_dragon_supported.zip`: 50,524,579 bytes, SHA-256 `f48cab5ab5b7edf6a84cc7bf14797c73d0ac61bf597ef76a587589a4522aeb0f`.
- [x] Verify contained CHITUBOX binary STL: 107,207,684 bytes, 2,144,152 triangles, SHA-256 `a01f600705a6daf79a8828474f227251a5680d4bb8bad4aa46659f9e06cf53d6`.
- [x] Separate print supports and reproduce the dominant dragon/scenic-base derivation semantics.
- [x] Reproduce the exact deterministic support-free OBJ: 860,349 bytes, SHA-256 `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`, 29,734 triangles, 13,465 serialized vertices / 13,431 referenced clusters.
- [x] Reproduce deterministic gzip: 352,348 bytes, SHA-256 `fd2f8253fcf5bc32b275640448511f59d20dcc7d01c307f99124b224431892d4`.
- [x] Vendor the exact reconstructable derived archive into this repository without changing geometry. `MountainDragonSourceArchiveTests.ReconstructObjBytes_CommittedArchiveMatchesPinnedIdentity` passed exact-SHA CI run `33451165954` for feature SHA `5b26f2b00924de6ec33f0798d2351c6b7dbc3ed0`; the previous corrupt-transfer failure was fixed rather than retried unchanged.
- [x] Replace corruption-history diagnostics with behavioral source-integrity regressions: committed exact reconstruction succeeds, missing logical parts fail closed, and valid-Base64 mutations fail pinned gzip identity.
- [x] Record project-owner commercial-use confirmation for this exact recovered source in `verification-source-license.txt`.
- [ ] Complete exact upstream provenance required by acceptance: original source URL, original author/creator, and named upstream license/permission text remain unavailable. Record as external blocker; do not invent metadata or weaken acceptance.
- [x] Verify the candidate is detailed/non-voxel-native with readable head/horns, body, wings, limbs/feet, long curved tail, secondary surface detail, and scenic base.
- [x] Isolate source-topology behavior and use explicit `VoxelShellFill` only after proving conservative rasterization closes the intended shell; preserve `Reject` and `SurfaceOnly` semantics.

## Behavior-first regressions / reusable pipeline
- [x] Add importer contract tests before production code.
- [x] Cover conservative curved-surface coverage, closed-interior fill, deterministic material ownership, mirrored/non-uniform transforms, thin features, malformed/non-finite/oversized input, topology policy, codec round-trip, and canonical replay.
- [x] Prove independent non-dragon reuse through `MeshVoxelizationReuseTests.IndependentBoxFixture_UsesImporterCodecAndCanonicalAuthoringPath`.
- [x] Cover one-shot selection and input ownership through `StructurePlacementInputRouterTests`.
- [x] Cover reusable fidelity/cost instrumentation: surface extraction, connectedness/material/brick counts, symmetric p95 distance, fixed-view silhouette IoU, and transformed mesh→bake measurement.
- [x] Add independent non-dragon nearly-closed-shell regression proving `VoxelShellFill` does not invent interiors for genuinely open rasters.
- [x] Add dragon-specific production regression proving required anatomical regions are non-empty/spatially plausible through produced bake data, not source-string/count-only assertions. `MountainDragonBakedArtifactTests.CheckedInBake_DecodesCanonicalArtifactAndPreservesDragonAnatomy` covers body, both wings, head/horns, both feet/claws, and curled tail against the canonical bake.
- [ ] Validate `MountainDragonBakeGenerationTests.CheckedInBake_MeetsPinnedSourceFidelityTargets` at an exact feature SHA; commit `c6e70b1c7ff3c3716f8601e8364c6eaf4639b12e` enforces sampled symmetric p95 <= 1.5 voxels and front/side/top silhouette IoU >= 0.90 against the exact reconstructed source.

## Implementation
- [x] Add reusable semantic/config-driven transformed triangle mesh→voxel API/configuration with bounded cost and explicit fill/topology/material/thin-feature policy.
- [x] Conservatively rasterize triangles, bounded-fill interiors, preserve deterministic material ownership, and support mirrored/non-uniform transforms.
- [x] Add deterministic sparse baked-cell codec/artifact and replay through canonical `IStructureAuthoringSession`.
- [x] Add generic Editor-only Unity hierarchy/skinned-mesh adapter with deterministic submesh mapping.
- [x] Add reusable bounded offline bake-analysis/fidelity metrics.
- [x] Add isolated one-shot structure-selection state and control-consumption router.
- [x] Add source-specific Editor reconstruction/import tooling; ordinary runtime never reads source triangles/archive.
- [x] Add explicit reusable `VoxelShellFill` policy and independent reuse coverage.
- [x] Add source-specific bake policy/configuration: 29,734 source triangles, 0.30 source units/voxel, bounded sparse envelope, canonical dragon material mapping.
- [x] Generate, validate, retrieve, and commit the exact sparse dragon bake produced by `MountainDragonBakeGenerator.GeneratePinnedBakeAndWriteArtifact()` through the shared importer/voxelizer. Exact generator CI run `33451568424` produced canonical SHA `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`, 99×107×107 bounds, and 98,100 authored voxels. The compact runtime transport was independently reproduced from that artifact at pinned SHA `758612c8b63316e3757a7695bfdb07f99ee5709f3706c504688d657017ecc961`; commit `da60731f20b829a0d25f25450a2b4bbaa0d504d9` repaired the corrupted Base64 payload without changing canonical bake identity.
- [x] Apply deterministic semantic showcase palette mapping. STL has no standard material/color regions, so unmaterialed exterior/interior maps to canonical `DarkStone`; do not claim absent source color preservation.
- [ ] Instantiate the decoded baked dragon through normal `ShowcaseWorld.PlaceBakedMeshStructure` / WorldBuilder voxel authoring so rendering, collision, edit, and destruction share canonical storage. `MountainDragonBakedArtifactTests.CheckedInBake_PlacesThroughNormalShowcaseWorldAuthoringPath` is committed. Exact-parent run `33466699310` failed before Dragon placement because the test-only seed made unrelated Kentridge planning exhaust; commit `5b93fd04447c2bc7d955b6366e74efa73397cbe1` uses the shipped Showcase seed. A later CI request was accidentally based on the previous CI commit rather than the feature SHA; run `33469292081` is queued and must be left untouched, then a fresh exact-parent request must be issued from the current feature head.
- [ ] Repair the explicit VoxelShowcase placement mode so it consumes the pinned `MountainDragonBakedArtifact` through Showcase composition. The earlier exact-parent run `33416544070` proved compilation only; inspection shows `Assets/Scenes/VoxelShowcase.unity` serializes no `m_MountainDragonVoxelBake`, and the current mode uses generic `BakedVoxelStructureCodec.Decode` on the compact MDVP Base64 transport if assigned. Do not move Dragon transport policy into the shared codec.
- [ ] Add labeled matched `Mesh -> Voxels` comparison area with identical effective pose/scale/orientation/ground/lighting; source mesh is presentation-only and has no gameplay/collider authority.
- [x] Add module-owned dragon validation scene/fixture using production systems; do not use Worldbuilding Gallery or the top-level showcase as the feature fixture. Current master now provides the repository-owned module-validation schema. `MountainDragonVoxelValidationShowcase`, `MountainDragonVoxelValidation.unity`, its standalone scenario, and `mountain-dragon-voxelization.module-validation.json` are committed under `Assets/Game/Composition/Showcase/Validation/MountainDragonVoxelization/`. The fixture uses the pinned bake, normal `ShowcaseWorld` structure authoring/storage, production rendering/material rules, deterministic terrain contact, and emits placement/memory readiness logs; built-player validation is still required.
- [ ] Integrate durable capture support for front, side, rear, front 3/4, rear 3/4, top/elevated 3/4, head/horns, wing, feet/claws, and tail. Semantic ten-view capture contract exists; real built-player evidence remains required.
- [ ] Emit/record source triangle count, voxel resolution, authored voxel count, sparse brick/chunk count, voxelization duration, serialized size, resident/runtime placement/build cost. Bake metrics already record source triangles/resolution/cells/bricks/voxelization/serialized bytes; module fixture now logs placement time and runtime allocated/reserved bytes, but exact-SHA built evidence remains required.
- [ ] Add destruction/world-truth validation proving edits affect rendered/collision truth without source-mesh shell/collider fallback.

## Reusability / ownership review
- [x] Keep shared mesh voxelization, codec, replay, and metrics APIs mesh-agnostic; no dragon/source/showcase policy in shared engine APIs.
- [x] Keep source reconstruction, dragon bake/palette policy, comparison staging, and placement controls in Editor/game/showcase composition.
- [x] Prove a second non-dragon consumer uses the importer/codec/canonical replay path without dragon branches.

## Dragon acceptance / validation
- [ ] Third-party source is legitimately redistributable with exact required provenance committed. Commercial-use permission is recorded; upstream URL/author/named-license metadata remains blocked.
- [ ] Generated structure is volumetric, sparse, bounded, and preserves recognizable head/body/wings/limbs/feet/tail/secondary detail. Structural/anatomy regressions exist; built-player visual proof remains required.
- [ ] Quantitative fidelity passes: surface distance <= 1.5 voxels and silhouette IoU >= 0.90 for required views. Dragon-specific regression is committed but not yet exact-SHA validated.
- [ ] Source and voxel exhibit use the same effective transform/pose; material/color acceptance is explicitly correct for STL with no standard material regions.
- [ ] Exact built VoxelShowcase/module fixture renders comparison without exceptions and records durable evidence.
- [ ] Human review confirms the voxel result is unmistakably this exact source, not merely a generic dragon, and classifies evidence `production-quality`.
- [ ] Built-app destruction/world truth and one-shot placement are directly validated.
- [ ] Record import/voxelization time, occupied cells, sparse bricks/chunks, serialized bytes, resident/storage impact, render/world-build cost, and confirm blast radius/budgets.
- [ ] Re-run final feature diff/reuse/ownership review after artifact/showcase/evidence work.
- [ ] Refresh/merge current `origin/master` immediately before final exact-SHA gates if advanced.
- [ ] Issue final exact-SHA focused + module-owned built-player validation only through `ci-test/fixes/agent-1`; never replace queued/running CI.
- [ ] Inspect all required captures directly and confirm no startup/runtime exceptions.

## Promotion / closure
- [ ] Complete issue metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`, `resolvedUtc`) only after every acceptance gate passes.
- [ ] Move only this assignment directly `open` → `closed` after green exact-SHA gates and human visual acceptance.
- [ ] Merge latest `origin/master` into `fixes/agent-1`, push feature head, then push that exact head to `origin/master` non-force; fetch/merge/retry if master advances.
