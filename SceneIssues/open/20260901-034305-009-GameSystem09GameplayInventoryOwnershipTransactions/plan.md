# 09 Gameplay inventory ownership & transactions — implementation plan

**Target ownership:** evolve existing `Assets/Game/Inventory/Api` / `Runtime`; do not create another inventory module.

## API

Stable `InventoryId`, item identity/type semantics already supported by the repository, inventory snapshot, transaction request/result, add/remove/transfer operations, change events, and ownership/binding metadata only where required.

## Runtime

1. Generalize current deterministic inventory implementation from prototype/use-case assumptions to stable inventory instances.
2. Make all mutations authoritative transactions with explicit success/failure and conservation rules.
3. Support character and container inventories through the same runtime.
4. Remove any direct collection mutation from callers once transaction parity exists.
5. Add deterministic capture/restore and #06 projection adapters.

## Dependencies

03 Characters API for character-owned binding when needed; otherwise keep inventory generic. #10 consumes Inventory API.

## Tests / proof

Add/remove/transfer, insufficient quantity, duplicate/racing requests, character and container consumers, deterministic snapshots, restore, and item conservation.

## Do not build

No equipment, crafting, slot grids, weight/capacity, use/consume semantics, or UI unless separately designed.

## Execution notes / baseline audit (2026-09-01)

- Baseline `Game.Inventory.Api` exposed `ItemRef`, `ItemDefinition`, `InventoryItemSnapshot`, and a mutable `IInventoryRuntime`; baseline `Game.Inventory.Runtime.InventoryRuntime` was one definition-backed quantity dictionary with `TryAddUnique`/`Add` mutations.
- The Kentridge campaign composition created the runtime and the well-quest reward called `TryAddUnique`; the playable inventory presentation read `Count`/`Snapshot`. No second authoritative inventory collection was found in the assigned integration path.
- `Game.GameplayReplication.Adapters.InventoryGameplayProjectionSource` consumed the legacy mutable interface even though it only read inventory truth; this was a boundary gap to migrate to the new query API.
- `Game.Composition.Kentridge.Runtime` is the composition root that constructs `Game.Inventory.Runtime`; player-facing code does not need the Runtime assembly and its direct reference is removed.
- `Game.Loot` / System10 is not present on current master, so automatic dependent Loot validation cannot be run until that external prerequisite exists. Inventory exposes only the API seam System10 needs and does not create Loot locally.
- No standalone `Game.Persistence` module is present; capture/restore is therefore an Inventory API seam plus deterministic runtime implementation, ready for System16 rather than inventing persistence transport.

## Implementation evidence (2026-09-02)

- `InventoryId` is stable and owner-agnostic; generic `InventoryBindingMetadata` is supplied by composition, with character and container fixtures proving the same runtime path.
- `IInventoryQuery`, `IInventoryAuthority`, and `IInventoryStatePort` separate read truth, authoritative mutation, and capture/restore without exposing Runtime collections.
- Add/remove/transfer are serialized under one authority gate, return explicit results/revisions, publish committed change events, reject invalid or insufficient mutations, and journal transaction ids so duplicate delivery cannot double-apply.
- Kentridge reward mutation now uses `IInventoryAuthority`; the playable presentation uses the query seam; only Kentridge composition constructs the concrete Runtime.
- GameplayReplication projects deterministic multi-inventory snapshots solely through `IInventoryQuery` and no longer depends on the mutable Runtime contract.
- `Game.Inventory.Tests.InventoryTransactionTests` covers invalid/unknown inputs, insufficient removal, successful and failed conservation, duplicate/conflicting ids, competing removals, character/container reuse, and deterministic capture/restore ordering and revisions.
- The System16 seam is intentionally limited to deterministic InventoryId/content state assigned here. Persisting transaction-delivery infrastructure is not added without the absent Persistence/System16 contract.
- Final repository-wide bypass/boundary claims remain pending the repository-selected automatic module validation. System10/Loot remains an external prerequisite and is not implemented or simulated by this assignment.
