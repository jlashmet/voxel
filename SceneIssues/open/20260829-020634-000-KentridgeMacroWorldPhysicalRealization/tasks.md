# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Inspect assignment; `captures: []`, so `issue.json` is the acceptance contract.
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and real CharacterMotor traversal.
- [x] Production/storage regressions cover determinism, reachability, roads, settlements, blocking geography, explicit route solutions, bounded slopes, all 16 generic building shells, water realization, and two-stage presentation readiness.

## Proven remediation history
- [x] Reject broad residency, serial building-centre prestreaming, external evidence helpers, remote blocking prewarm, and global feature-scheduler preemption.
- [x] Keep streaming demand grounded at semantic focus while survey camera moves independently.
- [x] Add validation-only opening-dialogue dismissal, restore normal time, player-height road evidence, near-overhead settlement framing, and supported <=60 s replay.
- [x] Bound Rossdam water primitive scan below 10M cells while preserving accepted `900 x 450 x 24 dm` lake and full basin with 2 dm water sheet.
- [x] Resolve route-intersection regressions: nominal lake offsets `(-400,-155)` keep endpoints dry while preserving genuine constrained outbound/Rossdam routes.
- [x] Exact run `33304172039`: PlayMode green/standalone clean, closure-red at Moordell; recorded as experiment 016.
- [x] Exact run `33311348299`: workflow/PlayMode green, closure-red; recover artifact `9732113329` and diagnose two deterministic driver handoffs.
- [x] Prove generic smoke harness sets `AutoSurvey` at t=50 while Rossdam is pending, collapsing residency (`visible=275 -> 0`, `residentGround=116.6m -> 14.2m`).
- [x] Prove queued `macro-moordell.png` loses survey framing during immediate Moordell -> macro-road continuation and is visually unacceptable.

## Current remediation
- [x] Preserve sequence: local CharacterMotor -> Moordell survey -> macro-road CharacterMotor -> player-height Moordell road arrival -> Rossdam -> lake -> Fairy -> Orc -> ridge/pass -> network.
- [x] Preserve real content-settled/renderer gates, normal load radius/budgets, and 60 s cap; no prestreaming or skipped target.
- [x] Keep dormant macro driver authoritative over `AutoSurvey` / `AutoRecede` in `LateUpdate` so later generic smoke-harness writes cannot own subsequent streaming demand.
- [x] Hold Moordell survey camera through existing post-capture dwell so queued screenshot rendering cannot inherit macro-road camera state.
- [x] Add throttled per-column diagnostics bounded to existing target content columns; log exact centre and X/Z presentation region without generation or world scans.
- [x] Extend focused regression to cover target order, macro-driver automation ownership, and post-capture survey hold.
- [x] Re-check blast radius: production scheduler, CharacterMotor, residency/load radius, device budgets, render budgets, and gameplay behavior remain unchanged; correction is validation-profile-only.
- [x] Exact run `33318399738` proves uninterrupted Rossdam convergence: pending columns 1/3 in region `(1,10)` settle under normal 3 ms gameplay streaming; no shared scheduler change is justified.
- [x] Exact run `33318399738` PlayMode regression is `1/1` green and standalone exits with zero harness assertions, no swap growth, and all named targets through ridge/pass captured.
- [x] Diagnose vertical-residency owner: static storage tests must explicitly generate separate timber/roof Y-regions when a generic building crosses a 51.2 m vertical boundary, while ordinary `ShowcaseWorld.RefreshPending` demands terrain-surface/viewer Y layers only and features are applied per exact generated 3D region.
- [ ] Add feature-aware vertical residency that queues only feature-bearing Y-regions intersecting loaded X/Z columns; do not blanket-load an extra Y layer across the horizontal ring.
  - Blocker 2026-08-30: the semantic, catalogue-driven `ShowcaseWorld.FeatureResidency.cs` helper is implemented, but `RefreshPending` still needs the single production hook `QueueFeatureRegionsForColumn(rx, rz)`. The available GitHub connector only supports whole-file replacement for existing files, `ShowcaseWorld.cs` is ~115 KB, and direct Git/network checkout is unavailable. Do not replace this with broad residency, surface-span inflation, alternate CI/editor transports, or scene-specific preload policy; continue independent acceptance work until a safe exact edit transport is available.
