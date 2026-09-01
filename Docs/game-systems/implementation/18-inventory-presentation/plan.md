# 18 Inventory UI & authoritative inventory interaction — implementation plan

**Target module:** `Assets/Game/InventoryPresentation/Api` / `Runtime` (`Game.InventoryPresentation.Api`, `Game.InventoryPresentation.Runtime`). Keep `Game.Inventory` authoritative.

## API

Local presentation model for one or more inventories, semantic transfer/drop intents, pending request identity/status, selection/filter state needed by views. Do not invent slot/equip/use concepts absent from Inventory API.

## Runtime

1. Project authoritative/replicated Inventory snapshots into stable view models.
2. Send transfer/drop requests through public inventory/loot APIs; never mutate quantities locally.
3. Represent pending operations without speculative authoritative quantity changes.
4. Handle races/rejections by refreshing from current truth.
5. Push/pop shared `Ui` InputContext and restore previous gameplay context.
6. Rebuild cleanly after reconnect/restore.

## Dependencies

09 Inventory, 10 Loot as needed, 06 client state, existing Input module.

## Tests / proof

Character/container transfer UI, race rejection, pending state, reconnect rebuild, context stack unwind, built-player visual validation.

## Do not build

No equipment/crafting/use semantics, replicated UI state, or inventory authority in presentation.
