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
