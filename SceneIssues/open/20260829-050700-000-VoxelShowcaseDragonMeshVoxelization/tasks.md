# Tasks

## Investigation
- [x] Read `AGENTS.md`, current `SceneIssues/feature-readme.md`, and canonical `SceneIssues/README.md`; the feature guide was absent on the resumed branch initially and is now present via the current-master refresh merge.
- [x] Confirm `fixes/agent-1` starts from/merges current `origin/master` before implementation.
- [x] Re-check CI transport state: `ci-test/fixes/agent-1` already exists; leave it untouched until the single final targeted-CI request and never replace queued/running CI.
- [x] Record execution limitation: no local Unity runner; exact-SHA CI is authoritative compiler/test/player validation.
- [x] Trace voxel authoring/storage, material palette, showcase composition, collision/edit, and player-capture owners enough to keep the feature additive.
- [x] Resolve canonical replay seam: `IStructureAuthoringSession` + `StructuresComposition.CreateAuthoringSession`; baked sparse cells replay through normal voxel storage.
- [x] Confirm no existing arbitrary mesh-to-voxel production importer to generalize.
- [x] Record competing implementation hypotheses/discriminator in `plan.md`.
- [x] Check blast radius/cost: importer/showcase additions only; bounded dense working set; no runtime voxelization or mesh gameplay truth.
- [x] Resolve live VoxelShowcase runtime owner and input discrepancy; structure selection must be an explicit mode so ordinary movement/brush controls remain unchanged outside it.
- [x] Reject the stale “original CC0 dragon” plan because acceptance explicitly requires a downloaded third-party mesh.
- [x] Reject artist_71 OpenGameArt three-headed dragon after visual inspection: despite CC-BY 4.0/~24k-triangle detail, it has no wings and cannot satisfy mandatory wing silhouette/close-up acceptance.
- [x] Reject Cethiel/Drummyfish CC0 winged dragon as final proof asset: redistribution is clean, but the mirrored DAE has only ~633 position vertices and is visibly low-poly, below the detailed/production-quality gate.
- [x] Select Delatronic `Dragon` (Blend Swap asset 15891 / historic id 80766) as the preferred source: CC-BY, detailed curved winged anatomy, Blender 2.7x source; verify Bitterli redistribution and Microsoft `DirectX-Graphics-Samples` PBRT/PLY mirror with asset-specific CC-BY license/provenance.
- [x] Verify Microsoft's PBRT scene maps the dragon material to `Mesh008.ply`, `Mesh013.ply`, `Mesh014.ply`, and `Mesh015.ply`, with one shared scene transform; source files are real non-LFS payloads.
- [x] Verify compact mirror `gkjohnson/3d-demo-data` `dragon.glb` is 1,651,276 bytes, Delatronic / CC BY 3.0, Draco-compressed without mesh simplification; verify independent `ErfanMo77/gltf-research-scenes` conversion reports 16 meshes / 831,812 scene triangles and preserves the same Blend Swap/Bitterli provenance.
- [x] Resolve exact SHA-256 identities for all four Delatronic Dragon-material PLY payloads from the independent Git LFS mirror and cross-check byte sizes against Microsoft's non-LFS mirror; record them in `verification-source-selection.txt`.
- [x] Exhaust connector-safe lossless transport options without lowering the source-quality bar: >1 MB binary contents/blob fetch, cross-repository blob reuse, GitHub Raw/CDN download, and same-assignment temp branch recovery are unavailable/no-op in this execution environment.
- [ ] Vendor the selected Delatronic source bytes into this repository. Current connectors can inspect/base64 small binary source objects but still cannot losslessly transfer the two multi-megabyte main payloads into this repo; direct shell network access was rechecked on 2026-08-30 and still cannot resolve github.com. Do not substitute lower-quality geometry merely to fit tooling.
- [ ] Commit exact source URL, author, license, original/mirrored format, vertex/triangle counts, original/mirror SHA-256, committed-source SHA-256, mirror blob/commit provenance, and required attribution/license text.
- [ ] Verify committed source is meaningfully detailed/non-voxel-native with readable head, body, wings, limbs/feet, tail, and secondary silhouette detail.

