# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Baseline production had an authoritative semantic macro graph but realized it mainly as markers/simple road paint. Closure requires physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, exact built-player evidence, and measured cost.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: no reusable water/barrier relationships, crossing/pass semantics, generic settlement realization, or blocked-route rejection.
2. **Keep the source-backed graph authoritative and add a reusable physical-plan layer.** Selected. `TopDownWorldLayout` remains topology/provenance authority; region/route/settlement intent resolves deterministic physical output before voxel emission.
3. **Run `33232755172` failed because Rossdam Lake was merely oversized.** Rejected as the repair. Mapping the hard graph showed the substantial modern lake genuinely grazes the verified Fighting Area I -> Bandit Hideout corridor. The correct contract-level response is an explicit dry `GoAround` shoreline solution, clearly labeled modern blockout rather than legacy geography.
4. **Run `33255557296` is infrastructure fallout from the prior repair.** Rejected. Compilation/build/player startup complete; the production planner deterministically rejects `south-fighting-area-1->orc-village` because that hard route crosses the authored `southern-ridge` without a semantic solution. Treat this as a second product-level intent defect.

## Implemented direction
- Region vocabulary: water, ridge/mountain, valley/pass, meadow, woodland, generic countryside; deterministic extents/elevation, relationships, exclusions, crossing/pass semantics.
- Hard-route solver respects settlement envelopes, terrain slope, blockers and semantic solutions; impossible blocked routes are rejected.
- Kentridge/Hightown keep richer generators; Moordell, Rossdam, Fairy Village and Orc Village receive >=4-building streamed blockouts plus streets/road arrivals.
- Rossdam is a carved, water-filled streamed basin. Intended north/Rossdam routes and the Bandit shoreline spur use explicit dry route-around semantics; Logan uses an authored ridge pass.
- Production road acceptance is <=6 voxels rise per 30 dm step. Evidence mode uses the real production motor/streamer and surveys all required macro destinations/geography.
- Production trace confirms `KentridgeDefinition.Build(seed)` selects the macro graph before `KentridgeCombinedVoxelCatalogue` consumes it one-shot; no second graph/static destination hierarchy is introduced.

## Current repair gate
Before changing production intent, confirm the source-backed identity/topology of `south-fighting-area-1 -> orc-village` and how `southern-ridge` was intentionally placed. If the ridge is deliberately centered on that route, preserve both authored geography and the hard graph with an explicit narrow ridge crossing/pass for that exact route. Add a focused regression that proves the route carries the semantic solution and that the planner produces a walkable physical route. Do not weaken generic blocker rejection, enlarge/shrink unrelated regions, or modify water/catalogue behavior.

Blast radius for this repair is constrained to Kentridge macro physical intent plus its focused regression. Runtime cost should remain effectively unchanged: one additional immutable route-solution definition consumed by the existing deterministic planner, with no eager GameObjects, catalogue layers, or streaming primitives added. Validate that assumption in final exact-run counts and budgets.

## Regression / CI state
Final target: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`.

Runs `33230924543` and `33231300309` were product-red namespace compile diagnostics. Run `33232755172` compiled, then exposed the Rossdam/Bandit semantic-route defect. Run `33255557296` compiled and built successfully, then exposed the Orc Village/southern-ridge semantic-route defect during production planning; its built-player evidence never reached a usable rendered state and is diagnostic only. Current master `9b452aedd9b5d1b1720bf0e9184d0381f159d352` was merged cleanly at `477f9159821ee466ad54d133c1aaf1dcb71433dd`, preserving the landed meadow/ecology work.

## Remaining gates
Resolve the Orc Village/ridge semantic intent with a focused regression, freeze the repaired head, obtain green exact-SHA focused CI + 60s built `KentridgePlayableSlice`, inspect roads/settlements/lake/ridge/CharacterMotor evidence, measure route/world-build/CPU/GPU/memory/streaming cost, complete every `tasks.md` checkbox, then open -> pending -> closed bookkeeping and final current-master merge/non-force master push.
