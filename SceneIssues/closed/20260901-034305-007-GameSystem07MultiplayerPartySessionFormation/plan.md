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
- Transport-neutral `IAuthoritativePlayerAdmission` plus Sessions adapter maps stable slots to transient network actor IDs without absorbing reconnect policy.
- Focused headless tests cover 2/3/4/6 capacity, identity continuity, readiness, JIP compatibility, automatic/explicit leader transfer, character uniqueness, provider reuse, admission rollback, and connection rebinding.

## Validation / blast radius
- First exact slice: run `33501748546`, Sessions 11/11 plus mandatory Kentridge green.
- Final exact feature parent `c31ac0e77a12eab5feb38ec72d5d080a9796d639`: request `98cb663e112a12eff7570707b70d194d4a61115a`, run `33504004316` green.
- Automatic diff planning selected only changed production modules `Assets/Game/Sessions` and `Assets/VoxelEngine/Net`, their EditMode suites, plus mandatory game-integration Kentridge player validation.
- Final results: Game.Sessions.Tests 17/17; VoxelEngine.Net.Tests.EditMode 93/93; Kentridge player validation green; 212.04s automatic validation total (73.33s Sessions, 9.30s Net, 129.41s player).
- No UTP/socket type leaks into Sessions.Api; connection identity remains Runtime-only and reconnect remains owned by VoxelEngine.Net.
- Durable identity proof: `SessionNetworkAdmissionAdapterTests.DurableMemberSlotAndCharacterSurviveConnectionChanges` rebinds connection 11 -> 99 while preserving the same `PartyMemberId`, `PlayerSlot`, and `CharacterId`.

All required gates and acceptance items are complete.
