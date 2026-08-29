# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the full repro contract. Baseline production had an authoritative semantic macro graph but realized it as neutral markers plus simple road paint. Closure requires physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, exact built-player evidence, and measured cost.

## Hypotheses / result
1. **Scale up the legacy markers/roads.** Rejected: that path had no reusable water/barrier relationships, pass/crossing semantics, generic settlement realization, or blocked-route rejection.
2. **Keep the source-backed graph authoritative and add a reusable physical-plan layer.** Selected. `TopDownWorldLayout` remains topology/provenance authority; shared region/route/settlement intent plans deterministic physical output before voxel emission.

Production trace confirms `KentridgeDefinition.Build(seed)` selects the macro layout before `KentridgeCombinedVoxelCatalogue` consumes it one-shot, so the implementation is connected to the playable scene rather than a second graph.

## Implemented direction
- Region vocabulary: water, ridge/mountain, valley/pass, meadow, woodland, generic countryside; deterministic extents/elevation, relationships, exclusions, crossing/pass semantics.
- Terrain-aware hard-route solver respects settlement envelopes, slope/obstacles and explicit semantic solutions; impossible blocked routes are rejected.
- Kentridge/Hightown keep richer generators; Moordell, Rossdam, Fairy Village and Orc Village receive >=4-building streamed blockouts plus streets/road arrivals.
- Bounded WorldBuilder feature definitions/placements preserve streaming/LOD ownership; no remote GameObject hierarchy or direct scene voxel writing.
- Rossdam uses a carved, water-filled streamed basin rather than surface repaint. Production roads have an additional <=6-voxel rise per 30 dm acceptance bound.
- Built-player evidence pre-streams and traverses a generated Moordell macro-road segment with the normal CharacterMotor/AutoWalk path before settlement/geography survey captures.

## Regression / CI state
Final PlayMode target:
`VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`

It nests deterministic realization/reachability/blocked-route coverage, then verifies strict road rise, carved water depth, and Select -> combined-production-catalogue survival. Static cost remains bounded by existing 1280-voxel feature footprints and one-shot selection; exact counts and player CPU/GPU/memory/streaming evidence remain runtime gates.

Run `33230924543` found a missing `Game.WorldBuilder.Api` import in the evidence driver; fixed at `339ca94f…`. Run `33231300309` then found the same missing import in the production acceptance test; fixed at `e40fb722…`. Both were product-red compile diagnostics and produced no acceptable built-player evidence. Current master `f3ce207c…` was merged cleanly at `8a5b386e…`; its changes only touched other SceneIssues.

## Remaining gates
Obtain green exact-SHA focused CI + 60s built `KentridgePlayableSlice` replay; inspect settlement/road/lake/ridge/CharacterMotor visuals and logs; record exact cost; complete every `tasks.md` checkbox; then open -> pending -> closed bookkeeping and final current-master merge/non-force master push.
