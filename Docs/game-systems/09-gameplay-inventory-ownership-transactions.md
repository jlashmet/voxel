# 09. Gameplay inventory ownership & transactions

**Status:** Approved

## Purpose

Productionize the existing deterministic inventory runtime by giving inventories stable identity and an authoritative mutation boundary that can be reused by characters, containers, loot, networking, quests, UI, and persistence.

The existing `ItemRef`, `ItemDefinition`, quantity state, and snapshot model remain the foundation. This design generalizes them rather than replacing them.

## Core model

- `ItemRef` identifies a semantic item definition.
- `InventoryId` identifies one authoritative inventory instance.
- Character or other gameplay composition binds an owner to an `InventoryId`.
- Item definitions remain shared catalog/configuration; mutable inventory state contains item quantities only.

A character may own an inventory, but the inventory API must not become character-specific. Containers and other demonstrated inventory owners use the same implementation.

## Authoritative transactions

All production mutations go through a semantic transaction boundary rather than direct dictionary/state mutation.

Required operations are conceptually:

- add an item quantity;
- remove an item quantity;
- transfer an item quantity between inventories.

Transactions enforce known items, positive quantities, sufficient source quantity, overflow safety, and atomic transfer semantics. Expected gameplay rejection should return a semantic result rather than require callers to interpret exceptions.

A transaction result should expose the inventory or inventories involved, item, previous/resulting quantity, actual quantity moved, acceptance/rejection, and a semantic rejection reason when applicable.

## Inventory change events

Successful authoritative mutations emit semantic inventory-change events containing stable inventory/item identity and the quantity transition. Optional source/cause metadata should be included only where gameplay needs to distinguish causes.

Consumers react independently:

- networking replicates authoritative state;
- loot/pickup gameplay finalizes a claim;
- quest integration can translate acquisition into quest observations;
- UI refreshes presentation;
- persistence snapshots state.

Inventory does not call those systems directly.

## Snapshots

Preserve deterministic snapshots, generalized to a specific `InventoryId`. Snapshots support reconnect/late join, UI initialization, replication repair, and later save/session persistence.

## Authority

The authoritative gameplay host/server owns inventory mutation. Clients request gameplay actions; they do not declare resulting inventory quantities. Single-player uses the same service locally.

## Unique items

`TryAddUnique` may remain as compatibility while production callers move toward ordinary transaction policy. Uniqueness is item/gameplay policy, not a foundational inventory primitive unless demonstrated by game requirements.

## Deliberately not assumed

This system does not automatically add:

- slot/grid backpacks;
- stack-size limits;
- capacity or encumbrance;
- equipment/loadouts;
- crafting;
- item combat behavior;
- world pickup representation.

Those require separate demonstrated gameplay requirements.

## Boundary with loot / pickup gameplay

Inventory answers: **who owns how many of each item?**

Loot/pickup gameplay answers: **how does an item exist in the world, become claimable, get picked up, or get dropped?**

A world pickup requests an authoritative inventory transaction. Only after that transaction succeeds does the loot system remove or update the world representation.

## Reuse proof / acceptance

Use one shared item catalog with at least two independent inventories and a non-character inventory fixture.

Minimum proof:

1. Character A owns 3 of an item.
2. Character B owns 1 of the same item.
3. An authoritative transfer of 1 from A to B leaves A with 2 and B with 2.
4. The transfer is atomic and emits deterministic semantic change information.
5. A container inventory uses the same inventory implementation without character-specific code.

## Architectural constraints

- Reuse and generalize the existing inventory API/runtime; do not create a parallel inventory implementation.
- Keep stable semantic IDs out of Unity scene-object identity.
- Keep campaign/quest/place-specific policy in composition/content.
- UI consumes inventory state; it never owns authoritative inventory state.
- Networking transports/replicates inventory state but does not define inventory rules.
