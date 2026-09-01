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
