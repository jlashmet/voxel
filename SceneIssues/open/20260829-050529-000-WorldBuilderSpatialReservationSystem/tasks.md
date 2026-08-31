# Tasks — WorldBuilder Spatial Reservation System

## Audit / ownership

- [x] Trace Kentridge town, canonical road, architecture, ecology/vegetation, hidden-space and gallery ownership.
- [x] Confirm no competing canonical reservation service exists on reconciled master.
- [x] Keep road solving/grade in `WorldRoadNetwork`, form/orientation/support in architecture, species/density in ecology, hidden-space topology in its planner, and rendering in presentation.
- [ ] Acceptance blocker: criterion (7) requires one production typed-`StructuralSocket` consumer of the shared reservation clearance/overlap path. Re-checked 2026-08-30 16:59 PDT: prerequisite feature `20260829-034505-000-WorldBuilderTypedStructuralSocketComposition` remains `open` on unchanged current `origin/master` (`ebdc2e4f63ef73153cd4e0ff5c62efe604f35470`) and its execution checklist is still entirely unchecked, so no canonical production typed-socket seam exists on this branch to integrate without taking another assignment's scope. Do not weaken or mark this acceptance complete; re-check after that prerequisite lands.
- [x] Reconcile current master into `fixes/agent-7` before implementation (`d256dc2044c88b254751448012b60a138e716f27`, current master `65e33762a0d0f1739e9a518484d119e551f01f81`); resumed again and merged `7f935b26cc7aa8aff971d3488e9f9629108e419a` via `a2639073b1cb61e5fc050d3254d232c116d054e5`, `5f07db5cd7677e84f617deb61c5b03a4b896159c` via `23d16dc51e49f17adf4c9bcedc9306c22e264bd1`, and current master `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` via two-parent merge `46b4e473ab6497d836053a072f3fe7050156756b`.
- [x] Reconcile the obsolete `pending/` state to the current authoritative `open/` workflow without changing acceptance.
- [x] Reconcile plan/checklist to the actual post-merge production paths.

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
- [x] `TopDownWorldVoxelCatalogue.Build` solves the canonical `TopDownWorldRoadNetwork` once and reuses it for road voxelization.
- [x] Validate road handoffs on that exact solved network before rasterization; do not solve roads twice.
- [x] Add focused production macro-road handoff regression (`SpatialReservationProductionIntegrationTests.MacroRoadHandoffKeepsLoweredCorridorInBothRegionBuckets`).

## Production architecture integration

- [x] Real production path is `KentridgeCombinedVoxelCatalogueCanonical` -> `KentridgeSharedStructureVoxelCatalogue`; comparison grammar is not production authority.
- [x] Build one canonical Kentridge reservation source snapshot per combined-catalogue build and thread it with the resolved `SettlementPlan` into shared-structure generation.
- [x] Validate every production structure `StructureSiteGeometry` clearance against other owners using a bounded role-local view that excludes only its matching host plot owner.
- [x] Preserve architecture ownership of form/orientation/support/piece selection.
- [x] Preserve master's per-program foundation-depth placement while retaining reservation validation/injected plan+snapshot support.
- [x] Add focused regression proving host-owner overlap is allowed and an incompatible external structure claim is rejected (`SpatialReservationProductionIntegrationTests.KentridgeProductionStructureQueryAllowsHostButRejectsExternalOwner`).

## Vegetation / hidden-space consumers

- [x] Grouped Kentridge tree+boulder planning consumes one shared snapshot and yields/suppresses against authoritative claims while ecology keeps species/density authority.
- [x] Decorative ground plants/moss/vines are non-authoritative surface dressing; do not add parallel occupancy authority.
- [x] Hidden-space batch planning consumes real 3D realization claims and a caller snapshot; vertical-only separation succeeds and true XYZ collision fails.
- [x] Explicit connector compatibility cannot be reused by unrelated underground consumers.
- [ ] Ensure affected vegetation/hidden-space regressions are green on final exact SHA. Blocked until the final exact-SHA targeted request is appropriate; continue independent gates first so the only CI transport is not spent on a non-final tree.

## Reusability review

- [x] Audit generic reservation core/adapters for Kentridge/place/material/gallery assumptions; core depends only on semantic geometry, ownership, categories, precedence, masks, and deterministic policy.
- [x] Keep conflict/yield behavior data-driven rather than hard-coded consumer pairs.
- [x] Move road-clearance -> vegetation yield choice from shared adapter into explicit composition/configuration while preserving production behavior.
- [x] Add independent non-Kentridge reuse regression (`SpatialReservationReusabilityTests.ClearanceYieldPolicyAndVerticalSeparationAreConsumerConfigured`).
- [x] Keep gallery reservation inspection presentation-only with no colliders or placement authority.

