# 07 Multiplayer party & session formation — implementation plan

**Target module:** `Assets/Game/Sessions/Api` / `Runtime` (`Game.Sessions.Api`, `Game.Sessions.Runtime`).

## API

`GameSessionId`, `PartyMemberId`, `PlayerSlot`, roster/member state, leader role, readiness state, startup configuration, join request/result, provider seam, and session lifecycle events. Transport connection id must stay outside durable identity.

## Runtime

1. Implement stable party roster and player-slot allocation.
2. Add semantic join-provider abstraction yielding connection information without coupling to matchmaking technology.
3. Authenticate a transport connection into a durable party member.
4. Coordinate launch/readiness barrier with #06 and #14; socket-connected is not GameplayReady.
5. Support configured join-in-progress policy while preserving existing members.
6. Publish snapshots/events for #20 and persistence hooks where required.

## Dependencies

Existing transport/network API, 03 Characters API for eventual slot-to-character binding, 06 replication readiness. #08 builds recovery on these durable identities.

## Tests / proof

Fresh 2-4 member formation, unique slots, readiness barrier, join-in-progress, incompatible join rejection, and headless provider fixture.

## Do not build

No global matchmaking, social platform integration, gameplay authority, or reconnect policy here.
