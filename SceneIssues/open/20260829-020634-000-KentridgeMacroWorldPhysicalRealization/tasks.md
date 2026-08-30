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
- [x] Exact green regression/storage runs include `33283034449`, `33288421041`, and `33289185080`; earlier visually insufficient artifacts were rejected rather than used for closure.

## Final evidence scheduling — discovered work
- [x] Inspect run `33290154012` / artifact `9725740286` for source `8cab72cc862f...`: focused CI and player harness are workflow-green, but closure evidence fails. The opening/pub consumes ~40 s; only macro road traversal and `moordell index=0` content readiness occur before the 60 s cutoff.
- [x] Discriminate startup cause: `Time.timeScale=12` cannot shorten auto-dialogue because `TryAdvanceDialogue` uses realtime; this explains the long validation opening without changing normal gameplay readiness.
- [x] Reject serial building-centre pinning: it regresses target progress versus the earlier stable survey-camera demand and causes streaming-demand churn.
- [x] In the dormant `kentridge-macro-world` evidence profile only, dismiss pending opening dialogue promptly while retaining normal production opening behavior outside validation.
- [x] Hold each production-derived settlement survey demand while all four building-centre presentation columns settle; do not serially move demand between building centres.
- [ ] Exact built `KentridgePlayableSlice` reaches usable gameplay without startup/runtime exceptions and leaves enough replay time for all macro targets.
- [ ] Full-resolution exact-scene evidence shows four readable blockouts at Moordell, Rossdam, Fairy Village, and Orc Village.
- [ ] Full-resolution evidence shows continuous roads/network, substantial Rossdam lake, Southern Ridge/pass response, and representative real CharacterMotor traversal.

## Blast radius / cost
- [x] No other SceneIssue, no feature-branch `.github/test-request.json`, no custom workflow/CI transport, no CharacterMotor/streaming-radius change.
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries.
- [x] Current feature branch includes `origin/master` `0901be5a0640e3eec103cdf3c97aa12b8cd42a9e`; comparison remains ahead-only and contains only this Kentridge feature/tests/docs beyond master.
- [x] Existing production metrics: regions=6, settlements=6, generic buildings=16, hardRoutes=20, routeTiles=833, constrainedRoutes=5, solveSteps=1108, maxRoadRiseVoxels=2, waterDepthVoxels=46.
- [x] Scoped readiness regression: 1,090,380 feature voxels; prior last feature step ~2500 ms, so player telemetry is required rather than inferred.
- [x] Failed run `33290154012` player process: elapsed=79 s, peak RSS=5859 MB, systemFree=31944 MB, swapGrowth=0 MB; after startup, captured FPS samples are generally well above 60 fps, but this run is rejected for evidence completeness.
- [ ] Measure final remediation CPU/GPU/frame/memory/streaming telemetry against the repository device budgets and record final feature/route/building counts.
- [ ] Re-check exact final feature diff against current master immediately before targeted CI and promotion.

## Acceptance / closure
- [ ] (1) Source-backed macro graph remains authoritative through shared WorldBuilder APIs.
- [ ] (2) Every settlement has readable physical presence, including >=4 grounded generic blockouts where no richer generator owns it.
- [ ] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] (4) Roads are terrain-aware and blocked geography requires explicit semantic solutions.
- [ ] (5) Reusable geographic authoring/query covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, and route/placement constraints.
- [ ] (6) Built world visibly contains a substantial lake + ridge and a geography-constrained hard route.
- [ ] (7) Regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [ ] (8) No second scene-local graph/direct voxel-writing/static destination hierarchy.
- [ ] (9) Focused production regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, and two-stage presentation readiness.
- [ ] (10) Exact built-player evidence covers settlements, roads, geography, constrained route, and CharacterMotor traversal.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost measured against budgets.
- [ ] Final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Every checkbox above is complete before `open -> pending`.
- [ ] Complete pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after green exact gates.
- [ ] Move only this assignment `open -> pending`, then after closure gate `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-6`, resolve no unrelated conflicts, and non-force push the exact feature head to `origin/master`; retry if master advances.