# 18. Inventory UI & authoritative inventory interaction

**Status:** Approved

## Purpose

Provide the player-facing inventory inspection and interaction surface over system 09's authoritative inventory model without introducing a second inventory representation in UI code.

The defining rule is:

> Inventory UI displays authoritative inventory ownership and requests semantic inventory actions; it never owns item quantities or performs inventory mutations itself.

Conceptually:

```text
authoritative InventoryId state
    -> system 06 replication where multiplayer requires it
        -> inventory snapshot / semantic changes
            -> Inventory UI presentation model
                -> Unity view

player UI action
    -> semantic inventory intent
        -> authoritative gameplay validation / transaction
            -> inventory state changes
                -> UI refreshes from resulting truth
```

## 1. Reuse system 09 rather than building a UI inventory model

System 09 owns:

- `InventoryId`
- `ItemRef`
- authoritative quantities
- add/remove/transfer transactions
- inventory snapshots
- inventory change events

System 18 must not recreate those concepts as independently mutable UI state.

A UI row/view model may contain presentation-ready copies of authoritative values, but those are projections of current inventory state. The authoritative quantity remains in system 09.

## 2. Bind to InventoryId, not implementation objects

The UI should open an inventory by semantic identity.

Conceptually:

```text
OpenInventory(InventoryId inventory)
```

not by receiving a concrete runtime object, searching for a `GameObject`, or using `FindObjectOfType`.

For a player's inventory:

```text
LocalPlayerId
    -> controlled CharacterId
        -> owned InventoryId
            -> Inventory UI
```

For a chest:

```text
WorldObjectId
    -> container capability
        -> InventoryId
            -> Inventory UI
```

The same inventory presentation path therefore works for characters, containers, and any later demonstrated inventory owner.

## 3. Initial UI does not assume a slot-based backpack

The repository currently models semantic item quantities, not physical backpack slots.

Therefore the first production UI should present an inventory as a collection of item entries containing semantic item identity, display metadata, quantity, and available semantic actions.

Do not invent:

- grid-cell backpack placement;
- stack-size rules;
- inventory capacity;
- weight/encumbrance;
- equipment slots;
- arbitrary drag-and-drop placement;

unless a gameplay system separately demonstrates those requirements.

The visual presentation may use cards, rows, tiles, or a grid, but visual layout must not imply authoritative slot semantics that do not exist.

## 4. Snapshot establishes inventory truth

Opening an inventory should start from an authoritative/current snapshot.

Conceptually:

```text
InventorySnapshot
    InventoryId
    Revision
    Items[]
```

with each item entry identifying at least `ItemRef` and `Quantity`, plus presentation metadata resolved separately as needed.

The UI reconstructs completely from that snapshot.

This supports reopening, reconnect, late join, UI recreation, scene transitions, and authoritative repair after stale client state.

The UI must not depend on receiving every historical item-added or item-removed event.

## 5. Semantic changes update an already-open view

After initial snapshot hydration, semantic inventory changes may update the open UI incrementally.

Events/deltas answer:

> What changed?

Snapshots answer:

> What is true now?

If incremental state becomes suspect, replacing the view model from a fresh authoritative snapshot must always be valid.

## 6. Personal inventory and container inventory use the same model

System 10 deliberately models containers as ordinary inventories. The UI should preserve that architecture.

A personal inventory view may show one `InventoryId`; a chest interaction may show a source `InventoryId` and destination `InventoryId`.

There should not be a separate `ChestContents` model with duplicated item quantities. A two-pane container presentation is merely a view over two ordinary inventories.

## 7. Inventory transfer is an authoritative action

Moving an item between inventories is not a UI operation.

For example, if the player selects `Take 3 Wood`, the UI produces semantic transfer intent identifying the source inventory, destination inventory, item, and quantity.

The authoritative transaction path validates source/destination validity, positive quantity, sufficient source quantity, relevant interaction permissions, and other owning-system rules.

