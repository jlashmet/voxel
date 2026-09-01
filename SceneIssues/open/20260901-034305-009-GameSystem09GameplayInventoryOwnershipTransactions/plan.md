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

- Current `Game.Inventory.Api` exposes `ItemRef`, `ItemDefinition`, `InventoryItemSnapshot`, and a mutable `IInventoryRuntime`; current `Game.Inventory.Runtime.InventoryRuntime` is one definition-backed quantity dictionary with `TryAddUnique`/`Add` mutations.
- The Kentridge campaign composition creates the runtime and the well-quest reward calls `TryAddUnique`; the playable inventory presentation reads `Count`/`Snapshot`. No second authoritative inventory collection was found in the assigned integration path.
- `Game.GameplayReplication.Adapters.InventoryGameplayProjectionSource` consumes the legacy mutable interface even though it only reads inventory truth; this is a boundary gap to migrate to the new query API.
- `Game.Composition.Kentridge.Runtime` is the composition root that constructs `Game.Inventory.Runtime`; player-facing code does not need the Runtime assembly and its direct reference will be removed.
- `Game.Loot` / System10 is not present on current master, so automatic dependent Loot validation cannot be run until that external prerequisite exists. Inventory will expose only the API seam System10 needs and will not create Loot locally.
- No standalone `Game.Persistence` module is present; capture/restore will therefore be an Inventory API seam plus deterministic runtime implementation, ready for System16 rather than inventing persistence transport.
