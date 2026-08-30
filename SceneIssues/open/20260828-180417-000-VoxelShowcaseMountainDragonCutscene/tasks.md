# Tasks

## Architecture / functional acceptance
- [x] Compose mountain, winding ascent, summit destination, cube dragon, reusable proximity trigger, cutscene/dialogue through WorldBuilder/shared modules; no scene-local voxel/proximity/UI implementation.
- [x] Production built-player replay uses normal `AutoWalk -> CharacterMotor.Step`, enforces grounded/Y arrival through all 17 waypoints, and requires 24-voxel headroom/support beneath the authored route.
- [x] Move the mountain footprint clear of castle-owned feature suppression and preserve accessible landings/switchbacks.
- [x] Add focused production regressions for mountain/path composition, castle ownership, ramp landings, support, headroom, startup bake, upper dragon structure coverage, proximity/cutscene, and exact dialogue `Hello, I'm Mr. Dragon.`
- [x] Keep shared Box/Frustum raster fast paths output-equivalent and inside existing primitive/write-accounting contracts.

## Bake / exact-CI infrastructure
- [x] Add bake-only explicit fixed-altitude `Structure` coverage and sparse `FixedAltitudeStructures` scope so the startup image includes the upper dragon layer without broad sky materialization or runtime-streaming changes.
- [x] Prove scoped/full upper-dragon builds match semantic hash + serialized region bytes.
- [x] Keep the 240 s / 14 GiB bake guards unchanged; prior accepted source bake measured ~5.37 GiB RSS / zero swap.
- [x] Restrict immediate bake termination to exact successful GitHub-Actions batch `ShowcaseWorldBaker.BakeShowcaseWorld`; interactive/local/test/other batch invocations retain normal teardown.
- [x] Replace insufficient managed `Environment.Exit(0)` with POSIX `_exit(0)` only on that exact successful CI path, after persistence/import/save/success logging.
- [x] Run `33310677691` on exact source `2100df40287a...`: fresh bake, native exit, Unity reopen, structural PlayMode acceptance, 17/17 built-player traversal, capture, and final status all green.

## Current-master visual quality gate
- [x] Merge master visual-quality instructions (`dfbc43b086b6...`) and re-review exact built-player frames under the AAA rubric.
- [x] Classify the previous mountain/path as `prototype/blockout quality`: dominant bright masonry, repeated primitive/frustum read, retaining-wall-like cut faces, weak terrain/material integration. Cube dragon remains explicitly permitted placeholder art.
- [x] Generalize reusable mountain presentation to semantic `rock`, `groundCover`, `path`, and `placeholder` roles while preserving the existing single-material physical catalogue as compatibility behavior.
- [x] Compose VoxelShowcase with dark-rock core, moss/ground-cover shoulders/support banks, dirt path, and unchanged red cube placeholder.
- [x] Bump startup landmark contract revision from 3 to 4 so the previous single-material bake is rejected as visually stale.
- [x] Add `MountainDragonVisualFinalAcceptanceTests.ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay`, proving role separation changes only additive shoulder/support frustum material fields while preserving primitive sequence/count, path, placeholder, and the established structural acceptance.
- [x] Add committed Unity `.meta` files for the new material-role source and visual acceptance test.
- [ ] Merge current `origin/master` immediately before the final request and verify `behind_by=0`.
- [ ] Run the exact new final PlayMode filter plus built-player VoxelShowcase replay through the existing `ci-test/fixes/agent-4` transport only; leave the request untouched while queued/running.
- [ ] Human-review exact approach/base/middle/upper/summit/dialogue frames and classify the mountain/path `production-quality`; if not, add concrete visual defects here and continue before closure.
- [ ] Re-check final bake/runtime cost under unchanged 240 s / 14 GiB contracts after the visual material change.

## Checked-in startup payload
- [x] Confirm runtime requires both `ShowcaseWorld.bytes` and matching `ShowcaseWorld.manifest.txt`; the currently tracked 11,074,525-byte legacy payload is stale.
- [ ] From the final visually accepted run, record exact payload size/SHA-256/content signature and manifest.
- [ ] Replace tracked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` with that exact accepted payload and add matching `ShowcaseWorld.manifest.txt` through a repository-sanctioned binary write/refresh path; do not weaken provenance, runtime-author the mountain, create a workflow, or create another CI transport.
- [ ] Validate a clean checkout consumes the exact checked-in accepted payload/manifest; satisfy any exact-source gate required by repository policy after the binary commit.

## Closure
- [ ] Confirm every `issue.json` acceptance criterion and every checkbox above is complete.
- [ ] Fill `status: pending`, `resolutionSummary`, `regressionTest`, and `fixCommit`; move only this capture `open -> pending` in a separate bookkeeping commit.
- [ ] After green exact-SHA focused + built-player gates, move only this capture `pending -> closed`, set `status: fixed` and `resolvedUtc`, preserving validation/capture provenance.
- [ ] Fetch and merge then-current `origin/master`; stop for any conflict outside this assignment and verify master ancestry.
- [ ] Push the exact feature head to `origin/master` non-force; if master advanced, fetch/merge/retry. Do not self-select more work.
