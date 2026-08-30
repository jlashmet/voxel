# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Inspect assignment; `captures: []`, so `issue.json` is the acceptance contract.
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and real CharacterMotor traversal.
- [x] Production/storage regressions cover determinism, reachability, roads, settlements, blocking geography, explicit route solutions, bounded slopes, all 16 generic building shells, water realization, and two-stage presentation readiness.

## Proven remediation history
- [x] Reject broad residency, serial building-centre prestreaming, external evidence helpers, and remote blocking prewarm.
- [x] Keep streaming demand grounded at semantic focus while survey camera moves independently.
- [x] Add validation-only opening-dialogue dismissal, restore normal time, player-height road evidence, near-overhead settlement framing, and supported <=60 s replay.
- [x] Bound Rossdam water primitive scan below 10M cells while preserving accepted `900 x 450 x 24 dm` lake and full basin with 2 dm water sheet.
- [x] Resolve route-intersection regressions: final nominal lake offsets `(-400,-155)` keep endpoints dry while preserving genuine constrained outbound/Rossdam routes.
- [x] Run exact source `cb4e1b0fa1464da214d799ba55750bea38159143` through wrapper `e5bdf88a387c486b72be5d8950fb6d1458a5aad9`; run `33304172039` is exact-attributed, PlayMode `1/1` green, standalone clean, but closure-red because visual evidence remains at Moordell through 59 s.
- [x] Record run `33304172039` as `experiment-016-exact-ci-moordell-content-convergence.md`; do not promote from workflow status alone.
- [x] Diagnose Moordell gate: evidence requires exactly four authored building centres; do not weaken readiness. Lake is in normal residency but does not share a Moordell building column.
- [x] Prove shared `FeatureRegionBuild` is tile-resumable and reject global feature-scheduler preemption after blast-radius review.

## Current required remediation
- [x] Reorder only the dormant `kentridge-macro-world` evidence flow: local CharacterMotor proof -> fully settled/captured Moordell -> real macro-road CharacterMotor traversal -> player-height Moordell road arrival -> Rossdam -> lake -> Fairy -> Orc -> ridge/pass -> network.
- [x] Preserve every real content-settled and renderer-coverage gate, normal load radius/budgets, and 60 s cap; no prestreaming or skipped target.
- [x] Add behavioral regression through the real driver planning/sequencing path proving Moordell evidence precedes macro-road capture while accepted later target order remains unchanged.
- [x] Inspect exact run `33311348299`: workflow/PlayMode are green, but only Moordell and road named captures exist; Rossdam renderer coverage settles while its four content columns remain unsettled until the 60 s cutoff.
- [ ] Identify the exact Rossdam presentation column / macro-feature region that blocks content settlement under normal real-player streaming.
- [ ] Fix the Rossdam publication/convergence defect without weakening `IsPresentationColumnContentSettled`, increasing residency, bypassing normal budgets, or skipping a required target.
- [ ] Add or adjust focused regression coverage for the diagnosed convergence defect and re-check blast radius/cost.
- [ ] Verify survey framing makes Moordell and Rossdam multi-building geometry visibly readable in full-resolution captures rather than merely telemetry-ready.
- [ ] Re-run final exact-SHA targeted CI and prove the focused regression plus supported 60-second real-player smoke are closure-quality green for the same source SHA, including every required target.

## Exact runtime / visual gate
- [ ] Exact built `KentridgePlayableSlice` reaches usable gameplay without startup/runtime exceptions and captures every required macro target inside 60 s.
- [ ] Full-resolution evidence shows at least four readable grounded blockouts at Moordell, Rossdam, Fairy Village, and Orc Village plus streets/open space and road arrival/exit.
- [ ] Full-resolution evidence shows continuous roads/network, substantial Rossdam lake, Southern Ridge/pass, visibly geography-constrained route behavior, differentiated countryside, and representative real CharacterMotor traversal.
- [x] Player-height road evidence is supported by the driver and prior artifact; final exact run must retain it.

## Blast radius / cost
- [x] No unrelated SceneIssue implementation, no feature-branch `.github/test-request.json`, no custom CI transport/workflow, no CharacterMotor/load-radius/device-budget change.
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries.
- [x] Current master `e17e858bfe0497c90b87db70fcfef80a142917a4` was merged before the final remediation test; its only delta was a separate road-presentation SceneIssue capture.
- [x] Driver remediation is validation-profile-only; regression addition is PlayMode-test-only, so shared runtime blast radius remains unchanged.
- [x] Baseline runtime metrics from run `33304172039`: assertions `0`, no swap growth, peak RSS `5,571,344 KB`, late FPS approximately `197/222/232/168/137`, final visible coverage `missingVisible=0 coverage=True`.
- [ ] Measure final lake dimensions/depth/primitive cells, route tile/solve/constrained counts, feature voxel work/last-feature time, CPU/frame/FPS, memory, streaming convergence, and render/far-field telemetry against budgets.
- [ ] Re-fetch current master and re-check exact feature diff immediately before final targeted CI.

## Acceptance / closure
- [ ] (1) Source-backed macro graph remains authoritative through shared WorldBuilder APIs.
- [ ] (2) Every settlement has readable physical presence, including >=4 grounded generic blockouts where no richer generator owns it.
- [ ] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] (4) Roads are terrain-aware; blocked geography requires explicit semantic solutions and no silent lake/cliff/building crossing.
- [ ] (5) Reusable geographic authoring/query covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, and route/placement constraints.
- [ ] (6) Built world visibly contains a substantial lake + ridge and at least one geography-altered hard route.
- [ ] (7) Regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [ ] (8) No second scene-local graph/direct voxel-writing/static destination hierarchy.
- [ ] (9) Focused behavioral regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, bounded water cost, evidence sequencing, and two-stage readiness.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal without runtime exceptions.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets.
- [ ] Corrected final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Every checkbox above is complete before `open -> pending`.
- [ ] Complete pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after green exact gates.
- [ ] Move only this assignment `open -> pending`, then after closure gate `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-6`, resolve only in-scope conflicts, and non-force push exact feature head to `origin/master`; retry if master advances.
