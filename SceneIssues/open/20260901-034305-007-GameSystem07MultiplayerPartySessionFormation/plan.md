# 07 Multiplayer party & session formation — implementation plan

**Target:** `Assets/Game/Sessions/Api` + `Runtime`.

## Acceptance / ownership
Durable `GameSessionId -> PartyMemberId -> PlayerSlot -> CharacterId` identity belongs to Sessions. Transport connection identity is runtime-only plumbing and never appears in API snapshots. Sessions.Api may reference Characters.Api for the controlled character identity, but contains no UTP/socket types. Sessions.Runtime may depend on Net.Api and Characters.Api. Reconnect policy stays outside Sessions.

## Inventory / hypotheses
Observed legacy network state: `ServerPlayerRegistry` indexes `PlayerSession` by `uint connectionId` and `ushort playerId`; `AuthoritativeServerSession.AuthenticateConnection` accepts both and disconnect immediately removes that player mapping. `Reconnect` is transport repair policy and must not move into Sessions. No `Assets/Game/Sessions` module existed.

Hypothesis A: extend `ServerPlayerRegistry` into party authority. Rejected because it couples durable roster identity to transport lifetime and lower-level networking.

Hypothesis B: add a game-level Sessions authority with a separate ephemeral connection association, then adapt networking to it. Selected; this preserves network ownership while giving systems 08/20/persistence a durable identity seam.

## Selected implementation
- Stable serialization-safe session/member/slot values and transport-neutral roster/join/provider/lifecycle contracts.
- Configured capacity/version/content/JIP/leader policy.
- Deterministic roster + lowest-free-slot allocation; member IDs never reused in-session.
- Runtime-only opaque connection handle; reconnect/rebind preserves member/slot/character.
- Semantic readiness progression Joined -> Connected -> Synchronized -> GameplayReady.
- Characters.Api binding through `party-member:<PartyMemberId>` semantics.
- Focused headless tests cover 2/3/4/6 capacity, identity continuity, readiness, JIP compatibility, leader transfer, character uniqueness and provider reuse.

## Remaining gates
1. Migrate legacy socket/player-id gameplay identity consumers behind Sessions without absorbing reconnect policy.
2. Run exact-SHA automatic Sessions/network-dependent validation and inspect any built-player gate selected by CI.
3. Audit API/runtime boundaries, blast radius, and close only after every checklist item is proven.
