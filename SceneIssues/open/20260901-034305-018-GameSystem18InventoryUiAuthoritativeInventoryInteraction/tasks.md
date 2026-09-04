# 18 Inventory UI & authoritative inventory interaction — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.InventoryPresentation.Api` / `Game.InventoryPresentation.Runtime`
**Execution rule:** presentation displays Inventory truth and sends semantic transfer/drop requests. It never speculates authoritative quantities or invents slot/equipment semantics.

## API / model

- [x] **T18-001 — Inventory current inventory UI/prototypes.** Repository-tree/code audit found no existing inventory screen/controller, drag/drop handler, UI-owned quantity cache, raw-input inventory UI, or `InventoryPresentation` module to migrate.
- [ ] **T18-002 — Establish asmdefs.** Presentation API/model is independent of Inventory Runtime; Runtime consumes Inventory/Loot/Replication/Input APIs.
- [ ] **T18-003 — Define inventory view model.** Stable inventory/item rows derived from authoritative snapshots with selection/filter metadata kept local.
- [ ] **T18-004 — Define transfer/drop presentation intents.** Map UI gestures to existing Inventory/Loot semantic requests rather than duplicating transaction contracts.
- [ ] **T18-005 — Define pending-operation model.** Track local request id/status/error without altering authoritative displayed quantity until confirmed truth arrives.
- [ ] **T18-006 — Define multi-inventory/container model.** Personal and world/container inventories use the same presentation concepts; no slot-grid assumption.

## Runtime / UI

- [ ] **T18-010 — Project current Inventory snapshots into view models.** Preserve stable row identity across revisions where semantic identity allows it.
- [ ] **T18-011 — Implement transfer request flow.** User intent -> Inventory/Loot API -> pending state -> authoritative refresh/result.
- [ ] **T18-012 — Implement drop request flow.** Same non-speculative pattern; world creation remains Loot authority.
- [ ] **T18-013 — Handle race/rejection.** Clear/annotate pending operation and refresh from current truth; never compensate by locally editing quantities.
- [ ] **T18-014 — Implement selection/filter/sort locally.** These must not be replicated or persisted as gameplay state.
- [ ] **T18-015 — Integrate `Ui` InputContext stack.** Opening inventory pushes Ui context; nested screens unwind correctly; opening inventory does not globally pause gameplay.
- [ ] **T18-016 — Rebuild after reconnect/restore.** Drop stale pending operations according to request status and reconstruct all authoritative rows from snapshots.
- [ ] **T18-017 — Replace prototype direct-mutation UI.** No prototype code was found; close after final parity/boundary audit confirms there is nothing to remove.
- [ ] **T18-018 — Move visible inventory realization into production Runtime and meet visual-quality bar.** First exact-SHA player proof was behaviorally green but the captured UI was drawn by Validation code and classified `prototype/blockout quality`. Add a production Runtime view, make Validation a thin composition consumer, and revalidate durable built-player evidence.

## Verification

- [ ] **T18-020 — Presenter/view-model unit tests.** Snapshot revisions, item additions/removals, stable selection and empty inventories.
- [ ] **T18-021 — Character/container transfer UI test.** Request succeeds only when authoritative transaction succeeds.
- [ ] **T18-022 — Race/rejection test.** Two clients/actions contend and loser UI converges without speculative residue.
- [ ] **T18-023 — Pending-state test.** Delayed response shows pending without changing quantity and resolves correctly on success/failure.
- [ ] **T18-024 — Input-context unwind test.** Nested UI closes back to prior gameplay context deterministically.
- [ ] **T18-025 — Reconnect rebuild test.** Current Inventory truth replaces stale local presentation state.
- [ ] **T18-026 — Module-local built-player visual/input validation through shared harness.**

## Cleanup / close

- [ ] **T18-030 — Remove UI-owned quantities/direct collection edits.** Final repository audit must confirm the new presenter also owns no authoritative collection/quantity mutation.
- [ ] **T18-031 — Scope audit.** No equipment/crafting/use/capacity/slot semantics unless separately approved and no cross-module Runtime dependency.
- [ ] **T18-032 — Close with authority proof.** Destroy/recreate UI while inventory remains correct, and all mutations still pass through Inventory/Loot authority.
