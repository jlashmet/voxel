# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, structural compatibility/topology, ecology policy, hidden-space topology, or presentation ownership. Close only after production consumers, focused regressions, module-local built-player evidence, Kentridge integration, cost/blast-radius checks, and exact-SHA CI are all green.

## Final implementation

The shared reservation core owns only stable semantic identity/provenance, integer-decimetre 3D geometry, categories/masks, hard/clearance/protected/handoff/soft semantics, deterministic precedence/diagnostics, and bounded snapshot queries. It does not use Unity Physics or mutable global first-writer state.

Production composition integrates that substrate without taking policy from its existing owners:
- Kentridge settlement placement publishes/query shared claims while retaining bounded deterministic site/topology policy.
- `WorldRoadNetwork` remains road/grade authority. Organic Kentridge solves one road network, derives reservations from that exact solved network, and reuses it for rasterization.
- `TopDownWorldReservationAdapter` exposes macro envelopes, solved roads, and compatible settlement-arrival handoffs.
- typed structural sockets retain compatibility/topology/orientation/support ownership; `StructuralSocketReservationAdapter` uses an accepted solved attachment only for external WorldBuilder clearance.
- ecology keeps species/density policy; vegetation uses configured reservation yield/suppression.
- hidden-space topology stays in its planner while true 3D realization claims use the same reservation query path.

The Kentridge adapter consumes its caller-supplied resolved `SettlementPlan` plus solved road network; it does not re-solve settlement data from seed. The production-structure regression source therefore matches the geometry physically rasterized by the road backend.

## Validation surface

Focused validation is module-local, not Worldbuilding Gallery:
- scene: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/SpatialReservationValidation.unity`
- scenario: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/spatial-reservations.player-scenario.json`
- metadata: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/spatial-reservations.module-validation.json`
- presentation: `Assets/Game/Composition/Showcase/SceneRuntime/SpatialReservationValidationShowcase.cs`

The scene is presentation-only and owns no placement authority or colliders. Direct inspection of final run `33366247235` shows readable white hard occupancy, cyan clearance, yellow road, green public access, red rejected overlap, and magenta underground evidence below a neutral surface slice. The final Kentridge survey capture is coherent and traversable-looking with roads, structures, plaza, terrain, and traversal overlay present.

## Exact-SHA evidence

Final product SHA: `a29fc6cb95f0c5f576105f8e88829ba55cbff5e2`.

Final CI transport request: `e4253ada651917bc8508710f10a097533b8cadda`; run `33366247235`; result `success`. The explicit affected Kentridge plot/foundation PlayMode regression passed, then automatic module validation passed:
- `VoxelEngine.Tests.EditMode.SpatialReservationTests`
- `VoxelEngine.Tests.EditMode.SpatialReservationProductionIntegrationTests`
- `VoxelEngine.Tests.EditMode.SpatialReservationReusabilityTests`
- `VoxelEngine.Tests.EditMode.SpatialReservationStructuralSocketIntegrationTests`
- built `Assets/Scenes/KentridgePlayableSlice.unity`
- built module-local `SpatialReservationValidation.unity`

The current workflow also ran `tools/tests/test_module_validation_plan.py` and full Unity compile/build through the focused and player gates. Repository/workflow search found no separate current `ProjectValidator` target, so no nonexistent gate is claimed.

Final local-player cost marker:
`SPATIAL_RESERVATION_COST build_ticks=347619 claims=81 query_buckets=4 query_candidates=14 query_tests=14 allocated_bytes=65560368 reserved_bytes=155123712 unused_reserved_bytes=89563344`.
The ready marker reports 81 claims and the deliberate `ClearanceConflict` rejection. No NullReferenceException, MissingReferenceException, or shader-error marker was present.

## Blast radius / closure state

Final compare against `origin/master` `2ea5f5c95f89fbf0403dbefb50b782829583d304` is 0 behind and contains only assignment-scoped WorldBuilder reservation/Kentridge integration, dedicated validation presentation/assets, and reservation regressions/bookkeeping. No CharacterMotor, global/device/region budget, or world-generation tolerance file is changed. Reservations remain compact derived data with bounded local queries; the validation scene creates presentation objects only.

All acceptance criteria are validated on the exact product SHA. Closure bookkeeping may now set `status=fixed`, record the exact evidence, move this directory directly from `open/` to `closed/`, re-fetch current master, and non-force promote the final feature head. If master advances before promotion, merge/revalidate/retry instead of forcing.
