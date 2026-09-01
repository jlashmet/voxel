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
- [x] Vendor the exact reconstructable derived archive into this repository without changing geometry; exact reconstruction passed CI `33451165954`.
- [x] Replace corruption-history diagnostics with behavioral source-integrity regressions: committed exact reconstruction succeeds, missing logical parts fail closed, and valid-Base64 mutations fail pinned gzip identity.
- [x] Record project-owner commercial-use confirmation for this exact recovered source in `verification-source-license.txt`.
- [ ] Complete exact upstream provenance required by acceptance: original source URL, original author/creator, and named upstream license/permission text remain unavailable. Public searches on 2026-09-01 for both exact source hashes and `mountain_dragon_supported.stl` returned no exact match. External blocker remains; do not infer metadata from similar listings or weaken acceptance.
- [x] Verify the candidate is detailed/non-voxel-native with readable head/horns, body, wings, limbs/feet, long curved tail, secondary surface detail, and scenic base.
- [x] Isolate source-topology behavior and use explicit `VoxelShellFill` only after proving conservative rasterization closes the intended shell; preserve `Reject` and `SurfaceOnly` semantics.

## Behavior-first regressions / reusable pipeline
- [x] Add importer contract tests before production code.
- [x] Cover conservative curved-surface coverage, closed-interior fill, deterministic material ownership, mirrored/non-uniform transforms, thin features, malformed/non-finite/oversized input, topology policy, codec round-trip, and canonical replay.
- [x] Prove independent non-dragon reuse through `MeshVoxelizationReuseTests.IndependentBoxFixture_UsesImporterCodecAndCanonicalAuthoringPath`.
- [x] Cover one-shot selection and input ownership through `StructurePlacementInputRouterTests`.
- [x] Cover reusable fidelity/cost instrumentation: surface extraction, connectedness/material/brick counts, symmetric p95 distance, fixed-view silhouette IoU, and transformed mesh→bake measurement.
- [x] Add independent non-dragon nearly-closed-shell regression proving `VoxelShellFill` does not invent interiors for genuinely open rasters.
- [x] Add dragon-specific production regression proving required anatomical regions are non-empty/spatially plausible through produced bake data, not source-string/count-only assertions.
- [x] Validate `MountainDragonBakeGenerationTests.CheckedInBake_MeetsPinnedSourceFidelityTargets` at exact feature SHAs; runs `33476063393`, `33477581134`, and `33490519425` passed required fidelity/module validation.

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
- [x] Generate and commit the exact sparse dragon bake through the shared importer/voxelizer: 99×107×107 bounds, 98,100 authored voxels, 594 sparse bricks, canonical SHA `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`, runtime transport SHA `758612c8b63316e3757a7695bfdb07f99ee5709f3706c504688d657017ecc961`.
- [x] Apply deterministic semantic showcase palette mapping. STL has no standard material/color regions, so unmaterialed exterior/interior maps to canonical `DarkStone`; do not claim absent source color preservation.
- [x] Instantiate the decoded baked dragon through normal `ShowcaseWorld.PlaceBakedMeshStructure` / WorldBuilder voxel authoring so rendering, collision, edit, and destruction share canonical storage. Exact run `33490519425` wrote all 98,100 voxels and then proved canonical destruction/collision truth in the built player.
- [x] Repair explicit VoxelShowcase placement mode in Showcase composition. Commit `b4b87cefcc1174d9c43cebc35b62d3eb62cc2def` removes the optional inspector bake dependency and calls `MountainDragonBakedArtifact.Load()`; shared codec remains Dragon-agnostic.
- [x] Add labeled matched `Mesh -> Voxels` comparison area with identical effective pose/scale/orientation/ground/lighting; source mesh is presentation-only and has no gameplay/collider authority. Exact run `33485072972` produced and passed direct human inspection of required views.
- [x] Add module-owned dragon validation scene/fixture using production systems; do not use Worldbuilding Gallery or the top-level showcase as the feature fixture.
- [x] Integrate durable capture support for front, side, rear, front 3/4, rear 3/4, top/elevated 3/4, head/horns, wing, feet/claws, and tail.
- [x] Emit/record source triangle count, voxel resolution, authored voxel count, sparse brick count, serialized transport size, runtime placement cost, storage footprint/capacity, and renderer/world-build diagnostics. Exact run `33490519425` plus `verification-ci-33490519425.txt` records these values without weakening budgets.
- [x] Add destruction/world-truth validation proving edits affect rendered/collision truth without source-mesh shell/collider fallback. Exact built run `33490519425` changed 1,144 voxels, changed the target from material 6 to empty, and changed collision from blocked to unblocked with zero source colliders.

