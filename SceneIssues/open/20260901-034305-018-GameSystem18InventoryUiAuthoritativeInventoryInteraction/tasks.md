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
- [x] **T18-018 — Move visible inventory realization into production Runtime and meet visual-quality bar.** `InventoryPresentationView` is the production fantasy inventory realization and Validation is a thin composition consumer. Exact-SHA built-player capture from workflow `33882017353` was directly inspected and classified `production-quality`: developer IDs/revisions are absent, hierarchy is player-facing, and parchment/wood/brass framing, medallions, search/sort rows, quantities, and activity state read coherently.

## Verification

- [x] **T18-020 — Presenter/view-model unit tests.** Snapshot revisions, item additions/removals, stable selection and empty inventories are covered by `Game.InventoryPresentation.Tests.InventoryPresenterTests` and passed on exact product SHA `b45c310b6127b4198eab5ea5265ca27341211609`.
- [x] **T18-021 — Character/container transfer UI test.** Pending transfer stays non-speculative and succeeds only through authoritative Loot/Inventory transaction flow.
- [x] **T18-022 — Race/rejection test.** Competing transfers prove the loser rejects and converges to current authoritative quantities with no speculative residue.
- [x] **T18-023 — Pending-state test.** Delayed transfer/drop state remains pending without changing displayed quantity and resolves to authoritative success/failure.
- [x] **T18-024 — Input-context unwind test.** Nested UI closes back to prior gameplay context deterministically, including the production view's lease.
- [x] **T18-025 — Reconnect rebuild test.** Rebuild and recreated presenter replace stale local state with current Inventory truth.
- [x] **T18-026 — Module-local built-player visual/input validation through shared harness.** Workflow `33882017353` passed `InventoryPresentationValidation` plus canonical `KentridgePlayableSlice`; required logs proved view binding, pending stability, transfer/drop commits, recreate stability, and nested `Ui` unwind. Five 1280x720 module captures were inspected.

## Cleanup / close

- [x] **T18-030 — Remove UI-owned quantities/direct collection edits.** Final module audit confirms Runtime reads quantities through `IInventoryQuery`; mutations only delegate semantic transfer/drop requests through Loot.
- [x] **T18-031 — Scope audit.** Contracts contain no equipment/crafting/use/capacity/slot semantics, and Runtime has no cross-module Runtime dependency.
- [x] **T18-032 — Close with authority proof.** Exact-SHA unit/player evidence proves destroy/recreate preserves Inventory truth and every transfer/drop mutation still passes through Inventory/Loot authority.
