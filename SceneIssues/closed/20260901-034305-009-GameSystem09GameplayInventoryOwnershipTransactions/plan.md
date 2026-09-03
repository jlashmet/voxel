# 09 Gameplay inventory ownership & transactions — implementation plan

**Target ownership:** evolve existing `Assets/Game/Inventory/Api` / `Runtime`; do not create another inventory module.

## API

Stable `InventoryId`, item identity/type semantics already supported by the repository, inventory snapshot, transaction request/result, add/remove/transfer operations, change events, and ownership/binding metadata only where required.

## Runtime

1. Generalize current deterministic inventory implementation from prototype/use-case assumptions to stable inventory instances.
2. Make all mutations authoritative transactions with explicit success/failure and conservation rules.
3. Support character and container inventories through the same runtime.
4. Remove any direct collection mutation from callers once transaction parity exists.
5. Add deterministic capture/restore and #06 projection adapters.

## Dependencies

03 Characters API for character-owned binding when needed; otherwise keep inventory generic. #10 consumes Inventory API.

## Tests / proof

Add/remove/transfer, insufficient quantity, duplicate/racing requests, character and container consumers, deterministic snapshots, restore, and item conservation.

## Do not build

No equipment, crafting, slot grids, weight/capacity, use/consume semantics, or UI unless separately designed.

## Execution notes / baseline audit (2026-09-01)

- Baseline `Game.Inventory.Api` exposed `ItemRef`, `ItemDefinition`, `InventoryItemSnapshot`, and a mutable `IInventoryRuntime`; baseline `Game.Inventory.Runtime.InventoryRuntime` was one definition-backed quantity dictionary with `TryAddUnique`/`Add` mutations.
- The Kentridge campaign composition created the runtime and the well-quest reward called `TryAddUnique`; the playable inventory presentation read `Count`/`Snapshot`. No second authoritative inventory collection was found in the assigned integration path.
- `Game.GameplayReplication.Adapters.InventoryGameplayProjectionSource` consumed the legacy mutable interface even though it only read inventory truth; this was a boundary gap to migrate to the new query API.
- `Game.Composition.Kentridge.Runtime` is the composition root that constructs `Game.Inventory.Runtime`; player-facing code does not need the Runtime assembly and its direct reference is removed.
- `Game.Loot` / System10 was absent from master during implementation, so Inventory exposes only the semantic API seam System10 needs and does not create Loot locally.
- No standalone `Game.Persistence` module is present; capture/restore is therefore an Inventory API seam plus deterministic runtime implementation, ready for System16 rather than inventing persistence transport.

## Implementation evidence

- `InventoryId` is stable and owner-agnostic; generic `InventoryBindingMetadata` is supplied by composition, with character and container fixtures proving the same runtime path.
- `IInventoryQuery`, `IInventoryAuthority`, and `IInventoryStatePort` separate read truth, authoritative mutation, and capture/restore without exposing Runtime collections.
- Add/remove/transfer are serialized under one authority gate, return explicit results/revisions, publish committed change events, reject invalid or insufficient mutations, and journal transaction ids so duplicate delivery cannot double-apply.
- Kentridge reward mutation now uses `IInventoryAuthority`; the playable presentation uses the query seam; only Kentridge composition constructs the concrete Runtime.
- GameplayReplication projects deterministic multi-inventory snapshots solely through `IInventoryQuery` and no longer depends on the mutable Runtime contract.
- `Game.Inventory.Tests.InventoryTransactionTests` covers invalid/unknown inputs, insufficient removal, successful and failed conservation, duplicate/conflicting ids, competing removals, character/container reuse, and deterministic capture/restore ordering and revisions.
- The System16 seam is intentionally limited to deterministic InventoryId/content state assigned here. Persisting transaction-delivery infrastructure is not added without the absent Persistence/System16 contract.
- After System10 reached master, its temporary dictionary-backed `InventoryTransactionsRuntime` was removed. `InventoryTransactionsAdapter` now supplies the cross-domain convenience seam without owning quantity state: it delegates mutations to `IInventoryAuthority`, reads to `IInventoryQuery`, and restore to `IInventoryStatePort`. Loot therefore shares the exact same Inventory authority as character/container flows.

## Validation history

- Initial CI exposed stale dependent callers in GameplayReplication and Kentridge; both were migrated to the assigned API boundary. Three subsequent retries were proven runner-memory infrastructure failures, not product failures.
- Exact-source run `33637591593` against `1cc6a9524fc11869632a2263a3372250717585ed` completed successfully. Repository-selected automatic validation executed Inventory and affected dependent modules; the Inventory transaction suite, Kentridge player integration, and standalone SceneIssue replay all passed.
- Post-green audits completed T09-030/T09-031: one Inventory authority/runtime remains; Kentridge mutations use `IInventoryAuthority`; readers use `IInventoryQuery`; Runtime dictionaries remain private; no equipment/crafting/slot/capacity/use/UI policy was added.
- Required master synchronization completed at source `43cec11cb7a57b94bd116ec18903ae9a1dcdc7cd`, incorporating `origin/master` `f5593cc1236ba3963fc5713a11df35292628e97d`. The inherited `.github/test-request.json` from master was removed again from the feature branch as required.
- Post-sync run `33802369720` completed successfully for source `43cec11cb7a57b94bd116ec18903ae9a1dcdc7cd`; automatic module validation and standalone SceneIssue replay both passed.
- System10/Loot run `33800800347` completed successfully before promotion. System10 was subsequently promoted to authoritative `origin/master` `149d7f85cc3fc293fb0abcaf9cb950346bb0aee5` and merged into agent-2 via true two-parent merge `44c24f73cfde809a9546d6e4dc5a1540f2c00035`.
- The authoritative merge exposed the expected duplicate Inventory transaction store. Acceptance-driven reconciliation removed that store, retained the richer T09 transaction/result/failure schema, added only the semantic `IInventoryTransactions` convenience seam required by Loot, and migrated Loot regressions to the stateless adapter over `InventoryRuntime`.
- Final combined exact-SHA run `33809208718` completed successfully for feature source `ca02da344946f45ec5ccfc045bb97145e877bfe5`. Its repository-selected automatic module validation and standalone SceneIssue replay both passed, and no manual test override was used. This closes the former T09-025/T09-032 validation gate with Inventory and dependent Loot on one authoritative runtime path.
- Before closure, current `origin/master` advanced from the integrated `149d7f85cc3fc293fb0abcaf9cb950346bb0aee5` to `81ffa4bbc76c3feb6e0bde2376065b4144f3f10a`. The intervening changes are Combat/Vitality and Kentridge combat composition/tests only; they do not modify Inventory, Loot, or this SceneIssue. The closure sequence therefore records the green exact-SHA proof first, then merges that current master as required before non-force promotion.
