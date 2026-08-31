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
- [x] Prove generic smoke harness sets `AutoSurvey` at t=50 while Rossdam is pending in run `33311348299`, collapsing residency (`visible=275 -> 0`, `residentGround=116.6m -> 14.2m`).
- [x] Prove queued `macro-moordell.png` loses survey framing during immediate Moordell -> macro-road continuation and is visually unacceptable.
- [x] Later exact run `33346099006` rejects that ownership failure as the current blocker: macro driver retains Rossdam demand across the generic t=50 survey write and Rossdam converges/captures.

## Current remediation
- [x] Preserve sequence: local CharacterMotor -> Moordell survey -> macro-road CharacterMotor -> player-height Moordell road arrival -> Rossdam -> lake -> Fairy -> Orc -> ridge/pass -> network.
- [x] Preserve real content-settled/renderer gates, normal load radius/budgets, and 60 s cap; no prestreaming or skipped target.
- [x] Keep dormant macro driver authoritative over `AutoSurvey` / `AutoRecede` in `LateUpdate` so later generic smoke-harness writes cannot own subsequent streaming demand.
- [x] Hold Moordell survey camera through existing post-capture dwell so queued screenshot rendering cannot inherit macro-road camera state.
- [x] Add throttled per-column diagnostics bounded to existing target content columns; log exact centre and X/Z presentation region without generation or world scans.
- [x] Extend focused regression to cover target order, macro-driver automation ownership, and post-capture survey hold.
- [x] Re-check blast radius: production scheduler, CharacterMotor, residency/load radius, device budgets, render budgets, and gameplay behavior remain unchanged; correction is validation-profile-only.
- [x] Exact run `33318399738` proves uninterrupted Rossdam convergence under normal gameplay streaming and no shared scheduler change is justified.
- [x] Diagnose vertical-residency owner: static storage tests explicitly generate separate timber/roof Y-regions when a generic building crosses a 51.2 m vertical boundary, while ordinary residency previously demanded terrain-surface/viewer Y layers only and features are applied per exact generated 3D region.
- [x] Refactor the oversized `ShowcaseWorld.cs` at the `RefreshPending`/residency boundary into focused residency partials; preserve behavior outside the extracted seam.
- [x] Add feature-aware vertical residency: loaded X/Z columns queue only Y-regions intersecting semantic explicit feature bounds; no blanket extra layer or scene-specific preload policy.
- [x] Add focused ordinary-`ShowcaseWorld.StepStreaming` regression with a deterministic production feature crossing a Y-region boundary; exact run `33346099006` proves it 1/1 green.
- [x] Inspect exact run `33346099006` full-resolution artifact: Rossdam still shows only one unmistakable building and lake still reads as a thin distant strip; Fairy is ready at cutoff but uncaptured, with Orc/ridge/network absent.
- [x] Isolate repeated-failure root cause before another fix in experiment 018: presentation readiness checked terrain/point Y but not the authored Y layers already requested by feature-aware residency.
- [x] Implement shared readiness correction using the same cached semantic feature-layer query; no Kentridge coordinates, radius/budget changes, pre-generation, or weakened readiness.
- [x] Extend the focused regression to create the lower-layer-ready/upper-layer-absent race and require presentation readiness to remain false until ordinary streaming publishes the upper shell.
- [x] Prove the updated readiness regression on exact feature source `9d51fb9a947af76d0b8005c35288a7007dd6d9e6` through `ci-test/fixes/agent-6`; run `33354287850` is 1/1 green and the supported 60 s built-player step completes technically clean.
- [x] Inspect run `33354287850` full-resolution evidence: settlement/lake visual closure remains red and Fairy/Orc/ridge/network remain uncaptured before 60 s.
- [x] Isolate the surviving repeated settlement symptom before another fix in experiment 019: distinguish authoritative four-building voxel volumes from downstream render/framing or grounding/placement failure.
- [x] Add the experiment-019 production-data regression and classify Rossdam's four expected building volumes from authoritative storage. Exact run `33359142139` is green: all four production structures contain roughly 1.03–1.20M structure voxels, start at their grounded placement Y, and meet/exceed semantic wall-height span. Generation/storage/grounding are rejected as the surviving visual owner.
- [ ] Experiment 020 root-cause discriminator: classify each Rossdam building against the actual survey-camera frustum and renderer publication/visibility state before another visual fix. Global `missingVisible=0` is insufficient because it does not prove all four authored bounds reached a ready draw representation.
- [ ] Verify full-resolution Rossdam/Fairy/Orc surveys show all four readable authored settlement blockouts after the identified production owner is corrected.
- [ ] Capture the final `macro-network-overview` inside the unchanged 60 s replay.
- [ ] Re-check Rossdam lake framing; exact runs `33346099006` and `33354287850` expose only a thin distant water strip and must visibly read as the substantial authored water body plus constrained route.
- [ ] Re-run final exact-SHA targeted CI and prove focused regression + supported 60 s real-player smoke are closure-quality green for the same source SHA.

## Reusability review
- [x] Audit `TopDownWorldPhysicalPlanner`, `TopDownWorldPhysicalIntent`, and generic physical/water catalogues for Kentridge/Rossdam/Fairy/Orc names, fixed landmark coordinates, evidence timing, or route exceptions; scene policy remains in `KentridgeTopDownWorldPhysicalIntent` or composition/evidence layers.
- [x] Keep generic planner inputs semantic and data-driven: geography, settlement intent, route constraints, water bodies, and terrain relationships are supplied as intent rather than inferred from Kentridge-specific IDs or ordering.
- [x] Independent non-Kentridge `a`/`b` blocked-water fixture proves the generic planner rejects an unsolved barrier and accepts semantic `GoAround` without Kentridge adapters or special cases.
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
- [x] Exact run `33346099006`: focused residency test green; combined process peak ~`5824MB`, no swap growth, late FPS generally >100 and often 200–360; visual closure remains red.
- [x] Exact run `33354287850`: readiness-race regression green; built-player capture step completes with zero harness assertions and no technical crash, while visual/timing closure remains red.
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
- [ ] After green exact gates, move this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, merge current master, and non-force push that exact feature head to master; retry if master advances.