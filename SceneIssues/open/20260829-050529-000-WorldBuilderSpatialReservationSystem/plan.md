# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, architecture compatibility, ecology policy, hidden-space topology, or presentation ownership. Close only after production consumers, focused regressions, module-local built-player evidence, Kentridge integration, cost/blast-radius checks, and exact-SHA CI are all green.

## Current state — 2026-08-30

Current `origin/master` remains unchanged at `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470`. `fixes/agent-7` was fully reconciled with master before the latest validation-architecture work.

Implemented acceptance seams remain:
- engine-free deterministic 3D reservations, bounded snapshots, diagnostics and precedence;
- Kentridge settlement, macro-road/handoff, production structure, vegetation and hidden-space consumers;
- composition-configured road-clearance yielding plus an independent non-Kentridge reuse fixture;
- focused deterministic and production-integration regressions;
- presentation-only reservation inspection code with no placement authority.

### Module-local validation architecture

Per the module-validation architecture established on `fixes/agent-8`, focused player-visible validation for this feature no longer uses `Assets/Scenes/WorldbuildingGalleryShowcase.unity`.

Agent-7 now owns a dedicated module-local validation surface:
- scene: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/SpatialReservationValidation.unity`;
- scenario: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/spatial-reservations.player-scenario.json`;
- module metadata: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/spatial-reservations.module-validation.json`;
- composition: `Assets/Game/Composition/Showcase/SceneRuntime/SpatialReservationValidationShowcase.cs`.

The local scene directly consumes the production `KentridgeTownPlanner.BuildReservationSnapshot` computation, renders read-only hard occupancy / clearance / road / public-access claims plus a deliberate rejected overlap, removes primitive colliders, and emits `SPATIAL_RESERVATION_VALIDATION ready:` for built-player readiness. This keeps visual policy in composition while reservation authority remains engine-free. The Worldbuilding Gallery is now non-gating for this assignment and must not be used as focused acceptance evidence.

The declarative metadata follows agent-8's pattern: production-path ownership, focused test declaration, module-local standalone scene, and a separate player scenario. No feature-specific scene/scenario selection is added to a generic harness.

The `master...fixes/agent-7` blast-radius review previously changed no global/device/region budget files or CharacterMotor/world-generation tolerances. Re-review after this validation-only addition must confirm the new delta is limited to module-local validation assets/composition plus existing assignment-scoped production/test work.

Original acceptance criterion (7) additionally requires a production typed-`StructuralSocket` consumer. Re-checked against authoritative master: prerequisite `SceneIssues/open/20260829-034505-000-WorldBuilderTypedStructuralSocketComposition` remains open and its execution checklist remains entirely unchecked. This remains an external blocker, not grounds to narrow acceptance. Agent-7 will not import or implement another assignment's socket system; re-check and integrate the canonical production seam after that feature lands.

`ci-test/fixes/agent-7` remains reserved for the final targeted request. Do not advance it while the typed-socket production acceptance seam is unavailable and the local validation scene still needs final underground-evidence completion/direct built-player inspection.

## Validation hypotheses / discriminator

1. **Likely:** after the typed-socket prerequisite lands, the narrow production adapter can consume `SpatialReservationSnapshot.Query`/clearance semantics without moving socket compatibility/topology ownership; focused reservation/Kentridge/vegetation/hidden-space regressions remain green.
2. **Alternative:** the socket integration or master's foundation-depth changes expose an ownership/bounds defect in the production structure path or module-local scene evidence.

Discriminator: merge the prerequisite only from current `master`, add the smallest production socket-to-reservation seam plus regression if acceptance is not already satisfied by the landed implementation, then issue an exact-SHA request that lets module metadata drive focused tests + the module-local standalone player, with Kentridge kept as the integration/regression gate. Any product failure is fixed before another CI request; repeated identical acceptance failure twice requires a minimal repro/root-cause isolation.

## Remaining gates

1. Complete the module-local scene's required underground 3D evidence without using Worldbuilding Gallery.
2. Re-check `origin/master` for the typed-structural-socket prerequisite; integrate its canonical production clearance seam only after it lands on master.
3. Merge any newer master before final validation and re-review the assignment-only diff.
4. Verify `ci-test/fixes/agent-7` is idle, then use it for the final exact-SHA request covering focused reservation/Kentridge/vegetation/hidden-space/foundation regressions, the module-local built-player validation scene, and Kentridge integration as repository tooling permits.
5. Record final reservation query/cost evidence and directly inspect durable module-local captures plus Kentridge runtime evidence.
6. After every acceptance gate is green, complete metadata, move `open/` directly to `closed/`, merge current master if it advanced, revalidate affected work, and non-force promote the exact feature head.

## Cost / ownership guardrails

No global registry, Unity Physics authority, per-claim authoritative GameObjects/colliders, duplicate road solver, duplicated structural socket solver, or duplicated ecology/hidden-space policy. Keep one shared source snapshot with bounded views and preserve existing deterministic/device budgets. Module-local validation objects are presentation-only and must not become placement authority.
