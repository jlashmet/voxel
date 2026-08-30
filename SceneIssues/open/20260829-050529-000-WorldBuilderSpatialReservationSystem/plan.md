# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, architecture compatibility, ecology policy, hidden-space topology, or presentation ownership. Close only after real production seams consume the shared data, focused regressions pass, runtime/gallery evidence is durable, cost/blast radius are checked, and exact-SHA CI is green.

## Resume gate — 2026-08-30

`fixes/agent-7` was fetched and reconciled with current `origin/master` `65e33762a0d0f1739e9a518484d119e551f01f81` by two-parent merge `d256dc2044c88b254751448012b60a138e716f27`. The incoming master delta is an unrelated open GPU SceneIssue only; no other assignment content was edited. `SceneIssues/feature-readme.md` is absent on both refs; `SceneIssues/README.md` is the repository's declared workflow authority.

Post-merge source audit:

- Core reservation identity, 3D geometry, semantics, bounded snapshots, deterministic resolution, diagnostics/metrics, planner-local replay/release, and resolved local+global snapshots are implemented.
- `KentridgeHiddenSpaceBatchPlanner` already consumes real 3D realization claims and a caller snapshot.
- `KentridgeVegetationPlanner` already filters grouped trees+boulders against one shared snapshot; decorative moss/vines/ground plants are non-authoritative visual dressing.
- Production Kentridge architecture is `KentridgeCombinedVoxelCatalogueCanonical` -> `KentridgeSharedStructureVoxelCatalogue`; it still bypasses reservation validation. `KentridgeBuildingGrammar` is the comparison compatibility path.
- `TopDownWorldVoxelCatalogue.Build` already solves the canonical `TopDownWorldRoadNetwork` exactly once and reuses it for rasterization. Its real gap is that it never invokes the existing shared reservation handoff validator for that solved network.

## Implementation

1. Keep Core conflict semantics unchanged.
2. Build one Kentridge canonical reservation source snapshot for the combined catalogue and thread it with the already-resolved `SettlementPlan` into `KentridgeSharedStructureVoxelCatalogue`.
3. Validate each production structure realization's `StructureSiteGeometry` clearance against a bounded role-local view of that source excluding only the matching host plot owner. Do not move form/orientation/support/piece selection into reservation policy.
4. In `TopDownWorldVoxelCatalogue.Build`, invoke `TopDownWorldReservationAdapter.ValidateRoadHandoffs` immediately after the one canonical road solve; do not solve roads twice or create parallel authority.
5. Add focused regressions for half-open touching semantics, architecture shared-source validation, macro road handoff, deterministic precedence/tie behavior, grouped vegetation, true-3D hidden space, replay/release, and bounded query work.
6. Trace and validate the existing gallery audit/reservation inspection path, authored benchmark paths, and current visual-highlight policy. Add only read-only inspection/evidence if gaps remain.
7. Run repository-supported compile/static, Unity/EditMode, ProjectValidator, runtime/built-player gallery/playable-slice, visual and cost/blast-radius gates.
8. After open-phase acceptance is complete, follow `SceneIssues/README.md`: move open -> pending, merge current master, make the final targeted request only through `ci-test/fixes/agent-7` without replacing a queued request, and require exact feature SHA PASS.
9. After green exact-SHA CI, complete pending metadata, move pending -> closed with `status=fixed` and `resolvedUtc`, merge current master again if required, revalidate any changed tree, and non-force promote the exact validated feature head to `origin/master`. If master advances, fetch/merge/revalidate/retry.

## Blast radius / cost

- No global registry, Physics authority, per-reservation authoritative GameObjects/colliders, duplicate road solver, or duplicate ecology/hidden-space policy.
- Preserve deterministic Kentridge candidate/role ordering and all existing budgets.
- Eliminate per-role town/road reconstruction in production architecture; one source snapshot plus bounded role-local filtered views is acceptable.
- Keep snapshots bounded and record query metrics/source-construction evidence.
- Scope changes to this assignment's WorldBuilder/Core seams, focused tests/evidence, and SceneIssue metadata only.