# 08 Player disconnect, reconnect & continuity — implementation plan

**Target module:** `Assets/Game/Continuity/Api` / `Runtime` (`Game.Continuity.Api`, `Game.Continuity.Runtime`). Durable party/slot identity remains owned by `Game.Sessions.Api`.

## API

Recovery state (`Connected`, `ConnectionInterrupted`, `Reconnecting`, `Resynchronizing`, `Recovered`, `Expired`, `Left`), reconnect request/result, continuity policy/grace configuration, and semantic presence changes keyed by `PartyMemberId`.

## Runtime

1. Observe transport loss without deleting Sessions membership or character binding.
2. Reserve member/slot/CharacterId through configured continuity grace.
3. Reauthenticate a new transport connection to the existing durable member.
4. Select fast repair versus full resynchronization through a semantic GameplayReplication API; this choice is an optimization, not identity policy.
5. Restore gameplay/input authority only after GameplayReplication reports `GameplayReady` at a valid authoritative revision.
6. Distinguish explicit LeaveGame from interruption/recovery.

## Dependencies

07 Sessions, 06 GameplayReplication API contract, 03 Characters, existing network reconnect/late-join mechanisms.

## Inventory / evidence

- `Game.Sessions.Runtime.PartySession.Disconnect` removes only its runtime connection association and resets presence/readiness; it retains `PartyMemberId`, `PlayerSlot`, and `CharacterId`. Rebinding a new connection is already identity-preserving.
- `VoxelEngine.Net.Runtime.Server.AuthoritativeServerSession` / `ServerPlayerRegistry` remove the authenticated network player record on transport close. That record is transient network actor state, not durable party identity.
- System 07 provides the durable Sessions identity seam and transport-neutral network admission seam; Continuity layers grace/recovery policy above them rather than moving reconnect policy into Sessions or socket callbacks.
- System-06 binding design defines `Game.GameplayReplication.Api` / Runtime ownership, monotonic authoritative gameplay revisions, typed snapshot/delta contracts, semantic synchronization/readiness state, current replicated truth, repair/resync, and `GameplayReady` only after required projections converge.

## API-only dependency strategy

The user explicitly authorized creating missing APIs from the binding SceneIssue gameplay design for testing, without implementing the owning system. T08 therefore adds only the minimal `Game.GameplayReplication.Api` contract needed for composition/testing:

- `GameplayRevision` for monotonic authoritative current-state revision.
- `GameplaySynchronizationPhase` / `GameplaySynchronizationStatus`, including semantic `GameplayReady`.
- `GameplayRecoveryMode` (`Repair` / `FullSnapshot`).
- Typed `GameplayProjectionSnapshot<TState>` current-state reads.
- `IGameplayReplicationClientState` for semantic recovery requests, readiness query, and typed current truth.

No `Game.GameplayReplication.Runtime`, transport loop, serialization, publication tick, delta application, UTP wiring, or system-06 implementation is added by T08.

## Continuity composition

- Transport-neutral reconnect credentials contain only `GameSessionId`, `PartyMemberId`, and an opaque token; runtime connection handles remain in `Game.Continuity.Runtime`.
- `ContinuityCoordinator` owns interruption/grace/recovery state and deterministic fast-window versus full-resync selection while querying Sessions as identity authority.
- `IReconnectTransportAdmission` is a runtime composition seam for rebinding the new transport to the existing member; an independent Sessions-backed fixture proves reuse without creating another member/character.
- After successful transport rebinding, Continuity requests `Repair` for fast recovery or `FullSnapshot` for full resynchronization through `IGameplayReplicationClientState`.
- `MarkGameplayReady` cannot complete recovery from transport state alone; it requires the replication API to report `GameplayReady` at a nonzero authoritative revision.
- Explicit leave and grace expiration invalidate recovery and hand terminal cleanup to owning systems through `IContinuityTerminalPolicySink`; Continuity does not invent removal, persistence, or AI policy.

## Tests / proof

Existing exact CI run `33506812126` validated the independent Continuity/Sessions core at feature parent `20d2e386e7fe9bd7b277ab339d5cc2b321dabb29`.

Fresh regressions now authored against the API-only GameplayReplication seam cover:

- fast reconnect requests `Repair`, preserves PartyMemberId/PlayerSlot/CharacterId across connection 11 -> 99, and cannot recover before `GameplayReady`;
- slow reconnect requests `FullSnapshot` without changing durable identity;
- typed vitality/inventory/progression current state is revision 1 before disconnect, mutates to revision 2 while absent, and revision 2/current values are what the reconnecting player observes after full resync and `GameplayReady`;
- duplicate reconnect, invalid credential, explicit leave, and grace expiration remain covered.

`Game.GameplayReplication.Tests` independently consumes the API-only scaffold and proves revision ordering, semantic readiness, and typed current-state projection without any GameplayReplication Runtime implementation.

## Validation attempts

- Exact run `33519266827` on feature `34aba4dc37ec81c33a4374895e29029b63d3066c` proved `Game.Continuity.Tests` **7/7** green, including repair/full-snapshot selection, GameplayReady gating, durable identity, and absent-state revision-2 convergence.
- That run later failed only because the new `GameplayReplication/Api` path had no independently discoverable owning test assembly, causing the fail-safe planner to choose a repository-wide fallback. The fallback exposed three unrelated pre-existing `Game.Materials.Tests` failures. This is not retried as infrastructure.
- Corrective action: add the independent `Game.GameplayReplication.Tests` assembly/fixture so the API scaffold is a discoverable module with its own validation consumer instead of an unknown fallback path. A new exact-SHA run is required.

## Do not build

No GameplayReplication Runtime implementation, second transport, AI takeover unless separately configured through #04, persistence-across-runs semantics, or socket-id identity.
