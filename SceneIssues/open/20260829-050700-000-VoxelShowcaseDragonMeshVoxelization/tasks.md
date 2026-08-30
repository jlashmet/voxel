# Tasks

## Investigation
- [x] Read `AGENTS.md` and canonical `SceneIssues/README.md`; confirm `SceneIssues/feature-readme.md` and `SceneIssues/test-workflow.md` are absent at this revision.
- [x] Confirm `fixes/agent-1` starts from current `origin/master` before implementation.
- [x] Re-check CI transport state: `ci-test/fixes/agent-1` already exists; leave it untouched until the single final targeted-CI request and never replace queued/running CI.
- [x] Record execution limitation before behavior coding: this execution host has no Unity Editor/test runner, so behavior-first tests are committed before production implementation but final exact-SHA CI is the authoritative Unity runner.
- [x] Trace mesh/SDF import, voxel authoring/storage, material palette, showcase composition, collision/edit, and player-capture owners sufficiently to keep the feature additive: no existing arbitrary-mesh voxelizer/structure loader exists; normal gameplay truth is voxel storage.
- [x] Resolve canonical structure-authoring seam: `IStructureAuthoringSession` + concrete `StructureAuthoringSession`/`VoxelBrush`, constructed by `StructuresComposition.CreateAuthoringSession`; baked sparse cells replay through this path rather than introduce mesh truth.
- [x] Resolve importer assembly split: deterministic mesh-data voxelizer/core codec stays reusable and source-format extraction remains an authoring adapter; do not add scene-object dependencies to Structures API.
- [x] Inspect prior dragon/SDF evidence only as reusable-code evidence; no existing arbitrary mesh-to-voxel production path is available to generalize and no other assignment will be modified.
- [x] Verify external source candidates/licensing: Meleagor Sketchfab dragon CC-BY 21.6k tris (binary gated); OpenGameArt artist_71 three-headed dragon CC-BY 4.0 ~24k tris (31.7 MB archive inaccessible here); Khronos Stanford dragon carries Stanford non-commercial restriction.
- [x] Choose a transfer-safe source strategy: commit an original conventional >=20k indexed-triangle dragon authored for this feature and dedicate it CC0-1.0; importer remains fully generic and dragon-free.
- [ ] Generate/commit the exact CC0 source glTF bytes, provenance, triangle/vertex counts, material colors, and SHA-256; verify >=20,000 non-padding triangles and recognizable anatomy.
- [x] Record at least two plausible implementation hypotheses and the smallest discriminator in `plan.md`; discriminator supports additive structure-authoring replay and finds no existing importer to generalize.
- [x] Check blast radius/cost: importer/showcase additions only; preserve terrain/building/storage/collision/edit semantics; preflight bounds/dense-cell budget before flood fill; no runtime voxelization.
- [x] Resolve exact VoxelShowcase scene/runtime owner: `Assets/Scenes/VoxelShowcase.unity` -> GUID `12be027be786465c9a6c8be1321251fd` -> `Assets/Game/Composition/Showcase/SceneRuntime/VoxelShowcase.cs`.
- [x] Resolve live input discrepancy and smallest integration point: Space is currently jump/fly and wheel is brush radius; add explicit structure-selection mode so wheel/Space are consumed only while selection is active, preserving normal movement otherwise.

## Behavior-first regressions (commit before production implementation)
- [x] Add compile-intended importer contract tests before production code (`9164857ad304dc95a6e182e8e982251d5a918567`).
- [ ] Curved synthetic closed geometry proves conservative surface coverage and solid interior fill.
- [ ] Same input/config produces stable ordered voxel/material output.
- [ ] Off-origin transform, rotation/non-uniform scale, and mirrored orientation are covered.
- [ ] Material mapping is deterministic through production importer output.
- [ ] Thin curved/sheet feature retention is covered without global bloat.
- [ ] Invalid indices/non-finite transforms/oversized bounds fail preflight before dense allocation.
- [ ] Sparse artifact codec round-trips exactly and rejects malformed/out-of-bounds data.
- [ ] Baked cells replay through `IStructureAuthoringSession` and normal occupancy reads.
- [ ] Showcase selection/Space commit is single-trigger; idle/update repetition cannot duplicate placement.

