# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, architecture compatibility, ecology policy, hidden-space topology, or presentation ownership. Close only after the real production seams consume the shared data, focused regressions pass, runtime/gallery evidence is durable, cost/blast radius are checked, and exact-SHA CI is green.

## Resume gate — 2026-08-30

`fixes/agent-7` was fetched and reconciled with then-current `origin/master` `dfbc43b086b60798d66ea36f49fabc8a0ad73297` by two-parent merge `1dd53c5cfd809845b6a5bb5d26eadf17bd44e4cc`; the incoming master delta was only the strengthened `AGENTS.md` visual-quality gate. `SceneIssues/feature-readme.md` is absent; `SceneIssues/README.md` is the repository's declared SceneIssue workflow authority.

Source audit after reconciliation:

- Core reservation identity, 3D bounds/corridors, semantics, bounded snapshots, deterministic resolution, diagnostics/metrics, planner-local replay/release, and resolved local+global snapshots are implemented.
- `KentridgeHiddenSpaceBatchPlanner` already consumes real 3D realization claims and a caller snapshot.
- `KentridgeVegetationPlanner.ApplySpatialReservations` already filters the grouped tree+boulder plan against one shared snapshot; decorative moss/vines/ground plants are a separate visual dressing pass and do not own world occupancy.
- Production combined Kentridge architecture uses `KentridgeSharedStructureVoxelCatalogue`, not the comparison-only `KentridgeGrammarVoxelCatalogue`; the production shared-structure path still needs a reservation query seam.
- `TopDownWorldVoxelCatalogue.Build` still creates an implicit-road town snapshot before solving the canonical `WorldRoadNetwork`, creates a second tree snapshot with `WorldRoadNetwork.Empty`, and does not call `ValidateRoadHandoffs`. That is a real production authority/cost defect.

## Implementation

1. Preserve the existing Core contract; add only the smallest reusable query capability needed for a structure child/site to ignore its own host-owner claims without rebuilding a filtered snapshot per role.
2. In `KentridgeCombinedVoxelCatalogueCanonical`, build the canonical `WorldRoadNetwork` and one caller-owned town reservation snapshot once, then thread the already-resolved `SettlementPlan` and snapshot into `KentridgeSharedStructureVoxelCatalogue`.
3. In `KentridgeSharedStructureVoxelCatalogue`, validate each generated/bespoke realization's site-clearance claim against that shared snapshot, excluding only the matching host plot owner. Architecture retains form/orientation/support/piece-selection authority.
4. In `TopDownWorldVoxelCatalogue`, solve the canonical road network once before reservation construction; use it for settlement and bounded tree snapshots, validate road handoffs once, and reuse the same network for road rasterization.
5. Add focused regressions for exact half-open touching semantics, owner-excluded structure queries, production canonical-road handoff, deterministic precedence/tie behavior, compatibility, grouped vegetation, true-3D hidden space, replay/release, and bounded query work.
6. Trace and validate the existing `WorldbuildingGalleryAuditHarness`/reservation inspection path; add only read-only visualization/evidence if a gap remains. Verify authored benchmark paths and highlight-policy requirements.
7. Run repository-supported static/Unity tests, ProjectValidator, runtime/built-player gallery/playable-slice checks, and record query/snapshot/blast-radius evidence. No visual acceptance from code inspection alone.
8. When implementation and runtime acceptance are complete, move open -> pending per `SceneIssues/README.md`; merge current master, perform the final targeted request only on `ci-test/fixes/agent-7` without replacing a queued request, and require exact feature SHA PASS.
9. After the green exact-SHA gate, complete required metadata, move pending -> closed with `status=fixed` and `resolvedUtc`, merge current master again if required by the workflow, revalidate any changed tree, and promote the exact validated feature head to `origin/master` non-force. If master advances, fetch/merge/revalidate/retry.

## Blast radius / cost constraints

- No global first-writer registry, Physics authority, per-reservation GameObjects/colliders, duplicate road solver, or duplicate ecology/hidden-space policy.
- Preserve Kentridge deterministic role/candidate ordering and existing generation budgets.
- Reuse one road solve and one reservation source snapshot at each production stage; do not rebuild the full town once per architecture role.
- Bound snapshots to caller windows; use query metrics (buckets, broad candidates, narrow tests, intersections) as durable cost evidence.
- Scope changes to this assignment's Core/WorldBuilder production seams, tests, audit evidence, and SceneIssue metadata only.