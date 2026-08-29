# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Baseline production had an authoritative semantic macro graph but realized it mainly as markers/simple road paint. Closure requires physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, exact built-player evidence, and measured cost.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: no reusable water/barrier relationships, crossing/pass semantics, generic settlement realization, or blocked-route rejection.
2. **Keep the source-backed graph authoritative and add a reusable physical-plan layer.** Selected. `TopDownWorldLayout` remains topology/provenance authority; region/route/settlement intent resolves deterministic physical output before voxel emission.
3. **Run `33232755172` failed because Rossdam Lake was merely oversized.** Rejected as the repair. Mapping the hard graph showed the substantial modern lake genuinely grazes the verified Fighting Area I -> Bandit Hideout corridor. The correct contract-level response is an explicit dry `GoAround` shoreline solution, clearly labeled modern blockout rather than legacy geography.

## Implemented direction
- Region vocabulary: water, ridge/mountain, valley/pass, meadow, woodland, generic countryside; deterministic extents/elevation, relationships, exclusions, crossing/pass semantics.
- Hard-route solver respects settlement envelopes, terrain slope, blockers and semantic solutions; impossible blocked routes are rejected.
- Kentridge/Hightown keep richer generators; Moordell, Rossdam, Fairy Village and Orc Village receive >=4-building streamed blockouts plus streets/road arrivals.
- Rossdam is a carved, water-filled streamed basin. Intended north/Rossdam routes and the Bandit shoreline spur use explicit dry route-around semantics; Logan uses an authored ridge pass.
- Production road acceptance is <=6 voxels rise per 30 dm step. Evidence mode uses the real production motor/streamer and surveys all required macro destinations/geography.
- Production trace confirms `KentridgeDefinition.Build(seed)` selects the macro graph before `KentridgeCombinedVoxelCatalogue` consumes it one-shot; no second graph/static destination hierarchy is introduced.

## Regression / CI state
Final target: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`.

Runs `33230924543` and `33231300309` were product-red namespace compile diagnostics. Run `33232755172` compiled, then exposed the Rossdam/Bandit semantic-route defect and the same exception in the built scene; its visuals are diagnostic only. Current master `9b452aedd9b5d1b1720bf0e9184d0381f159d352` was merged cleanly at `477f9159821ee466ad54d133c1aaf1dcb71433dd`, preserving the landed meadow/ecology work.

## Remaining gates
Freeze the repaired head; obtain green exact-SHA focused CI + 60s built `KentridgePlayableSlice`; inspect roads/settlements/lake/ridge/CharacterMotor evidence; measure route/world-build/CPU/GPU/memory/streaming cost; complete every `tasks.md` checkbox; then open -> pending -> closed bookkeeping and final current-master merge/non-force master push.
