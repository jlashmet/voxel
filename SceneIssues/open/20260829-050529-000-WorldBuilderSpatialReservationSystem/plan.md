# Plan — WorldBuilder Spatial Reservation System

## Observed gap

WorldBuilder has engine-free integer planning contracts, but spatial ownership is fragmented. Kentridge still rejects sites with private rectangle/plaza tests; ecology owns exclusions separately; hidden-space contracts already model local 3D bounds; architecture exposes structure-site geometry; and no canonical production reservation/claim service exists on current `master`. The typed `StructuralSocket` work named by the feature is not merged, so this branch must integrate through the existing architecture boundary rather than depend on another assignment.

## Ownership decision

Create the canonical reservation substrate in `Assets/Game/WorldBuilder/Generation/Core` using `Int2`/`Int3` decimetres. It owns only stable spatial identity/provenance, hard occupancy, clearance, protected corridors, compatible handoffs, soft-yield semantics, bounded integer broad-phase queries, deterministic precedence, diagnostics, and query-cost counters.

Settlement topology, route solving/grade, socket compatibility/orientation/support, ecology species/density, cave topology, terrain suitability, and presentation remain with their current owners.

## Implementation sequence

1. Add immutable reservation contracts, 3D box/corridor intersection, bounded window snapshots, planner-local state, order-independent precedence resolution, and stable conflict diagnostics/cost metrics.
2. Migrate `KentridgeTownPlanner` building/plaza rejection to the shared service and publish building clearance plus entrance/route claims.
3. Add production adapters at existing settlement/road, architecture, ecology/vegetation, and hidden-space boundaries; do not create parallel spatial indices.
4. Add focused EditMode regressions for deterministic identity/order, hard/clearance conflicts, compatible handoff, true 3D separation, Kentridge production use, vegetation/architecture use, diagnostics, and bounded work.
5. Add/extend `WorldbuildingGalleryShowcase` inspection visualization and exercise the real Kentridge path.
6. Measure blast radius and cost, then run the smallest exact-SHA CI requests plus the required built-application scene gate. Only after both gates are green will metadata move `open -> pending -> closed`.

## Current discriminator / risk

The main compatibility risk is changing Kentridge layout while replacing its asymmetric “candidate expanded by 18dm vs existing footprint/plaza” checks. The shared claims will preserve that effective separation before adding access-corridor protection; regressions will compare deterministic layouts and public access. Large envelopes/corridors will be clipped to a bounded planning window so they cannot materialize world-scale bucket sets.

Current source head before implementation: `ec23c95466ae8cb1627fe318733a88290932764f`.
