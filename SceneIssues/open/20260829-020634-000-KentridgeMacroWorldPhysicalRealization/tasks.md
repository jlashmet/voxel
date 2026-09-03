# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Inspect assignment; `captures: []`, so `issue.json` is the acceptance contract.
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and real CharacterMotor traversal.
- [x] Production regressions cover determinism, reachability, roads, settlements, blocking geography, explicit semantic route solutions, bounded slopes, storage, water realization, and evidence sequencing.
- [x] Prove reuse with independent non-Kentridge blocked-water and spatial-reservation fixtures; keep Kentridge placement policy out of shared planner/catalogue APIs.
- [x] Keep feature-aware vertical residency within the existing horizontal radius/device/scheduler policy.
- [x] Replace demonstrated-cost solid fallback blockouts with bounded hollow wall shells and perimeter plinths while retaining grounding, footprint/material/envelope, wall support, collision silhouette, and bounds.
- [x] Isolate and correct the playable one-shot macro catalogue ownership transition; runtime catalogue now contains 480 macro-aware definitions.
- [x] Quantify synthetic generic-blockout work reduction: foundation 82,432 -> 29,440 voxels (64.3%), timber body 416,000 -> 71,552 (82.8%), combined foundation+timber 498,432 -> 100,992 (79.7%); roof unchanged.
- [x] Merge the explicitly requested current master renderer restoration into `fixes/agent-6` at `5185b5acddd3d35ec29730ea73d30ae796a705bf`.
- [x] Validate the perimeter-foundation throughput fix on the post-master exact feature state: run `33800775569` passed repository-derived module validation, `GenericBuildingBlockoutUsesBoundedFoundationAndWallShellsInsteadOfSolidVolumes`, and the standalone player process on exact source `5185b5acddd3d35ec29730ea73d30ae796a705bf`.
- [x] Preserve strict publication acceptance after that green gate; the player still stalls after Moordell because `HasCompletePublishedNearSurfaceCoverage()` remains false under the elevated survey, so do not misclassify process success as visual success.
- [x] Apply the two-material-fix rule and isolate the publication root cause before another fix. Experiment 037 proves the radius-3 discrete lattice (`dx²+dz² <= 9`, 29 columns) cannot guarantee the nominal 153.6m continuous renderer disk for every within-region demand point; the conservative guaranteed disk is 102.4m.
- [x] Add independent `KentridgeStreamingCoveragePolicyTests`, which enumerate excluded lattice cells and measure point-to-square distance rather than restating the production formula.
- [x] Keep the selected coverage repair scene-local and config-driven: a Kentridge-only post-scene-load installer narrows only the renderer near-surface completeness ring to the guaranteed radius. Streaming radius 3, far-field streamed radius, generation/device budgets, scheduler policy, and strict coverage semantics remain unchanged.

## Current exact-SHA validation
- [x] Validate the streaming-coverage radius regression through `ci-test/fixes/agent-6` with the required 180-second SceneIssue replay: run `33804821454` is green on exact feature source `50bc9533b811ac587d24503844d3d2c73d0c37bf`, including the requested regression, repository-derived module validation, and standalone player process.
- [x] Because the same strict-coverage symptom remained after that fix, isolate the new boundary before another production change. Experiment 038 shows `missingVisible=0/coreAbsent=0` immediately before the first macro survey, then `coreAbsent=2178` and `2922` as the evidence camera moves ~70m above the ground-pinned CharacterMotor; GPU requests remain admission-bound while strict coverage never recovers.
- [x] Correct only the macro evidence composition to match the existing shipped `StepAutoSurvey` contract: explicit pre-slice execution order, one resolved survey camera position, and `CharacterMotor.EyePosition == rendered survey camera`. No shared renderer/world policy, load radius, generation/device budget, or strict coverage semantic changed.
- [x] Classify run `33811917743` on exact source `a0f9998b755ab9bd467c05a2b1c478598ec84734` as test-harness compile red only: the new focused regression omitted `using MountingForce.WorldGen;`, so `Int2` did not resolve in either module validation or player build. No NUnit/product/player signal exists; experiment 039 records the boundary and the namespace-only correction.
- [ ] Validate `KentridgeMacroWorldSurveyStreamingAlignmentTests.ElevatedSurveyPinsStreamingDemandToRenderedCameraBeforeSliceStreaming` on the corrected current exact feature SHA through `ci-test/fixes/agent-6` with the required 180-second SceneIssue replay.
- [ ] Require strict built-player coverage to advance beyond Moordell after the survey-demand alignment fix; if the same symptom remains, isolate the new failure boundary before another fix.
- [ ] Require real-player authored shell/roof storage and readable evidence for Fairy and Orc, not only Moordell.

