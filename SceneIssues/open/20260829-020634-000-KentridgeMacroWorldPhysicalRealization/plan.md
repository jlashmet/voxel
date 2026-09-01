# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Preserve the source-backed Mounting Force/Kentridge graph while physically realizing settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded slopes, semantic constrained routes, lake/ridge realization, feature-aware vertical residency/readiness, and production evidence sequencing.
- Experiments 019-025 ruled out missing planned structures, coarse visibility/readiness, and settlement survey framing. Experiment 023 replaced the costly solid blockout fallback with bounded hollow wall shells and retained an independent cost regression.
- Experiment 026 exact run `33417092425` proved Moordell and Rossdam authored centres were visible and render-ready while authoritative top-solid sampling still matched procedural terrain.
- Agent 7's shared spatial reservation system is integrated without moving geography/route ownership out of `TopDownWorldPhysicalPlanner`; an independent non-Kentridge fixture proves road/building conflicts are rejected.
- Spatial-reservation integration was exact-SHA green at source `7033ee755ae6bac7cc3c1cc3c83bb4ee2e7d5f5e` in run `33441865025`.

## Experiment 027 root cause
Run `33448157883` completed failure before structure generation. Both the focused production-storage test and real-player replay abort in `TopDownWorldPhysicalPlanner.BuildAround` while solving a hard route around `southern-ridge`; the player then refuses the legacy fallback, producing the repeated sky/fog-only capture. Because planning aborts before feature publication, the missing settlement `FEATUREGEN_TRACE` is downstream noise rather than the owner.

This satisfies the two-fix root-cause gate: the repeated visual symptom is caused by a deterministic hard-route geography contract failure, not another camera/readiness/publication hypothesis.

The minimal correction is deliberately split by ownership:
1. Shared WorldBuilder API/planner supports an explicit `TopDownWorldConstraintRelaxationMode` and remains strict by default.
2. `EndpointEscape` permits only the contiguous route segment needed for an authored endpoint to leave a blocker, records deterministic plan diagnostics, and still rejects routes that remain inside the blocker.
3. Scene policy lives in `KentridgeTopDownWorldPhysicalIntent`: only the authored South Fighting Area -> Orc Village `SouthernRidge` GoAround relationship opts into endpoint escape; lake detours and designated crossings stay strict.
4. Independent regression proves generic consumers default to strict and opt-in must be explicit; Kentridge regression proves exactly one authored constraint is relaxed.

The first corrected-ridge exact rerun (`33461949335`, source `6e7a546c64adc397c14c75d34e4324fc092eb5f1`) proves the planner recovery: all 20 routes and 824 route tiles plan, all 16 generic buildings exist, and the standalone replay reaches feature generation. The remaining focused failure is not production geometry: the storage regression sampled the intentionally hollow centre of the four-wall generic shell and correctly read air. The regression now probes the authored back perimeter timber wall at the same high-ground-relative height; the hollow-shell production program is unchanged.

## Next validation
1. Run exact-SHA CI on the focused production-storage settlement acceptance test plus the 60-second SceneIssue replay using the new perimeter-wall probe.
2. Require physical planning to complete, the focused storage assertions to reach generic shell/roof publication, and the standalone player to run without the macro-realization exception.
3. Inspect new full-resolution evidence. The previous sky-only capture is not closure evidence; require readable settlements, roads, Rossdam lake, Southern Ridge/pass, constrained route, and representative CharacterMotor traversal.
4. Re-run Moordell/Rossdam/Fairy/Orc surveys and macro-network overview after planner recovery.
5. Measure vertical residency and world-build/route/feature/CPU/FPS/memory/streaming/render/far-field cost against budgets.
6. Re-fetch/merge current master if it advanced, pass final exact-SHA focused + automatic module + SceneIssue gates, then close/move the assignment and non-force promote the exact feature head per workflow.