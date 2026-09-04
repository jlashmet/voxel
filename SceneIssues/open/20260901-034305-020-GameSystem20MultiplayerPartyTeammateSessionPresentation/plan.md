# 20 Multiplayer party, teammate & session presentation — implementation plan

**Target module:** `Assets/Game/SessionPresentation/Api` / `Runtime` (`Game.SessionPresentation.Api`, `Game.SessionPresentation.Runtime`).

## Observed behavior / acceptance

Current master has no SessionPresentation module and no connection-indexed lobby/HUD member-row implementation to migrate. Sessions already owns durable `PartyMemberId`, `PlayerSlot`, leader/presence/readiness and `CharacterId`; Continuity owns `RecoveryState`; GameplayReplication exposes per-member synchronization/`GameplayReady`. `PartySession` keeps transport handles runtime-private. Acceptance therefore requires a new read-only semantic projection, deterministic row identity/order, semantic intents, shared rich/compact views, regressions, and module-local built-player proof.

## Hypotheses / discriminating result

1. **Selected:** existing public authority APIs are sufficient for presentation identity/state. Inventory confirmed all durable identity and recovery/replication truth are already public; no transport dependency is needed.
2. **Falsified:** an existing raw lobby/HUD path must be migrated. Repository search found no current connection/socket/GameObject-keyed party presentation consumer; Systems 17/23 can consume the new semantic contracts instead.

## Selected design

- `Game.SessionPresentation.Api`: immutable member/session/compact snapshots, stable presentation categories, rich + compact read-only query contracts, display-metadata resolver contract, and semantic ready/start/leave intents.
- `Game.SessionPresentation.Runtime`: project `IPartySessionQuery` + `IContinuityQuery` + `IGameplayReplicationClientState`; order by `PlayerSlot`, then `PartyMemberId`; preserve identity through reconnect/rebuild; route intents through a Sessions-owned application command API only.
- Sessions gets only the acceptance-required semantic command/status seam. Member “ready to start” remains distinct from replication `GameplayReady`; existing transport/gameplay readiness semantics are not weakened.
- Compact teammate status carries `CharacterId` as the stable health lookup reference rather than importing mutable Vitality/HUD state.
- Add module-local EditMode regressions plus `SessionPresentation/Validation/` standalone scene/scenario exercising the real projection and visual consumer.

## Remaining gates

Implement API/runtime + Sessions seam; prove reconnect, connected-vs-ready, explicit leave, ordering/binding, rich/compact sharing, intent routing and boundary searches; exact-SHA targeted CI with repository-derived module/player validation; inspect built-player evidence; then complete tasks, close, sync master, PR + auto-merge.

## Non-goals

No chat, matchmaking browser, transport control, raw disconnect reasons/timers, gameplay authority, or screen-owned mutable party state.