Only after authoritative acceptance does inventory truth change. The UI then reflects the resulting state.

## 8. World-container actions preserve system 10's boundary

Opening or interacting with a chest begins through the authoritative WorldObject/interaction path.

The UI does not gain permission to access arbitrary `InventoryId`s merely because it knows their identity.

For a world container:

```text
Character interaction
    -> system 13 validation
        -> system 10 container semantics
            -> permitted inventory interaction context
                -> system 18 presentation
```

Transfers requested from the container UI remain subject to the same authoritative gameplay context.

Closing the chest UI does not mutate the container.

## 9. Dropping items remains gameplay-owned

If dropping is exposed from the inventory UI, the UI requests system 10's authoritative drop flow.

It must not locally subtract quantity, spawn a Unity pickup, and assume success.

System 10 owns item-conserving inventory-to-world transfer. System 18 presents the option and resulting outcome.

## 10. Expected rejection is normal UI state

Inventory actions can legitimately fail because authoritative state changed after the UI was rendered.

Examples include another player taking the item, source quantity changing, a container becoming unavailable, interaction context becoming invalid, controlled-character changes, or session readiness changes.

These are normal semantic rejections rather than UI exceptions.

The UI clears pending state, reconciles to current authoritative truth, and presents a semantic failure indication where useful. It must not reverse-engineer failure from missing quantity changes.

## 11. Pending action state is presentation state

A multiplayer client may temporarily display an action as pending while waiting for authority.

That pending state is local presentation state and must not be represented by prematurely changing authoritative-looking quantities.

Until the server accepts the action, authoritative quantity remains unchanged.

This avoids inventing inventory prediction where the game has not demonstrated a need for it.

## 12. Selection is local UI state

The following are presentation-only:

- selected item;
- highlighted row;
- scroll position;
- selected transfer quantity;
- active tab;
- tooltip visibility;
- sorting/filter choice.

They do not belong in system 09 inventory state, system 06 replication, or system 16 persistence unless an explicit future UX requirement says otherwise.

## 13. Item identity and item presentation stay separable

`ItemRef` remains semantic item identity.

The current `ItemDefinition` includes basic display name and icon text, which is sufficient as a present foundation.

Do not grow authoritative inventory state into a dumping ground for presentation data such as Unity sprites, prefabs, fonts, localized final strings, UI colors, or panel layouts.

Richer presentation should resolve from `ItemRef` through content/presentation metadata.

Inventory ownership remains semantic quantity state.

## 14. Do not introduce equipment semantics through the UI

Selecting an item does not automatically imply `Equip`, `Use`, `Consume`, `Craft`, or stat inspection.

Those actions require owning gameplay systems.

System 18 may display a semantic action when another system explicitly exposes one, but Inventory UI must not implement the gameplay behavior merely because it displays the item.

Equipment/loadouts remain outside system 18 unless separately designed.

## 15. UI input uses the existing input-context mechanism

Opening inventory should acquire the existing `Ui` input context.

Conceptually:

```text
inventory opened
    -> Push(InputContextId.Ui)
        -> UI navigation owns relevant local input
```

Closing the inventory releases that context and restores the preceding gameplay context.

System 18 must not directly disable combat/exploration controllers through ad-hoc component toggles.

## 16. Opening inventory does not automatically pause gameplay

Inventory UI and world pause are separate policies.

This matters especially in multiplayer: one player opening inventory cannot implicitly pause authoritative simulation for everybody else.

```text
Open Inventory != Pause Game
```

If single-player pause behavior is later desired, system 23/session composition owns that policy.

## 17. Gameplay-ready lifecycle applies

An actionable inventory UI requires valid authoritative gameplay state.

During connecting, restoring, or synchronizing, the previous inventory view must not remain actionable.

Once systems 08/14 establish `GameplayReady`, system 18 resolves the current controlled character and inventory identity again.

Reconnect rebuilds inventory presentation from current state rather than stale pre-disconnect quantities.