## Remaining visual acceptance
- [ ] Full-resolution Moordell/Rossdam/Fairy/Orc surveys visibly show readable grounded authored blockouts, internal street/open space, and road arrival/exit.
- [ ] Rossdam lake frame visibly shows substantial authored water plus the constrained route, not a thin distant strip.
- [ ] Southern Ridge/pass frame visibly establishes the barrier/pass relationship.
- [ ] Final `macro-network-overview` is closure-quality on the final exact SHA.
- [ ] Representative built-player CharacterMotor road traversal is visible and process-clean on the final exact SHA.

## Runtime / cost
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries; shared feature scheduler unchanged.
- [x] Existing exact physical baseline: 20 hard routes, 824 route tiles, 5 constrained routes, 1,090 solve steps, 16 generic buildings, max road rise 2 voxels, road step 30 dm, Rossdam water depth 24 voxels, water primitive bounding cells 9,281,584.
- [x] Horizontal residency remains radius 3 = exactly 29 accepted X/Z columns (`dx²+dz² <= 9`); no horizontal interest-radius expansion or device-budget change.
- [x] Latest pre-survey-alignment replay preserves Moordell readiness around 85 s with 29 in-radius baseline residents and zero feature-only vertical extras.
- [x] Partial runtime cost context captured: median FPS 103.9, median mean-frame 9.61 ms, worker-p95 median 3.018 ms, admission-total median 1.1605 ms, ~26.99 MiB cumulative render-arena uploads over 114 calls. These are not final steady-state/memory closure evidence.
- [ ] Quantify final additional vertical resident/generated region count across the completed multi-target replay.
- [ ] Quantify final target convergence timing for Moordell, Rossdam/lake, Fairy, Orc, ridge/pass, and network.
- [ ] Measure final feature work/time, CPU/FPS, streaming convergence, render/far-field telemetry, and process/managed/native/GPU memory footprint against budgets.
- [x] No unrelated SceneIssue implementation, feature-branch `.github/test-request.json`, alternate CI transport/workflow, CharacterMotor/load-radius/device-budget change, shared renderer semantic change, or weakened coverage gate.
- [ ] Re-check the final feature diff after the required final current-master merge and revalidate affected work if master advanced.

## Acceptance / closure
- [x] (1) Source-backed macro graph remains authoritative through shared WorldBuilder APIs.
- [ ] (2) Every settlement has readable physical presence, including >=4 grounded generic blockouts where no richer generator owns it.
- [x] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [x] (4) Roads are terrain-aware; blocked geography requires explicit semantic solutions and no silent lake/cliff/building crossing.
- [x] (5) Reusable geographic authoring/query covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, and route/placement constraints.
- [ ] (6) Built world visibly contains a substantial lake + ridge and at least one geography-altered hard route.
- [ ] (7) Regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [x] (8) No second scene-local graph/direct voxel-writing/static destination hierarchy.
- [x] (9) Focused behavioral regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, spatial reservations, bounded water cost, evidence sequencing, vertical residency/readiness, settlement framing, hollow-shell/plinth cost, and runtime publication/storage discriminators. The perimeter-foundation assertion is green in exact-SHA run `33800775569`; the independent coverage-radius regression is green in `33804821454`; the survey-demand alignment regression is pending corrected exact-SHA validation after the harness-only compile failure in `33811917743`.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal without runtime exceptions.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets; memory footprint and final multi-target steady-state evidence remain missing.
- [ ] Complete `resolutionSummary`, `regressionTest`, and `fixCommit` from the verified final result.
- [ ] Every checkbox above is complete before closure.
- [ ] After green exact gates, move this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, merge current `origin/master`, and non-force push that exact feature head to master; fetch/merge/retry if master advances.
