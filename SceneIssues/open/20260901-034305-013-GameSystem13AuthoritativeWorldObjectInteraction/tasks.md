# 13 Authoritative world-object interaction — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.WorldObjects.Api` / `Game.WorldObjects.Runtime`
**Execution rule:** WorldObjects owns authoritative interactive object identity/state/behavior dispatch. Input/UI only express intent; Loot/Progression react through semantic adapters.

## Baseline / API

- [ ] **T13-001 — Inventory current world-object behaviors.** Find existing authoritative WorldBuilder/world-object state, interactable MonoBehaviours, raycast/E-key handlers, doors/containers/secrets/NPC interaction bridges and duplicate ids.
- [ ] **T13-002 — Establish asmdefs and migration boundary.** Generalize existing authoritative behavior instead of creating parallel state; API depends on Characters.Api for actor identity but no scene types.
- [ ] **T13-003 — Define stable `WorldObjectId`.** Specify binding/serialization semantics for realized objects and migration from existing ids.
- [ ] **T13-004 — Define semantic object state snapshot.** Expose only reusable current behavior/capability state needed by consumers/replication/persistence.
- [ ] **T13-005 — Define interaction intent/context.** CharacterId + WorldObjectId + semantic action/context; no Camera/RaycastHit/GameObject in API.
- [ ] **T13-006 — Define interaction result/fact and rejection reasons.** Distinguish invalid actor, unavailable capability, reach/context failure and state conflict.
- [ ] **T13-007 — Define behavior capability/handler seam.** Runtime dispatches to object behavior without generic WorldObjects knowing Loot/Quest-specific state.

## Runtime / migration

- [ ] **T13-010 — Implement authoritative object registry/binding.** Bind realized world objects to stable ids/state/behavior and reject duplicate bindings.
- [ ] **T13-011 — Centralize interaction validation.** Validate actor identity, current object state, semantic capability and demonstrated spatial/reach context using owning APIs.
- [ ] **T13-012 — Dispatch behavior deterministically.** Apply state transitions exactly once and return authoritative result; repeated requests follow explicit idempotency/conflict rules.
- [ ] **T13-013 — Emit semantic state-change/interaction facts.** Downstream adapters consume facts rather than reaching into behavior Runtime.
- [ ] **T13-014 — Add Loot adapter seam.** System 10 can coordinate pickup/container behavior without WorldObjects directly mutating Inventory.
- [ ] **T13-015 — Add Progression/Story interaction adapter seam.** Semantic facts can become observations/events through composition without WorldObjects knowing quest/story rules.
- [ ] **T13-016 — Add capture/restore and replication projection seams.** Restore current object truth without replaying prior interaction one-shots.
- [ ] **T13-017 — Migrate raw interaction input.** Unity/Input layer selects candidate and sends semantic request; remove `E`/raycast-to-behavior direct authority paths.

## Verification

- [ ] **T13-020 — Two-behavior reuse test.** Exercise two distinct object behaviors through one registry/interaction pipeline.
- [ ] **T13-021 — Validation rejection tests.** Invalid CharacterId, object, range/context, capability and stale state all reject without mutation.
- [ ] **T13-022 — Repeated interaction/state conflict tests.** No duplicate effects from repeated/late requests.
- [ ] **T13-023 — Snapshot/restore test.** Current interactive state round-trips with stable WorldObjectId and no replayed effects.
- [ ] **T13-024 — Independent non-Kentridge fixture.** Prove semantic interaction outside the primary composition.
- [ ] **T13-025 — Run automatic WorldObjects and dependent Loot/Progression tests.**

## Cleanup / close

- [ ] **T13-030 — Remove scene-object authoritative identity and direct behavior shortcuts.** Repository-wide search for interaction handlers bypassing WorldObjects.
- [ ] **T13-031 — Boundary audit.** No UI prompt ownership, Inventory mutation, quest policy or named scene/object policy in generic module; no external Runtime dependency.
- [ ] **T13-032 — Close with single-owner proof.** Every production world-object interaction reaches the same authoritative registry/behavior pipeline.
