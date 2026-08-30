# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Inspect assignment; `captures: []`, so `issue.json` is the acceptance contract.
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic region intent for water, ridge/mountain, valley/pass, plains/meadow, woodland, generic country, extents/elevation, relationships, route/placement constraints, and terrain output.
- [x] Add deterministic terrain-aware hard-route solving with explicit go-around/pass/crossing semantics and rejection of unsolved blocked hard routes.
- [x] Physically realize all macro settlements; retain rich Kentridge/Hightown and generate four grounded reusable blockouts plus streets for Moordell, Rossdam, Fairy Village, and Orc Village.
- [x] Emit continuous generated hard-route surfaces and exercise representative real CharacterMotor traversal.
- [x] Add carved Rossdam basin plus Southern Ridge/pass while preserving ecology/streaming ownership and normal load radius.
- [x] Production/storage regressions cover determinism, reachability, roads, settlements, blocking geography, explicit route solutions, bounded slopes, all 16 generic building shells, and two-stage presentation readiness.

## Proven evidence / remediation history
- [x] Reject broad residency and serial building-centre prestreaming; both delay/churn normal production streaming.
- [x] Add validation-only realtime opening-dialogue dismissal and restore normal time before CharacterMotor evidence.
- [x] Keep streaming demand grounded at semantic focus while survey camera moves independently.
- [x] Add player-height Moordell road-arrival evidence and hold that camera through the queued screenshot frame.
- [x] Reject diagonal settlement surveys; move generic town framing into the proven driver as near-overhead views derived from actual building bounds.
- [x] Reject external evidence helper; remove it and keep one dormant validation driver.
- [x] Record rejected helper attempt in `experiment-013-direct-driver-evidence-remediation.md`.
- [x] Reject replay >60 s; CI admission `33293402602` proves supported range is 20..60 seconds.
- [x] Bound the original ~42M Rossdam water primitive scan to 16,591,536 cells while preserving deterministic `900 x 450 x 24 dm` accepted lake size/depth.
- [x] Run `33293664047`: focused editor is native Burst infrastructure-red (no NUnit XML); built player is clean but closure-incomplete, ending after Fairy becomes content-ready.
- [x] Run `33294931953`: product-red managed focused failure because `road-kentridge-rossdam` is not geography-constrained; lake-first built-player ordering also ends after Rossdam at the 60 s cap.
- [x] Discriminate route cause from exact geometry: lake centre `(868,3325)dm`, half-depth `225dm`; closest direct route point ~`(860,2920)dm`, so the direct route misses the lake by ~180dm beyond its edge.
- [x] Falsify lake-first scheduling: after Moordell (~29 s), lake publication consumes roughly the next 12 s and Rossdam consumes the remaining ~18 s.

## Current required remediation
- [x] Move Rossdam Lake south semantically while preserving its `Between(MoordellCorridor,RossdamApproach)` authority, accepted `900 x 450 x 24 dm` resolved dimensions, and deterministic variation; direct Rossdam route must truly intersect it and the solved route/endpoint must remain dry.
- [x] Preserve the full 24 dm carved basin but represent non-solid water as a 2 dm surface sheet at the basin top; tighten aggregate primitive-scan regression from 17M to 10M cells (expected 9,281,584).
- [x] Restore evidence target order to `Moordell -> Rossdam -> Rossdam lake -> Fairy -> Orc -> Southern Ridge/pass -> network`; do not skip real content-settled or renderer-coverage gates.
- [x] Add/maintain behavioral regression for the water-surface realization rather than relying on source-string assertions.
- [x] Record this failed lake-first/product-red attempt as `experiment-014-lake-first-route-intersection-and-cost.md` before final exact CI.

