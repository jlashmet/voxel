# 07 — Multiplayer Party & Session Formation

## Decision

Provide a player/session orchestration layer above the repository's existing authoritative Unity Transport networking stack. This system owns party membership, stable player-slot assignment, session formation, launch readiness, and the path from a discovered/invited player into synchronized gameplay.

It must not become a second networking stack, and party leadership must remain distinct from gameplay/server authority.

## Existing foundation

The existing authoritative server networking already accepts transport connections, authenticates a connection into a player identity, tracks connection open/close, and records player join/leave. The missing game-level layer is the stable party/session model and the flow that establishes which person/member a connection represents before gameplay begins.

## Core identities

Keep these identities separate:

- `GameSessionId` — one game session.
- `PartyMemberId` — stable identity of a member of that session.
- transport connection id — the member's current network connection, which may change after reconnect.
- `PlayerSlot` — stable authored/gameplay slot assigned to the member.
- `CharacterId` — gameplay character currently controlled by that slot/member.

Never use a transport connection id as the durable party or gameplay identity.

## Party/session model

A minimal session model should support:

- stable roster membership,
- connected/disconnected state,
- player-slot assignment,
- leader designation where needed for party actions,
- loading/synchronization readiness,
- semantic startup configuration such as campaign/content id and join-in-progress policy.

A simple session lifecycle is sufficient initially:

`Forming -> Launching -> InGame -> Completed`

Add states only when demonstrated by a real requirement.

## Party leader is not server authority

`PartyLeader` is a social/session role. It may authorize party actions such as starting the game or removing a member.

It must not imply authority over combat, vitality, encounters, inventory, quests, characters, or world state. Those remain authoritative server gameplay state.

This separation must remain valid if hosting topology changes later.

## Join-provider seam

Do not bake a specific discovery platform into gameplay/session code.

Use a semantic seam conceptually equivalent to:

`JoinRequest -> SessionConnectionInfo`

Possible providers can later include direct/LAN connection, invite code, platform friend invite, or an external lobby service. The party/session domain remains unchanged.

Public matchmaking, skill ratings, queues, regional fleet allocation, and global lobby browsers are not part of this design unless a future game requirement demands them.

## Join flow

The intended flow is:

1. Player selects or accepts a session.
2. Join information is resolved through the configured provider.
3. Transport connection opens.
4. The connection authenticates as a stable `PartyMemberId`.
5. The member receives or restores a stable `PlayerSlot`.
6. Gameplay-state replication performs authoritative snapshot synchronization.
7. The player's controlled `CharacterId` is resolved/created.
8. The member becomes gameplay-ready.

Joining the party and becoming gameplay-active are deliberately separate transitions.

A useful readiness flow is:

`Joining -> Connected -> Synchronizing -> GameplayReady`

## Player slots

The session layer owns stable `PartyMemberId -> PlayerSlot` assignment.

Other systems resolve `PlayerSlot -> CharacterId` rather than depending on transport connections. This supports cutscenes, campaign composition, player spawn bindings, inventories, reconnect, and UI.

## Fresh-session launch barrier

Starting a new game is an explicit coordinated transition:

1. Party is formed.
2. Session start is requested by authorized party/session policy.
3. Required clients load required content.
4. Required members report ready.
5. Authoritative gameplay session starts.
6. Characters are assigned/spawned and gameplay-state synchronization completes.

Do not allow simulation to begin merely because one client finished loading first.

## Join in progress

Join-in-progress is session/game policy, not a transport assumption.

When enabled, a new member can join an already-running session, authenticate, receive the current gameplay snapshot from the gameplay-state replication layer, receive/restore a player slot and character, and then become gameplay-ready without disrupting existing members.

Campaign-specific restrictions may disable joining during selected phases without changing the shared party/session architecture.

## Compatibility handshake

Before admitting a member into synchronized gameplay, validate the compatibility facts that materially affect deterministic/authoritative behavior, such as:

- network protocol compatibility,
- game/content compatibility,
- required campaign/content availability.

Fail early with a semantic reason rather than allowing an incompatible client to partially enter gameplay.

## Events

Expose semantic session events such as:

- `PartyMemberJoined`
- `PartyMemberLeft`
- `PartyMemberConnectionChanged`
- `PlayerSlotAssigned`
- `GameSessionStarted`
- `GameSessionCompleted`

UI and gameplay composition observe these events rather than polling low-level transport state.

## Relationship to reconnect

This system owns normal formation and joining. The reconnect system owns interruption recovery.

Both share the same stable `PartyMemberId`. An unexpected disconnect must not destroy the member identity merely because its current transport connection disappeared.

## Explicitly out of scope

- transport implementation or replacement,
- gameplay-state replication internals,
- reconnect/grace-period recovery,
- public matchmaking unless later required,
- gameplay authority,
- character/combat/vitality rules,
- teammate/session UI presentation.

## Reuse / acceptance proof

### Fresh party

1. Create a session.
2. Multiple members join through the join abstraction.
3. Each receives a unique stable member identity and player slot.
4. Required members synchronize and report ready.
5. Session starts once the readiness barrier is satisfied.
6. Each member controls the correct gameplay character.

### Join in progress

1. Start a running session with existing players.
2. A new member joins without interrupting existing gameplay.
3. The member authenticates into the party/session model.
4. Gameplay-state replication supplies the authoritative current snapshot.
5. The correct slot/character becomes active.
6. All clients converge on the same authoritative gameplay state.
