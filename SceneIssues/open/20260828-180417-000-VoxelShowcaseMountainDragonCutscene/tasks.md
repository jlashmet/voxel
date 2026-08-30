# Tasks

## Architecture / functional acceptance
- [x] Compose mountain, winding ascent, summit destination, cube dragon, reusable proximity trigger, cutscene/dialogue through WorldBuilder/shared modules; no scene-local voxel/proximity/UI implementation.
- [x] Production built-player replay uses normal `AutoWalk -> CharacterMotor.Step`, enforces grounded/Y arrival through all 17 waypoints, and requires 24-voxel headroom/support beneath the authored route.
- [x] Move the mountain footprint clear of castle-owned feature suppression and preserve accessible landings/switchbacks.
- [x] Add focused production regressions for mountain/path composition, castle ownership, ramp landings, support, headroom, startup bake, upper dragon structure coverage, proximity/cutscene, and exact dialogue `Hello, I'm Mr. Dragon.`
- [x] Keep shared Box/Frustum raster fast paths output-equivalent and inside existing primitive/write-accounting contracts.
- [x] Reconcile `fixes/agent-4` with current `origin/master` before new implementation; merge PR #174 brought only documentation/SceneIssue metadata and had no Mountain Dragon overlap.
- [x] Refresh with current master/workflow before the remaining reuse pass; merge PR #177 updated only workflow guidance and unrelated master work.

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
- [x] Replace each duplicated full-height support pair with one full-height support-covering rock ridge plus one lower/narrow rock buttress; keep primitive count and all carve/ramp/path instructions unchanged.
- [x] Update the visual regression for deterministic ridge+buttress pairing and add a conservative support-volume cost proxy; current authored revision-6 proxy is ~57.3% of the generic support bound and the regression requires <75%.
- [x] Bump startup landmark realization provenance to revision 6 so rejected revision-5 bytes cannot satisfy the changed realization.
- [x] Refresh from current master `65e33762a0d0...`; no additional merge was needed before revision-6 request because master was already an ancestor.
- [x] Run exact revision-6 request `33318216711` from source `c597b35512f1...` through existing `ci-test/fixes/agent-4` only and leave it untouched: fresh bake ~232 s under unchanged guard, focused visual-final PlayMode green, same-run player replay complete with 17-waypoint route / 16 grounded-required waypoints / exact dialogue.
- [x] Human-review exact revision-6 approach/base/middle/upper/summit/dialogue frames and reject the candidate as `prototype/blockout quality`: exposed rounded/cylindrical berms, tiled retaining walls, slab road edges, causeway terraces, repeated upper supports, and artificial summit platform. Record in `experiment-009-revision6-visual-review.md`; reject its generated payload/manifest.
- [x] Treat revision 6 as the third genuine failed fix attempt and stop production-code iteration until a minimal reproduction is committed.
- [x] Isolate and record the route/core failure in `experiment-010-switchback-core-gap-minimal-repro.md`: every fixed-Z tier begins 10 voxels outside the tapered core and ends 79 voxels outside it, while the constant 360-voxel upper run cannot fit the 86-voxel near-summit radius.
- [ ] Re-author the reusable path/core topology so switchback runs taper with elevation and integrate into one coherent mountain mass instead of requiring freestanding walls/berms; share the same geometry helpers with route evidence/waypoints and preserve winding ascent, normal grounded traversal, headroom, landings, and supported summit. Revision-8 shell-following segmentation is implemented but not yet acceptance-green.
- [ ] Preserve an exposed shell-hugging ascent: each walking lane must overlap the natural mountain flank enough to read as a cut terrace, but must not sit wholly outside the core as a freestanding causeway or wholly inside it as a trench/tunnel; validate this envelope at both low and high ends of every tier. Revision-8 regressions cover this shape but await a green exact wrapper.
- [ ] Add regressions proving each tier's complete walking lane is integrated with the tapered core or modest embankment envelope, upper run length narrows deterministically with elevation, traversal instructions/evidence use the same helpers, and support raster-cost proxy does not regress. Exact source `5e13bbe...` run `33337836269` reached this regression and failed the unchanged support-cost invariant: displayed proxy `1,029,312,508` versus required `< 904,378,308` (raw `257,328,127` versus generic `301,459,436`, ~85.35% rather than <75%). Fresh bake succeeded, so this is a product/test failure, not infrastructure.
- [x] Bump startup realization provenance for the topology change to revision 8 so rejected revision-6 bytes cannot satisfy the changed realization.
- [ ] Reduce the revision-8 semantic support realization below the existing <75% generic support-cost proxy without inflating the generic baseline or weakening the 240 s / 14 GiB guard; the shell-integrated route should use modest local embankment/support rather than exposed full-height retaining masses. Same-run player evidence from `33337836269` still showed engineered/blockout support forms, so visual quality and cost point to the same required fix.
- [ ] Merge then-current master immediately before the next exact visual-final request, then use the existing `ci-test/fixes/agent-4` transport only and leave it untouched while queued/running.
- [ ] Human-review the next exact approach/base/middle/upper/summit/dialogue frames and require `production-quality`; if below bar, record concrete defects and continue before closure.
- [ ] Re-check final accepted bake/runtime cost under unchanged 240 s / 14 GiB contracts. The `33337836269` capture replay reached only 8/17 route waypoints within its 45 s capture window, so it is not final traversal/dialogue evidence.

