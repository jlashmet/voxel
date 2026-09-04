# 18 Inventory UI & authoritative inventory interaction — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.InventoryPresentation.Api` / `Game.InventoryPresentation.Runtime`
**Execution rule:** presentation displays Inventory truth and sends semantic transfer/drop requests. It never speculates authoritative quantities or invents slot/equipment semantics.

## API / model

- [x] **T18-001 — Inventory current inventory UI/prototypes.** Repository-tree/code audit found no existing inventory screen/controller, drag/drop handler, UI-owned quantity cache, raw-input inventory UI, or prior `InventoryPresentation` module to migrate.
- [x] **T18-002 — Establish asmdefs.** `Game.InventoryPresentation.Api` is engine-free and references only Inventory/Loot APIs; Runtime references only Presentation/Inventory/Loot/Input/GameplayReplication APIs and no cross-module Runtime assembly.
- [x] **T18-003 — Define inventory view model.** `InventoryRowKey(InventoryId, ItemRef)` is stable semantic identity; rows project authoritative quantities while selection/filter/sort remain local metadata.
- [x] **T18-004 — Define transfer/drop presentation intents.** `InventoryTransferIntent` and `InventoryDropIntent` wrap the existing Loot semantic request contracts rather than duplicating transaction authority.
- [x] **T18-005 — Define pending-operation model.** Pending id/kind/status/error is local presentation state; queued operations do not alter displayed authoritative quantities.
- [x] **T18-006 — Define multi-inventory/container model.** The same panel/row projection presents character and container inventories with no slot-grid assumption.

## Runtime / UI

- [x] **T18-010 — Project current Inventory snapshots into view models.** `InventoryPresenter.Capture()` reads `IInventoryQuery`; row identity and valid selection survive snapshot revisions.
- [x] **T18-011 — Implement transfer request flow.** Queued intent remains pending until `ILootRuntime.TryContainerTransfer`; capture then reflects Inventory truth.
- [x] **T18-012 — Implement drop request flow.** Queued drop remains non-speculative and delegates world-item creation to `ILootRuntime.TryDrop`.
- [x] **T18-013 — Handle race/rejection.** Rejected operations retain error/status while row quantities are re-read from current Inventory truth; no compensating local edits exist.
- [x] **T18-014 — Implement selection/filter/sort locally.** Presenter-local state is not persisted or replicated as gameplay state.
- [x] **T18-015 — Integrate `Ui` InputContext stack.** Presenter/UI use stackable `Ui` leases; production view owns its lease and nested scopes unwind deterministically without pausing gameplay.
- [x] **T18-016 — Rebuild after reconnect/restore.** Rebuild drops stale pending operations, clears invalid selection, and reprojects snapshots.
- [x] **T18-017 — Replace prototype direct-mutation UI.** Initial audit found no legacy inventory presenter to delete; final parity audit also found no second authoritative UI path.
- [ ] **T18-018 — Move visible inventory realization into production Runtime and meet visual-quality bar.** `InventoryPresentationView` is now the production fantasy inventory realization and Validation is a thin composition consumer; final built-player capture must still be revalidated and classified `production-quality`.

## Verification

- [ ] **T18-020 — Presenter/view-model unit tests.** Snapshot revisions, item additions/removals, stable selection and empty inventories.
- [ ] **T18-021 — Character/container transfer UI test.** Request succeeds only when authoritative transaction succeeds.
- [ ] **T18-022 — Race/rejection test.** Two clients/actions contend and loser UI converges without speculative residue.
- [ ] **T18-023 — Pending-state test.** Delayed response shows pending without changing quantity and resolves correctly on success/failure.
- [ ] **T18-024 — Input-context unwind test.** Nested UI closes back to prior gameplay context deterministically, including the production view's lease.
- [ ] **T18-025 — Reconnect rebuild test.** Current Inventory truth replaces stale local presentation state.
- [ ] **T18-026 — Module-local built-player visual/input validation through shared harness.** Validation now binds the production `InventoryPresentationView`; exact-SHA player evidence is pending.

## Cleanup / close

- [x] **T18-030 — Remove UI-owned quantities/direct collection edits.** Final module audit confirms Runtime reads quantities through `IInventoryQuery`; mutations only delegate semantic transfer/drop requests through Loot.
- [x] **T18-031 — Scope audit.** Contracts contain no equipment/crafting/use/capacity/slot semantics, and Runtime has no cross-module Runtime dependency.
- [ ] **T18-032 — Close with authority proof.** Destroy/recreate UI while inventory remains correct, and all mutations still pass through Inventory/Loot authority; final exact-SHA proof pending.
