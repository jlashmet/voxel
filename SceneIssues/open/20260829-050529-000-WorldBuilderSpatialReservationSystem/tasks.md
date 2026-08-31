# Tasks — WorldBuilder Spatial Reservation System

## Audit / ownership

- [x] Trace Kentridge town, canonical road, architecture, ecology/vegetation, hidden-space and validation ownership.
- [x] Confirm no competing canonical reservation service exists on reconciled master.
- [x] Keep road solving/grade in `WorldRoadNetwork`, typed socket compatibility/topology/orientation/support in `StructuralCompositionPlanner`, form policy in architecture, species/density in ecology, hidden-space topology in its planner, and rendering in presentation.
- [x] Reconcile authoritative `origin/master` and remain 0 behind through final product validation.
- [x] Use the dedicated module-local validation scene rather than Worldbuilding Gallery for focused evidence.

## Canonical reservation contract

- [x] Stable semantic id/owner/provenance/category/precedence and stable-id tie break.
- [x] Typed consumer/category masks; hard/clearance/protected/handoff/soft semantics.
- [x] Integer-decimetre half-open 3D box/corridor geometry and true vertical separation.
- [x] Engine-free authority; deterministic diagnostics and bounded query metrics.
- [x] Immutable bounded snapshots; deterministic independent resolution.
- [x] Planner-local idempotent replay/replacement/release and resolved local+global snapshot behavior.
- [x] Exact half-open touching/equality regression coverage.

## Production road / macro integration

- [x] Canonical road adapter consumes already-resolved `WorldRoadNetwork` geometry/width/clearance.
- [x] Macro adapter publishes source-backed envelopes, solved road claims, and explicit settlement-arrival handoffs.
- [x] Top-down and Kentridge composition solve road networks once and reuse those exact networks for reservation validation and voxelization.
- [x] Kentridge adapter reuses the supplied resolved `SettlementPlan` instead of rebuilding settlement reservations from seed.
- [x] Macro-road handoff and Kentridge solved-road production regressions pass on final exact SHA.

## Architecture / structural composition

- [x] Production Kentridge structure path remains `KentridgeCombinedVoxelCatalogueCanonical` -> `KentridgeSharedStructureVoxelCatalogue`.
- [x] Production structures validate bounded role-local clearance against the shared source while excluding only their matching host owner.
- [x] Preserve architecture ownership of form/orientation/support/piece selection and per-program foundation placement.
- [x] Typed structural-socket integration consumes canonical solved socket decisions rather than duplicating socket policy.
- [x] `SpatialReservationStructuralSocketIntegrationTests` is green on final exact SHA; acceptance criterion (7) validated.
- [x] Existing affected Kentridge plot/foundation PlayMode regression passes on final exact SHA.

## Vegetation / hidden-space / reuse

- [x] Kentridge vegetation planning consumes a shared snapshot and yields/suppresses while ecology keeps species/density authority.
- [x] Hidden-space batch planning consumes true 3D realization claims; vertical-only separation succeeds, true collision fails, and compatibility cannot leak to unrelated underground consumers.
- [x] Independent non-Kentridge reuse fixture proves configured vegetation yielding versus landmark rejection plus vertical separation.
- [x] `SpatialReservationTests` and `SpatialReservationReusabilityTests` are green on final exact SHA; affected vegetation/hidden-space acceptance validated.

## Determinism / lifecycle / cost

- [x] Stable ids, insertion-order independence, equal-precedence tie, hard/clearance/soft/handoff outcomes are covered and green.
- [x] Replay/release ownership and bounded-window query-work regressions are green.
- [x] Final module-local player emitted `SPATIAL_RESERVATION_COST`: 81 claims, 4 query buckets, 14 broad-phase candidates/tests, 65,560,368 allocated bytes, 155,123,712 reserved bytes.
- [x] No global/device/region budget, CharacterMotor, or world-generation tolerance file is changed in the final assignment diff.

## Module-local runtime evidence

- [x] Dedicated `SpatialReservationValidation.unity`, scenario, module metadata, and presentation-only runtime composition exist.
- [x] Scene consumes production Kentridge reservation/hidden-space computation and owns no placement authority or colliders.
- [x] Final run `33366247235` built and ran the exact module-local scene successfully.
- [x] Directly inspect newest captures: white hard, cyan clearance, yellow road, green access, red rejection, and magenta underground-below-surface evidence are all clearly visible.
- [x] Final run built and ran real `KentridgePlayableSlice`; direct survey capture review is coherent/traversable-looking with roads, buildings, plaza, terrain, and traversal overlay.
- [x] No NullReferenceException, MissingReferenceException, or shader-error marker in final player evidence.

## Validation / closure

- [x] Follow `AGENTS.md`, `SceneIssues/feature-readme.md`, and common `SceneIssues/README.md`; keep work open until gates pass.
- [x] Keep `.github/test-request.json` only on `ci-test/fixes/agent-7`; never replace queued/running CI or create another transport.
- [x] Final product SHA `a29fc6cb95f0c5f576105f8e88829ba55cbff5e2` validated by run `33366247235` (`success`).
- [x] Requested affected Kentridge plot/foundation PlayMode regression passes on that exact source.
- [x] Automatic module plan/static check and all four reservation suites pass on that exact source.
- [x] Repository/workflow audit found no separate current `ProjectValidator` target; Unity compile/build plus the module-plan static test are the concrete current gates and are green.
- [x] Required module-local built-player/visual gate and canonical Kentridge built-player integration gate are green with durable evidence.
- [x] Final assignment compare against `origin/master` `2ea5f5c95f89fbf0403dbefb50b782829583d304` is 0 behind and assignment-scoped.
- [x] Blast radius, cost, acceptance mapping, exact-SHA CI evidence, and prior blockers/root causes are recorded in `plan.md` / issue metadata.
- [x] Every acceptance criterion is validated; closure bookkeeping may proceed.
- [x] Verify transport is idle after final run completion.
- [x] Obtain green exact-SHA CI and record request/run/tested-SHA evidence.
- [ ] Set issue metadata to fixed/resolved and move this directory directly `open/` -> `closed/`.
- [ ] Re-fetch current `origin/master`; merge only if advanced, and revalidate any changed product tree.
- [ ] Push the final feature head to `origin/master` non-force; if master advances, fetch/merge/revalidate/retry.
