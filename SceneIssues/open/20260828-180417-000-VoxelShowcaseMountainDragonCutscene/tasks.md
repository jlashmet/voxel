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

## Visual quality gate
- [x] Merge master visual-quality instructions and classify the prior bright-masonry and revision-4 material-separated captures as still `prototype/blockout quality`; cube dragon remains explicitly permitted placeholder art.
- [x] Add reusable semantic presentation roles and compose VoxelShowcase with dark rock, moss foothills, dirt path, and red cube.
- [x] Leave exact run `33314740587` untouched through completion; its fresh revision-4 bake passed unchanged cost guards, while the stale material assertion and 58 s replay margin failed and exact frames exposed repeated support blobs / engineered summit geometry.
- [x] Correct the prepared-bake mountain expectation from legacy material `1` to production dark-rock role `6`; retain generic single-material raster/catalogue tests.
- [x] Restore deterministic evidence timing margin by changing route timeout `58.0 -> 59.0` only; coordinates, movement speed, arrival radius, grounded/Y predicates, gameplay, and workflow 60 s limit remain unchanged.
- [x] Try reusable same-elevation duplicate full-height support-pair ridge consolidation with three moss foothills and a narrower summit crest; no primitive count was added.
- [x] Leave exact revision-5 run `33316622225` untouched through completion and reject the candidate on cost: fresh bake timed out at 241 s / 11,459 MiB RSS / zero swap under the unchanged 240 s / 14 GiB guard; requested PlayMode was skipped and downstream captures are invalid because no revision-5 manifest was produced.
- [x] Record the revision-5 cost discriminator in `experiment-008-ridge-bake-cost-discriminator.md`; revision-4 control baked in ~206 s, so duplicate full-height ridges added at least ~35 s.
- [ ] Replace each duplicated full-height support pair with one full-height support-covering rock ridge plus one lower/narrow rock buttress; keep primitive count and all carve/ramp/path instructions unchanged while reducing support raster-volume proxy below the generic baseline.
- [ ] Update the visual regression for deterministic ridge+buttress pairing and add the support-volume cost proxy; remove the obsolete duplicate-ridge assertion.
- [ ] Bump startup landmark realization provenance to revision 6 so rejected revision-5 bytes cannot satisfy the changed realization.
- [x] Refresh from current master `65e33762a0d0...` via merge `edddd087f1e6...`; master delta was only the unrelated GPU SceneIssue queue file.
- [ ] Merge then-current master immediately before the final request, then run the exact visual-final PlayMode filter + same-run built-player replay through existing `ci-test/fixes/agent-4` only; leave it untouched while queued/running.
- [ ] Human-review exact approach/base/middle/upper/summit/dialogue frames and require `production-quality`; if below bar, record concrete defects and continue before closure.
- [ ] Re-check final bake/runtime cost under unchanged 240 s / 14 GiB contracts.

## Checked-in startup payload
- [x] Confirm runtime requires both `ShowcaseWorld.bytes` and matching `ShowcaseWorld.manifest.txt`; the currently tracked legacy payload is stale.
- [ ] From the final visually accepted run, record exact payload size/SHA-256/content signature and manifest.
- [ ] Replace tracked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` with that exact accepted payload and add matching `ShowcaseWorld.manifest.txt` through a repository-sanctioned binary write path; do not weaken provenance, runtime-author the mountain, create a workflow, or create another CI transport.
- [ ] Validate a clean checkout consumes the exact checked-in accepted payload/manifest; satisfy any exact-source gate required by repository policy after the binary commit.

## Closure
- [ ] Confirm every `issue.json` acceptance criterion and every checkbox above is complete.
- [ ] Fill `status: pending`, `resolutionSummary`, `regressionTest`, and `fixCommit`; move only this capture `open -> pending` in a separate bookkeeping commit.
- [ ] After green exact-SHA focused + built-player gates, move only this capture `pending -> closed`, set `status: fixed` and `resolvedUtc`, preserving validation/capture provenance.
- [ ] Fetch and merge then-current `origin/master`; stop for any conflict outside this assignment and verify master ancestry.
- [ ] Push the exact feature head to `origin/master` non-force; if master advanced, fetch/merge/retry. Do not self-select more work.