## Behavior-first regressions
- [x] Add compile-intended importer contract tests before production code (`9164857ad304dc95a6e182e8e982251d5a918567`).
- [x] Add compile-intended open/non-manifold topology policy contract before production topology handling (`32703c26a0a1f2d9a91b6ff3986b98d8f3e46142`).
- [x] Curved synthetic closed geometry proves conservative surface coverage and solid interior fill.
- [x] Same input/config produces stable ordered voxel/material output.
- [x] Off-origin transform, rotation/non-uniform scale, and mirrored orientation are covered.
- [x] Material mapping is deterministic through production importer output.
- [x] Thin curved/sheet feature retention is covered without global bloat.
- [x] Invalid triangle indices and oversized bounds fail preflight before rasterization/dense allocation.
- [x] Focused non-finite source and non-finite transform preflight regressions reject before rasterization.
- [x] Open/non-manifold input either rejects clearly or explicitly falls back to surface-only without invented interior.
- [x] Sparse artifact codec round-trips exactly and rejects malformed/out-of-bounds data.
- [x] Baked cells replay through `IStructureAuthoringSession` at arbitrary requested origin.
- [x] Showcase selection/Space commit state is single-trigger; idle/update repetition cannot duplicate placement.
- [x] Reusable fidelity/cost instrumentation has synthetic regressions for surface extraction, connectedness/material/brick counts, symmetric p95 distance, fixed-view silhouette IoU, and transformed mesh→bake measurement.
- [ ] Add dragon-specific production regression proving required anatomical regions are non-empty/spatially plausible without source-string/count-only assertions.

## Implementation
- [x] Add reusable transformed triangle mesh→voxel API/configuration with voxel size, fill policy, bounds/cost limits, material input/fallback, and thin-feature policy.
- [x] Conservatively rasterize triangle coverage rather than vertex-only quantization.
- [x] Fill intended closed interiors using bounded exterior flood fill while preserving surface material ownership.
- [x] Apply source transform before grid quantization, including non-uniform/mirrored transforms.
- [x] Diagnose welded boundary/non-manifold topology and make open-source fill behavior explicit (`Reject` or safe `SurfaceOnly`) rather than silently inventing solids.
- [x] Add deterministic sparse baked-cell codec/artifact and replay through `IStructureAuthoringSession`.
- [x] Add generic Unity authoring bridge for nested `MeshFilter` hierarchies and reusable baked `SkinnedMeshRenderer` poses with deterministic submesh material mapping.
- [x] Add reusable bounded offline bake-analysis/fidelity metrics for surface cells, connected components, material/sparse-brick counts, sampled symmetric p95 distance, and fixed-view silhouette IoU.
- [x] Add isolated one-shot structure-selection state object; production VoxelShowcase input wiring still pending.
- [ ] Add source-specific authoring/bake configuration for the chosen Delatronic model and generate/commit the baked dragon artifact; ordinary runtime must never execute mesh voxelization.
- [ ] Preserve major source material/color regions with deterministic palette mapping/quantization.
- [ ] Instantiate baked dragon through normal `ShowcaseWorld`/WorldBuilder voxel authoring so collision/edit/destruction read the same storage.
- [ ] Wire explicit VoxelShowcase structure-selection mode: scroll selects while active, Space commits once, ordinary controls unchanged outside mode.
- [ ] Add dedicated labeled `Mesh -> Voxels` comparison area with matched pose/scale/orientation/ground/lighting; source mesh is presentation-only and has no collider/gameplay authority.
- [ ] Add durable comparison capture support for front, side, rear, front 3/4, rear 3/4, elevated/top 3/4, plus head/horns, wing, feet/claws, tail closeups.
- [ ] Add supplemental symmetric source↔voxel surface-distance metric and fixed-view silhouette IoU evidence (targets p95 <=1.5 voxels, primary IoU >=0.90 unless a documented thin-feature limitation is visually acceptable).
- [ ] Emit deterministic source triangle count, voxel resolution, authored voxel count, sparse brick/chunk count, voxelization duration, serialized size, resident/runtime placement/build cost.
- [ ] Add destruction/world-truth validation instance proving voxel edit changes rendering/collision without source-mesh shell/collider fallback.

