# 18 Inventory UI & authoritative inventory interaction — implementation plan

**Current base:** `aa61895f28d70f35c67d07db6a4fa93beee635eb` (current `origin/master`).
**Owning module:** `Assets/Game/InventoryPresentation/{Api,Runtime,Tests,Validation}`. API is engine-free; Runtime depends only on public Presentation/Inventory/Loot/Input/GameplayReplication APIs. The module owns `Validation/InventoryPresentationValidation.unity` and its player scenario.

## Observed behavior / acceptance

Inventory is authoritative snapshot/query truth; Loot owns container transfer/drop; Input owns stackable `Ui` leases. Initial repository audit found no legacy inventory screen/controller or UI-owned quantity store to migrate. Presentation must therefore remain a projection plus semantic intent layer, never a second inventory authority.

## Hypotheses and result

1. **Existing UI owns direct inventory mutation.** Falsified: no prior inventory presentation/controller path existed.
2. **Canonical snapshots + Loot intents are sufficient.** Confirmed. Stable row identity is `(InventoryId, ItemRef)`; selection/filter/sort/pending are ephemeral. Displayed quantities are always projected from `IInventoryQuery`; transfer/drop execute only through `ILootRuntime`.

## Selected implementation / blast radius

`Game.InventoryPresentation.Api` defines row/panel/pending models and semantic intent wrappers. `InventoryPresenter` projects personal/container snapshots, tracks local UI state, delegates mutations to Loot, handles rejection/rebuild, and acquires `Ui` leases. `InventoryPresentationView` is the production player-visible fantasy inventory realization. Validation now only composes real `InventoryRuntime` + `LootRuntime` + `InputContextService`, binds the production view, drives deterministic intents, and records assertions. No Inventory/Loot/Input production implementation changed; no slot/equipment/crafting/use/capacity semantics were added.

## Material validation result / remaining gates

Exact-SHA request `4882e1a1836a821363780d00e37332b8b1babc9c` (run `33875652528`) passed the first presenter/scene behavior, but its capture exposed a quality/fidelity defect: Validation itself drew a blockout UI. That path is removed. The production view now owns rendering and its input lease, with a regression for bind/nested-unwind/unbind. Remaining gate: run targeted CI from the new exact feature SHA, inspect durable standalone-player captures and require `production-quality`; then complete verification checkboxes, close bookkeeping, sync current master, and promote by PR + auto-merge `affected` gate.
