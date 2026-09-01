# 06 Gameplay-state replication — implementation plan

**Target module:** `Assets/Game/GameplayReplication/Api` / `Runtime` (`Game.GameplayReplication.Api`, `Game.GameplayReplication.Runtime`). Existing voxel/network transport remains underneath.

## API

Authoritative gameplay revision, typed snapshot/delta contracts for registered subsystem projections, synchronization/readiness state, and read-only client semantic state. Do not expose transport packet types as gameplay contracts.

## Runtime

1. Define subsystem projection/adaptation seams so Characters, Vitality, Encounters, Combat, Inventory, Progression, Session identity, and Outcomes contribute state without depending on replication Runtime.
2. Reuse the existing authoritative UTP/network tick, serialization, ordering, late-join, catch-up, and repair foundations.
3. Add server publication and client application of gameplay snapshots/deltas with deterministic revision ordering.
4. Build late-join/reconnect current-state convergence; do not replay historical one-shot events to reconstruct truth.
5. Expose `GameplayReady` only after required projections converge.

## Tests / proof

In-process deterministic projection tests, existing UTP loopback integration, two-client convergence, late join, stale delta rejection, resync, and headless operation.

## Do not build

No second transport, NGO adoption, UI state replication, or subsystem-specific authority inside this module.
