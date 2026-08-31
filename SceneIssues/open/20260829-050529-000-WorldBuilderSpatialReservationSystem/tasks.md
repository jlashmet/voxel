# Tasks — WorldBuilder Spatial Reservation System

## Audit / ownership

- [x] Trace Kentridge town, canonical road, architecture, ecology/vegetation, hidden-space and validation ownership.
- [x] Confirm no competing canonical reservation service exists on reconciled master.
- [x] Keep road solving/grade in `WorldRoadNetwork`, socket compatibility/topology/orientation/support in `StructuralCompositionPlanner`, form policy in architecture, species/density in ecology, hidden-space topology in its planner, and rendering in presentation.
- [x] Reconcile authoritative `origin/master` `2ea5f5c95f89fbf0403dbefb50b782829583d304` into `fixes/agent-7` with two-parent merge `8dc03acbd5e8359034a815021eb03b43b69020bf`; post-merge compare is 0 behind master.
- [x] Deliberately drop agent-7's obsolete Worldbuilding Gallery validation diffs during reconciliation; keep the dedicated module-local validation scene as the focused surface.
- [x] Reconcile the obsolete `pending/` state to the current authoritative `open/` workflow without changing acceptance.

## Canonical reservation contract

- [x] Stable semantic id/owner/provenance/category/precedence and stable-id tie break.
- [x] Typed consumer/category masks; hard/clearance/protected/handoff/soft semantics.
- [x] Integer-decimetre half-open 3D box/corridor geometry and true vertical separation.
- [x] Engine-free authority; deterministic diagnostics and bounded query metrics.
- [x] Immutable bounded snapshots; deterministic independent resolution.
- [x] Planner-local idempotent replay/replacement/release and resolved local+global snapshot behavior.
- [x] Exact half-open touching/equality regression coverage (`SpatialReservationProductionIntegrationTests.HalfOpenReservationBoundsAllowExactFaceTouching`).

## Production road / macro integration

- [x] Canonical `WorldRoadReservationAdapter` consumes already-resolved `WorldRoadNetwork` geometry/width/clearance.
- [x] `TopDownWorldReservationAdapter` publishes source-backed node envelopes, canonical road claims and explicit settlement-arrival handoffs.
- [x] `TopDownWorldVoxelCatalogue.Build` solves the canonical `TopDownWorldRoadNetwork` once and reuses it for road voxelization.
- [x] Validate road handoffs on that exact solved network before rasterization; do not solve roads twice.
- [x] Focused production macro-road handoff regression (`SpatialReservationProductionIntegrationTests.MacroRoadHandoffKeepsLoweredCorridorInBothRegionBuckets`).

## Production architecture / typed structural composition

- [x] Real Kentridge production structure path remains `KentridgeCombinedVoxelCatalogueCanonical` -> `KentridgeSharedStructureVoxelCatalogue`; comparison grammar is not production authority.
- [x] Build one canonical Kentridge reservation source snapshot per combined-catalogue build and thread it with the resolved `SettlementPlan` into shared-structure generation.
- [x] Validate every production structure `StructureSiteGeometry` clearance against other owners using a bounded role-local view that excludes only its matching host plot owner.
- [x] Preserve architecture ownership of form/orientation/support/piece selection and master's per-program foundation-depth placement.
- [x] Host-owner/external-conflict regression (`SpatialReservationProductionIntegrationTests.KentridgeProductionStructureQueryAllowsHostButRejectsExternalOwner`).
- [x] Typed structural-socket prerequisite is landed on authoritative master; consume its canonical `SlotSpec` / `StructuralAttachmentDecision` / `StructuralCompositionPlanner` contracts rather than implementing a second socket solver.
- [x] Add production `StructuralSocketReservationAdapter` that derives external WorldBuilder clearance only from an **accepted solved attachment**, matches the planner's cardinal transform, converts voxel half-open bounds conservatively to integer decimetres, and queries the shared `SpatialReservationSnapshot` as `StructuralChild`.
- [x] Add production-computation regression `SpatialReservationStructuralSocketIntegrationTests.AcceptedTypedSocketUsesSharedReservationClearanceAgainstExternalWorldClaims`: run real `StructuralCompositionPlanner.ExpandRoot`, consume its accepted decision, reject an intersecting external building, and allow true vertical separation.
- [ ] Confirm typed-socket integration regression is green on the final exact SHA before marking acceptance criterion (7) validated.

## Vegetation / hidden-space consumers

- [x] Grouped Kentridge tree+boulder planning consumes one shared snapshot and yields/suppresses against authoritative claims while ecology keeps species/density authority.
- [x] Decorative ground plants/moss/vines are non-authoritative surface dressing; do not add parallel occupancy authority.
- [x] Hidden-space batch planning consumes real 3D realization claims and a caller snapshot; vertical-only separation succeeds and true XYZ collision fails.
- [x] Explicit connector compatibility cannot be reused by unrelated underground consumers.
- [ ] Ensure affected vegetation/hidden-space regressions are green on final exact SHA.

