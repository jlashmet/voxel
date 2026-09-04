# 20 Multiplayer party, teammate & session presentation — implementation plan

**Target module:** `Assets/Game/SessionPresentation/Api` / `Runtime` (`Game.SessionPresentation.Api`, `Game.SessionPresentation.Runtime`).

## Observed behavior / acceptance

Baseline had no SessionPresentation module and no connection-indexed lobby/HUD member-row consumer. Sessions already owns durable `PartyMemberId`, `PlayerSlot`, leader/presence/readiness and `CharacterId`; Continuity owns `RecoveryState`; GameplayReplication owns per-member synchronization/`GameplayReady`. `PartySession` keeps transport handles runtime-private.

## Hypotheses / result

1. **Selected:** existing public authority APIs are sufficient for durable presentation identity/state. Exact-SHA tests and player validation confirmed reconnect/rebuild behavior without transport identity.
2. **Falsified:** a raw lobby/HUD path needed migration. No socket/connection/GameObject-keyed party presentation consumer exists on current master; Systems 17/23 are not yet present there.

## Selected implementation

- `Game.SessionPresentation.Api` owns immutable rich/compact snapshots, stable presentation categories, display metadata, read-only query contracts, and semantic ready/start/leave intents.
- `Game.SessionPresentation.Runtime` projects Sessions + Continuity + GameplayReplication + Characters API truth, ordered by `PlayerSlot` then `PartyMemberId`; no transport/runtime dependency.
- Sessions owns the acceptance-required application command/query seam. Explicit user ready is distinct from replication `GameplayReady`; leader start requires both.
- Compact teammate rows carry stable `CharacterId` as the health lookup reference rather than importing mutable HUD/Vitality state.
- Module-local validation uses the real projector with bounded deterministic authority inputs; final screen shows the same three durable rows in rich and compact views.

## Validation result

Exact source `1312f423d4a92a3948404eaf415764eb425952a6` passed transport `9e8cd8387e5447356cad2059070a88974949647f`, workflow `33868484121`. Repository-selected EditMode assemblies for Continuity, GameplayReplication, SessionPresentation and Sessions passed; requested SessionPresentation test completed in 14.35s. Module player validation passed in 59.1s and canonical Kentridge integration passed in 100.9s. Player logs proved initial three-member projection, same-row reconnect for `party:bravo` slot 1 / `character:bravo`, and `ReadyToStart canStart=True`. Captures clearly show connected vs synchronization state and identical rich/compact member identity. Validation UI is focused instrumentation; final Systems 17/23 screen art remains owned by those consumers.

## Non-goals / audit

No chat, matchmaking browser, transport control, raw disconnect reasons/timers, gameplay authority, or screen-owned mutable party state was added.
