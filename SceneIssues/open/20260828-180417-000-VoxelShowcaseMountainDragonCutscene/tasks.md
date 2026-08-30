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
- [x] Issue exact-parent request commit `53469c7ab882...` with feature parent `2106820d31cb...` only through `ci-test/fixes/agent-4`; no feature-branch request edit, reset transport, or request replacement.
- [x] Keep run `33298125653` untouched through completion and inspect its source-matched result/artifact before any further request.
- [x] Fresh current-master bake completed in 225 s under the 240 s guard at ~5.37 GB peak RSS / 0 swap growth, persisted 178,034,009-byte payload digest `fd994e3ab598...`, and the next Unity invocation reopened successfully.
- [ ] Run `VoxelEngine.Tests.PlayMode.MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay` green on the exact final feature SHA.
- [x] Prove the complete 17-waypoint route via production `AutoWalk -> CharacterMotor.Step`: run `33298125653` reached 17/17 in 57.4 s with required grounded/Y predicates through the summit.
- [x] Human-review run `33298125653` named approach/base/middle/upper/summit/dialogue captures: the mountain/path are visible across the route, the summit placeholder is visibly supported, and the dialogue frame visibly presents `Hello, I'm Mr. Dragon.`; keep these as traversal evidence but not final acceptance while the focused test is red.
- [x] Verify run `33298125653` focused failure is not path/headroom/traversal: startup-bake acceptance requires dragon material at centre `(-1112,530,200)`, but the source-matched bake omits that upper vertical region while runtime streaming later realizes the visible placeholder.
- [x] Implement bake-only explicit fixed-altitude `Structure` coverage planning and materialisation so the source baker requests structure-owned vertical layers without changing runtime streaming or gallery baking.
- [x] Keep exact request `670d9f16a920...` / run `33299149060` untouched through completion; classify it as a compile-only product failure before bake/test execution, so it supplies no new bake-cost evidence.
- [x] Repair only the unsupported `int3` vector arithmetic in `ShowcaseWorld.BakeCoverage` and the focused test's signed region conversion; preserve the existing half-open footprint and floor-division contracts.
- [x] Re-check compile-repair blast radius: exactly the bake planner plus its focused PlayMode regression changed; no runtime streaming, geometry, collision, movement, structure program, or bake-budget contract changed.
- [x] Keep exact request `b68865a45a70...` / run `33300355968` untouched through completion and inspect its artifact: Unity compiled and generated/imported a corrected `200 regions, 18.2 MiB` startup image with content signature `0x7C8A5152`, then crossed the 240 s wrapper during shutdown; requested test was skipped while built-player capture still succeeded.
- [x] Add the smallest generic bake-only optimization that materializes an explicit fixed-altitude structure in an otherwise sparse vertical region without paying unnecessary terrain/landform work; `FeatureRegionBuildScope.All` remains the runtime/default contract and only absent bake-discovered sparse structure layers select `FixedAltitudeStructures`.
- [x] Add focused behavioral coverage proving the optimized bake-only path is output-equivalent for the required explicit fixed structure and does not materialize unrelated sky: compare semantic hash + serialized region bytes for full vs scoped actual Mountain Dragon upper-region builds, require one scoped structure instance/one resident region, and retain the exactly-two-layer planner assertion.
- [x] Invoke both sparse-bake coverage regressions from `MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay` so the single exact final request executes them directly.
- [x] Record experiment `experiment-003-sparse-fixed-structure-bake.md` with the 240 s discriminator, selected scope contract, expected blast radius, semantic proof, and remaining cost measurement.
- [x] Keep exact request `606fb586c8ce...` / run `33302046675` untouched through completion; classify its 23 s / ~1.6 GB failure as compile-only, with no bake-cost conclusion. Artifact compiler diagnostics show `ShowcaseWorld.BakeCoverage` tried to exchange `VoxelEngine.Composition.FeatureRegionBuild` with `VoxelEngine.Structures.Runtime.FeatureRegionBuild` at lines 114/115/122.
- [x] Repair the scoped-builder call through the existing `VoxelEngine.Composition.FeatureRegionBuild` bridge so Showcase composition does not cross the Structures.Runtime type boundary; preserve the bridge's default full-runtime scope and existing `CompleteFeatureBuild` bookkeeping.
- [x] Keep exact request `b9fdc14c3c25...` / run `33302337781` untouched through completion and inspect its result artifact before any new request.
- [x] Classify run `33302337781` as a post-bake shutdown timeout, not a generation-cost failure: the bake persisted/imported `200 regions, 18.2 MiB` (`18,136,152`-byte payload + `504,058`-byte manifest), logged successful in-run cost gates, requested CI shutdown, and only then exhausted the 240 s wrapper during Unity graceful teardown.
- [x] Preserve the 240 s / 14 GB workflow contracts and the sparse fixed-structure semantics; do not weaken startup coverage or trade away production-equivalent output to solve a process-teardown overrun.
- [x] Add and regression-cover the strict exact-CI success policy through injectable `CompleteSuccessfulBake`; interactive/local/general test processes retain normal teardown.
- [x] Make the prepared startup payload include the upper vertical region containing the authored summit dragon placeholder without broadly materializing unrelated sky-only regions; run `33302337781` proves the repaired sparse path persisted/imported the 200-region source bake successfully before teardown.
- [x] Add `ShowcaseBakeExplicitStructureCoverageTests` proving the mountain-only catalogue plans exactly the dragon placeholder's lower/upper layers across the Y=512 boundary and does not expand the landform/headroom sky footprint.
- [x] Keep exact request `90e824415962...` / run `33310154810` untouched through completion and inspect its artifact before any further request.
- [x] Classify run `33310154810` as a managed/native shutdown discriminator: the log ends immediately after successful `200 regions, 18.2 MiB` persistence/import with no graceful Unity teardown output, but the native Unity PID remains alive until `tools/unity-run.sh` hits its 240 s watchdog; `Environment.Exit(0)` is therefore insufficient.
- [ ] Harden only the exact successful GitHub Actions showcase bake with a native process-level successful exit (`_exit(0)` on POSIX; managed fallback elsewhere) after all required persistence/import/save/success logging, so the watchdog sees the Unity PID disappear.
- [ ] Re-check bake time/memory blast radius after required-upper-layer capture; do not weaken the 240 s/14 GB contracts.
- [ ] Record final measured bake/runtime evidence and accepted source/payload provenance.
- [ ] Produce/commit the accepted source-matched generated startup payload + manifest before closure if the repository workflow permits retrieval without a second CI transport.

## Closure gate
- [ ] Confirm every acceptance criterion in `issue.json` and every checkbox above is complete.
- [ ] Complete pending metadata on `fixes/agent-4` and move only this assignment `open -> pending`.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`, and preserve validation/capture provenance.
- [ ] Fetch current `origin/master`, merge it into `fixes/agent-4`, and verify master ancestry again.
- [ ] Push the exact feature head to `origin/master` non-force; if master advanced, fetch/merge/retry without modifying another assignment.