## Newly discovered exact-SHA CI regressions
- [x] Run `33301222246` proved the south-shifted lake intersects hard route `forest -> fighting-area-1` without semantic intent.
- [x] Attempt the smallest explicit `forest -> fighting-area-1` `GoAround` plus focused dry-route regression; exact run `33302489798` proves that attempt invalid because the route endpoint itself is inside the blocker.
- [x] Record run `33302489798` in experiment/CI evidence; do not rerun that product-red source.
- [x] Discriminate endpoint engulfment versus shared detour-solver insufficiency using authoritative graph coordinates: `fighting-area-1` is inside current road-expanded lake bounds, so the shared solver is correct.
- [x] Move Rossdam Lake northwest to nominal offsets `(-400,-155)` while preserving resolved `900 x 450 x 24 dm`; keep `fighting-area-1` dry, its outbound north road constrained, and direct Rossdam approach genuinely constrained.
- [x] Remove the now-unnecessary inbound `forest -> fighting-area-1` lake constraint and replace the temporary regression with endpoint-dry + inbound-dry + outbound/Rossdam-constrained behavioral assertions.
- [x] Run exact source `cb4e1b0fa1464da214d799ba55750bea38159143` through wrapper `e5bdf88a387c486b72be5d8950fb6d1458a5aad9`; run `33304172039` is exact-attributed, focused PlayMode `1/1` green, standalone process clean, but visual evidence is closure-red because the driver remains at Moordell through 59 s.
- [x] Record run `33304172039` as `experiment-016-exact-ci-moordell-content-convergence.md`; do not promote from workflow status alone.
- [ ] Diagnose persistent Moordell readiness telemetry `contentCols=5 readyContent=4 missingContent=1` that prevents evidence target advancement within 60 s.
- [ ] Preserve real content-settled and renderer-coverage gates; do not skip readiness, extend replay beyond 60 s, increase load radius, or reintroduce broad/serial prestreaming.
- [ ] Apply only the smallest proved validation-driver/readiness-demand correction while retaining semantic-focus streaming ownership and target order `Moordell -> Rossdam -> Rossdam lake -> Fairy -> Orc -> Southern Ridge/pass -> network`.
- [ ] Re-run exact-SHA targeted CI only after source remediation and prove the focused Unity suite plus supported 60-second real-player smoke are closure-quality green for that same source SHA, including every required target.

## Exact runtime / visual gate
- [ ] Exact built `KentridgePlayableSlice` reaches usable gameplay without startup/runtime exceptions and captures every required macro target inside the supported 60 s replay. Run `33304172039` is runtime-clean but visually incomplete at Moordell.
- [ ] Full-resolution evidence shows at least four readable grounded blockouts at Moordell, Rossdam, Fairy Village, and Orc Village plus streets/open space and road arrival/exit.
- [ ] Full-resolution evidence shows continuous roads/network, substantial Rossdam lake, Southern Ridge/pass, visibly geography-constrained route behavior, differentiated countryside, and representative real CharacterMotor traversal.
- [x] Verify player-height road entering/leaving settlement evidence is actually captured at player height: run `33304172039` artifact includes `macro-road-character-motor.png` / player-height Moordell road evidence.

## Blast radius / cost
- [x] No unrelated SceneIssue, no feature-branch `.github/test-request.json`, no custom CI transport/workflow, no CharacterMotor/load-radius/device-budget change.
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries.
- [x] Current master `e95324aeaef619cb49d84bf2b07f770184bead81` was merged at `0cea93d647e5860795dd4938e3b8d8ab5b1bbc6c`; comparison was ahead-only and assignment-scoped.
- [x] Existing pre-final metrics: regions=6, settlements=6, generic buildings=16, hardRoutes=20, prior routeTiles=833, constrainedRoutes=5, solveSteps=1108, maxRoadRiseVoxels=2; scoped readiness previously 1,090,380 feature voxels and ~2500 ms last feature step.
- [x] Rejected-run resource data remains bounded: `33292881845` player peak RSS 5233 MB; `33293664047` editor peak 3448 MB with zero swap growth; post-start built-player FPS generally >60 and often >100.
- [x] Record run `33304172039` runtime budget sample: assertion failures `0`, no swap growth, peak RSS `5,571,344 KB`, late-run FPS approximately `197/222/232/168/137`, and final visible coverage `missingVisible=0 coverage=True`; visual progression remains blocked by one unready content column.
- [ ] Measure final resolved lake dimensions/depth/primitive cells, route tile/solve/constrained counts, feature voxel work/last-feature time, CPU/frame/FPS, memory, streaming convergence, and render/far-field telemetry against existing budgets on closure-quality evidence.
- [x] Re-check exact feature diff/current master immediately before targeted CI: master remains `e95324aeaef619cb49d84bf2b07f770184bead81`, branch is ahead-only, and the endpoint remediation is limited to Kentridge intent/test plus assignment evidence.

## Acceptance / closure
- [ ] (1) Source-backed macro graph remains authoritative through shared WorldBuilder APIs.
- [ ] (2) Every settlement has readable physical presence, including >=4 grounded generic blockouts where no richer generator owns it.
- [ ] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] (4) Roads are terrain-aware; blocked geography requires explicit semantic solutions and no silent lake/cliff/building crossing.
- [ ] (5) Reusable geographic authoring/query covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, and route/placement constraints.
- [ ] (6) Built world visibly contains a substantial lake + ridge and at least one geography-altered hard route.
- [ ] (7) Regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [ ] (8) No second scene-local graph/direct voxel-writing/static destination hierarchy.
- [ ] (9) Focused behavioral regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, bounded water cost, and two-stage readiness.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal without runtime exceptions.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets.
- [ ] Corrected final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Every checkbox above is complete before `open -> pending`.
- [ ] Complete pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after green exact gates.
- [ ] Move only this assignment `open -> pending`, then after closure gate `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-6`, resolve only in-scope conflicts, and non-force push exact feature head to `origin/master`; retry if master advances.