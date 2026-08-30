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

## Latest-master / exact-SHA evidence
- [x] Merge current `origin/master` `e95324aeaef6...` into `fixes/agent-4` as true two-parent merge `a0e56301d676...`; preserve both current road/WorldBuilder work and agent-4 fast paths.
- [x] Resolve the shared `PrimitiveRasteriser` overlap while retaining agent-4 Box/Frustum fast paths plus master `TerrainCorridor` dispatch/containment.
- [x] Keep exact run `33298125653` untouched; its fresh bake completed in 225 s at ~5.37 GB RSS/0 swap and real-player traversal reached 17/17 grounded/Y-checked waypoints in 57.4 s.
- [x] Diagnose its only focused failure as omitted upper startup-bake dragon material rather than path/headroom/traversal failure.
- [x] Implement bake-only explicit fixed-altitude `Structure` coverage plus `FixedAltitudeStructures` sparse build scope without changing runtime streaming/gallery baking.
- [x] Add `ShowcaseBakeExplicitStructureCoverageTests` and scoped/full semantic-byte equivalence coverage; invoke both from the exact acceptance filter.
- [x] Preserve 240 s / 14 GB workflow contracts through compile/shutdown experiments; do not trade away startup coverage or production-equivalent output.
- [x] Keep run `33310154810` untouched and prove managed `Environment.Exit(0)` stops managed logging but leaves the native Unity PID alive to the 240 s watchdog.
- [x] Harden only the exact successful GitHub Actions bake with POSIX `_exit(0)` after persistence/import/save/success logging; retain managed fallback elsewhere.
- [x] Merge then-current `origin/master` `e17e858bfe04...` as the second parent of `2100df40287a...`; only an unrelated road SceneIssue is inherited.
- [x] Issue request `0b1cac9068bb...` from exact source `2100df40287a...` through existing `ci-test/fixes/agent-4` and leave it untouched.
- [x] Run `MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay` green on source `2100df40287a...`: run `33310677691` / job `99255036891` is green end-to-end.
- [x] Final fresh bake stays under unchanged 240 s / 14 GiB guards, then the next Unity invocation reopens successfully.
- [x] Record final measured bake/runtime/source/payload evidence in `experiment-006-final-exact-sha-acceptance.md`.
- [x] Exact-run captures prove structural acceptance and exact dialogue; keep them as traversal/functional evidence.
- [x] Merge current `origin/master` `dfbc43b086b6...` as second parent of `fb7dd3b14853...`; inherited change tightens the repository visual-quality gate only.

## Visual quality gate — current master
- [x] Re-review exact green-run frames under current `AGENTS.md`: classify mountain/path `prototype/blockout quality`; the issue lowers the bar only for cube dragon art.
- [x] Record specific defects: dominant bright masonry over all masses/supports, repeated primitive/frustum read, retaining-wall-like cut faces, weak ground-cover/material integration around the dirt ascent.
- [ ] Generalize the reusable mountain catalogue to distinct rock and ground-cover/support material roles while preserving a single-material compatibility overload.
- [ ] Compose VoxelShowcase mountain with dark rock + moss/ground-cover support banks + distinct dirt road; keep the explicitly permitted red cube placeholder unchanged.
- [ ] Bump the startup landmark contract revision because the rendered realization/material program changes without layout-dimension changes.
- [ ] Add focused behavioral regression proving rock/ground-cover role separation while preserving tapered supports, traversal lane/headroom, primitive budget, and non-scene-local composition.
- [ ] Run exact final PlayMode + built-player evidence after visual changes and classify approach/base/middle/upper/summit/dialogue frames `production-quality`; if not, continue with silhouette/support improvements before closure.

## Checked-in startup payload gate
- [x] Retrieve the accepted source-matched generated startup payload + manifest from run `33310677691` without another CI transport: payload `19,122,896` bytes, SHA-256 `cf51ac9fd9da8a753bf625cc14430ee4941e1ea2f7404be43fd3a2f8323c5da4`, signature `0x7C8A5152`.
- [x] Verify the feature branch still ships stale startup state: tracked `ShowcaseWorld.bytes` is `11,074,525` bytes and `ShowcaseWorld.manifest.txt` is absent while provenance validation requires both.
- [ ] After the visual fix is accepted, replace the stale checked-in `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` with that new exact accepted artifact and add its matching `ShowcaseWorld.manifest.txt` through a repository-sanctioned binary write/refresh path; do not weaken provenance validation, runtime-author the mountain, or create another CI transport.
- [ ] Validate a clean checkout consumes the exact checked-in accepted payload/manifest; if the binary commit creates a new required exact-source gate, validate that source before closure.

## Closure gate
- [ ] Confirm every acceptance criterion in `issue.json` and every checkbox above is complete.
- [ ] Complete pending metadata on `fixes/agent-4` and move only this assignment `open -> pending`.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`, and preserve validation/capture provenance.
- [ ] Fetch current `origin/master`, merge it into `fixes/agent-4`, and verify master ancestry again.
- [ ] Push the exact feature head to `origin/master` non-force; if master advanced, fetch/merge/retry without modifying another assignment.
