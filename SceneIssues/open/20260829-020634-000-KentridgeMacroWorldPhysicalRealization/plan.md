# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Preserve the source-backed Mounting Force/Kentridge graph while physically realizing settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded slopes, semantic constrained routes, lake/ridge realization, feature-aware vertical residency/readiness, and production evidence sequencing.
- Experiments 019-025 ruled out missing planned structures, coarse visibility/readiness, and settlement survey framing. Experiment 023 replaced the costly solid blockout fallback with bounded hollow wall shells and retained an independent cost regression.
- Experiment 026 exact run `33417092425` proved Moordell and Rossdam authored centres were visible and render-ready while authoritative top-solid sampling still matched procedural terrain.
- Agent 7's shared spatial reservation system is integrated without moving geography/route ownership out of `TopDownWorldPhysicalPlanner`; an independent non-Kentridge fixture proves road/building conflicts are rejected.
- Spatial-reservation integration was exact-SHA green at source `7033ee755ae6bac7cc3c1cc3c83bb4ee2e7d5f5e` in run `33441865025`.

## Experiment 027 root cause and correction
Run `33448157883` proved the repeated sky/fog symptom originated in deterministic hard-route planning around `southern-ridge`, before structure publication. After the endpoint-gate/settlement-approach mismatch was isolated, source-backed geometry showed the Orc generic north gate overlapped the corridor-expanded ridge by 20dm. The durable correction stayed in scene composition: preserve the ridge dimensions and move its authored centre 30dm north; shared `GoAround` remains strict by default, with only the explicit Kentridge ridge-shoulder relationship using `EndpointEscape`.

Run `33461949335` then proved planner recovery: all 20 routes / 824 route tiles and all 16 generic buildings plan, and the standalone player reaches feature generation. Its remaining focused failure was test-only: the storage regression sampled the intentionally hollow centre of the four-wall generic shell. The regression now probes an authored perimeter wall without changing production geometry.

Exact source `b500683169ed0ea2f1e4997ce83560f78093f8e0` passed the focused storage test, repository-derived module validation, and standalone replay in run `33464366092` attempt 3 (artifact `9784884651`); attempts 1-2 were the already-proven runner free-memory-floor infrastructure failure. The 60-second artifact is behaviorally valid but not visual closure evidence: the existing driver reports seven targets in order (`moordell`, `rossdam`, `rossdam-lake`, `fairy-village`, `orc-village`, `southern-ridge`, `macro-network-overview`), and Rossdam only reaches readiness around t=59s. Therefore the timeout, not a new product hypothesis, prevents the later acceptance captures.

## Next validation
1. Run the unchanged supported SceneIssue evidence sequence for 180 seconds on the new exact docs-recorded feature SHA; retain the same focused test and automatic repository-derived module gates.
2. Inspect full-resolution Moordell/Rossdam/Fairy/Orc surveys, arrival/exit roads, Rossdam lake + constrained route, Southern Ridge/pass, macro-network overview, and representative CharacterMotor traversal. Only standalone-player evidence can close visual criteria.
3. If a target is visually defective, record that demonstrated acceptance defect and fix its owner; do not alter the evidence sequence merely to frame around it.
4. Extract final route/water/feature/streaming/render/FPS/memory telemetry and quantify vertical-residency blast radius without changing horizontal interest/device budgets.
5. Re-fetch current master immediately before the final exact-SHA gate. After every criterion is green, move the issue open->closed, set `status=fixed` / `resolvedUtc`, merge current master, and non-force promote the exact feature head.