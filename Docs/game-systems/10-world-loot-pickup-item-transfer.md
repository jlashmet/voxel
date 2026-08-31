# 10. World loot, pickup & item transfer

**Status:** Approved

## Purpose

Provide the authoritative bridge between existing world objects/interactions and #09 inventory ownership/transactions. Reuse the world-object lifecycle rather than introducing a parallel pickup-object framework.

The system answers: **how does item ownership move safely between a world representation and an inventory?**

## World loot identity

A simple pickup is an existing world object with loot semantics:

- `WorldObjectId`
- `ItemRef`
- quantity

Do not introduce a separate `PickupId` unless a demonstrated requirement needs identity distinct from the world object.

A container such as a chest or crate binds its `WorldObjectId` to an ordinary `InventoryId` and uses the same inventory implementation as characters.

## Authoritative pickup flow

Pickup uses the existing interaction routing:

1. a character requests interaction with a world object;
2. interaction validation/routing confirms the object can be acted on;
3. the loot-transfer coordinator verifies that the loot is still claimable;
4. #09 performs the authoritative inventory add/transfer;
5. only after the inventory transaction succeeds does the world object become depleted or otherwise update.

A pickup must never disappear before the inventory transaction succeeds.

## Concurrent claims

Claims are authoritative, race-safe, and idempotent against retries/stale client messages. If two players claim the same pickup, exactly one succeeds and total item quantity remains conserved.

## Containers

Container contents are ordinary inventories. Taking an item from a chest is an authoritative transfer from the chest `InventoryId` to the character `InventoryId`; the loot system coordinates the world-object interaction but does not maintain a second quantity model.

## Dropping items

Dropping is the inverse operation: character inventory to world loot. The combined operation must not lose or duplicate items if either inventory mutation or world-object creation fails.

The implementation may choose an ordering/transaction strategy, but its externally visible guarantee is atomic conservation of item quantity.

## World-object lifecycle

Reuse existing world-object identity/state for loot availability and depletion. A simple pickup may transition conceptually from `Available` to `Claimed`/`Depleted`; do not create a separate authoritative loot-state database when world-object state already owns that lifecycle.

## Semantic results/events

Expose only semantic outcomes demonstrated as useful, such as:

- `LootClaimed`
- `LootDepleted`
- `ItemDropped`

Events carry stable world-object, character/inventory, item, and quantity identity as applicable. Networking, quests, audio, VFX, and presentation observe these outcomes independently; loot does not call those systems directly.

## Boundary with WorldBuilder/content

This system defines what claiming, dropping, and transferring world loot means.

WorldBuilder/game composition decides which authored/generated objects have loot or container capabilities and which items/quantities they contain. Place/campaign-specific loot content stays in composition rather than shared runtime code.

## Deliberately not assumed

Do not add without demonstrated requirements:

- random loot tables or weighted RNG;
- rarity/affix generation;
- equipment systems;
- crafting-material economy;
- corpse inventories;
- personal/instanced loot;
- need/greed rules;
- ownership timers.

## Reuse proof / acceptance

Minimum proof:

1. **Ground pickup:** claim a world object containing 2 items; inventory gains exactly 2 and the world object depletes once.
2. **Container:** move an item from a chest inventory to a player inventory through the same #09 transfer implementation.
3. **Concurrent claim:** two characters request the same pickup; exactly one succeeds and total quantity is conserved.
4. **Drop and reclaim:** Character A drops 3 items into a world representation and Character B later claims exactly those 3.
5. Failure paths prove a rejected inventory add or failed world-drop creation causes neither duplication nor item loss.

## Architectural constraints

- Reuse existing world-object identity, lifecycle, and interaction routing.
- Reuse #09 inventory transactions for authoritative quantity changes.
- Keep Unity presentation objects non-authoritative.
- Keep multiplayer authority on the host/server; clients request claims/drops rather than declaring results.
- Keep content-specific loot placement and contents in composition/content.
