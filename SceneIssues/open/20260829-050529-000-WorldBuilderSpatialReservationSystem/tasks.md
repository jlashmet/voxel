# Tasks — WorldBuilder Spatial Reservation System

## Audit / ownership

- [x] Trace Kentridge town, canonical road, architecture, ecology/vegetation, hidden-space and gallery ownership.
- [x] Confirm no competing canonical reservation service exists on reconciled master.
- [x] Keep road solving/grade in `WorldRoadNetwork`, form/orientation/support in architecture, species/density in ecology, hidden-space topology in its planner, and rendering in presentation.
- [x] Confirm typed `StructuralSocket` is not required by the current production seam; do not import another assignment.
- [x] Reconcile current master into `fixes/agent-7` before implementation (`1dd53c5cfd809845b6a5bb5d26eadf17bd44e4cc`).
- [x] Reconcile plan/checklist to the actual post-merge production paths before implementation.

## Canonical reservation contract

- [x] Stable semantic id/owner/provenance/category/precedence and stable-id tie break.
- [x] Typed consumer/category masks; hard/clearance/protected/handoff/soft semantics.
- [x] Integer-decimetre half-open 3D box/corridor geometry and true vertical separation.
- [x] Engine-free authority; deterministic diagnostics and bounded query metrics.
- [x] Immutable bounded snapshots; deterministic independent resolution.
- [x] Planner-local idempotent replay/replacement/release and resolved local+global snapshot behavior.
- [ ] Add exact half-open touching/equality regression coverage.

## Production road / macro integration

- [x] Canonical `WorldRoadReservationAdapter` consumes the already-resolved `WorldRoadNetwork` geometry/width/clearance.
- [x] `TopDownWorldReservationAdapter` publishes source-backed node envelopes, canonical road claims and explicit settlement-arrival handoffs.
- [x] `TopDownWorldVoxelCatalogue.Build` already solves the canonical `TopDownWorldRoadNetwork` once and reuses it for road voxelization.
- [ ] Call `TopDownWorldReservationAdapter.ValidateRoadHandoffs` on that exact solved network before rasterization; do not solve roads twice.
- [ ] Add/adjust focused production macro-road handoff regression.

## Production architecture integration

- [x] Real production path is `KentridgeCombinedVoxelCatalogueCanonical` -> `KentridgeSharedStructureVoxelCatalogue`; comparison grammar is not production authority.
- [ ] Build one canonical Kentridge reservation source snapshot per combined-catalogue build and thread it with the already-resolved `SettlementPlan` into shared-structure generation.
- [ ] Validate every production structure `StructureSiteGeometry` clearance against other owners using a bounded role-local view that excludes only its matching host plot owner.
- [ ] Preserve architecture ownership of form/orientation/support/piece selection.
- [ ] Add focused regression proving host-owner overlap is allowed and an incompatible external structure claim is rejected.

## Vegetation / hidden-space consumers

- [x] Grouped Kentridge tree+boulder planning consumes one shared snapshot and yields/suppresses against authoritative claims while ecology keeps species/density authority.
- [x] Decorative ground plants/moss/vines are non-authoritative surface dressing; do not add parallel occupancy authority.
- [x] Hidden-space batch planning consumes real 3D realization claims and a caller snapshot; vertical-only separation succeeds and true XYZ collision fails.
- [x] Explicit connector compatibility cannot be reused by unrelated underground consumers.
- [ ] Ensure affected vegetation/hidden-space regressions are green on final exact SHA.

## Determinism / lifecycle / cost

- [x] Stable ids, insertion-order independence, equal-precedence stable-id tie, hard/clearance/soft/handoff outcomes.
- [x] Replay/release ownership regression and bounded-window query-work regression authored.
- [ ] Add architecture shared-source/host-filter regression and macro-road production regression.
- [ ] Record representative snapshot/source construction and query metrics after integration.
- [ ] Check repository-supported allocation/memory evidence where available.
- [ ] Verify generation/device budgets and unrelated world-generation behavior did not move.

## Gallery / runtime evidence

- [ ] Trace `WorldbuildingGalleryReservationInspection` from the actual audit/showcase runtime path and prove presentation-only behavior.
- [ ] Ensure surface hard/clearance/access, underground 3D claims and a deliberate rejected candidate are visibly/readably inspectable against corresponding physical content.
- [ ] Verify issue benchmark scene paths and update stale evidence paths.
- [ ] Satisfy current `SceneTestHighlightPolicy.csv`/classifier requirements for affected scenes.
- [ ] Run exact built `WorldbuildingGalleryShowcase` and visually inspect required captures.
- [ ] Run real `KentridgePlayableSlice` built/runtime traversal check.

## Validation / closure

- [x] `SceneIssues/feature-readme.md` is absent; use `SceneIssues/README.md` plus assignment README.
- [x] Never edit `.github/test-request.json` on `fixes/agent-7`, create extra transports, or replace queued CI.
- [ ] Run focused `SpatialReservationTests` + affected Kentridge regressions and repository compile/static/ProjectValidator gates.
- [ ] Run required scene/runtime/built-player/visual gates and capture durable evidence.
- [ ] Review assignment-only blast radius and record commands/results/cost/acceptance mapping in issue metadata.
- [ ] Complete every open-phase checkbox, then move open -> pending with required testing metadata.
- [ ] Fetch/merge current master and rerun affected focused gates.
- [ ] Use `ci-test/fixes/agent-7` only for the final targeted-CI request; verify no request is already queued first.
- [ ] Obtain green exact-SHA CI and record request/run/tested-SHA evidence.
- [ ] Complete pending metadata, then move pending -> closed with `status=fixed` and `resolvedUtc` only when every acceptance criterion is complete.
- [ ] Merge current master again if required; revalidate any changed tree.
- [ ] Push the exact validated feature head to `origin/master` non-force; if master advances, fetch/merge/revalidate/retry.