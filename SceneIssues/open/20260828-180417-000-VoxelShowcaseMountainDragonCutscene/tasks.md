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
- [x] Merge master visual-quality instructions and re-review built-player frames under the AAA rubric.
- [x] Classify the original bright-masonry mountain/path as `prototype/blockout quality`; cube dragon remains explicitly permitted placeholder art.
- [x] Add reusable semantic mountain presentation roles (`rock`, `groundCover`, `path`, `placeholder`), compose VoxelShowcase with dark rock/moss/dirt/red-cube roles, and bump startup landmark contract revision to 4.
- [x] Add `MountainDragonVisualFinalAcceptanceTests.ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay` plus Unity metadata.
- [x] Merge current `origin/master` `c2cdde7fef88...` before request `2773c67f655a...`; exact source `a6288a9411c5...` had `behind_by=0`.
- [x] Leave exact run `33314740587` untouched through completion and inspect its NUnit result, bake log, standalone-player log, and exact built-player captures.
- [x] Record run `33314740587` cost result: revision-4 fresh bake succeeded under unchanged 240 s / 14 GiB guards, logged `200 regions, 13.9 MiB`, signature `0x217FA141`, and reopened Unity normally.
- [x] Classify requested-test failure: material-role regression itself passed; old prepared-startup acceptance still expected legacy mountain material `1` although production VoxelShowcase core rock is now `6`.
- [x] Classify standalone-player failure: normal production movement reached grounded waypoint `16/17`, then the evidence harness hit its 58.0 s internal timeout; dialogue had already triggered in the summit frame, but final waypoint/capture was not completed.
- [x] Human-review exact run `33314740587` frames as still `prototype/blockout quality`: repeated giant rounded support banks, pile-of-domes silhouette, hard/extruded path edges with weak terrain transition, and an engineered flat summit pad.
- [ ] Correct only the stale prepared-bake material expectation while retaining generic single-material catalogue tests and exact path/placeholder semantics.
- [ ] Restore deterministic evidence timing margin without changing route coordinates, movement speed, grounded/Y predicates, or gameplay behavior.
- [ ] Replace repetitive segmented support blobs with a reusable natural ridge/support realization that blends into the mountain and avoids repeated cylindrical/domed forms; preserve continuous occupied support and headroom.
- [ ] Improve mountain/summit/path terrain integration enough that exact built-player approach/base/middle/upper/summit/dialogue frames classify `production-quality`; add behavioral geometry regressions for the selected reusable invariant rather than screenshot-string/source checks.
- [ ] Re-run one exact final PlayMode filter + same-run built-player replay through existing `ci-test/fixes/agent-4` only after product fixes and then-current master merge; leave it untouched while queued/running.
- [ ] Re-check final bake/runtime cost under unchanged 240 s / 14 GiB contracts.

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