## Reusability review

- [x] Generic reservation core/adapters contain no Kentridge/place/material/Gallery policy; core depends only on semantic geometry, ownership, categories, precedence, masks, and deterministic policy.
- [x] Keep conflict/yield behavior data-driven rather than hard-coded consumer pairs.
- [x] Move road-clearance -> vegetation yield choice from shared adapter into explicit composition/configuration while preserving production behavior.
- [x] Independent non-Kentridge reuse regression (`SpatialReservationReusabilityTests.ClearanceYieldPolicyAndVerticalSeparationAreConsumerConfigured`).
- [x] Typed-socket adapter accepts generic solved socket data and explicit scale/category inputs; no Kentridge/Gallery policy is embedded in the shared seam.

## Determinism / lifecycle / cost

- [x] Stable ids, insertion-order independence, equal-precedence stable-id tie, hard/clearance/soft/handoff outcomes.
- [x] Replay/release ownership regression and bounded-window query-work regression authored.
- [x] Macro-road production regression and architecture shared-source/host-filter regression authored.
- [ ] Record representative snapshot/source construction and query metrics from the final module-local built-player run; scene emits `SPATIAL_RESERVATION_COST` with build ticks, bounded query work, and Unity allocated/reserved memory.
- [x] No global/device/region budget files or CharacterMotor/world-generation tolerances changed; re-check final diff after exact-SHA reconciliation.

## Module-local runtime evidence

- [x] `WorldbuildingGalleryShowcase` is non-gating for this assignment and must not be used as the focused validation surface.
- [x] Add module-local validation scene `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/SpatialReservationValidation.unity` with dedicated runtime composition `SpatialReservationValidationShowcase`.
- [x] Add generic module-validation metadata `spatial-reservations.module-validation.json` and separate built-player scenario `spatial-reservations.player-scenario.json`; no feature-specific logic is added to the shared harness.
- [x] Local scene consumes deterministic production Kentridge reservation data and visibly distinguishes hard occupancy, clearance, road, public access, underground, and a deliberate rejected overlap; it owns no placement authority or colliders.
- [x] Underground evidence uses production `KentridgeHiddenSpacePlanner` + `WorldBuilderReservationFactory.HiddenSpaceVolume`; do not fall back to Worldbuilding Gallery.
- [ ] Run the exact built module-local `SpatialReservationValidation.unity` player and directly inspect all required surface/underground/rejection captures.
- [ ] Run real `KentridgePlayableSlice` built/runtime traversal check as the integration/regression gate, not as the module's focused validation scene.

## Validation / closure

Validation history: exact-SHA run `33362347013` on source `91b5fba348af4d9c464e8131b47c18b62fdbc2a0` stopped at Unity compile because the solved-road composition fix omitted `Game.WorldBuilder.Api` imports in the two files naming `WorldRoadNetwork`. The narrow import fix is applied; behavioral/module/player gates remain unchecked until rerun.

- [x] Follow current `SceneIssues/feature-readme.md`, common `SceneIssues/README.md`, and `AGENTS.md`; unfinished work remains in `open/`.
- [x] Never edit `.github/test-request.json` on `fixes/agent-7`, create extra transports, or replace queued/running CI.
- [ ] Re-fetch latest `origin/master`; merge if advanced and re-review assignment-only diff before exact-SHA validation.
- [ ] Run focused `SpatialReservationTests`, `SpatialReservationProductionIntegrationTests`, `SpatialReservationReusabilityTests`, `SpatialReservationStructuralSocketIntegrationTests`, affected Kentridge/vegetation/hidden-space/foundation regressions, and repository compile/static/ProjectValidator gates on the reconciled exact SHA.
- [ ] Run required module-local scene/runtime/built-player/visual gates plus Kentridge integration and capture durable evidence.
- [ ] Review final assignment-only blast radius and record commands/results/cost/acceptance mapping in issue metadata.
- [ ] Complete every required acceptance checkbox; keep the assignment in `open/` until gates pass.
- [ ] Verify `ci-test/fixes/agent-7` is idle, then use it only for the final exact-SHA targeted request; `.github/test-request.json` stays only on that transport.
- [ ] Obtain green exact-SHA CI and record request/run/tested-SHA evidence.
- [ ] Complete metadata, then move `open/` -> `closed/` with `status=fixed` and `resolvedUtc` only when every acceptance criterion is complete.
- [ ] Merge current master again if required and revalidate any changed tree.
- [ ] Push the exact validated feature head to `origin/master` non-force; if master advances, fetch/merge/revalidate/retry.
