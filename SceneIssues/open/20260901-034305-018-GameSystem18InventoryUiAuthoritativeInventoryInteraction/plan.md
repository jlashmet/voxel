# 18 Inventory UI & authoritative inventory interaction — implementation plan

**Current base:** `aa61895f28d70f35c67d07db6a4fa93beee635eb` (synced with `origin/master`).
**Owning module:** `Assets/Game/InventoryPresentation/{Api,Runtime,Tests,Validation}`. API/model is engine-free and independent of Inventory Runtime; Runtime depends only on public Inventory/Loot/Input/GameplayReplication APIs. The module owns `Validation/InventoryPresentationValidation.unity` plus its player scenario.

## Observed behavior / acceptance

Inventory and Loot are already authoritative and headless. Repository-tree/code search found no existing inventory screen/controller, drag/drop handler, UI-owned quantity cache, or `InventoryPresentation` module to migrate; therefore T18-017/T18-030 require a final boundary audit, not deletion of a legacy presenter. Inventory exposes snapshot/query truth; Loot owns container-transfer/drop semantics; Input owns stackable `Ui` leases.

## Hypotheses and result

1. **Prototype UI owns quantities/direct mutations.** Falsified by repository audit: no inventory presentation/controller layer exists.
2. **A presentation module can project canonical snapshots and delegate intents without new authority.** Selected. Stable row identity is `(InventoryId, ItemRef)`; selection/filter/sort/pending are ephemeral local state. Transfer/drop requests are queued as presentation pending operations, then executed through `ILootRuntime`; displayed quantities always come from `IInventoryQuery`.

## Implementation / blast radius

Add `Game.InventoryPresentation.Api` contracts and `Game.InventoryPresentation.Runtime.InventoryPresenter`. Personal and container inventories use the same panel/row model with no slot/equipment/crafting/use/capacity semantics. `OpenUi()` acquires a stackable `Ui` input lease; rebuild/reconnect clears stale pending state and reprojects authoritative snapshots. No Inventory/Loot/Input production implementation changes are planned.

## Validation gates

Module-local EditMode regressions cover snapshot revisions, empty/add/remove selection behavior, delayed pending, authoritative transfer/drop success, race rejection convergence, nested input-context unwind, reconnect rebuild, and destroy/recreate authority. Module-local built-player validation exercises the same presenter over real `InventoryRuntime` + `LootRuntime` and captures the functional personal/container UI. Then run exact-SHA targeted CI, close bookkeeping, merge current master into the branch, and use final PR + auto-merge `affected` gate.
