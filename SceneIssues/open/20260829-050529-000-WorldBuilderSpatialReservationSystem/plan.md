# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, structural compatibility/topology, ecology policy, hidden-space topology, or presentation ownership. Close only after production consumers, focused regressions, module-local built-player evidence, Kentridge integration, cost/blast-radius checks, and exact-SHA CI are all green.

## Current state — 2026-08-31

Authoritative `origin/master` was re-fetched at `2ea5f5c95f89fbf0403dbefb50b782829583d304`. Agent-7 reconciled onto that tree with two-parent merge `8dc03acbd5e8359034a815021eb03b43b69020bf`; `master...fixes/agent-7` is now 0 behind. The reconciliation deliberately dropped agent-7's obsolete Worldbuilding Gallery validation diffs while preserving assignment-owned production work and the module-local validation assets.

Implemented acceptance seams:
- engine-free deterministic 3D reservations, bounded snapshots, diagnostics and precedence;
- Kentridge settlement, macro-road/handoff, production structure, vegetation and hidden-space consumers;
- composition-configured road-clearance yielding plus an independent non-Kentridge reuse fixture;
- focused deterministic and production-integration regressions;
- module-local presentation-only reservation validation with no placement authority.

### Typed structural socket integration

The prerequisite typed structural socket feature is now landed on authoritative master. Agent-7 consumes its canonical `SlotSpec`, `StructuralAttachmentDecision`, and `StructuralCompositionPlanner` contracts; it does not copy compatibility, orientation selection, topology, support, capacity, recursion, or sibling-clearance solving.

`StructuralSocketReservationAdapter` is the WorldBuilder boundary for external spatial claims. Given an accepted production attachment decision, its matching typed socket, the solved parent orientation, and the explicit voxels-per-decimetre conversion, it:
- reproduces only the planner's cardinal vector transform for the socket-authored clearance volume;
- derives the resolved half-open world clearance at the production attachment point;
- conservatively converts voxel min/max into integer-decimetre reservation bounds using floor-min / ceil-max conversion, including negative coordinates;
- publishes a normal `StructuralChild` clearance claim and queries the existing `SpatialReservationSnapshot` as `ReservationConsumerKind.StructuralChild`.

The focused regression `SpatialReservationStructuralSocketIntegrationTests.AcceptedTypedSocketUsesSharedReservationClearanceAgainstExternalWorldClaims` begins with a real `StructuralCompositionPlanner.ExpandRoot` typed-socket solve, consumes its accepted decision through the production adapter, rejects an intersecting external building through the shared reservation path, and accepts a vertically separated building. This is the criterion-(7) seam; structural composition remains authoritative for its own graph and socket semantics.

### Module-local validation architecture

Focused player-visible validation for this feature does **not** use `Assets/Scenes/WorldbuildingGalleryShowcase.unity`.

Agent-7 owns:
- scene: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/SpatialReservationValidation.unity`;
- scenario: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/spatial-reservations.player-scenario.json`;
- module metadata: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/spatial-reservations.module-validation.json`;
- composition: `Assets/Game/Composition/Showcase/SceneRuntime/SpatialReservationValidationShowcase.cs`.

The local scene consumes production Kentridge reservation and hidden-space computations, visibly distinguishes hard occupancy / clearance / road / public access / underground claims plus a deliberate rejected overlap, removes primitive colliders, and emits `SPATIAL_RESERVATION_COST` and `SPATIAL_RESERVATION_VALIDATION ready:`. The Worldbuilding Gallery is non-gating and must not be used as focused acceptance evidence.

## Validation hypotheses / discriminator

1. **Likely:** the merged typed-socket seam compiles and its production-computation regression passes together with existing reservation/Kentridge/vegetation/hidden-space coverage.
2. **Alternative:** exact voxel-to-decimetre bounds or assembly dependencies expose a compile/bounds defect after reconciliation.

Discriminator: run the narrow structural-socket reservation regression together with existing focused reservation tests on the exact final source SHA. If it fails, inspect the specific compile/assertion evidence and fix only the demonstrated cause. If the same acceptance symptom survives two materially different fixes, isolate a minimal repro/root cause before another change.

## Remaining gates

1. Re-fetch `origin/master`; merge if it advanced and re-review the assignment-only diff.
2. Verify `ci-test/fixes/agent-7` has no queued/running request, then use only that transport for the final exact-SHA validation.
3. Run focused reservation tests including the typed-socket integration plus affected Kentridge/vegetation/hidden-space/foundation regressions and repository compile/static/ProjectValidator gates.
4. Build/run the exact module-local `SpatialReservationValidation.unity` standalone player through the generic module-validation path and directly inspect required surface, underground, rejection, readiness, and cost evidence. Do not use Worldbuilding Gallery.
5. Run the real Kentridge built/runtime traversal integration gate.
6. Record final query/build/memory/streaming evidence and final assignment-only blast radius.
7. After every acceptance gate is green, complete issue metadata, move `open/` directly to `closed/`, merge current master if it advanced, revalidate affected work, and non-force promote the exact feature head.

## Cost / ownership guardrails

No global registry, Unity Physics authority, per-claim authoritative GameObjects/colliders, duplicate road solver, duplicated structural socket solver, or duplicated ecology/hidden-space policy. Keep one shared source snapshot with bounded views and preserve existing deterministic/device budgets. Module-local validation objects are presentation-only and must not become placement authority.
