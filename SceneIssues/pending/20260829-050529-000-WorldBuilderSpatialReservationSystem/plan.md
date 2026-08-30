# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, architecture compatibility, ecology policy, hidden-space topology, or presentation ownership. Close only after real production seams consume the shared data, focused regressions pass, runtime/gallery evidence is durable, cost/blast radius are checked, and exact-SHA CI is green.

## Resume gate — 2026-08-30

`fixes/agent-7` was reconciled with then-current `origin/master` `5f07db5cd7677e84f617deb61c5b03a4b896159c` by two-parent merge `23d16dc51e49f17adf4c9bcedc9306c22e264bd1`. The issue is already in `pending`; do not move it backward. `SceneIssues/feature-readme.md`, `SceneIssues/README.md`, and `AGENTS.md` govern the remaining work.

Current source audit:

- Core reservation identity, 3D geometry, semantics, bounded snapshots, deterministic resolution, diagnostics/metrics, planner-local replay/release, and resolved local+global snapshots are implemented.
- `KentridgeHiddenSpaceBatchPlanner` consumes real 3D realization claims and a caller snapshot.
- `KentridgeVegetationPlanner` filters grouped trees+boulders against one shared snapshot; decorative moss/vines/ground plants remain non-authoritative visual dressing.
- Production Kentridge architecture is `KentridgeCombinedVoxelCatalogueCanonical` -> `KentridgeSharedStructureVoxelCatalogue` and validates production structure clearance against the shared reservation source while keeping architecture form/orientation/support/piece authority.
- `TopDownWorldVoxelCatalogue.Build` solves the canonical `TopDownWorldRoadNetwork` exactly once, validates reservation handoffs on that solved network, and reuses it for rasterization; the focused macro-road handoff regression already exists.
- Reuse review found one real policy leak: `WorldRoadReservationAdapter` hard-coded road-clearance yield to vegetation. The generic adapter now defaults to neutral configurable `clearanceYieldingConsumers`; Kentridge/macro composition explicitly supplies vegetation, preserving production behavior.
- `SpatialReservationReusabilityTests.ClearanceYieldPolicyAndVerticalSeparationAreConsumerConfigured` is the independent non-Kentridge proof for configurable yield, unrelated consumers, and true 3D separation.
- Gallery trace found the report builder was not wired into `WorldbuildingGalleryShowcase` or its built-player audit. `SpatialReservationGalleryOverlay` now copies the read-only report into one transient camera-space line mesh; the report also exposes the deliberate rejected candidate bounds. `WorldbuildingGalleryAuditHarness` captures a feature-specific reservation screenshot after the existing physical town evidence.

## Remaining execution

1. Run repository-supported compile/static and focused reservation/Kentridge/vegetation/hidden-space regressions; fix only demonstrated acceptance/correctness defects.
2. Check `SceneTestHighlightPolicy.csv`/classifier requirements and repository-supported ProjectValidator gates.
3. Merge current `origin/master`, then rerun affected focused gates on the reconciled exact feature SHA.
4. Use built-player/runtime validation to capture `WorldbuildingGalleryShowcase` reservation evidence and physical town content; visually inspect surface hard/clearance/access, underground separation, and the red rejected candidate. Run the real `KentridgePlayableSlice` production traversal/scene validation required by acceptance.
5. Record reservation build/query metrics from `SPATIAL_RESERVATION_COST`, allocation/memory from the built audit, and confirm generation/device/streaming budgets did not move.
6. Review assignment-only blast radius and finish issue acceptance/evidence metadata.
7. Verify `ci-test/fixes/agent-7` has no queued/running request. Use only that branch for the final exact-SHA targeted CI request; never edit `.github/test-request.json` on the feature branch or replace queued/running CI.
8. After green exact-SHA CI, complete pending metadata, move pending -> closed with `status=fixed` and `resolvedUtc`, merge current master again if required, revalidate any changed tree, and non-force promote the exact feature head to `origin/master`. If master advances, fetch/merge/revalidate/retry.

## Blast radius / cost

- No global registry, Physics authority, per-reservation authoritative GameObjects/colliders, duplicate road solver, or duplicate ecology/hidden-space policy.
- The gallery uses one transient presentation mesh and copied report data; it cannot mutate reservation authority.
- Preserve deterministic Kentridge candidate/role ordering and all existing budgets.
- Keep one shared source snapshot plus bounded role-local filtered views rather than reconstructing town/road data per role.
- Keep snapshots bounded and record query metrics/source-construction evidence.
- Scope changes to this assignment's WorldBuilder/Core seams, focused tests/evidence, gallery presentation seam, and SceneIssue metadata only.
