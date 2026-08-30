# Tasks — Kentridge macro-world physical realization

## Implemented feature foundation
- [x] Inspect assignment; `captures: []`, so `issue.json` is the acceptance contract.
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic regional intent for lake/water, mountain/ridge, valley/pass, plains/meadow, woodland, generic regions, extents/elevation, relationships, route/placement constraints, and terrain output.
- [x] Add deterministic terrain-aware hard-route solving with slope limits and explicit crossing/pass/around semantics; reject unsolved blocked hard routes.
- [x] Physically realize every macro settlement; retain richer Kentridge/Hightown and generate four grounded reusable blockouts plus streets for Moordell, Rossdam, Fairy Village, and Orc Village.
- [x] Emit continuous generated route surfaces and validate representative real CharacterMotor traversal.
- [x] Add bounded carved Rossdam basin and production Southern Ridge/pass geography.
- [x] Preserve ecology ownership and streaming/LOD semantics; no eager remote GameObjects or streaming-radius increase.

## Regressions / proven remediation
- [x] Production acceptance covers determinism, all-settlement reachability, continuous roads, <=6 voxel/3 m rise, constraints, explicit route solutions, and blocked-route failure.
- [x] Ground all 16 generic buildings with bounded 5x5 relief sampling; verify foundation/timber/roof material reaches final region storage.
- [x] Add all-building water/separative-region footprint regression and remediate the proven Orc/Southern-Ridge overlap by bounding the modern ridge extent while preserving authored route solutions.
- [x] Add production two-stage readiness regression proving terrain-only publication is not final, later feature publication makes the queried presentation column ready, and Moordell timber reaches authoritative storage.
- [x] Scope authored-content readiness to presentation columns; normal opening startup uses terrain-column readiness plus renderer coverage.
- [x] Add bounded Rossdam-water regression capping aggregate carve/fill primitive bounding scans at 17,000,000 cells.
- [x] Exact green regression/storage runs include `33283034449`, `33288421041`, and `33289185080`; earlier visually insufficient artifacts were rejected rather than used for closure.

## Final evidence / streaming work discovered from exact runs
- [x] Reject run `33290154012` / artifact `9725740286` for closure: opening consumes ~40 s and only Moordell column 0 becomes content-ready.
- [x] Discriminate opening cause: dialogue uses realtime, so `Time.timeScale=12` cannot shorten auto-dialogue.
- [x] Reject serial building-centre pinning because it churns streaming demand.
- [x] In dormant `kentridge-macro-world` validation only, dismiss pending opening dialogue promptly and restore normal time before CharacterMotor evidence.
- [x] Run `33292088730` proves the opening correction: gameplay by ~t15, local CharacterMotor 3.61 m, macro-road CharacterMotor 4.73 m, all four Moordell columns ready, and a full-resolution Moordell capture.
- [x] Classify run `33292088730` focused-test failure as native Unity/Burst infrastructure crash: no NUnit XML/assertion result; `single.log` terminates in `Burst.Compiler.IL.Hashing.CacheBuilder.ILHasher` SIGSEGV; built-player step succeeds with zero harness assertions.
- [x] Discriminate Rossdam stall as real streaming cost rather than renderer convergence.
- [x] Bound Rossdam lake work from the original ~42M primitive bounding cells toward the 17M-cell cost ceiling while preserving carve/fill and semantic shoreline routing.
- [x] Keep production streaming demand at target semantic focus on ground level; frame the validation camera independently so evidence does not queue an extra camera-height residency layer.
- [x] Add a player-height Moordell road-arrival capture after settlement content is loaded.
- [x] Inspect exact run `33292881845` / artifact `9726538126`: this is a managed product failure, not Burst infrastructure. NUnit reports resolved Rossdam width `888 dm` against the >=900 dm landmark floor.
- [x] Reject `33292881845` visual evidence for closure: Moordell road-arrival proves structures exist, but diagonal settlement surveys terrain-occlude them; Fairy/Orc captures do not visibly show four blockout buildings.
- [x] Reject the original target schedule at the 60 s cutoff: Southern Ridge becomes content-ready near t59.4 and ridge/network captures never occur.
- [x] Add deterministic nominal guard margin so fixed-seed Rossdam resolves to exactly `900 x 450 dm`, resolved depth `24 dm`, and aggregate water primitive scan `16,591,536` cells (below the 17M cap).
- [x] Add validation-only near-overhead generic-settlement camera correction; CharacterMotor/streaming demand remains grounded at settlement focus and the separate player-height Moordell road-arrival view is preserved.
- [x] CI admission run `33293402602` proves `replay_seconds` is hard-capped at 20..60; the 75 s request was rejected before Unity ran and produced no product/evidence result.
- [x] Reorder dormant validation targets to `Moordell -> Rossdam lake -> Rossdam settlement` so shared lake content is streamed once before the settlement wait; no readiness/capture gate is skipped.
- [x] Run `33293551570`, exact source `2245eb810cbc7a09af4a4a33b32427f0ffce6d4b`, is product-red at compile: `KentridgeMacroWorldSettlementEvidenceCamera.cs` cannot resolve `MountingForceTopDownWorldDefinition`; focused test and player build both stop before execution.
- [x] Add the missing `Game.WorldBuilder.Runtime` namespace import to the validation helper; no behavior/runtime budget change.
- [x] Run `33293664047`, exact source `72c5986b71d79473a1d7725b9d65085cf520509a`, with supported `replay_seconds=60`. Built-player capture succeeds with zero harness assertions; focused PlayMode produces no `single.xml` and crashes natively in the Burst child domain (`Burst.Compiler.IL.Server.EntryPointMethodGrouper`), so the editor result is infrastructure-red rather than a managed product assertion.
- [x] Reject run `33293664047` / artifact `9726759390` for closure despite the infrastructure-red editor result: the player reaches Moordell, Rossdam, and Rossdam lake, makes Fairy content-ready at ~59 s, then the 60 s harness ends before Fairy capture, Orc, Southern Ridge/pass, or network overview.
- [x] Reject the current settlement survey evidence: exact `macro-moordell.png` and `macro-rossdam.png` each visibly frame only one blockout building. The separate Moordell road-arrival frame shows multiple buildings but is not proof of all four at every settlement.
- [x] Diagnose why the intended validation helper did not remediate the exact player: no `target-order=lake-before-rossdam` log appears, the driver still runs `Moordell -> Rossdam -> lake`, and its own diagonal 36 m survey camera remains active. Do not rely on the helper for closure.
- [x] Diagnose Moordell road-arrival camera race: `ScreenCapture.CaptureScreenshot` queues the frame, `_moordellRoadArrivalPending` is cleared in the same `Update`, then `LateUpdate` reapplies the survey camera before capture. Preserve road-arrival camera mode until the queued screenshot frame completes.
- [x] Move lake-before-Rossdam ordering directly into `KentridgeMacroWorldEvidenceDriver.BuildTargetsAndRoadTraversal`; exact target construction is now `Moordell -> Rossdam lake -> Rossdam -> Fairy -> Orc -> Southern Ridge/pass -> network`, with all real readiness/coverage gates and the 60 s CI cap unchanged.
- [x] Move near-overhead generic-settlement framing directly into the proven evidence driver (`70 m` height, `6 m` horizontal offset from computed settlement bounds), preserve `_moordellRoadArrivalPending` through the queued capture frame, and remove the ineffective separate helper + `.meta`.
- [ ] Exact built `KentridgePlayableSlice` reaches usable gameplay without startup/runtime exceptions and captures every macro target.
- [ ] Full-resolution exact-scene evidence shows four readable blockouts at Moordell, Rossdam, Fairy Village, and Orc Village.
- [ ] Full-resolution evidence shows continuous roads/network, substantial Rossdam lake, Southern Ridge/pass response, geography-constrained route behavior, and representative real CharacterMotor traversal.

