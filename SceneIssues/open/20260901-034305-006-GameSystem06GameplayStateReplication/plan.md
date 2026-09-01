# 06 Gameplay-state replication — implementation plan

**Target module:** `Assets/Game/GameplayReplication/Api` / `Runtime` (`Game.GameplayReplication.Api`, `Game.GameplayReplication.Runtime`). Existing voxel/network transport remains underneath when its production spine is available.

## Inventory / ownership

- `Packages/manifest.json` includes `com.unity.transport` 6.5.0 and intentionally does not include NGO, matching the approved transport direction.
- Repository/source inventory on the current `fixes/agent-2` head found no production UTP/server-authoritative spine to reuse: no `NetworkDriver`, `DataStreamWriter`/`DataStreamReader`, connection/session loop, authoritative network tick, gameplay serialization/frame codec, snapshot/delta publication, late-join/catch-up, repair/resync, prediction/reconciliation, or interest-management implementation is present under `Assets/`.
- This is an external prerequisite blocker for transport-dependent acceptance (`T06-010`, `T06-016` repair integration, `T06-017`, `T06-018`, `T06-022`–`T06-024`). Acceptance is unchanged. Agent-2 will not create a second transport or silently invent the missing network spine.
- Independent work remains valid: define engine-neutral replication contracts/revision semantics/readiness, deterministic semantic projection state, subsystem adapters over existing gameplay APIs, and headless regressions. Runtime transport adaptation stays behind an explicit boundary so the missing UTP spine can plug in without changing gameplay contracts.
- Existing gameplay authority remains in its owning modules. Characters, Combat, Encounters, Inventory and progression/quest APIs are candidate projection sources; replication may snapshot/project them but must not mutate or replace their authority.

## API

Authoritative gameplay revision, typed snapshot/delta contracts for registered subsystem projections, synchronization/readiness state, and read-only client semantic state. Do not expose transport packet types as gameplay contracts.

A coherent authoritative revision is one publication barrier: all registered required subsystem projections in that publication share the same monotonic gameplay revision. Clients expose a revision as current only after the complete required set for that revision is applied. Duplicate or older revisions are harmless; a forward gap is detected and exposed as requiring repair rather than guessed through.

Subsystem identity/versioning is semantic and stable. Projection producers depend on replication API contracts only through adapters/composition; owning gameplay modules never depend on replication Runtime.

`GameplayReady` requires a complete coherent snapshot/revision for every configured required projection and no outstanding revision gap. Transport/socket connectivity alone is insufficient.

## Runtime

1. Define subsystem projection/adaptation seams so Characters, Vitality, Encounters, Combat, Inventory, Progression, Session identity, and Outcomes contribute state without depending on replication Runtime.
2. Reuse the existing authoritative UTP/network tick, serialization, ordering, late-join, catch-up, and repair foundations **when that production spine lands**; this assignment will not create a replacement transport.
3. Add transport-independent server publication and client application of gameplay snapshots/deltas with deterministic revision ordering.
4. Build current-state convergence semantics so late-join/reconnect restoration consumes authoritative current snapshots and never replays historical one-shot events. Transport-driving integration remains blocked on the missing network spine.
5. Expose `GameplayReady` only after required projections converge.

## Tests / proof

In-process deterministic projection tests, stale/gap/dedupe tests, independent subsystem fixtures, restore/current-state convergence semantics, and headless operation can run without presentation or transport. Existing-UTP loopback, two-client convergence, late join and reconnect gates remain blocked until the prerequisite production network spine exists.

## Do not build

No second transport, NGO adoption, UI state replication, or subsystem-specific authority inside this module. Do not weaken or reinterpret transport-dependent acceptance because the prerequisite is missing.
