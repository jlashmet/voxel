# 10 World loot, pickup & item transfer — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Loot.Api` / `Game.Loot.Runtime`
**Execution rule:** WorldObjects validates interaction/context; Inventory owns quantities; Loot coordinates the cross-domain transaction and world-item lifecycle.

## API / state

- [x] **T10-001 — Inventory current pickup/drop/container paths.** Find scene pickup components, world-item state, direct inventory mutation, container behavior and race handling.
  - Discovery: no `Game.Loot` module or shared pickup/drop/container transfer path exists on the synced feature head. `Game.Inventory.Api.IInventoryRuntime` exposes `TryAddUnique`, `Add`, `Count`, and `Snapshot` only; it has no remove/transfer/capacity/transaction result contract. Kentridge quest rewards directly call `Inventory.TryAddUnique` in composition (`KentridgeWellQuestRewardRuntime`) and Kentridge well interaction/presentation uses scene-local proximity + `Input.GetKeyDown(KeyCode.E)`. The existing composition `WorldObjects` folder contains runtime bootstrap/composition only and no `Game.WorldObjects.Api` assembly. Existing showcase interaction remains an independent presentation fixture, not loot authority.
- [x] **T10-002 — Establish asmdefs.** Loot.Runtime depends on Inventory.Api, WorldObjects.Api and Characters.Api only; Loot.Api contains no prefab/Transform references.
  - `Game.Loot.Api` and `Game.Loot.Runtime` are engine-free asmdefs; Runtime references only Loot.Api plus the three required API assemblies. The API contract uses semantic ids/payloads only and has no UnityEngine, Transform, GameObject or prefab type.
- [x] **T10-003 — Define stable loot/world-item identity.** Reuse WorldObjectId where appropriate and add only the semantic item/payload identity actually needed.
  - Loot identity is the prerequisite `WorldObjectId`; `LootPayload` adds only `ItemRef` plus positive quantity, so no parallel scene/prefab identity was introduced.
- [x] **T10-004 — Define pickup/drop/container-transfer requests/results.** Include actor, source/destination and explicit failure reasons without duplicating Inventory transaction schema.
  - `PickupRequest`, `ContainerTransferRequest`, `DropRequest`, and `LootTransferResult` carry `CharacterId`, world context, `InventoryId` endpoints and loot-specific failure while preserving the underlying `InventoryTransactionFailure`/`WorldInteractionFailure` instead of copying their schemas.
- [x] **T10-005 — Define claim/current-state contract.** Represent enough authoritative state to serialize competing pickup attempts and project current availability.
  - `LootStateSnapshot` stores object id, payload, `Available`/`Claimed`/`Removed`, and claimant identity; capture/restore uses this current-state representation.
- [x] **T10-006 — Define committed transfer facts.** Emit semantic results after both world and inventory sides commit; downstream systems never infer success from presentation disappearance.
  - `LootTransferFact` records transfer kind, actor, object, payload and inventory endpoints; Runtime constructs it only on the success path after the inventory transaction and authoritative loot state update succeed.

## Runtime

- [x] **T10-010 — Bind lootable WorldObjects to Inventory payload.** Keep one authoritative payload mapping and reject stale/unknown bindings.
  - `TryBind` owns one snapshot per `WorldObjectId`, rejects duplicate ids, and pickup rejects unknown ids before mutation.
- [x] **T10-011 — Validate actor/object interaction through WorldObjects.Api.** Do not replicate reach/permission checks inside Loot.
  - Pickup/container/drop all call `IWorldInteractionValidator.Validate(actorId, objectId)` and surface its semantic rejection without implementing reach/permission policy in Loot.
- [x] **T10-012 — Execute pickup as coordinated transaction.** Claim/validate object, perform Inventory transaction, then update/remove world loot only on success.
  - Pickup validates availability and interaction, records an in-lock claim, performs `IInventoryTransactions.TryAdd`, restores the previous snapshot on rejection, and transitions to `Removed` only after success.
- [x] **T10-013 — Implement deterministic competing claims.** Exactly one actor can win a single-claim pickup; losers receive semantic rejection and no inventory change.
  - All loot transitions are serialized by one runtime gate; once a pickup commits `Removed`, subsequent contenders receive `AlreadyRemoved`, preventing duplicate inventory mutation.
- [x] **T10-014 — Implement container transfer.** Use the same Inventory transaction path for character/container sources and destinations.
  - Container transfer delegates atomically to `IInventoryTransactions.TryTransfer` using generic source/destination `InventoryId`s.
- [x] **T10-015 — Implement drop round-trip.** Remove from inventory transactionally, create/bind authoritative world loot only after successful removal, and preserve semantic identity needed for persistence.
  - Drop rejects duplicate world ids and invalid interaction before `TryRemove`; only a successful removal creates the new available `WorldObjectId` + `LootPayload` binding.
- [x] **T10-016 — Handle rollback/failure ordering.** Any failure before commit leaves both world and inventory at the prior conserved state.
  - Interaction failures occur before inventory mutation; pickup restores pre-claim state on inventory rejection; transfer validation occurs before either side mutates; drop creates world state only after inventory removal succeeds.
- [x] **T10-017 — Add persistence/replication projection seams.** Current loot availability/container contents are reconstructible without replaying pickup events.
  - Loot `Capture`/`TryRestore` projects sorted current world-item truth; the prerequisite inventory transaction seam independently captures/restores current per-inventory quantities, allowing container truth to be reconstructed without event replay.

## Verification

- [x] **T10-020 — Two-actor pickup race.** `TwoActorPickupRace_CommitsExactlyOnceAndConservesQuantity` proves one successful contender, one rejection, one inventory increment and one removed world state.
- [x] **T10-021 — Failed transfer invariant.** Regressions cover unknown destination, rejected interaction, and `DestinationRejected` (full/capacity-style policy) while asserting the loot payload stays available and inventory remains unchanged.
- [x] **T10-022 — Container transfer tests.** Both directions and concurrent competing transfers preserve total quantity.
- [x] **T10-023 — Drop/pickup round-trip.** Regression proves quantity and semantic payload are conserved across inventory -> world -> inventory.
- [x] **T10-024 — Restore test.** Available/claimed/removed loot and character/container quantities restore current truth without duplicate world ids.
- [x] **T10-025 — Independent non-Kentridge fixture and automatic module tests.** The Harbor semantic fixture consumes the same runtime with non-Kentridge ids; `Game.Loot.Tests` owns the automatic regressions.

## Cleanup / close

- [x] **T10-030 — Remove scene-local pickup authority/direct Inventory edits.** Discovery found no existing pickup/drop/container authority to migrate. The remaining Kentridge `TryAddUnique` call is quest-reward composition rather than loot pickup/container authority, so changing it would be unrelated refactoring.
- [x] **T10-031 — Boundary audit.** Loot contains no random tables, rarity/economy/UI/equipment semantics, Unity object references, or dependency on Inventory/WorldObjects Runtime assemblies. Minimal #09/#13 prerequisite contracts were added because those dependencies are absent on this branch.
- [x] **T10-032 — Close with conservation proof.** Source regressions cover pickup, contention, container transfer, drop, restore and current-state projection. Exact-SHA source `a9e2f3c9907a94a2aa771b0ed817f8d2d8035e40` passed targeted/automatic validation in run `33800800347` with `ci/single-test=success` after synchronizing the renderer prerequisite from master.
