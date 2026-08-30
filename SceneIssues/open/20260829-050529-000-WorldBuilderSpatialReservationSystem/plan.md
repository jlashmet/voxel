# Plan — WorldBuilder Spatial Reservation System

## Observed gap

WorldBuilder has engine-free integer planning contracts, but spatial ownership is fragmented. Kentridge historically rejected sites with private rectangle/plaza tests; ecology owns exclusions separately; hidden-space contracts already model local 3D bounds; architecture exposes `StructureSiteGeometry`; and no canonical production reservation/claim service exists on current `master`. The typed `StructuralSocket` work named by the feature is still not merged, so this branch must integrate through the existing architecture boundary rather than depend on another assignment.

Current `master` now includes the completed road-network integration. `WorldRoadNetwork` is the canonical resolved road aggregate and explicitly owns shared road geometry, shoulder, clearance and local-frame sampling. This feature will consume that aggregate; it will not reproduce polyline-distance math or take over road solving/grade policy.

## Ownership decision

Create the canonical reservation substrate in `Assets/Game/WorldBuilder/Generation/Core` using integer decimetres. It owns only stable spatial identity/provenance, hard occupancy, clearance, protected corridors, compatible handoffs, soft-yield semantics, bounded integer broad-phase queries, deterministic precedence, diagnostics, and query-cost counters.

Settlement topology, route solving/grade, socket compatibility/orientation/support, ecology species/density, cave topology, terrain suitability, and presentation remain with their current owners.

Production adapters are intentionally thin:

- roads: adapt `WorldRoadNetwork` resolved route/core/clearance geometry into bounded reservation claims;
- structures: adapt `StructureSiteGeometry` and accepted child/attachment clearances without deciding compatibility/orientation/support;
- vegetation: let `KentridgeVegetationLayout` / `KentridgeVegetationPlanner` query shared claims after ecology chooses candidates, then optionally publish accepted vegetation ownership;
- underground: adapt `SiteHiddenSpaceRealization` through `KentridgeHiddenSpaceBatchPlanner` / `KentridgeHiddenSpacePlanner` with true Y extents;
- inspection: expose the same claims/decisions non-authoritatively in the existing Worldbuilding Gallery runtime path.

## Implementation sequence

1. Add immutable reservation contracts, 3D box/corridor intersection, bounded window snapshots, planner-local state, order-independent precedence resolution, and stable conflict diagnostics/cost metrics. **Implemented.**
2. Migrate `KentridgeTownPlanner` building/plaza rejection to the shared service and publish building clearance plus entrance/access claims. **Implemented; temporary pre-road-integration inferred road claims remain to be replaced.**
3. On reconciled current master, replace temporary road inference with `WorldRoadNetwork` claims, then wire the existing structure, vegetation and hidden-space production boundaries through caller-owned snapshots.
4. Add focused EditMode regressions for canonical road adaptation, deterministic identity/order, hard/clearance conflicts, compatible handoff, true 3D separation, Kentridge production use, vegetation/architecture/hidden-space use, diagnostics, bounded work and regeneration/order stability.
5. Extend `WorldbuildingGalleryShowcase` inspection visualization and exercise the real Kentridge path with surface, underground and deliberate rejection evidence.
6. Measure blast radius and cost, run the required exact-SHA workflow gates, then make the single final targeted-CI request through `ci-test/fixes/agent-7`. Only after exact-SHA CI and built-application evidence are green will metadata move `open -> pending -> closed` and the exact final feature head be promoted non-force to `master`.

## Current discriminator / risk

The main compatibility risk is changing Kentridge layout while replacing its asymmetric “candidate expanded by 18dm vs existing footprint/plaza” checks and temporary inferred roads. The shared claims preserve the 18dm effective separation; canonical road claims must use `WorldRoadNetwork` widths/clearance rather than a second approximation. Existing candidate order and the bounded 256-attempt budget remain fixed.

Large envelopes/corridors are clipped to a bounded planning window so they cannot materialize world-scale bucket sets. Query/source construction cost and bucket/candidate/narrow-phase work will be measured before closure. No authoritative GameObject/collider-per-reservation representation is allowed.

## Reconciliation state

The prior feature head `dc40995b5c33ecb80a48b4495a5294015ddec724` was reconciled with current `master` `e95324aeaef619cb49d84bf2b07f770184bead81` in two-parent merge `2b6c5b7912d30b4b923298a4d394e813cc3228d5`. The task-record reconciliation commit is `31dcb49ce20d795c2956459535da026006f20bb6`. Re-fetch `master` again before every final workflow/promotion gate.
