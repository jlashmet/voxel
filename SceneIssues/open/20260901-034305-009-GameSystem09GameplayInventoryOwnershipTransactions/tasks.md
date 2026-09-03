# 09 Gameplay inventory ownership & transactions — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** evolve existing `Game.Inventory.Api` / `Game.Inventory.Runtime`; do not create a second inventory module.
**Execution rule:** all authoritative quantity changes become explicit inventory transactions.

## Baseline / API

- [x] **T09-001 — Inventory existing inventory stores/callers.** Find prototype inventories, mutable collections, direct add/remove calls, character/container assumptions and current item identity semantics.
- [x] **T09-002 — Verify existing Api/Runtime boundary.** Remove planned cross-module Runtime references; record API gaps instead of bypassing the boundary.
- [x] **T09-003 — Define stable `InventoryId`.** Specify identity/serialization and binding metadata needed for character/container ownership without baking owner type into inventory core.
- [x] **T09-004 — Normalize inventory snapshot contract.** Stable item/type/quantity representation using existing item semantics; no UI slots/capacity assumptions.
- [x] **T09-005 — Define mutation request/results.** Authoritative add/remove/transfer commands with explicit success/failure and resulting revision/state.
- [x] **T09-006 — Define conservation/change events.** Events describe committed inventory changes and stable transaction identity where duplicate delivery is possible.

## Runtime / migration

- [x] **T09-010 — Generalize inventory instances.** Remove prototype/use-case singleton assumptions and support multiple stable InventoryIds deterministically.
- [x] **T09-011 — Implement transactional add/remove.** Validate quantities/item ids, reject insufficient/invalid mutations and commit atomically.
- [x] **T09-012 — Implement atomic transfer.** Source decrement and destination increment must succeed/fail as one authoritative operation.
- [x] **T09-013 — Resolve duplicate/racing requests.** Use existing command semantics/request identity as needed so repeated delivery cannot duplicate items.
- [x] **T09-014 — Bind character inventories through composition/API.** Characters owns CharacterId; Inventory owns inventory contents.
- [x] **T09-015 — Bind container inventories through the same runtime.** Prove no special container collection path exists.
- [x] **T09-016 — Migrate direct collection mutations.** Replace callers with public transaction requests, then make internal collections inaccessible outside Runtime.
- [x] **T09-017 — Add capture/restore seam.** Deterministic snapshots preserve InventoryId and contents for system 16.
- [x] **T09-018 — Add replication projection seam.** Expose current inventory truth for system 06 without referencing its Runtime.

## Verification

- [x] **T09-020 — Add/remove tests.** Success, invalid amount, unknown item/inventory and insufficient quantity.
- [x] **T09-021 — Transfer atomicity/conservation tests.** Sum of conserved item quantity remains unchanged across success/failure.
- [x] **T09-022 — Duplicate/race tests.** Same request or competing requests never create negative/duplicated quantities.
- [x] **T09-023 — Character/container reuse test.** Both consumers use identical transaction/runtime path.
- [x] **T09-024 — Snapshot/restore determinism.** Stable ordering/identity and exact contents after restore.
- [ ] **T09-025 — Run automatic Inventory and dependent Loot tests.** Inventory-only post-sync run `33802369720` is green. System10/Loot was promoted to authoritative `origin/master` `149d7f85cc3fc293fb0abcaf9cb950346bb0aee5`; agent-2 merged it in two-parent merge `44c24f73cfde809a9546d6e4dc5a1540f2c00035`. The published temporary `InventoryTransactionsRuntime` duplicate quantity store was removed and replaced by stateless `InventoryTransactionsAdapter`, which delegates mutations/reads/restore to the single validated `InventoryRuntime`; Loot tests now consume that same authority. Combined exact-SHA Inventory + dependent Loot automatic validation is the next required gate.

## Cleanup / close

- [x] **T09-030 — Repository-wide mutation bypass search.** Remove external direct collection edits and duplicate authoritative item stores. Exact-source tree and changed-production audit found one Inventory authority/runtime; after System10 integration its temporary dictionary-backed InventoryTransactionsRuntime was removed and Loot is routed through a stateless adapter over that same authority.
- [x] **T09-031 — Boundary audit.** No equipment/crafting/slots/capacity/UI semantics added; no external Inventory.Runtime reference. Reusable/playable consumers depend on `Game.Inventory.Api`; Runtime dependencies are construction/adaptation boundaries only and no second quantity store exists.
- [ ] **T09-032 — Close with conservation proof.** Character, container and Loot flows now share one Inventory authority; closure remains gated on combined exact-SHA validation proving all authoritative mutations conserve quantities through that path.