## Blast radius / cost
- [x] No other SceneIssue, no feature-branch `.github/test-request.json`, no custom workflow/CI transport, no CharacterMotor/streaming-radius/device-budget change.
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries.
- [x] Master `61d03336390ed9079498b183217cbf0ecf0abcd2` was merged cleanly; intervening master commits were disjoint from this assignment.
- [x] Existing production metrics before final resolved-lake adjustment: regions=6, settlements=6, generic buildings=16, hardRoutes=20, routeTiles=833, constrainedRoutes=5, solveSteps=1108, maxRoadRiseVoxels=2.
- [x] Existing scoped readiness regression: 1,090,380 feature voxels; prior last feature step ~2500 ms.
- [x] Rejected run `33290154012`: elapsed=79 s, peak RSS=5859 MB, systemFree=31944 MB, swapGrowth=0 MB; post-start FPS generally >60.
- [x] Run `33292088730` focused editor process before native Burst crash: elapsed=31 s, peak RSS=2287 MB, systemFree=29019 MB, swapGrowth=0 MB.
- [x] Run `33292881845` exact player: elapsed=70 s, peak RSS=5233 MB, systemFree=28711 MB, swapGrowth=0 MB; after startup one-second FPS windows are generally >60 and often >100.
- [x] Run `33293664047` focused editor native crash: elapsed=32 s, peak RSS=3448 MB, systemFree=27274 MB, swapGrowth=0 MB. Exact player remains smooth after startup (generally >60 FPS, often >100 FPS) and reports zero harness assertions, but the evidence sequence is incomplete.
- [x] Re-check corrected feature diff against current master before the rejected 75 s request: master remained `61d03336390ed9079498b183217cbf0ecf0abcd2`; feature was ahead-only/behind=0 and contained only this assignment beyond master.
- [ ] Measure final resolved lake footprint/depth/primitive scan cells, route solve counts, feature voxel work/last-feature time, CPU/frame/FPS, memory, streaming convergence, and far-field/render telemetry against existing budgets.
- [ ] Re-check the exact source diff/current master immediately before the next supported 60 s targeted request and again before promotion.

## Acceptance / closure
- [ ] (1) Source-backed macro graph remains authoritative through shared WorldBuilder APIs.
- [ ] (2) Every settlement has readable physical presence, including >=4 grounded generic blockouts where no richer generator owns it.
- [ ] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] (4) Roads are terrain-aware and blocked geography requires explicit semantic solutions.
- [ ] (5) Reusable geographic authoring/query covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, and route/placement constraints.
- [ ] (6) Built world visibly contains a substantial lake + ridge and a geography-constrained hard route.
- [ ] (7) Regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [ ] (8) No second scene-local graph/direct voxel-writing/static destination hierarchy.
- [ ] (9) Focused production regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, bounded water cost, and two-stage presentation readiness.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost measured against budgets.
- [ ] Corrected final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Every checkbox above is complete before `open -> pending`.
- [ ] Complete pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after green exact gates.
- [ ] Move only this assignment `open -> pending`, then after closure gate `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-6`, resolve no unrelated conflicts, and non-force push the exact feature head to `origin/master`; retry if master advances.