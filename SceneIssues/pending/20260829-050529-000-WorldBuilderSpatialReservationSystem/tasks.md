# Tasks — WorldBuilder Spatial Reservation System

## Audit / ownership

- [x] Trace Kentridge town, canonical road, architecture, ecology/vegetation, hidden-space and gallery ownership.
- [x] Confirm no competing canonical reservation service exists on reconciled master.
- [x] Keep road solving/grade in `WorldRoadNetwork`, form/orientation/support in architecture, species/density in ecology, hidden-space topology in its planner, and rendering in presentation.
- [x] Confirm typed `StructuralSocket` is not required by the current production seam; do not import another assignment.
- [x] Reconcile current master into `fixes/agent-7` before implementation (`d256dc2044c88b254751448012b60a138e716f27`, current master `65e33762a0d0f1739e9a518484d119e551f01f81`); resumed again on 2026-08-30 and merged then-current master `7f935b26cc7aa8aff971d3488e9f9629108e419a` via `a2639073b1cb61e5fc050d3254d232c116d054e5`.
- [x] Reconcile plan/checklist to the actual post-merge production paths before implementation.

## Canonical reservation contract

- [x] Stable semantic id/owner/provenance/category/precedence and stable-id tie break.
- [x] Typed consumer/category masks; hard/clearance/protected/handoff/soft semantics.
- [x] Integer-decimetre half-open 3D box/corridor geometry and true vertical separation.
- [x] Engine-free authority; deterministic diagnostics and bounded query metrics.
- [x] Immutable bounded snapshots; deterministic independent resolution.
- [x] Planner-local idempotent replay/replacement/release and resolved local+global snapshot behavior.
- [x] Add exact half-open touching/equality regression coverage (`SpatialReservationProductionIntegrationTests.HalfOpenReservationBoundsAllowExactFaceTouching`).

## Production road / macro integration

- [x] Canonical `WorldRoadReservationAdapter` consumes the already-resolved `WorldRoadNetwork` geometry/width/clearance.
- [x] `TopDownWorldReservationAdapter` publishes source-backed node envelopes, canonical road claims and explicit settlement-arrival handoffs.
- [x] `TopDownWorldVoxelCatalogue.Build` already solves the canonical `TopDownWorldRoadNetwork` once and reuses it for road voxelization.
- [x] Call `TopDownWorldReservationAdapter.ValidateRoadHandoffs` on that exact solved network before rasterization; do not solve roads twice.
- [ ] Add/adjust focused production macro-road handoff regression.

## Production architecture integration

- [x] Real production path is `KentridgeCombinedVoxelCatalogueCanonical` -> `KentridgeSharedStructureVoxelCatalogue`; comparison grammar is not production authority.
- [x] Build one canonical Kentridge reservation source snapshot per combined-catalogue build and thread it with the already-resolved `SettlementPlan` into shared-structure generation.
- [x] Validate every production structure `StructureSiteGeometry` clearance against other owners using a bounded role-local view that excludes only its matching host plot owner.
- [x] Preserve architecture ownership of form/orientation/support/piece selection.
- [x] Add focused regression proving host-owner overlap is allowed and an incompatible external structure claim is rejected (`SpatialReservationProductionIntegrationTests.KentridgeProductionStructureQueryAllowsHostButRejectsExternalOwner`).

## Vegetation / hidden-space consumers

- [x] Grouped Kentridge tree+boulder planning consumes one shared snapshot and yields/suppresses against authoritative claims while ecology keeps species/density authority.
- [x] Decorative ground plants/moss/vines are non-authoritative surface dressing; do not add parallel occupancy authority.
- [x] Hidden-space batch planning consumes real 3D realization claims and a caller snapshot; vertical-only separation succeeds and true XYZ collision fails.
- [x] Explicit connector compatibility cannot be reused by unrelated underground consumers.
- [ ] Ensure affected vegetation/hidden-space regressions are green on final exact SHA.

## Reusability review

- [ ] Audit `SpatialReservations.cs` and generic adapters for Kentridge role names, settlement IDs, road names, vegetation species, hidden-space identities, or gallery-only assumptions; generic core may depend only on reservation geometry, ownership, categories, precedence, masks, and deterministic policy.
- [ ] Keep conflict/yield behavior data-driven through generic reservation categories/masks/precedence rather than hard-coded consumer pairs such as road-vs-tree or structure-vs-hidden-space.
- [ ] Add a non-Kentridge fixture/regression that exercises the same reservation source/snapshot/query/conflict APIs with at least two unrelated consumer categories and 3D vertical separation.
- [ ] Keep `WorldbuildingGalleryReservationInspection` presentation-only; visualization/debug inspection must never become reservation authority or alter conflict results.

## Determinism / lifecycle / cost

- [x] Stable ids, insertion-order independence, equal-precedence stable-id tie, hard/clearance/soft/handoff outcomes.
- [x] Replay/release ownership regression and bounded-window query-work regression authored.
- [ ] Add macro-road production regression; architecture shared-source/host-filter regression is already covered by `SpatialReservationProductionIntegrationTests`.
- [ ] Record representative snapshot/source construction and query metrics after integration.
- [ ] Check repository-supported allocation/memory evidence where available.
- [ ] Verify generation/device budgets and unrelated world-generation behavior did not move.

## Gallery / runtime evidence

- [ ] Trace `WorldbuildingGalleryReservationInspection` from the actual audit/showcase runtime path and prove presentation-only behavior.
- [ ] Add a presentation-only runtime/gallery renderer for the inspection primitives; the existing report builder alone does not satisfy the visualization acceptance criterion.
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
- [ ] Complete every open-phase checkbox, then move open -> pending with required testing metadata (folder was moved early by prior work; metadata remains intentionally incomplete until gates pass).
- [ ] Fetch/merge current master and rerun affected focused gates.
- [ ] Use `ci-test/fixes/agent-7` only for the final targeted-CI request; verify no request is already queued first.
- [ ] Obtain green exact-SHA CI and record request/run/tested-SHA evidence.
- [ ] Complete pending metadata, then move pending -> closed with `status=fixed` and `resolvedUtc` only when every acceptance criterion is complete.
- [ ] Merge current master again if required; revalidate any changed tree.
- [ ] Push the exact validated feature head to `origin/master` non-force; if master advances, fetch/merge/revalidate/retry.