## Reusability review
- [x] Keep `MeshVoxelization` and its metrics/configuration completely mesh-agnostic; no dragon names, anatomy rules, source IDs, or showcase controls in engine mesh-import/runtime code.
- [x] Keep source selection, dragon-specific bake configuration, palette choices, comparison staging, and placement/input modes in game/showcase composition above the generic importer.
- [x] Add a regression proving a second non-dragon synthetic or fixture mesh can use the same public importer/codec/authoring path without dragon-specific setup or branches (`MeshVoxelizationReuseTests.IndependentBoxFixture_UsesImporterCodecAndCanonicalAuthoringPath`).

## Dragon artifact acceptance
- [ ] Downloaded third-party source is legitimately redistributable and provenance/checksums are committed.
- [ ] Source is detailed/curved/non-voxel-native and genuinely exercises anatomy/silhouette fidelity (roughly 20k+ triangles preferred/practical per issue).
- [ ] Generated structure is volumetric, sparse, within X/Z<=127 and Y<=511, and preserves recognizable head/body/wings/limbs/feet/tail/secondary detail.
- [ ] Source and voxel exhibit use same effective transform/pose; major material regions remain recognizable.
- [ ] Human review confirms voxelized result is unmistakably the exact source model, not merely a generic dragon.

## Validation / cost
- [ ] Exact-SHA final CI compiles/passes focused importer/codec/authoring/showcase + dragon-specific regressions.
- [ ] Record import/voxelization time, occupied voxel count, sparse brick/chunk count, serialized size, runtime resident/storage impact, and incremental render/world-build cost within repository budgets.
- [x] Verify ordinary runtime does not execute mesh voxelization and no `MeshCollider`/source-mesh gameplay fallback exists; `ShowcaseWorld.PlaceBakedMeshStructure` accepts/replays only `BakedVoxelStructure`, Unity mesh extraction is isolated to an Editor-only assembly, and repository search finds no `MeshCollider` use. Recorded in `verification-regression-coverage.txt`.
- [x] Current feature-vs-master blast-radius review shows additions confined to mesh-import editor/runtime, showcase mesh-structure/selection helpers, focused EditMode tests, and this issue's plan/tasks; no workflow request or unrelated SceneIssue changes.
- [x] Refresh-merged `origin/master` through `5f07db5cd7677e84f617deb61c5b03a4b896159c` at merge commit `76ecb118cd93010a0169e270822d769e46804123`, preserving current master changes without altering another assignment directly.
- [ ] Re-run full feature diff review after source/showcase/evidence implementation.
- [ ] Refresh/merge current `origin/master` again immediately before final CI if it advanced.
- [ ] Issue one final exact-source PlayMode request only through `ci-test/fixes/agent-1`; do not replace queued/running CI.
- [ ] Obtain green focused behavioral regression plus exact built-player `VoxelShowcase` validation in that final request.
- [ ] Inspect all required built-player views directly for same-source silhouette/anatomy/material fidelity, grounding, negative spaces, and no holes/fused/bloated/broken parts.
- [ ] Classify exact built-player visual evidence `production-quality`; lower classifications fail under current `AGENTS.md`.
- [ ] Confirm built-app destruction/world truth, one-shot spawn, metrics, and no startup/runtime exceptions.

## Promotion / closure
- [ ] Complete pending metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after all gates pass.
- [ ] Move only this assignment `open` → `pending` in separate bookkeeping after implementation/evidence is complete.
- [ ] After green exact-SHA CI/built-app/human visual review, set `status=fixed` + `resolvedUtc` and move only this assignment `pending` → `closed`.
- [ ] Merge latest `origin/master` into `fixes/agent-1`, push feature head, then push that exact head to `origin/master` non-force; fetch/merge/retry if master advances.
