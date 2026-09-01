# 08 Player disconnect, reconnect & continuity — implementation plan

**Target module:** `Assets/Game/Continuity/Api` / `Runtime` (`Game.Continuity.Api`, `Game.Continuity.Runtime`). Durable party/slot identity remains owned by `Game.Sessions.Api`.

## API

Recovery state (`Connected`, `ConnectionInterrupted`, `Reconnecting`, `Resynchronizing`, etc.), reconnect request/result, continuity policy/grace configuration, and semantic presence changes keyed by `PartyMemberId`.

## Runtime

1. Observe transport loss without deleting Sessions membership or character binding.
2. Reserve member/slot/CharacterId through configured continuity grace.
3. Reauthenticate a new transport connection to the existing durable member.
4. Select fast repair versus full resynchronization using existing network capabilities; this choice is an optimization, not identity policy.
5. Restore input authority only after #06 GameplayReady.
6. Distinguish explicit LeaveGame from interruption/recovery.

## Dependencies

07 Sessions, 06 GameplayReplication, 03 Characters, existing network reconnect/late-join mechanisms.

## Tests / proof

Brief reconnect, longer full resync, authoritative character state changes while absent, identity preservation, explicit leave, expiration, and no duplicate character creation.

## Do not build

No AI takeover unless separately configured through #04, no persistence-across-runs semantics, no socket-id identity.
