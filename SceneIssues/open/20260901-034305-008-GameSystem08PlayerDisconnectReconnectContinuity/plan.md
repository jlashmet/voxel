# 08 Player disconnect, reconnect & continuity — implementation plan

**Target module:** `Assets/Game/Continuity/Api` / `Runtime` (`Game.Continuity.Api`, `Game.Continuity.Runtime`). Durable party/slot identity remains owned by `Game.Sessions.Api`.

## API

Recovery state (`Connected`, `ConnectionInterrupted`, `Reconnecting`, `Resynchronizing`, `Recovered`, `Expired`, `Left`), reconnect request/result, continuity policy/grace configuration, and semantic presence changes keyed by `PartyMemberId`.

## Runtime

1. Observe transport loss without deleting Sessions membership or character binding.
2. Reserve member/slot/CharacterId through configured continuity grace.
3. Reauthenticate a new transport connection to the existing durable member.
4. Select fast repair versus full resynchronization using existing network capabilities; this choice is an optimization, not identity policy.
5. Restore input authority only after #06 GameplayReady.
6. Distinguish explicit LeaveGame from interruption/recovery.

## Dependencies

07 Sessions, 06 GameplayReplication, 03 Characters, existing network reconnect/late-join mechanisms.

## Inventory / evidence

- `Game.Sessions.Runtime.PartySession.Disconnect` removes only its runtime connection association and resets presence/readiness; it retains `PartyMemberId`, `PlayerSlot`, and `CharacterId`. Rebinding a new connection is already identity-preserving.
- `VoxelEngine.Net.Runtime.Server.AuthoritativeServerSession` / `ServerPlayerRegistry` remove the authenticated network player record on transport close. That record is transient network actor state, not durable party identity.
- System 07 provides the durable Sessions identity seam and transport-neutral network admission seam; Continuity must layer grace/recovery policy above them rather than moving reconnect policy into Sessions or socket callbacks.
- **External prerequisite blocker:** current `origin/master` contains no `GameplayReplication.Api`, `GameplayReady`, or gameplay replication/resync API from system 06. Therefore the required T08-002 dependency, real repair/resync capability selection, GameplayReady authority gate, absent-state current-truth proof, and combined Replication validation cannot be completed yet. Acceptance is unchanged.

## Independent implementation while #06 is unavailable

- Transport-neutral reconnect credentials contain only `GameSessionId`, `PartyMemberId`, and an opaque token; runtime connection handles remain in `Game.Continuity.Runtime`.
- `ContinuityCoordinator` owns interruption/grace/recovery state and deterministic fast-window versus full-resync intent, while querying Sessions as the identity authority.
- `IReconnectTransportAdmission` is a runtime composition seam for rebinding the new transport to the existing member; an independent Sessions-backed fixture proves reuse without creating another member/character.
- Explicit leave and grace expiration invalidate recovery and hand terminal cleanup to owning systems through `IContinuityTerminalPolicySink`; Continuity does not invent removal, persistence, or AI policy.
- `MarkGameplayReady` is intentionally a semantic completion hook only; wiring it to system-06 current-state synchronization remains blocked until that API exists.

## Tests / proof

Independent regressions cover brief reconnect with connection change, full-resync path selection after the fast window, duplicate reconnect rejection, invalid credential rejection, explicit leave, expiration, and durable member/slot/character preservation. System-06-dependent current-state mutation and real GameplayReady gating remain required blockers.

## Do not build

No AI takeover unless separately configured through #04, no persistence-across-runs semantics, no socket-id identity.
