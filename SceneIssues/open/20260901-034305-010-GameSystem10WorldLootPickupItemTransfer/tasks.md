# 10 World loot, pickup & item transfer — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Loot.Api` / `Game.Loot.Runtime`
**Execution rule:** WorldObjects validates interaction/context; Inventory owns quantities; Loot coordinates the cross-domain transaction and world-item lifecycle.

## API / state

- [x] **T10-001 — Inventory current pickup/drop/container paths.** Find scene pickup components, world-item state, direct inventory mutation, container behavior and race handling.
  - Discovery: no `Game.Loot` module or shared pickup/drop/container transfer path exists on the synced feature head. `Game.Inventory.Api.IInventoryRuntime` exposes `TryAddUnique`, `Add`, `Count`, and `Snapshot` only; it has no remove/transfer/capacity/transaction result contract. Kentridge quest rewards directly call `Inventory.TryAddUnique` in composition (`KentridgeWellQuestRewardRuntime`) and Kentridge well interaction/presentation uses scene-local proximity + `Input.GetKeyDown(KeyCode.E)`. The existing composition `WorldObjects` folder contains runtime bootstrap/composition only and no `Game.WorldObjects.Api` assembly. Existing showcase interaction remains an independent presentation fixture, not loot authority.
- [x] **T10-002 — Establish asmdefs.** Loot.Runtime depends on Inventory.Api, WorldObjects.Api and Characters.Api only; Loot.Api contains no prefab/Transform references.
  - `Game.Loot.Api` and `Game.Loot.Runtime` are engine-free asmdefs; Runtime references only Loot.Api plus the three required API assemblies. The API contract uses semantic ids/payloads only and has no UnityEngine, Transform, GameObject or prefab type.
- [ ] **T10-003 — Define stable loot/world-item identity.** Reuse WorldObjectId where appropriate and add only the semantic item/payload identity actually needed.
- [ ] **T10-004 — Define pickup/drop/container-transfer requests/results.** Include actor, source/destination and explicit failure reasons without duplicating Inventory transaction schema.
- [ ] **T10-005 — Define claim/current-state contract.** Represent enough authoritative state to serialize competing pickup attempts and project current availability.
- [ ] **T10-006 — Define committed transfer facts.** Emit semantic results after both world and inventory sides commit; downstream systems never infer success from presentation disappearance.

## Runtime

- [ ] **T10-010 — Bind lootable WorldObjects to Inventory payload.** Keep one authoritative payload mapping and reject stale/unknown bindings.
- [ ] **T10-011 — Validate actor/object interaction through WorldObjects.Api.** Do not replicate reach/permission checks inside Loot.
- [ ] **T10-012 — Execute pickup as coordinated transaction.** Claim/validate object, perform Inventory transaction, then update/remove world loot only on success.
- [ ] **T10-013 — Implement deterministic competing claims.** Exactly one actor can win a single-claim pickup; losers receive semantic rejection and no inventory change.
- [ ] **T10-014 — Implement container transfer.** Use the same Inventory transaction path for character/container sources and destinations.
- [ ] **T10-015 — Implement drop round-trip.** Remove from inventory transactionally, create/bind authoritative world loot only after successful removal, and preserve semantic identity needed for persistence.
- [ ] **T10-016 — Handle rollback/failure ordering.** Any failure before commit leaves both world and inventory at the prior conserved state.
- [ ] **T10-017 — Add persistence/replication projection seams.** Current loot availability/container contents are reconstructible without replaying pickup events.

## Verification

- [ ] **T10-020 — Two-actor pickup race.** Prove one winner, one rejection, one inventory increment and one world removal.
- [ ] **T10-021 — Failed transfer invariant.** Full/invalid/unknown destination or rejected interaction leaves world state unchanged.
- [ ] **T10-022 — Container transfer tests.** Both directions and competing transfers preserve total item quantity.
- [ ] **T10-023 — Drop/pickup round-trip.** Quantity and semantic payload are conserved across inventory -> world -> inventory.
- [ ] **T10-024 — Restore test.** Available/claimed/removed loot and container contents restore to current truth without duplicate items.
- [ ] **T10-025 — Independent non-Kentridge fixture and automatic module tests.**

## Cleanup / close

- [ ] **T10-030 — Remove scene-local pickup authority/direct Inventory edits.** Search MonoBehaviours and interaction handlers for bypass paths.
- [ ] **T10-031 — Boundary audit.** No random loot tables, rarity/economy/UI/equipment semantics and no dependency on Inventory/WorldObjects Runtime assemblies.
- [ ] **T10-032 — Close with conservation proof.** Demonstrate item conservation across pickup, contention, container transfer, drop, restore and replication projection.
