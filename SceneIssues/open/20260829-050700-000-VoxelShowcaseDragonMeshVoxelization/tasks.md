# Tasks

## Investigation
- [x] Read `AGENTS.md` and canonical `SceneIssues/README.md`; confirm `SceneIssues/feature-readme.md` is absent at this revision.
- [x] Confirm `fixes/agent-1` starts from current `origin/master` before implementation.
- [ ] Trace existing mesh/SDF import, voxel stamp/storage, structure placement, material palette, showcase composition, collision/edit, and player-capture owners.
- [ ] Inspect prior dragon/SDF work only as reusable-code evidence; do not modify another assignment.
- [ ] Verify and document a detailed conventional dragon source with redistribution-compatible license, URL, author, format, triangle/vertex counts, checksum, and required attribution files.
- [ ] Record at least two plausible implementation hypotheses and the smallest discriminator in `plan.md`.
- [ ] Check blast radius against terrain/building voxel paths and authoritative voxel/world-truth invariants.

## Implementation
- [ ] Add reusable editor/build mesh→voxel API/configuration accepting source mesh/hierarchy, scale/transform, voxel resolution, occupancy/interior-fill policy, material mapping, and thin-feature policy.
- [ ] Conservatively rasterize triangle coverage rather than vertex-only quantization.
- [ ] Fill intended closed/nearly-closed interiors predictably; preflight/report unsupported/open/non-manifold cases.
- [ ] Handle nested transforms, non-uniform transforms where supportable, mirrored orientation, pivot/bounds, and deterministic repeat conversion.
- [ ] Preserve major material/color regions through deterministic normal voxel-material mapping.
- [ ] Bake reusable sparse canonical voxel output; no runtime triangle voxelization or second gameplay geometry truth.
- [ ] Integrate baked dragon through normal WorldBuilder/voxel placement and canonical collision/edit/destruction path.
- [ ] Add dedicated labeled `Mesh -> Voxels` comparison area to built `VoxelShowcase` with matched pose/scale/orientation/ground/lighting.
- [ ] Add durable comparison-capture support for required six overall views and head/wing/feet/tail close-ups without creating a new CI transport.
- [ ] Add source-vs-voxel surface-distance and fixed-view silhouette-IoU evidence.

## Regressions
- [ ] Curved synthetic closed geometry proves triangle surface coverage and solid interior fill.
- [ ] Same input/config produces byte/voxel-stable deterministic output.
- [ ] Transform/orientation/mirroring behavior is covered.
- [ ] Material mapping is covered through production importer output.
- [ ] Thin curved/sheet feature retention is covered without global bloat.
- [ ] Dragon artifact anatomy regions are non-empty/spatially plausible without count-only/source-string proof.
- [ ] Baked artifact places through normal WorldBuilder/sparse storage and participates in normal occupancy/collision queries.
- [ ] Destruction/edit regression proves removed canonical voxels affect both rendering/world truth and collision, with no source-mesh fallback.

## Validation / cost
- [ ] Record source import/voxelization time, occupied voxel count, sparse brick/chunk count, serialized baked size, runtime resident memory, and incremental render/world-build cost.
- [ ] Verify ordinary runtime does not execute mesh voxelization.
- [ ] Review full feature diff for unrelated capture/workflow/request changes.
- [ ] Merge current `origin/master` before final CI if it advanced.
- [ ] Issue one final exact-source request only through `ci-test/fixes/agent-1`; do not replace queued/running CI.
- [ ] Obtain green focused behavioral regression plus exact built-player `VoxelShowcase` validation in the final request.
- [ ] Inspect all required built-player comparison views directly for same-specific-dragon fidelity, silhouette/anatomy, material regions, grounding, negative spaces, and absence of holes/fused/bloated/broken parts.
- [ ] Confirm surface-distance p95 target <= 1.5 voxels and primary silhouette IoU target >= 0.90, investigating visible outliers instead of lowering thresholds.
- [ ] Confirm built-app destruction/world-truth evidence and no startup/runtime exceptions.

## Promotion / closure
- [ ] Complete pending metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after all gates pass.
- [ ] Move only this assignment `open` → `pending` in separate bookkeeping.
- [ ] After green exact-SHA CI/built-app/human visual review, set `status=fixed` + `resolvedUtc` and move only this assignment `pending` → `closed`.
- [ ] Merge latest `origin/master` into `fixes/agent-1`, push feature head, then push that exact head to `origin/master` non-force; fetch/merge/retry if master advances.
