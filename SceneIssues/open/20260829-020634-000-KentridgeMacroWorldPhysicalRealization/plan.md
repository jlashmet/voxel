# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Baseline production had an authoritative semantic macro graph but realized it mainly as markers/simple road paint. Closure requires physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, exact built-player evidence, and measured cost.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: no reusable water/barrier relationships, crossing/pass semantics, generic settlement realization, or blocked-route rejection.
2. **Keep the source-backed graph authoritative and add a reusable physical-plan layer.** Selected. `TopDownWorldLayout` remains topology/provenance authority; region/route/settlement intent resolves deterministic physical output before voxel emission.
3. **Run `33232755172` failed because Rossdam Lake was merely oversized.** Rejected as the repair. Mapping the hard graph showed the substantial modern lake genuinely grazes the verified Fighting Area I -> Bandit Hideout corridor. The correct contract-level response is an explicit dry `GoAround` shoreline solution, clearly labeled modern blockout rather than legacy geography.
4. **Run `33255557296` is infrastructure fallout from the prior repair.** Rejected. Compilation/build/player startup complete; the production planner deterministically rejects `south-fighting-area-1->orc-village` because that hard route crosses the authored `southern-ridge` without a semantic solution. Treat this as a second product-level intent defect.
5. **Give the Orc branch a second ridge pass.** Rejected after source/topology inspection. The verified graph puts Orc Village directly south of South Fighting Area I. `southern-ridge` is explicitly modern geography across the Logan route, with a `Separates(South Fighting Area I, Logan Approach)` relationship and source text naming Logan. The Orc conflict is only a west-edge graze of that Logan barrier.
6. **Shrink/move the ridge until Orc no longer collides.** Rejected. That would weaken the substantial ridge and its intended Logan-route constraint just to hide an incidental collision.
7. **Preserve both the authoritative hard graph and the substantial Logan ridge, and explicitly skirt the ridge shoulder on the Orc route.** Selected and implemented. The existing deterministic `GoAround` semantic targets `southern-ridge`; the focused production regression verifies the authored semantic and that the resulting Orc travel corridor remains outside the ridge margin while the full acceptance path continues to require walkability.

## Implemented direction
- Region vocabulary: water, ridge/mountain, valley/pass, meadow, woodland, generic countryside; deterministic extents/elevation, relationships, exclusions, crossing/pass semantics.
- Hard-route solver respects settlement envelopes, terrain slope, blockers and semantic solutions; impossible blocked routes are rejected.
- Kentridge/Hightown keep richer generators; Moordell, Rossdam, Fairy Village and Orc Village receive >=4-building streamed blockouts plus streets/road arrivals.
- Rossdam is a carved, water-filled streamed basin. Intended north/Rossdam routes and the Bandit shoreline spur use explicit dry route-around semantics; Logan uses an authored ridge pass; the distinct Orc Village branch uses an authored ridge-shoulder `GoAround` rather than a second pass.
- Production road acceptance is <=6 voxels rise per 30 dm step. Evidence mode uses the real production motor/streamer and surveys all required macro destinations/geography.
- Production trace confirms `KentridgeDefinition.Build(seed)` selects the macro graph before `KentridgeCombinedVoxelCatalogue` consumes it one-shot; no second graph/static destination hierarchy is introduced.

## Final repair / blast radius
The Orc repair is complete in `KentridgeTopDownWorldPhysicalIntent` plus the focused production-path regression. The generic planner, ridge size/placement, catalogue, and water behavior remain unchanged. Runtime cost should remain effectively unchanged: one additional immutable route-solution definition consumed by the existing deterministic planner, with no eager GameObjects, catalogue layers, or streaming primitives added.

Before final CI, current `master` `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c` was integrated as a true two-parent merge at `379439a571b3e941ee9fc818c402fc49331ebf28`. Its unrelated opening-cutscene/campaign and CI-bridge state was preserved; the feature overlay remained limited to agent-6 Kentridge/WorldBuilder/test/assignment paths.

## Regression / CI state
Final target: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`.

Runs `33230924543` and `33231300309` were product-red namespace compile diagnostics. Run `33232755172` compiled, then exposed the Rossdam/Bandit semantic-route defect. Run `33255557296` compiled and built successfully, then exposed the Orc Village/southern-ridge semantic-route defect during production planning; its built-player evidence never reached a usable rendered state and is diagnostic only. Both semantic defects now have narrow authored solutions and focused regressions.

## Remaining gates
Freeze the repaired/current-master feature head, obtain one fresh green exact-SHA focused CI + built `KentridgePlayableSlice`, inspect roads/settlements/lake/ridge/CharacterMotor evidence, measure route/world-build/CPU/GPU/memory/streaming cost, complete every `tasks.md` checkbox, then open -> pending -> closed bookkeeping, re-merge current master if it advanced, and non-force promote the exact feature head to `master`.
