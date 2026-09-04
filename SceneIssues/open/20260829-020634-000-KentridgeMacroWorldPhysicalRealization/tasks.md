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
- [x] Validate `KentridgeMacroWorldSurveyStreamingAlignmentTests.ElevatedSurveyPinsStreamingDemandToRenderedCameraBeforeSliceStreaming`: run `33812580159` is workflow-green on exact source `32b8972a9e6f3876b87b2c2885f806727da23d78`, including the requested regression, repository-derived module validation, and the full 180-second standalone SceneIssue replay.
- [x] Reject artifact `9915578779` for closure despite that green run: Moordell content becomes ready and radius-3 residency is correct, but strict publication never advances to Rossdam/Fairy/Orc/ridge/network. Eight GPU requests remain admission-bound while some geometry continues publishing.
- [x] Apply the required two-material-fix rule before a third remediation. Experiment 040 isolates a deterministic shared-renderer liveness defect: four two-record count-batch lanes share one dispatch-per-frame token, while fixed lane-0 sealing plus inline refill can starve the six records held by lanes 1-3 indefinitely.
- [x] Implement the smallest renderer correctness correction: remove inline sealing, retain all existing capacities/budgets/fences/backpressure, and service eligible full/aged lanes through one round-robin seal cursor that advances only after successful GPU submission.
- [x] Add `GpuCountBatchLaneFairnessTests.SaturatedCountBatchLanesUseRoundRobinSealAuthorityWithoutInlineBypass` as an independent source-contract/liveness regression for the demonstrated saturated-refill pattern.
- [x] Validate the fairness regression itself on exact source `247477d9bf11c7902fdb75a632d5b5535905525a`: run `33816510783` is green and completes in about 3m45s, including the required 180-second SceneIssue replay. Reject the replay for closure because the macro evidence still does not advance beyond Moordell.
- [x] Correct the continuing failure boundary from the replay telemetry: `phases=0x2` is a phase bitmask and therefore means active request phase 1 (`_stageAdmissionPending`), not phase 2. At the stall, `ready=65536`, `pending≈25k-30k`, `demand=8`, and admitted requests reconcile (`published + stale == mirrorReady`), so the remaining boundary is shared-mirror recovery/admission rather than count-batch sealing.
- [x] Merge current `master` into the feature via PR #245 before further remediation/final validation; merge commit `a01e8bd3e2b8659225ebb7373516e7e0ebe57681` has parents `247477d9bf11c7902fdb75a632d5b5535905525a` and master `04c43482768548f96db6f18234f1709a25b0d983`.
- [ ] Validate the post-master fairness + 180-second replay baseline on exact source `a01e8bd3e2b8659225ebb7373516e7e0ebe57681`; request commit `78ad9b4a6cadf4b7a4f77a5274fe3a531cdd55c0` is queued and must not be replaced while queued/running.
- [x] Add `GpuSurfaceMirrorRelocationLivenessTests.DistantRelocationCannotLeaveEveryGpuWorkerAdmissionPending` as the minimal independent repro for the Kentridge survey-relocation boundary. It performs a 384m relocation and fails if all eight demand chains remain mirror-admission pending with zero active extraction for 20 seconds, while reporting mixed-resident slots/capacity to distinguish capacity exhaustion from bookkeeping starvation.
- [ ] Validate the relocation liveness regression on the current exact feature SHA through `ci-test/fixes/agent-6`; if it reproduces, use its slot/residency diagnostics to isolate the smallest renderer correction before changing production behavior.
- [ ] Require strict built-player coverage to advance beyond Moordell after the demonstrated mirror-admission defect is corrected; if the same symptom remains after two materially different fixes, isolate the new failure boundary before another fix.
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
- [x] Latest aligned-survey replay reports Moordell radius-3 residency with `horizontalColumns=29`, `residentInRadius=58`, `baselineResident=58`, and `featureVerticalExtra=0`; the publication failure is not caused by added vertical residency.
- [x] Partial runtime cost context captured: median FPS 103.9, median mean-frame 9.61 ms, worker-p95 median 3.018 ms, admission-total median 1.1605 ms, ~26.99 MiB cumulative render-arena uploads over 114 calls. These are not final steady-state/memory closure evidence.
- [ ] Quantify final additional vertical resident/generated region count across the completed multi-target replay.
- [ ] Quantify final target convergence timing for Moordell, Rossdam/lake, Fairy, Orc, ridge/pass, and network.
- [ ] Measure final feature work/time, CPU/FPS, streaming convergence, render/far-field telemetry, and process/managed/native/GPU memory footprint against budgets.
- [x] No unrelated SceneIssue implementation, feature-branch `.github/test-request.json`, alternate CI transport/workflow, CharacterMotor/load-radius/device-budget change, weakened coverage gate, or increased renderer concurrency/budget.
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
- [ ] (9) Focused behavioral regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, spatial reservations, bounded water cost, evidence sequencing, vertical residency/readiness, settlement framing, hollow-shell/plinth cost, runtime publication/storage discriminators, GPU count-batch liveness, and distant-relocation mirror liveness. Prior regressions are green; the new relocation regression is pending exact-SHA validation.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal without runtime exceptions.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets; memory footprint and final multi-target steady-state evidence remain missing.
- [ ] Complete `resolutionSummary`, `regressionTest`, and `fixCommit` from the verified final result.
- [ ] Every checkbox above is complete before closure.
- [ ] After green exact gates, move this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, merge current `origin/master` if it advanced, revalidate the exact merged feature SHA as required, then promote only by PR + auto-merge. Never push the exact `fixes/agent-6` head directly to `origin/master`.
