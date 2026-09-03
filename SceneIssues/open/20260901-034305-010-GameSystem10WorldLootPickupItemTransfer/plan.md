# 10 World loot, pickup & item transfer — implementation plan

**Target module:** `Assets/Game/Loot/Api` / `Runtime` (`Game.Loot.Api`, `Game.Loot.Runtime`). World-object identity/interaction comes from #13; inventory mutation comes from #09.

## API

Semantic loot/world-item identity, pickup/drop/container-transfer request/result, claim state where needed for races, and resulting transfer facts. Avoid Unity transform/prefab references.

## Runtime

1. Bind lootable WorldObject state to an authoritative inventory/item payload.
2. Route pickup/container/drop requests through #13 validation then #09 transactions.
3. Resolve competing claims deterministically and exactly once.
4. Update/remove/create world loot only after inventory transaction success so items are conserved.
5. Project current loot/container state for replication and persistence through adapters.

## Dependencies

09 Inventory, 13 WorldObjects, 03 Characters for actor identity/context.

## Tests / proof

Two actors racing one pickup, failed transfer leaves world unchanged, container transfers, drop round-trip, restore, and conservation invariant.

## Do not build

No inventory UI, equipment, random loot tables, rarity system, or scene-local pickup authority.

## Execution notes / blockers

- The synced feature head did not contain the #09 transaction API required by this design. GameSystem10 therefore adds only the minimal generic `InventoryId`/transaction contract and deterministic transaction runtime needed for pickup, container transfer, drop and conservation; it does not implement the rest of #09 ownership/UI scope.
- The synced feature head did not contain `Game.WorldObjects.Api` from #13. GameSystem10 therefore adds only the semantic `WorldObjectId` and interaction-validation contract required to delegate reach/permission/state validation; no WorldObjects runtime or scene policy is implemented here.
- No local Unity checkout/runtime is available in this session, so validation uses the required targeted-CI transport only.
- Targeted CI run `33722283905` validated source SHA `763e999df8ffbba2c82d63df36e07e6d495d3957`. `Game.Loot.Tests` compiled and passed all 10 tests (`failed=0`, `skipped=0`). The overall automatic affected-module gate failed later in `VoxelEngine.Tests.EditMode` with 16 GPU-rendering failures (geometry-arena architecture/cutover and GPU/CPU oracle/parity tests), none in GameSystem10.
- The external renderer prerequisite advanced on `master` to `f5593cc1236ba3963fc5713a11df35292628e97d` and was merged into `fixes/agent-8` as `92843fa8199f05cea52c49f76ad23a807407b0fc`. The prior blocker is therefore cleared. T10-032 now requires a fresh exact-SHA targeted validation of the synchronized feature head before closure; acceptance remains unchanged.