## Reusability / ownership review
- [x] Keep shared mesh voxelization, codec, replay, and metrics APIs mesh-agnostic; no dragon/source/showcase policy in shared engine APIs.
- [x] Keep source reconstruction, dragon bake/palette policy, comparison staging, and placement controls in Editor/game/showcase composition.
- [x] Prove a second non-dragon consumer uses the importer/codec/canonical replay path without dragon branches.
- [x] Re-run feature diff/reuse/ownership review against current `master`: feature paths remain concentrated in Structures mesh import, Editor adapter/tooling, Showcase composition/validation, tests, resource bake, and this SceneIssue. No global voxel scale, terrain/building storage semantics, collision rules, or device budgets are intentionally changed. `master` is substantially advanced/diverged, so a normal merge is still required before final validation/promotion.

## Dragon acceptance / validation
- [ ] Third-party source is legitimately redistributable with exact required provenance committed. Commercial-use permission is recorded; upstream URL/author/named-license metadata remains externally blocked.
- [x] Generated structure is volumetric, sparse, bounded, and preserves recognizable head/body/wings/limbs/feet/tail/secondary detail.
- [x] Quantitative fidelity passes: surface distance <= 1.5 voxels and silhouette IoU >= 0.90 for required views.
- [x] Source and voxel exhibit use the same effective transform/pose; material/color acceptance is explicitly correct for STL with no standard material regions.
- [x] Exact built module-owned fixture renders the comparison without exceptions and records durable evidence.
- [x] Human review confirms the voxel result is unmistakably this exact source, not merely a generic dragon, and classifies the ten-view comparison evidence as production-quality for this feature fixture.
- [ ] Built-app one-shot VoxelShowcase placement input is directly exercised end-to-end. Composition now consumes the pinned artifact and router behavior is covered independently, but the top-level built replay does not synthesize B/Space input; keep this unproven gate unchecked rather than substituting editor-only evidence.
- [x] Built-app destruction/world truth is directly validated by exact run `33490519425`; rendering/collision both follow canonical voxel mutation and no source collider survives.
- [x] Record import/bake characteristics, occupied cells, sparse bricks, serialized bytes, placed storage footprint/capacity, placement/world-build cost, renderer diagnostics, and confirm no budget weakening. Durable values are in `verification-ci-33490519425.txt`.
- [x] Inspect exact run `33490519425` post-destruction frames directly and confirm no runtime/assertion exception; all ten pristine comparison frames remain present.
- [ ] Merge current `origin/master` immediately before final exact-SHA gates; master is currently ahead/diverged and must be integrated non-force.
- [ ] Issue final exact-SHA focused + module-owned built-player validation only through `ci-test/fixes/agent-1`; never replace queued/running CI.
- [ ] Inspect all required final captures directly after the final master merge and confirm no startup/runtime exceptions.

## Promotion / closure
- [ ] Complete issue metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`, `resolvedUtc`) only after every acceptance gate passes.
- [ ] Move only this assignment directly `open` → `closed` after green exact-SHA gates and human visual acceptance.
- [ ] Merge latest `origin/master` into `fixes/agent-1`, push feature head, then push that exact head to `origin/master` non-force; fetch/merge/retry if master advances.