## Implementation
- [ ] Add reusable transformed triangle mesh→voxel API/configuration with configurable voxel size, fill policy, bounds/cost limits, material input/fallback, and thin-feature policy.
- [ ] Conservatively rasterize triangle coverage rather than vertex-only quantization.
- [ ] Fill intended closed interiors predictably using bounded exterior flood fill; preserve surface material ownership.
- [ ] Handle off-origin/nested-equivalent transforms, non-uniform scale, rotation, mirroring, pivot/bounds, and deterministic repeat conversion.
- [ ] Preserve major material/color regions with deterministic fallback only when source color is unavailable.
- [ ] Add deterministic sparse baked-cell codec/artifact; ordinary runtime never executes mesh voxelization.
- [ ] Replay baked artifact through `StructuresComposition.CreateAuthoringSession` / `IStructureAuthoringSession.Set`, then normal storage publication; no dragon-specific procedural voxel shortcut.
- [ ] Integrate generated dragon through `ShowcaseWorld` normal voxel placement so collision/edit/destruction read the same storage.
- [ ] Add explicit VoxelShowcase structure-selection mode: scroll selects while active, Space commits selected dragon once, normal jump/brush controls remain unchanged outside mode.
- [ ] Add dedicated labeled `Mesh -> Voxels` comparison area with matched pose/scale/orientation/ground/lighting; source mesh is presentation-only and has no collider/gameplay authority.
- [ ] Add durable comparison-capture support for source solid/wireframe, voxel preview, final spawn, and head/wing/feet/tail closeups without creating another CI transport.
- [ ] Add source-vs-voxel metrics (surface distance and fixed-view silhouette evidence) where deterministic built-player capture supports them.
- [ ] Emit deterministic source triangle count, voxel resolution, authored voxel count, voxelization duration, serialized size, and runtime placement duration.

## Dragon artifact acceptance
- [ ] Source is a real indexed triangle mesh with >=20,000 non-padding triangles and committed CC0 provenance.
- [ ] Generated structure has >=6,000 authored voxels, volumetric occupancy on all three axes, X/Z <=127 and Y<=511.
- [ ] Head/neck/body, two wings/negative spaces, four legs/feet, horns/spines, and curved tail remain spatially plausible in the baked artifact.
- [ ] Source and voxel exhibit use the same transform/pose and major material regions remain recognizable.

## Validation / cost
- [ ] Run focused EditMode importer/codec/authoring/showcase tests in final exact-SHA CI.
- [ ] Record source import/voxelization time, occupied voxel count, sparse serialized size, runtime resident/storage impact, and incremental placement/build cost.
- [ ] Verify ordinary runtime does not execute mesh voxelization and no `MeshCollider`/source-mesh gameplay fallback exists.
- [ ] Review full feature diff for unrelated capture/workflow/request changes.
- [ ] Merge current `origin/master` before final CI if it advanced.
- [ ] Issue one final exact-source request only through `ci-test/fixes/agent-1`; do not replace queued/running CI.
- [ ] Obtain green focused behavioral regression plus exact built-player `VoxelShowcase` validation in the final request.
- [ ] Inspect required built-player views directly for same-source fidelity, silhouette/anatomy, material regions, grounding, negative spaces, and absence of holes/fused/bloated/broken parts.
- [ ] Confirm built-app one-shot spawn, destruction/world-truth evidence, metrics, and no startup/runtime exceptions.

## Promotion / closure
- [ ] Complete pending metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after all gates pass.
- [ ] Move only this assignment `open` → `pending` in separate bookkeeping after implementation/evidence is complete.
- [ ] After green exact-SHA CI/built-app/human visual review, set `status=fixed` + `resolvedUtc` and move only this assignment `pending` → `closed`.
- [ ] Merge latest `origin/master` into `fixes/agent-1`, push feature head, then push that exact head to `origin/master` non-force; fetch/merge/retry if master advances.