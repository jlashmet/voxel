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
- `Game.Loot` / System10 is not present on current master, so automatic dependent Loot validation cannot be run until that external prerequisite exists. Inventory exposes only the API seam System10 needs and does not create Loot locally.
- No standalone `Game.Persistence` module is present; capture/restore is therefore an Inventory API seam plus deterministic runtime implementation, ready for System16 rather than inventing persistence transport.

## Implementation evidence (2026-09-02)

- `InventoryId` is stable and owner-agnostic; generic `InventoryBindingMetadata` is supplied by composition, with character and container fixtures proving the same runtime path.
- `IInventoryQuery`, `IInventoryAuthority`, and `IInventoryStatePort` separate read truth, authoritative mutation, and capture/restore without exposing Runtime collections.
- Add/remove/transfer are serialized under one authority gate, return explicit results/revisions, publish committed change events, reject invalid or insufficient mutations, and journal transaction ids so duplicate delivery cannot double-apply.
- Kentridge reward mutation now uses `IInventoryAuthority`; the playable presentation uses the query seam; only Kentridge composition constructs the concrete Runtime.
- GameplayReplication projects deterministic multi-inventory snapshots solely through `IInventoryQuery` and no longer depends on the mutable Runtime contract.
- `Game.Inventory.Tests.InventoryTransactionTests` covers invalid/unknown inputs, insufficient removal, successful and failed conservation, duplicate/conflicting ids, competing removals, character/container reuse, and deterministic capture/restore ordering and revisions.
- The System16 seam is intentionally limited to deterministic InventoryId/content state assigned here. Persisting transaction-delivery infrastructure is not added without the absent Persistence/System16 contract.
- Final repository-wide bypass/boundary claims remain pending the repository-selected automatic module validation. System10/Loot remains an external prerequisite and is not implemented or simulated by this assignment.

## Validation history

- Exact-source run `33636160073` against `f2f3867a1507a1703e021fd3bc9f7c00b4fe0675` completed failed. The uploaded persistent Unity log isolated stale dependent regressions: `GameplayReplicationRuntimeTests` still implemented removed `IInventoryRuntime`/`InventoryItemSnapshot`, and `KentridgeWellQuestInventoryTests` still constructed/called the legacy single-inventory API. The standalone replay failure in the same run was secondary runner memory pressure after module validation was killed.
- Migrated those dependent tests to the assigned API boundary: GameplayReplication now uses an independent `IInventoryQuery` fixture and validates schema-v2 inventory keys; Kentridge PlayMode constructs a stable inventory descriptor, uses the authority/query APIs, and binds presentation with the explicit InventoryId. Retry must use the same `ci-test/fixes/agent-2` transport.
- Runs `33636561791`, `33636808865`, and `33636914897` against exact migrated source `a0626a789a7f320efda43e14f677e72d692a6352` all completed as proven runner-memory infrastructure failures before Unity compilation/tests could execute. The runner respectively refused at 998 MB free, killed Unity after free memory fell below the 8192 MB floor from 15.7 GB, and started with only 5.3 GB free. No further code change is justified by those runs; the exact gate remains blocked until the runner can execute Unity normally.
- Static boundary audit while CI is blocked: playable Kentridge and GameplayReplication reference `Game.Inventory.Api` only; the sole production `Game.Inventory.Runtime` dependency is the Kentridge composition root that constructs the authority. Inventory API/runtime add no equipment, crafting, slot, capacity, or generic UI policy. Final T09-030/T09-031 completion remains gated on a successful repository compile/automatic validation to catch any stale whole-repository callers.
- Exact-source retry run `33637591593` against source `1cc6a9524fc11869632a2263a3372250717585ed` completed successfully. Repository-selected automatic validation executed `Game.Inventory.Tests`, `Game.GameplayReplication.Tests`, and `Game.Continuity.Tests`; the requested `Game.Inventory.Tests.InventoryTransactionTests` suite ran; the Kentridge real-player integration validation and standalone SceneIssue replay both built and ran successfully; commit status `ci/single-test` is success for that exact source SHA.
- Post-green repository audit completed T09-030/T09-031: the exact-source recursive tree contains one `Assets/Game/Inventory` API/runtime implementation; changed production callers do not maintain a duplicate authoritative item collection; Kentridge mutation routes through `IInventoryAuthority`; Kentridge presentation and GameplayReplication read through `IInventoryQuery`; Runtime-owned dictionaries remain private. No equipment, crafting, slots, capacity, consume/use, or generic UI policy was added. The only concrete `Game.Inventory.Runtime` production dependency is the Kentridge composition root, which is the required implementation construction boundary; reusable/playable consumers depend on `Game.Inventory.Api`.
- Current `origin/master` `b18d470f66221c7cb6091249f4683c2d994bffec` still returns no `Assets/Game/Loot` subtree. Therefore T09-025 cannot complete its required dependent Loot validation. This is an external System10 prerequisite, not a defect in the assigned Inventory implementation; acceptance remains unchanged and closure/T09-032 remain blocked until the dependency exists and its tests can run.