## 18. HUD and inventory screen remain separate

System 17 may expose a small inventory-related HUD indicator if demonstrated.

System 18 owns the dedicated inventory interaction surface for browsing contents, inspecting quantities, moving permitted items, and dropping permitted items.

Do not place the entire inventory screen inside the HUD controller.

## 19. Multiplayer authority

Clients never declare their resulting inventory quantities. They request semantic actions.

The authoritative host/server determines resulting state and client UI consumes replicated results.

Manipulating a local widget tree, closing the inventory, disconnecting, or reopening it can never alter item ownership by itself.

## 20. Headless-server independence

Systems 09 and 10 must operate identically with no Inventory UI assembly loaded.

A headless host should be able to add, remove, transfer, claim, drop, persist, and replicate inventory without system 18.

System 18 depends on semantic inventory APIs. Inventory gameplay does not depend on system 18.

## 21. Suggested presentation structure

Avoid one controller that combines gameplay rules, networking, input, and rendering.

Conceptually:

```text
InventoryScreen
    InventoryPresenter
    InventoryActionPresenter
    optional ContainerTransferPresenter
```

with local read projections such as:

```text
InventoryViewModel
    InventoryId
    Entries[]

InventoryEntryViewModel
    ItemRef
    display metadata
    Quantity
    AvailableActions
```

Actual authoritative changes still go through systems 09/10 and the normal multiplayer command path.

## Acceptance / reuse proof

### Character inventory

1. Bind system 18 to Character A's `InventoryId`.
2. Snapshot contains 3 Wood and 1 Key.
3. UI renders exactly those authoritative quantities.
4. An authoritative add changes Wood to 4.
5. UI updates without maintaining a second inventory store.

### Independent character

1. Bind the same presentation implementation to Character B's different `InventoryId`.
2. B has unrelated item quantities.
3. Verify the UI renders B's inventory with no player-specific inventory implementation.

### Container reuse

1. Open a chest through systems 13/10.
2. Bind one pane to the chest `InventoryId`.
3. Bind the other to the character `InventoryId`.
4. Request transfer of one item.
5. Authority performs exactly one system-09 transfer.
6. Both panes reflect resulting state.

### Concurrent container mutation

1. Player A and Player B view the same container.
2. Both initially see one remaining item.
3. Player A successfully claims it.
4. Player B's stale action is rejected/reconciled.
5. Both UIs converge on an empty container.
6. No quantity duplication occurs.

### Reconnect

1. Open an inventory showing current state.
2. Disconnect.
3. Authoritative inventory changes while disconnected.
4. Reconnect and resynchronize.
5. Inventory UI reconstructs entirely from current authoritative state.

### Headless independence

Execute equivalent inventory mutations and transfers without loading system 18 and verify gameplay results remain identical.

## Out of scope

- slot/grid backpack rules
- inventory capacity/encumbrance
- equipment/loadouts
- crafting
- item-use gameplay
- item combat behavior
- loot-table generation
- rarity/affix systems
- authoritative world loot — system 10
- HUD — system 17
- quest/objective UI — system 19
- pause/menu policy — system 23
- Unity-specific UI framework shared across the whole game

## Architectural constraints

- System 18 never owns authoritative item quantity.
- Inventory screens bind through stable `InventoryId`.
- Character and container inventories use the same presentation path.
- Current snapshots establish truth; semantic changes incrementally update presentation.
- UI requests transfers/drops; systems 09/10 execute them authoritatively.
- Expected authoritative rejection is a normal reconciled UI outcome.
- Selection, sorting, scrolling, pending indicators, and animation remain local presentation state.
- `ItemRef` remains semantic identity; Unity presentation assets do not become inventory ownership state.
- Opening inventory uses the existing UI input context and does not inherently pause the game.
- Reconnect can completely reconstruct the UI from synchronized authoritative state.
- Inventory gameplay remains runnable with no UI assembly loaded.