## Reusability review
- [ ] Remove VoxelShowcase/player-specific physical assumptions from reusable WorldBuilder mountain APIs. Derive headroom, traversal-lane clearance, voxel scale, and movement-envelope requirements from shared traversal/profile inputs or explicit landmark configuration; keep showcase-specific values in Showcase composition. A partial composition profile exists, but the landmark API is not yet wired to it; the branch constructor was restored to the existing compilable signature before further work.
- [x] Replace `WorldBuilderMountainLandmarkMaterialCatalogue` post-processing of compiled feature-program indices/order/material slots with semantic mountain authoring/configuration. Naturalization now emits directly from `MountainLandmarkSpec`, semantic material roles, and `MountainLandmarkPresentationProfile`; it does not patch a compiled definition/instruction stream.
- [ ] Replace `ShowcaseWaypointReplayHarness` reflection into private `VoxelShowcase` fields and its duplicated AutoWalk turn-rate policy with a narrow public replay/movement-control seam. Evidence replay must survive ordinary internal refactors of `VoxelShowcase` and continue to use real normal-movement physics after the initial start placement.
- [ ] Generalize startup-bake provenance so the reusable mechanism accepts a caller-provided content/source signature; keep `ShowcaseMountainDragonLayout` and manual Mountain Dragon revision composition outside the generic provenance implementation. Implementation + independent fixture are committed; exact focused CI is still pending because run `33337836269` failed later in the topology/visual wrapper.
- [ ] Clean engine-level rasterizer comments/names that imply mountain-only behavior where the implementation is generic, and retain regression coverage proving the Box/Frustum fast paths remain reusable for non-Mountain-Dragon callers.

## Checked-in startup payload
- [x] Confirm runtime requires both `ShowcaseWorld.bytes` and matching `ShowcaseWorld.manifest.txt`; the currently tracked legacy payload is stale.
- [ ] From the final visually accepted run, record exact payload size/SHA-256/content signature and manifest.
- [ ] Replace tracked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` with that exact accepted payload and add matching `ShowcaseWorld.manifest.txt` through a repository-sanctioned binary write path; do not weaken provenance, runtime-author the mountain, create a workflow, or create another CI transport.
- [ ] Validate a clean checkout consumes the exact checked-in accepted payload/manifest; satisfy any exact-source gate required by repository policy after the binary commit.

## Closure
- [ ] Confirm every `issue.json` acceptance criterion and every checkbox above is complete.
- [ ] After green exact-SHA focused + built-player gates and accepted visual evidence, fill `resolutionSummary`, `regressionTest`, `fixCommit`, set `status: fixed` and `resolvedUtc`, and move only this assignment directly `open -> closed` in the feature branch.
- [ ] Fetch and merge then-current `origin/master`; stop for any conflict outside this assignment and verify master ancestry.
- [ ] Push the exact feature head to `origin/master` non-force; if master advanced, fetch/merge/retry. Do not self-select more work.
