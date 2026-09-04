# 18 Inventory UI & authoritative inventory interaction — implementation plan

**Current base:** `aa61895f28d70f35c67d07db6a4fa93beee635eb` (`origin/master` at implementation freeze).
**Owning module:** `Assets/Game/InventoryPresentation/{Api,Runtime,Tests,Validation}`. API is engine-free; Runtime depends only on public Presentation/Inventory/Loot/Input/GameplayReplication APIs. The module owns `Validation/InventoryPresentationValidation.unity` and its player scenario.

## Observed behavior / acceptance

Inventory is authoritative snapshot/query truth; Loot owns container transfer/drop; Input owns stackable `Ui` leases. Initial repository audit found no legacy inventory screen/controller or UI-owned quantity store to migrate. Presentation must therefore remain a projection plus semantic intent layer, never a second inventory authority.

## Hypotheses and result

1. **Existing UI owns direct inventory mutation.** Falsified: no prior inventory presentation/controller path existed.
2. **Canonical snapshots + Loot intents are sufficient.** Confirmed. Stable row identity is `(InventoryId, ItemRef)`; selection/filter/sort/pending are ephemeral. Displayed quantities are always projected from `IInventoryQuery`; transfer/drop execute only through `ILootRuntime`.

## Selected implementation / blast radius

`Game.InventoryPresentation.Api` defines row/panel/pending models and semantic intent wrappers. `InventoryPresenter` projects personal/container snapshots, tracks local UI state, delegates mutations to Loot, handles rejection/rebuild, and acquires `Ui` leases. `InventoryPresentationView` is the production player-visible inventory realization. Validation only composes real `InventoryRuntime` + `LootRuntime` + `InputContextService`, binds the production view, drives deterministic intents, and records assertions. No Inventory/Loot/Input production implementation changed; no slot/equipment/crafting/use/capacity semantics were added.

## Material validation result / remaining gates

Request `252b50dca70740366cc0508afa2efdb164cee3b7` validated exact product SHA `afbb0cf1c13b8cd4c27f9217e006f8aecbac49ab`; run `33879956141` passed the owned EditMode assembly, module player scenario, and integration player. Behavioral logs prove pending quantities remain stable, transfer/drop commit through authority, recreate preserves truth, and nested `Ui` unwinds.

Direct inspection of the module player captures rejected visual closure as **acceptable but improvable**, not `production-quality`: the production path is correct, but the screen still exposes developer-facing revision/owner/item-id copy, letter-like icon treatment, and flat debug-panel hierarchy. T18-018 remains open. Selected fix: keep the same production view/presenter seam, remove technical copy, render player-facing inventory/storage/activity language, strengthen textured fantasy framing and item medallions, and re-run exact-SHA built-player evidence. No authority or domain contract changes are required.
