# WorldBuilder Spatial Reservation System

## Summary

Create one shared, deterministic WorldBuilder spatial-reservation layer for semantic world-space claims, clearances, protected approach/travel corridors, compatible handoffs, and soft-yield exclusions.

The feature exists to stop towns, roads, macro-world envelopes, caves/dungeons, vegetation, POIs, and composed structures from each inventing their own overlap logic. It must preserve the engine's region-local deterministic generation model: world shape may not depend on streaming order, thread order, Unity Physics, or a mutable global first-writer-wins registry.

This is **not** a Wave Function Collapse feature and is **not** a complete dungeon, cave, town, road, or ecology generator. Those systems remain planners/consumers above this substrate.

## Existing behavior to reconcile

- Kentridge currently has bespoke rectangle/clearance checks in `KentridgeTownPlanner`.
- `specs/002-world-feature-authoring/research.md` R-002 already defines order-independent candidate claims using deterministic precedence and stable identity.
- The completed Kentridge top-down macro-world feature already creates reserved settlement/region envelopes.
- The open road-network feature needs reserved settlement/building envelopes and protected travel corridors.
- The open typed structural-socket feature needs child clearance/overlap rejection.
- Vegetation and future underground/POI placement need the same queryable spatial facts.

Do not build a second system beside a production reservation/claim implementation if the audit finds one. Consolidate and migrate.

## Required proving cases

1. **Kentridge settlement migration** — shared reservations replace/own the behavior behind local building/plaza overlap checks.
2. **Road + settlement handoff** — buildings cannot block an entrance/road corridor, while the intended road is explicitly compatible with that reserved handoff.
3. **True 3D underground separation** — a tunnel may pass under a surface building when vertically separated; a real forbidden volume collision is rejected; an explicit connector can be allowed.
4. **Cross-system use** — structural composition and vegetation/ecology consume the same reservation data in production paths.
5. **Inspection** — debug tooling explains why a candidate was accepted/rejected, including owner, category, bounds, precedence, compatibility, and conflicting claims.

## Validation

The assigned implementation must keep `plan.md` and `tasks.md` current, add behavioral regressions, validate `WorldbuildingGalleryShowcase` in the built application, exercise the real Kentridge integration, and measure query/build/streaming cost before closure.

See `issue.json` for full requirements and acceptance criteria.
