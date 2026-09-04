# 18 Inventory UI & authoritative inventory interaction — implementation plan

**Current base:** `aa61895f28d70f35c67d07db6a4fa93beee635eb` (`origin/master` at implementation freeze).
**Owning module:** `Assets/Game/InventoryPresentation/{Api,Runtime,Tests,Validation}`. API is engine-free; Runtime depends only on public Presentation/Inventory/Loot/Input/GameplayReplication APIs. The module owns `Validation/InventoryPresentationValidation.unity` and its player scenario.

## Observed behavior / acceptance

Inventory is authoritative snapshot/query truth; Loot owns container transfer/drop; Input owns stackable `Ui` leases. Initial repository audit found no legacy inventory screen/controller or UI-owned quantity store to migrate. Presentation therefore remains a projection plus semantic intent layer, never a second inventory authority.

## Hypotheses and result

1. **Existing UI owns direct inventory mutation.** Falsified: no prior inventory presentation/controller path existed.
2. **Canonical snapshots + Loot intents are sufficient.** Confirmed. Stable row identity is `(InventoryId, ItemRef)`; selection/filter/sort/pending are ephemeral. Displayed quantities are always projected from `IInventoryQuery`; transfer/drop execute only through `ILootRuntime`.

## Selected implementation / blast radius

`Game.InventoryPresentation.Api` defines row/panel/pending models and semantic intent wrappers. `InventoryPresenter` projects personal/container snapshots, tracks local UI state, delegates mutations to Loot, handles rejection/rebuild, and acquires `Ui` leases. `InventoryPresentationView` is the production player-visible inventory realization. Validation only composes real `InventoryRuntime` + `LootRuntime` + `InputContextService`, binds the production view, drives deterministic intents, and records assertions. No Inventory/Loot/Input production implementation changed; no slot/equipment/crafting/use/capacity semantics were added.

## Validation result / closure

Initial exact-SHA validation request `252b50dca70740366cc0508afa2efdb164cee3b7` proved the authority/runtime seam but its captures were classified **acceptable but improvable** because they exposed revision/owner/item IDs and a debug-panel visual hierarchy. The selected production-view polish removed that technical copy and strengthened fantasy framing without changing authority contracts.

Final request commit `6728771d06eb3dbf1eeafb30880c4f0e294eeda1` validated exact product SHA `b45c310b6127b4198eab5ea5265ca27341211609` in workflow `33882017353`. `Game.InventoryPresentation.Tests.EditMode` passed; the module-local standalone player passed all required view-binding/pending/transfer/drop/recreate/input-unwind assertions; canonical `KentridgePlayableSlice` integration passed. Five 1280x720 module captures were directly inspected and classified **production-quality**: parchment/wood/brass hierarchy is coherent, visible copy is player-facing, technical IDs/revisions are absent, and inventory/storage/activity states remain legible across the scenario.

All feature acceptance is complete. Remaining work is closure bookkeeping, merge current `origin/master` if it advanced, then PR + auto-merge through the required `affected` gate.
