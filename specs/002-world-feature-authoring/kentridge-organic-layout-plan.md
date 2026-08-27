# Plan: Organic Kentridge Layout

**Branch**: `feature/kentridge-organic-layout`  
**Task list**: [kentridge-organic-layout-tasks.md](./kentridge-organic-layout-tasks.md)

## Observed behavior

Kentridge currently authors four fixed street centerlines and one plaza, then places all named plots with road-facing helpers. `SettlementPlan` exposes streets, `BuildingPlot` stores cardinal frontage plus `PlannedSiteAccess`, and access is defined as an explicit street/plaza network point. Lower Kentridge generation turns that authored movement network into road, sidewalk, frontage-path, access, terrace, and dressing geometry. Campaign/world-fact code then consumes the resulting settlement movement facts for semantic reachability.

This makes streets the source of town topology. The desired model is the opposite: Kentridge authors the character of the town—districts, landmarks, open spaces, placement preferences, density, terrain suitability—and circulation is inferred from the placed settlement. Gameplay may still require `ReachableFrom`; Kentridge content must not author `Connect(A, B)` edges.

## Acceptance criteria

- Kentridge contains no fixed street axes and no named-site placement relative to a street centerline.
- Named role IDs and campaign bindings remain stable.
- Site public access/entrance is represented independently of `PlannedStreet`.
- Same seed/input yields byte-identical planning output; different seeds yield bounded meaningful variation.
- Generated sites do not overlap and obey terrain/clearance constraints.
- Circulation is inferred from entrances, open spaces, terrain and settlement geometry, not authored edges.
- Campaign reachability requirements are validated against realized traversability.
- Voxel realization supports open ground, alleys, paths, stairs/ramps and plazas without requiring roads.
- Architecture/shape-program/rasterizer boundaries remain backend-independent and deterministic.

## Competing hypotheses

1. **Replace streets in one rewrite.** Simpler final model, but high blast radius because access, orientation, traversal facts and multiple voxel passes currently assume streets.
2. **Decouple access first, then replace Kentridge topology.** Slightly more migration work, but preserves a runnable vertical slice after each stage and isolates regressions.

**Selected approach:** hypothesis 2.

## First discriminating experiment

Add a street-independent site-access/entrance representation and adapt the existing Kentridge plan to produce identical physical placement while downstream architecture and campaign resolution consume the new access form. If this cannot preserve current behavior without leaking street assumptions, stop and redesign the access boundary before changing town topology.

## Validation gates

Each phase must retain deterministic planning tests. Before deleting legacy street code, prove multi-seed Kentridge generation, no-overlap/clearance, entrance accessibility, campaign `ReachableFrom`, generation-order parity, and rendered visual regressions. Run Unity only through `tools/unity-run.sh`; targeted CI uses `ci-test/feature/kentridge-organic-layout` per `AGENTS.md`.