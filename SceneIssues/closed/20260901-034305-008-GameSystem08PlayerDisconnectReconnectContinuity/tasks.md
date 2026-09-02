# 08 Player disconnect, reconnect & continuity — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Continuity.Api` / `Game.Continuity.Runtime`
**Execution rule:** Sessions owns durable member/slot identity; Continuity owns interruption/recovery policy around temporary connections.

## API / policy

- [x] **T08-001 — Inventory current disconnect/reconnect behavior.** Sessions drops only ephemeral connection association and preserves member/slot/CharacterId; lower Net removes transient authenticated network-player state.
- [x] **T08-002 — Establish asmdefs.** Continuity references the API-only `Game.GameplayReplication.Api` scaffold derived directly from system-06 binding gameplay design; no GameplayReplication Runtime implementation was added. Final exact CI discovered owned Continuity and GameplayReplication modules with no fallback.
- [x] **T08-003 — Define recovery state machine.** Connected, ConnectionInterrupted, Reconnecting, Resynchronizing, Recovered, Expired and Left plus guarded transitions.
- [x] **T08-004 — Define reconnect request/result.** Durable session/member credential plus opaque token; no socket/connection identity in API.
- [x] **T08-005 — Define continuity policy.** Configured grace and fast-repair window with validation; no transport hardcoded timing.
- [x] **T08-006 — Define presence/recovery events.** Semantic continuity event stream and recovery query keyed by PartyMemberId.

## Runtime

- [x] **T08-010 — Observe unexpected transport loss.** Coordinator enters interruption without mutating Sessions membership/slot/CharacterId.
- [x] **T08-011 — Reserve durable identity through grace.** Sessions member remains authoritative while Continuity holds recoverable credential/state; no replacement member allocation occurs.
- [x] **T08-012 — Reauthenticate new connection.** Runtime admission seam binds a new opaque connection to the existing member snapshot; Sessions-backed independent fixture rebinds 11 -> 99 with unchanged durable identity.
- [x] **T08-013 — Select repair vs full resync.** Final exact CI proves fast-window recovery requests semantic `Repair` and slow recovery requests `FullSnapshot` through `IGameplayReplicationClientState`; transport/serialization implementation remains system-06-owned.
- [x] **T08-014 — Gate input authority on GameplayReady.** Final exact CI proves recovery cannot complete until the replication API reports `GameplayReady` at a valid authoritative revision.
- [x] **T08-015 — Handle authoritative changes while absent.** Final exact CI proves typed vitality/inventory/progression current state advances from revision 1 to revision 2 while absent and revision 2/current truth is observed after full resync. Production replication remains system-06-owned.
- [x] **T08-016 — Implement explicit Leave Game separately.** Leave invalidates recovery immediately and hands removal semantics to owning systems; it never enters reconnect grace.
- [x] **T08-017 — Implement grace expiration.** Expiration invalidates old credentials and deterministically hands final cleanup to owning systems without fabricating identity.

## Verification

- [x] **T08-020 — Fast reconnect test.** Final `Game.Continuity.Tests` verifies connection 11 -> 99 with unchanged PartyMemberId/PlayerSlot/CharacterId, Repair selection, and GameplayReady gating.
- [x] **T08-021 — Full-resync reconnect test.** Final exact CI verifies FullSnapshot selection and current-state convergence without changing durable identity.
- [x] **T08-022 — Absent-state mutation test.** `StateMutatedWhileAbsentIsReadAsCurrentTruthAfterFullResync` passed in final exact CI: revision 1 is replaced by revision 2 while absent and reconnect observes revision 2 values.
- [x] **T08-023 — Duplicate-character regression.** Final exact CI proves repeated reconnect cannot create another roster member/CharacterId.
- [x] **T08-024 — Explicit leave test.** Final exact CI passes explicit-leave regression.
- [x] **T08-025 — Grace-expiration test.** Final exact CI passes expiration/credential-invalidation regression.
- [x] **T08-026 — Run automatic Continuity/Sessions/Replication tests.** Exact run `33520630916` on tested source `e897c2b298a02395ea1425f9c3fb070d04535431`: Continuity 7/7, GameplayReplication API consumer 3/3, mandatory Kentridge built-player green, no fallback paths, total 213.45s.

## Cleanup / close

- [x] **T08-030 — Remove transport-owned identity policy.** Audit found lower-level disconnect only removes transient network-player state; Sessions preserves gameplay identity. Continuity API exposes no socket id and runtime policy is keyed by PartyMemberId.
- [x] **T08-031 — Boundary audit.** Continuity contains no persistence-across-runs or AI takeover policy; terminal leave/expiration policy is handed to owning systems. The local GameplayReplication contribution is API-only and contains no transport/runtime implementation.
- [x] **T08-032 — Close with continuity proof.** Final exact run `33520630916` proves durable identity, repair/full-snapshot recovery, GameplayReady gating, state convergence after absence, narrow module ownership, and built-player integration.
