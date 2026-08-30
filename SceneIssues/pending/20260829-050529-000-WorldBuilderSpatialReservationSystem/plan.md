# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, architecture compatibility, ecology policy, hidden-space topology, or presentation ownership. Close only after real production seams consume the shared data, focused regressions pass, runtime/gallery evidence is durable, cost/blast radius are checked, and exact-SHA CI is green.

## Resume gate — 2026-08-30

`fixes/agent-7` is reconciled with then-current `origin/master` `5f07db5cd7677e84f617deb61c5b03a4b896159c` by two-parent merge `23d16dc51e49f17adf4c9bcedc9306c22e264bd1`. The incoming delta is workflow-guide maintenance plus removal of an unrelated queued SceneIssue; no other assignment implementation was changed. `SceneIssues/feature-readme.md` now exists and is used together with `SceneIssues/README.md` and `AGENTS.md`.

Current source audit:

- Core reservation identity, 3D geometry, semantics, bounded snapshots, deterministic resolution, diagnostics/metrics, planner-local replay/release, and resolved local+global snapshots are implemented.
- `KentridgeHiddenSpaceBatchPlanner` consumes real 3D realization claims and a caller snapshot.
- `KentridgeVegetationPlanner` filters grouped trees+boulders against one shared snapshot; decorative moss/vines/ground plants remain non-authoritative visual dressing.
- Production Kentridge architecture is `KentridgeCombinedVoxelCatalogueCanonical` -> `KentridgeSharedStructureVoxelCatalogue` and now validates production structure clearance against the shared reservation source while keeping architecture form/orientation/support/piece authority.
- `TopDownWorldVoxelCatalogue.Build` solves the canonical `TopDownWorldRoadNetwork` exactly once, validates reservation handoffs on that solved network, and reuses it for rasterization.
- The next required implementation task is the focused production macro-road handoff regression; no opportunistic enhancements are in scope.

## Implementation

1. Keep Core conflict semantics unchanged.
2. Maintain one Kentridge canonical reservation source snapshot for the combined catalogue and thread it with the already-resolved `SettlementPlan` into `KentridgeSharedStructureVoxelCatalogue`.
3. Keep production structure validation bounded to role-local source views that exclude only the matching host plot owner; reservation policy must not own architecture form/orientation/support/piece selection.
4. Keep `TopDownWorldVoxelCatalogue.Build` on its single canonical road solve and validate handoffs on that exact network before rasterization.
5. Add the focused production macro-road regression, then complete only acceptance-required reuse, cost, gallery/runtime, and regression evidence from `tasks.md`.
6. Prove the generic reservation surface from an independent non-Kentridge fixture without place/material IDs or consumer-pair policy in shared code.
7. Keep gallery/debug inspection presentation-only; add only the runtime visualization needed by the acceptance criterion.
8. Run repository-supported compile/static, Unity/EditMode, ProjectValidator, runtime/built-player gallery/playable-slice, visual and cost/blast-radius gates.
9. Merge current master before final validation as required. Use only `ci-test/fixes/agent-7` for the final targeted exact-SHA request and never replace queued/running CI.
10. After green exact-SHA CI, complete pending metadata, move pending -> closed with `status=fixed` and `resolvedUtc`, merge current master again if required, revalidate any changed tree, and non-force promote the exact feature head to `origin/master`. If master advances, fetch/merge/revalidate/retry.

## Blast radius / cost

- No global registry, Physics authority, per-reservation authoritative GameObjects/colliders, duplicate road solver, or duplicate ecology/hidden-space policy.
- Preserve deterministic Kentridge candidate/role ordering and all existing budgets.
- Keep one shared source snapshot plus bounded role-local filtered views rather than reconstructing town/road data per role.
- Keep snapshots bounded and record query metrics/source-construction evidence.
- Scope changes to this assignment's WorldBuilder/Core seams, focused tests/evidence, gallery presentation seam, and SceneIssue metadata only.