## Determinism / lifecycle / cost

- [x] Stable ids, insertion-order independence, equal-precedence stable-id tie, hard/clearance/soft/handoff outcomes.
- [x] Replay/release ownership regression and bounded-window query-work regression authored.
- [x] Add macro-road production regression; architecture shared-source/host-filter regression is covered by `SpatialReservationProductionIntegrationTests`.
- [ ] Record representative snapshot/source construction and query metrics from the module-local built-player validation run; the scene now emits `SPATIAL_RESERVATION_COST` with build ticks, bounded query work, and Unity allocated/reserved memory, but final values must come from the final exact-SHA run.
- [x] Check repository-supported allocation/memory evidence where available; final focused evidence now comes from the module-local scene rather than Worldbuilding Gallery.
- [x] Verify generation/device budgets and unrelated world-generation behavior did not move: `master...fixes/agent-7` changes no global/device/region budget files; reservation queries stay bounded and existing budget/tolerance constants are untouched.

## Module-local runtime evidence

- [x] Retire `WorldbuildingGalleryShowcase` as the focused validation surface for this assignment; existing gallery inspection code is non-gating presentation only.
- [x] Add module-local validation scene `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/SpatialReservationValidation.unity` with dedicated runtime composition `SpatialReservationValidationShowcase`.
- [x] Add agent-8-compatible declarative module metadata `spatial-reservations.module-validation.json` and separate built-player scenario `spatial-reservations.player-scenario.json`; no feature-specific logic is added to the shared harness.
- [x] Local scene consumes a deterministic production `KentridgeTownPlanner.BuildReservationSnapshot` and visibly distinguishes hard occupancy, clearance, road, public access, and a deliberate rejected overlap; it owns no placement authority or colliders.
- [x] Add required underground 3D evidence directly to the local scene through the production `KentridgeHiddenSpacePlanner` + `WorldBuilderReservationFactory.HiddenSpaceVolume` path; do not fall back to Worldbuilding Gallery.
- [ ] Run the exact built module-local `SpatialReservationValidation.unity` player and directly inspect all required surface/underground/rejection captures.
- [ ] Run real `KentridgePlayableSlice` built/runtime traversal check as the integration/regression gate, not as the module's focused validation scene.

## Validation / closure

- [x] Follow current `SceneIssues/feature-readme.md`, common `SceneIssues/README.md`, and `AGENTS.md`; unfinished work remains in `open/`.
- [x] Never edit `.github/test-request.json` on `fixes/agent-7`, create extra transports, or replace queued CI.
- [x] Fetch/merge current master through `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` into feature head via `46b4e473ab6497d836053a072f3fe7050156756b`; current master remained unchanged at the 2026-08-30 16:59 PDT blocker re-check.
- [ ] Run focused `SpatialReservationTests` + affected Kentridge/vegetation/hidden-space and incoming foundation-surface regressions plus repository compile/static/ProjectValidator gates on the reconciled exact SHA.
- [ ] Run required module-local scene/runtime/built-player/visual gates plus Kentridge integration and capture durable evidence.
- [ ] Review assignment-only blast radius and record commands/results/cost/acceptance mapping in issue metadata. Source-level blast-radius review remains limited to reservation core/adapters, intended Kentridge consumers, module-local validation evidence, focused tests, and this SceneIssue bookkeeping; final cost/acceptance metadata remains blocked on exact-SHA runtime evidence.
- [ ] Complete every required acceptance checkbox; keep the assignment in `open/` until gates pass.
- [ ] Use `ci-test/fixes/agent-7` only for the final targeted-CI request; re-verified idle/stale at `8cc6ff94dcbbca46b1c522d08752235b891b1851` at 2026-08-30 16:59 PDT; do not publish until the typed-socket prerequisite lands and the feature tree is final.
- [ ] Obtain green exact-SHA CI and record request/run/tested-SHA evidence.
- [ ] Complete metadata, then move `open/` -> `closed/` with `status=fixed` and `resolvedUtc` only when every acceptance criterion is complete.
- [ ] Merge current master again if required; revalidate any changed tree.
- [ ] Push the exact validated feature head to `origin/master` non-force; if master advances, fetch/merge/revalidate/retry.
