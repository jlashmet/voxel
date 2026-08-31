# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, structural compatibility/topology, ecology policy, hidden-space topology, or presentation ownership. Close only after production consumers, focused regressions, module-local built-player evidence, Kentridge integration, cost/blast-radius checks, and exact-SHA CI are all green.

## Current state — 2026-08-31

Authoritative `origin/master` was re-fetched at `2ea5f5c95f89fbf0403dbefb50b782829583d304`. Agent-7 reconciled onto that tree with two-parent merge `8dc03acbd5e8359034a815021eb03b43b69020bf`; `master...fixes/agent-7` was 0 behind at the last reconciliation check. The reconciliation deliberately dropped agent-7's obsolete Worldbuilding Gallery validation diffs while preserving assignment-owned production work and the module-local validation assets.

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

The focused regression `SpatialReservationStructuralSocketIntegrationTests.AcceptedTypedSocketUsesSharedReservationClearanceAgainstExternalWorldClaims` begins with a real `StructuralCompositionPlanner.ExpandRoot` typed-socket solve, consumes its accepted decision through the production adapter, rejects an intersecting external building through the shared reservation path, and accepts a vertically separated building. Targeted run `33360994835` proved this regression green before the automatic module suite reached the next failure.

### Solved-road production reservation boundary

Targeted run `33360994835` then failed `SpatialReservationProductionIntegrationTests.CanonicalKentridgeStructureSitesPassSharedReservationValidation`: role 15 intersected raw planner claim `kentridge-route:organic-access-2:0`. Root-cause tracing showed a production-source mismatch rather than a shared conflict-policy defect: organic Kentridge physically lowers `KentridgeWorldRoadNetwork` through `WorldRoadNetworkVoxelCatalogue`, while structure validation was still consuming `KentridgeTownPlanner`'s pre-road route geometry.

The correction keeps road solving authoritative and composition-owned. Canonical Kentridge now solves the organic `WorldRoadNetwork` once, derives structure reservations from that exact solved network through `KentridgeSpatialReservationAdapter`, and reuses the same network for road voxelization. The standalone shared-structure entry point likewise derives reservations from solved roads. The production regression now validates canonical structure sites against those solved production road claims. Shared conflict semantics are unchanged; roads are not ignored and the town planner is not given a second routing policy.

Exact-SHA run `33362347013` on source `91b5fba348af4d9c464e8131b47c18b62fdbc2a0` did not reach the behavioral assertion: Unity compile failed because the two production files newly naming `WorldRoadNetwork` omitted the existing `Game.WorldBuilder.Api` namespace import. This is a compile-boundary defect introduced by the solved-road composition fix, not a second occurrence of the role-15 conflict. The narrow correction adds only those imports; no reservation semantics or routing policy changed.

### Module-local validation architecture

Focused player-visible validation for this feature does **not** use `Assets/Scenes/WorldbuildingGalleryShowcase.unity`.

Agent-7 owns:
- scene: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/SpatialReservationValidation.unity`;
- scenario: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/spatial-reservations.player-scenario.json`;
- module metadata: `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/spatial-reservations.module-validation.json`;
- composition: `Assets/Game/Composition/Showcase/SceneRuntime/SpatialReservationValidationShowcase.cs`.

The local scene consumes production Kentridge reservation and hidden-space computations, visibly distinguishes hard occupancy / clearance / road / public access / underground claims plus a deliberate rejected overlap, removes primitive colliders, and emits `SPATIAL_RESERVATION_COST` and `SPATIAL_RESERVATION_VALIDATION ready:`. The Worldbuilding Gallery is non-gating and must not be used as focused acceptance evidence.

## Validation hypotheses / discriminator

1. **Likely:** with the missing API imports corrected, solved production-road reservations remove the raw planner-route false conflict while preserving real road/structure exclusions; the four focused reservation suites then proceed to the module-local player gate.
2. **Alternative:** after compile succeeds, the solved road itself still intersects canonical structure geometry, proving a real road/structure layout defect rather than a source-selection mismatch.

Discriminator: run the exact feature SHA through the standard transport. If `CanonicalKentridgeStructureSitesPassSharedReservationValidation` fails behaviorally again, inspect the solved claim id/bounds and isolate a minimal repro/root cause before another layout or policy change. Do not weaken conflict semantics to make the test pass.

## Remaining gates

1. Re-fetch `origin/master`; merge if it advanced and re-review the assignment-only diff.
2. Verify `ci-test/fixes/agent-7` has no queued/running request, then use only that transport for exact-SHA validation.
3. Run focused reservation tests including the typed-socket integration plus affected Kentridge/vegetation/hidden-space/foundation regressions and repository compile/static/ProjectValidator gates.
4. Build/run the exact module-local `SpatialReservationValidation.unity` standalone player through the generic module-validation path and directly inspect required surface, underground, rejection, readiness, and cost evidence. Do not use Worldbuilding Gallery.
5. Run the real Kentridge built/runtime traversal integration gate.
6. Record final query/build/memory/streaming evidence and final assignment-only blast radius.
7. After every acceptance gate is green, complete issue metadata, move `open/` directly to `closed/`, merge current master if it advanced, revalidate affected work, and non-force promote the exact feature head.

## Cost / ownership guardrails

No global registry, Unity Physics authority, per-claim authoritative GameObjects/colliders, duplicate road solver, duplicated structural socket solver, or duplicated ecology/hidden-space policy. Keep one shared source snapshot with bounded views and preserve existing deterministic/device budgets. Module-local validation objects are presentation-only and must not become placement authority.
