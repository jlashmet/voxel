# Tasks

## Feature / architecture gate
- [x] Express the mountain, winding path, summit, dragon placeholder, proximity trigger, and dialogue through WorldBuilder/shared game modules rather than scene-local voxel/proximity/UI code.
- [x] Add focused behavioral regressions for production WorldBuilder mountain/path composition, supported dragon placement, proximity-to-cutscene behavior, and exact dialogue `Hello, I'm Mr. Dragon.`
- [x] Add production built-player waypoint replay through normal `AutoWalk -> CharacterMotor.Step`; no waypoint teleport or test-only movement path.
- [x] Make waypoint arrival enforce production feet Y and `Grounded` where required, with anchored expected elevations through every switchback and summit approach.
- [x] Require a 24-voxel / 2.4 m clear walking envelope above the 1.8 m production motor and occupied support below authored walking floors.

## Mountain composition / suppression gate
- [x] Diagnose missing realized path voxels as castle-owned feature suppression rather than a rasterizer/output mismatch.
- [x] Move the Mountain Dragon footprint one 512-voxel region west so its three X-region columns no longer overlap castle-owned feature-suppression regions.
- [x] Update only layout-derived route/look coordinates for the westward move; preserve the mountain primitive program and route semantics.
- [x] Add `MountainDragonCastleRegionOwnershipTests` and invoke it from the exact final acceptance filter.
- [x] Diagnose landing headroom obstruction as X-ramp overlap with 30-voxel turn landings.
- [x] Add `MountainDragonRampLandingProgramTests`, narrow alternating X ramps to the interior continuity span, add the level-0 base landing, and preserve exact high-end tier elevation.

## Bake cost / semantic fast-path gate
- [x] Measure the nine-region bake repeatedly under the 240 s Unity subprocess contract instead of inferring cost from nominal footprint.
- [x] Add output-equivalent canonical-empty and Uniform full-block `Carve + Box` fast paths while preserving Mixed/partial/non-box semantics and logical write accounting.
- [x] Add output-equivalent fully interior `Fill + Frustum` atomic fill and deep-interior halo-skip paths with focused real-storage/boundary regressions.
- [x] Extend the proven frustum fast paths to `FillIfEmpty` canonical-empty/Uniform-solid cases while leaving Mixed/partial/boundary behavior on the exact path.
- [x] Preserve positive/negative authored boundary samples and deep-interior no-boundary semantics in focused regressions.
- [x] Re-check shared-rasterizer blast radius after every optimization; primitive order, footprint, surface semantics, and observable logical write counts remain unchanged.

## Route obstruction / shutdown discriminator
- [x] Inspect run `33291011394` without replacing it: payload/manifest persistence and baker success completed, but Unity exceeded the 240 s wrapper during compiler/MSBuild shutdown; built-player waypoint 0 also straight-lined through castle-owned geometry.
- [x] Repair only the assignment-owned evidence route with south-exit and southwest-clear transit waypoints before the existing mountain approach; leave every mountain switchback/summit coordinate and grounded-Y expectation unchanged.
- [x] Add a focused editor-policy regression proving immediate post-success process termination is allowed only for the exact GitHub Actions batch `-executeMethod VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeShowcaseWorld` invocation.
- [x] Reject interactive editor bakes, local/non-CI batches, `GITHUB_ACTIONS=false`, `-runTests`, other execute methods, and generic batch processes from the immediate-exit policy.
- [x] Implement the smallest editor-only successful-bake termination after world disposal, capture-suppression restoration, payload/manifest import/save, and success logging.
- [x] Re-check editor-only blast radius: no player/runtime/world-generation behavior changed; only the exact successful CI bake owns the fast termination.

## Exact run `33296679805`
- [x] Issue request commit `1707a18a2572...` with exact feature parent `04a060f2745e...` only through existing `ci-test/fixes/agent-4`; no intermediate reset and no queued/running request replacement.
- [x] Prove a fresh source-matched bake completes cleanly in 224 s, restoring about 16 s of margin under the 240 s process wrapper.
- [x] Prove the following Unity invocation can reopen immediately and run the focused acceptance against the generated source-matched payload.
- [x] Inspect the focused failure: the highest headroom probe `(-1277,512,62)` crossed into an intentionally unmaterialized sky-only vertical region; this was a strict test-reader assumption, not a realized obstruction.
- [x] Correct only headroom-read semantics so an absent sparse vertical region is empty air; floor/path/support reads remain strict and still fail on absent/empty material.
- [x] Inspect the real-player failure: the castle detour worked and the player reached grounded waypoint `12/17` before the route's 55 s internal timeout.
- [x] Re-budget only assignment-owned evidence timing: retain every coordinate, motor speed, grounded requirement, expected Y offset, and capture name; reduce six capture-settle holds from 6.25 s total to 0.6 s and set internal timeout to 58 s inside the fixed 60 s workflow replay ceiling.
- [x] Re-check latest corrective-change blast radius/cost: one PlayMode semantic-reader change plus assignment JSON timing only; no voxel output, geometry, collision, movement speed, streaming, or runtime composition change.

## Latest-master / final exact-SHA gate
- [x] Merge current `origin/master` `e95324aeaef6...` into `fixes/agent-4` as true two-parent merge `a0e56301d676...`; inherit current road WorldBuilder/rendering/storage/structure/test/lifecycle work and preserve agent-4 Mountain Dragon changes.
- [x] Resolve the shared `PrimitiveRasteriser` overlap in `b64ee242a7c2...`: retain all agent-4 proven Box/Frustum fast paths and current master's `TerrainCorridorRasteriser.Rasterise` dispatch plus `PrimitiveShape.TerrainCorridor -> TerrainCorridorRasteriser.Contains`; compare from master shows `behind_by=0` and no master deletions in the rasterizer.
- [x] Verify current `origin/master` `e95324aeaef6...` is an ancestor of the merged feature head (`behind_by=0`).
- [ ] Issue the new exact-parent final request only through `ci-test/fixes/agent-4`; do not edit `.github/test-request.json` on the feature branch or publish an intermediate CI reset head.
- [ ] Keep that exact request untouched while queued/running and inspect the completed result before any further request.
- [ ] Require the bake step to complete cleanly under 240 s with meaningful margin and the next Unity invocation to reopen.
- [ ] Run `VoxelEngine.Tests.PlayMode.MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay` green on the exact final feature SHA.
- [ ] Prove all authored landing floor/headroom columns survive without castle-region suppression on the source-matched bake.
- [ ] Traverse the complete 17-waypoint route via production `AutoWalk -> CharacterMotor.Step`, with every required grounded/Y predicate satisfied.
- [ ] Save and human-review durable approach/base/middle/upper/summit/dialogue captures; reject generic stationary fallback screenshots.
- [ ] Verify the summit dragon is visibly supported and the built-player proximity flow visibly presents `Hello, I'm Mr. Dragon.`
- [ ] Record measured bake/runtime evidence and accepted source/payload provenance.
- [ ] Produce/commit the accepted source-matched generated startup payload + manifest before closure if the repository workflow permits retrieval without a second CI transport.

## Closure gate
- [ ] Confirm every acceptance criterion in `issue.json` and every checkbox above is complete.
- [ ] Complete pending metadata on `fixes/agent-4` and move only this assignment `open -> pending`.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`, and preserve validation/capture provenance.
- [ ] Fetch current `origin/master`, merge it into `fixes/agent-4`, and verify master ancestry again.
- [ ] Push the exact feature head to `origin/master` non-force; if master advanced, fetch/merge/retry without modifying another assignment.