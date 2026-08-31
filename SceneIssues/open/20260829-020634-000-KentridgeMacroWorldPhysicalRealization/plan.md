# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Preserve the source-backed Mounting Force/Kentridge graph while physically realizing settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded slopes, semantic constrained routes, lake/ridge realization, feature-aware vertical residency/readiness, and production evidence sequencing.
- Experiments 019-025 ruled out missing planned structures, coarse visibility/readiness, and settlement survey framing. Experiment 023 replaced the costly solid blockout fallback with bounded hollow wall shells and retained an independent cost regression.
- Experiment 026 exact run `33417092425` is decisive: Moordell and Rossdam have all four authored building centres in the 90-degree survey with stable production coverage, but authoritative `TryFindTopSolid` reports `delta=0` from procedural terrain at every sampled centre. The surviving defect is therefore geometry/publication cardinality, not camera, timing, readiness, or renderer-only presentation.
- Current master `b64e9456b150b374dc950ab11f02a2427412cc66` was merged into `fixes/agent-6` at `9e6f0c47651fb8b9f797ce6e1000088e28022c5a`, bringing in Agent 7's shared WorldBuilder spatial reservation system.
- Product commit `82482e000b2c7d5c441be4dfd16532b96de78fc2` integrates the already-resolved macro physical plan with that shared substrate. `TopDownWorldPhysicalPlanner` retains geography/route solving ownership; the new adapter publishes settlement envelopes, generic building footprint/clearance, resolved road segments, and settlement-arrival handoffs through `SpatialReservationSnapshot` and validates conflicts before Kentridge rasterization. An independent `alpha`/`beta` fixture proves a road-through-building conflict is rejected without Kentridge coordinates.

## Selected approach / next gate
First validate the reservation integration on the exact branch SHA. This is an architecture/reuse prerequisite, not a substitute for Experiment 026's selected product fix. Once green, trace the generic building shell from explicit placement through feature generation into authoritative storage and correct the smallest demonstrated geometry/publication defect. No further camera/readiness experiments before that correction.

## Remaining gates
1. Exact-SHA focused CI for shared-reservation integration, including the independent conflict fixture and production Kentridge path.
2. Fix the Experiment 026 geometry/publication owner and retain a focused behavioral regression.
3. Built-player closure: readable Moordell/Rossdam/Fairy/Orc blockouts with streets/road arrivals, substantial Rossdam water + constrained route, Southern Ridge/pass, differentiated countryside, network overview, and CharacterMotor road traversal.
4. Measure vertical residency, world-build/route/feature/CPU/FPS/memory/streaming/render/far-field cost against budgets.
5. Re-fetch master, re-check exact diff, pass final exact-SHA gates, then close/move the assignment and non-force promote the exact feature head per workflow.