- [ ] Add focused ordinary-`ShowcaseWorld.Update` regression proving a generic building that crosses a Y-region boundary publishes its upper shell without direct/test-injected region generation.
- [ ] Verify full-resolution Rossdam/Fairy/Orc surveys show all four readable authored settlement blockouts after vertical residency correction.
- [ ] Capture the final `macro-network-overview` inside the unchanged 60 s replay; run `33318399738` reaches ridge/pass at the cutoff but ends before the final network target.
- [ ] Re-check Rossdam lake framing: current lake-detour capture exposes only a thin distant water strip and must visibly read as the substantial authored water body plus constrained route.
- [ ] Re-run final exact-SHA targeted CI and prove focused regression + supported 60 s real-player smoke are closure-quality green for the same source SHA.

## Reusability review
- [ ] Audit `TopDownWorldPhysicalPlanner`, `TopDownWorldPhysicalIntent`, and generic physical/water catalogues for Kentridge/Rossdam/Fairy/Orc names, fixed landmark coordinates, evidence timing, or route exceptions; move all such policy into `KentridgeTopDownWorldPhysicalIntent` or composition/evidence layers.
- [ ] Keep generic planner inputs semantic and data-driven: geography, settlement intent, route constraints, water bodies, and terrain relationships should be supplied as intent rather than inferred from Kentridge-specific IDs or ordering.
- [ ] Add a focused non-Kentridge fixture/regression proving the generic top-down physical planner can realize a different small macro layout without Kentridge adapters or special cases.
- [x] Keep the large `KentridgeMacroWorldEvidenceDriver` validation-only; no production world-generation, streaming, route-solving, or publication policy depends on the evidence driver.

## Exact runtime / visual gate
- [ ] Exact built `KentridgePlayableSlice` reaches usable gameplay without startup/runtime exceptions and captures every required macro target inside 60 s.
- [ ] Full-resolution evidence shows at least four readable grounded blockouts at Moordell, Rossdam, Fairy Village, and Orc Village plus streets/open space and road arrival/exit.
- [ ] Full-resolution evidence shows continuous roads/network, substantial Rossdam lake, Southern Ridge/pass, visibly geography-constrained route behavior, differentiated countryside, and representative real CharacterMotor traversal.
- [x] Player-height road evidence is supported by the driver and prior artifact; final exact run must retain it.

## Blast radius / cost
- [x] No unrelated SceneIssue implementation, no feature-branch `.github/test-request.json`, no custom CI transport/workflow, no CharacterMotor/load-radius/device-budget change.
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries.
- [x] Shared `FeatureRegionBuild` scheduling remains unchanged.
- [x] Per-column diagnostics are validation-only, bounded to existing target content, and throttled to <=1 Hz while pending.
- [x] Moordell camera correction reuses existing dwell; no replay/residency/render budget increase.
- [x] Baseline run `33304172039`: assertions `0`, no swap growth, peak RSS `5,571,344 KB`, late FPS ~`197/222/232/168/137`, final `missingVisible=0 coverage=True`.
- [x] Run `33318399738`: elapsed `73s`, final RSS `598MB`, peak RSS `5640MB`, swap growth `0MB`; settled-target FPS is typically ~150–370 with target-transition p95 frame time ~7–13 ms.
- [ ] Quantify additional vertical resident/generated regions from feature-aware residency and prove it does not expand empty columns or change horizontal interest radius/device budgets.
- [ ] Measure final lake dimensions/depth/primitive cells, route tile/solve/constrained counts, feature work/time, CPU/FPS, memory, streaming convergence, and render/far-field telemetry against budgets.
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
- [ ] (9) Focused behavioral regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, bounded water cost, evidence sequencing, feature-aware vertical residency, and two-stage readiness.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal without runtime exceptions.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets.
- [ ] Corrected final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Every checkbox above is complete before closure.
- [ ] After green exact gates, move this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, merge current `origin/master`, and non-force push that exact feature head to `origin/master`; retry if master advances.
