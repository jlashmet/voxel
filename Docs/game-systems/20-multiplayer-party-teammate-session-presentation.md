# 20. Multiplayer party, teammate & session presentation

**Status:** Approved

## Purpose

Provide player-facing presentation of party membership, teammate state, session readiness, connection interruption/recovery, and other multiplayer-session facts defined by systems 07 and 08.

The defining rule is:

> Multiplayer UI presents durable party/session identities and semantic continuity state; it never treats a socket, transport connection, or transient network event as the player.

Conceptually:

```text
system 07 party/session authority
system 08 continuity/reconnect state
system 06 replicated gameplay state
        |
        v
party/session presentation model
        |
        +--> lobby/session roster
        +--> in-game teammate HUD
        +--> reconnect/recovery presentation
        +--> menu/session widgets hosted by system 23
```

System 20 is the presentation layer for multiplayer/session semantics, not another networking or party-management implementation.

## 1. Bind rows and widgets by PartyMemberId

A teammate is identified by stable `PartyMemberId`.

Do not key UI state by:

- transport connection id;
- socket;
- endpoint;
- `NetworkConnection`;
- Unity GameObject instance;
- current `CharacterId` alone.

The durable relationship is:

```text
PartyMemberId
    -> PlayerSlot
        -> current CharacterId
```

with a temporary transport connection attached when connected.

A reconnect therefore updates the existing member's presentation instead of destroying one row and creating a supposedly new teammate.

## 2. One semantic party/session snapshot

System 20 should consume a coherent party/session read model rather than independently querying transport, characters, readiness, and reconnect systems for every row.

Conceptually:

```text
PartySessionSnapshot
    GameSessionId
    SessionLifecycle
    LocalPartyMemberId
    LeaderPartyMemberId?
    Members[]
```

with entries conceptually containing:

```text
PartyMemberSnapshot
    PartyMemberId
    PlayerSlot
    CharacterId?
    PresenceState
    ReadinessState
    presentation/profile metadata reference
```

Only fields actually owned by systems 07/08 belong in this semantic snapshot.

Character gameplay state such as vitality remains system 02/system 06 state and may be joined into a teammate presentation model by the client presentation layer.

## 3. Avoid a chatty per-member UI API

Do not create a UI flow equivalent to:

```text
GetPartyMembers()
for each member:
    GetConnectionState(member)
    GetSlot(member)
    GetCharacter(member)
    GetReady(member)
    GetLeader(member)
```

every frame.

The party/session API should expose the coherent semantic state necessary for replication, reconnect, and presentation.

Events/deltas can update an existing view, but a complete snapshot must remain available to reconstruct it.

## 4. Connection state and party membership are different

System 08 explicitly separates temporary connection loss from party departure.

Therefore:

```text
transport disconnected
    !=
party member left
```

When a teammate's connection drops unexpectedly, system 20 should preserve their party row and present semantic state such as:

```text
Connected
ConnectionInterrupted
Reconnecting
Resynchronizing
```

as exposed by system 08.

Only an authoritative/member-continuity transition such as explicit leave or reconnect expiry should remove or terminally mark the member according to session policy.

## 5. Do not expose raw networking machinery to players

The low-level networking implementation may know:

- packet rejection;
- region repair;
- authoritative event queues;
- snapshot catch-up;
- transport send errors;
- protocol messages;
- full-region resynchronization.

Those are diagnostics/implementation mechanics, not ordinary party UI state.

System 08 maps relevant recovery into semantic player-facing states.

So the UI displays concepts such as:

```text
Connection interrupted
Reconnecting
Synchronizing game
Unable to reconnect
```

rather than:

```text
RegionResyncRequired reason=2
UTP send error -5
64 pending authoritative events
```

Technical details may exist in developer/debug diagnostics, but they are not the production multiplayer presentation contract.

## 6. Joining and gameplay-ready remain distinct

System 07 defines the progression:

```text
Joining
    -> Connected
        -> Synchronizing
            -> GameplayReady
```

System 20 must preserve that distinction.

A connected player is not automatically ready to play.

Examples:

- party roster may show a member while still loading;
- teammate HUD should not present that player as fully gameplay-active before synchronization;
- local gameplay controls remain gated by the gameplay-ready barrier defined by systems 07/08/14.

Do not derive readiness from transport connection alone.

## 7. Fresh-session readiness presentation

Before a fresh session begins, system 07 owns the authoritative launch/readiness state.

System 20 may present:

- current roster;
- assigned player slots;
- each required member's readiness/loading state;
- party leader where relevant;
- whether the session launch barrier is currently satisfied.

The UI does not independently decide that enough players are ready.

It renders the semantic result of session policy.

If the local member may request a ready/unready transition, that request goes through system 07.

## 8. Start-session intent is semantic

Where system 07 allows an authorized party leader to request session start, system 20 may expose that action.

Conceptually:

```text
Start Game button
    -> RequestStartSession()
        -> system 07 validates leadership/readiness/policy
            -> authoritative transition
```

The UI must not:

```text
load gameplay scene
start authoritative simulation
spawn characters
```

directly.

The session/orchestration layers own those transitions.

System 23 may host the screen containing the button; system 20 owns the multiplayer/session presentation model and action semantics behind it.

## 9. Party leader is presentation of a social role, not gameplay authority

System 07 deliberately separates party leadership from authoritative gameplay ownership.

System 20 may display `Leader` next to a member and may expose leader-authorized party/session actions.

It must never imply that the leader owns:

- combat decisions;
- other players' vitality;
- inventory truth;
- quest progression;
- world state;
- server gameplay simulation.

A future host/topology change must not require rewriting party presentation semantics.

## 10. PlayerSlot and CharacterId may both be useful

`PlayerSlot` describes stable session/game composition.

`CharacterId` identifies the current gameplay character.

The UI should not collapse them into one concept.

A party member can remain valid while:

- their character is not created yet;
- their character is being synchronized;
- their character has been defeated;
- future gameplay policy rebinds them to a different character.

The member row is fundamentally a party-member presentation.

Character-specific widgets attach through the current `CharacterId`.

## 11. Teammate vitality consumes system 02

An in-game teammate HUD may display demonstrated character information such as:

- vitality;
- defeated/incapacitated state;
- perhaps other future public teammate gameplay state.

That information remains owned by the character/vitality systems.

Conceptually:

```text
PartyMemberId
    -> CharacterId
        -> replicated vitality state
            -> teammate presentation
```

Do not add `PartyMember.Health` as a duplicate authoritative field merely because the teammate HUD needs a health bar.

The same system-02 vitality state used by system 17 for the local character is reused for teammates.

## 12. Character defeat does not mean teammate disconnected

Presentation must distinguish:

```text
member presence
character vitality/defeat
```

A connected teammate controlling a defeated character remains connected.

A disconnected teammate's character may remain alive or may become defeated while they are away.

System 20 joins these facts for presentation but does not merge their authorities.

## 13. Reconnect preserves teammate identity

Suppose `PartyMemberId B` controls `CharacterId 12`.

Their connection disappears.

The expected UI transition is:

```text
B — Connected
    ->
B — Connection Interrupted
    ->
B — Reconnecting
    ->
B — Synchronizing
    ->
B — Connected
```

The teammate row does not disappear and later reappear as a different player.

Likewise any presentation state keyed to `PartyMemberId` can remain associated with the same durable member identity.

## 14. Reconnect state for the local player is also system 20 presentation

System 08 owns semantic recovery state.

System 20 owns the gameplay/session-facing presentation of that state.

During local interruption, the UI may display a modal/overlay or status surface such as:

```text
Connection interrupted
Attempting to reconnect...
Synchronizing game...
```

while system 17 makes ordinary gameplay HUD controls non-actionable.

The exact full-screen/menu container may be hosted by system 23, but it consumes system-20 session/recovery presentation state.

System 20 does not run the reconnect algorithm.

## 15. Remote interruption should be less disruptive

When another member reconnects, local gameplay generally should not be replaced by a blocking recovery screen.

Instead, system 20 can present an appropriate teammate status indicator or short semantic notification.

This is presentation policy over system-08 state.

The gameplay simulation continues according to authoritative session policy.

## 16. Explicit leave and unexpected interruption look different

System 08 explicitly distinguishes `LeaveGame` from connection failure.

System 20 must present them differently.

Unexpected:

```text
ConnectionInterrupted
```

Deliberate/terminal:

```text
Left
```

A user should not appear to have intentionally quit merely because Wi-Fi dropped for several seconds.

Likewise an explicitly departed member should not be shown indefinitely as `reconnecting`.

## 17. Member display metadata is not gameplay identity

`PartyMemberId` is stable semantic identity but is not necessarily good player-facing text.

Presentation may require metadata such as:

- display name;
- avatar/profile icon;
- platform/account display information where available.

That metadata should be associated with the party/session member through an appropriate identity/profile seam.

Do not use display name as the authoritative identity key.

Two members may have the same visible name while still having distinct `PartyMemberId`s.

Conversely, changing a display name must not change party identity.

## 18. Do not bake one platform's lobby model into system 20

System 07 deliberately has a join-provider seam.

System 20 should therefore avoid baking in concepts that only exist for one provider, such as a particular platform lobby id, Steam-specific invite state, or vendor-specific friend object.

Provider-specific invitation/discovery screens may adapt their provider data into the semantic join/session model.

The shared party/session presentation operates on system-07 concepts.

## 19. Public matchmaking is still not assumed

Do not expand system 20 into:

- server browser;
- global public lobby list;
- ranked matchmaking;
- skill ratings;
- matchmaking queue;
- regional fleet selection.

System 07 explicitly leaves those out until demonstrated.

If the game later requires them, they should be designed as separate discovery/matchmaking capability rather than inferred from teammate UI.

## 20. Session lifecycle is distinct from network lifecycle

System 07 defines game-session concepts such as:

```text
Forming
Launching
InGame
Completed
```

while lower-level networking has transport/server lifecycle concerns.

System 20 presents game-session state.

It should not infer:

```text
socket exists -> InGame
socket closed -> Completed
```

A session may be:

- forming while all transports are connected;
- launching while clients load;
- in-game while a teammate is temporarily disconnected;
- completed before the transport is technically torn down.

## 21. Game outcome and session completion remain separate inputs

System 15 owns authoritative gameplay outcome.

System 07/14 own game-session orchestration/lifecycle.

System 20 may present that the multiplayer session is transitioning/completed, but the actual victory/failure destination screens belong to system 23.

Conceptually:

```text
15 GameOutcome
    -> 14 session aftermath/orchestration
        -> session semantic state
            -> 20 session presentation
                -> 23 destination/end screen
```

Do not let teammate UI invent whole-game victory/failure rules.

## 22. Session leave is an authoritative/session action

A local `Leave Game` command requests the semantic leave operation defined by the session layer.

System 20/23 may present the action.

The UI should not fake leaving by merely unloading the current scene or disconnecting the socket first.

Conceptually:

```text
Leave Game
    -> session leave intent
        -> system 07/08 policy
            -> member/session transition
                -> transport shutdown/orchestration
```

This preserves explicit-leave semantics and gives the authoritative session a chance to handle roster/slot/character policy correctly.

## 23. Removing another party member is not assumed as a generic UI operation

System 07 allows for leader-authorized party actions where needed, but system 20 should expose only operations actually supported by production party policy.

Do not automatically add:

- Kick;
- Promote;
- Transfer host;
- Ban;
- Vote kick;

simply because multiplayer games often have them.

If system 07 later defines one of those semantic operations, system 20 can present it.

## 24. Snapshot establishes roster truth; events animate changes

Opening/recreating the multiplayer UI should begin from a complete session/party snapshot.

Events such as:

- `PartyMemberJoined`;
- `PartyMemberConnectionChanged`;
- `PlayerSlotAssigned`;
- `GameSessionStarted`;

may update the view incrementally and produce transient notifications.

But events do not replace the snapshot.

After reconnect or late join, the UI must reconstruct the entire current roster without replaying all historical join/leave events.

## 25. Late join uses the same presentation model

A newly joining client receives the current authoritative party/session state.

The UI should immediately reconstruct:

- existing members;
- their stable slots;
- their current presence states;
- the local member;
- the current session lifecycle.

It must not require that the new client witnessed every earlier `PartyMemberJoined` event.

Existing clients receive the new member through normal semantic session updates.

## 26. Local presentation state remains local

System-20-local presentation state may include:

- expanded/collapsed party panel;
- selected teammate;
- temporary join/leave toast timing;
- overlay animation;
- scroll position.

These are not party/session authority.

Unless independently designed otherwise, they do not replicate or persist in system 16.

## 27. Voice/chat is not silently part of teammate UI

Party presentation does not imply the existence of:

- voice chat;
- text chat;
- mute/block systems;
- speech indicators.

Those are separate capabilities with privacy/platform/network implications.

If later introduced, system 20 may host their presentation alongside a teammate row, but it must not invent their runtime semantics.

## 28. Waypoints and teammate-world markers are not automatically included

A teammate roster does not automatically imply:

- minimap;
- compass markers;
- through-wall outlines;
- player ping system;
- world-space nameplates.

Those require demonstrated presentation/navigation requirements.

System 20 can later consume a dedicated capability if one is designed, but should not smuggle a navigation system into party UI.

## 29. System 17 and system 20 have a deliberate boundary

System 17 owns the always-present gameplay HUD shell.

System 20 owns teammate/session presentation semantics.

Therefore an in-game teammate widget can be structured as:

```text
system 20
    PartyMemberPresentation[]
        name
        presence
        character/vitality projection

system 17
    renders compact teammate HUD region
```

The same system-20 model may also feed a larger party/session screen hosted by system 23.

This avoids independently rebuilding party state in the HUD and menus.

## 30. System 23 hosts navigation/screens, not multiplayer truth

System 23 will own the game's broader menu/start/settings/session-screen flow.

System 20 owns the reusable party/session presentation model those screens consume.

For example:

```text
23 Main/Session screen
    hosts
        20 PartyRosterPresenter
        20 ReadinessPresenter
        20 SessionStatusPresenter
```

System 23 decides which screen is open.

System 20 decides how semantic party/session state is projected.

Neither owns system-07/08 authority.

## 31. Headless server independence

Systems 07 and 08 must run identically with no system-20 presentation assembly loaded.

A headless host can:

- form a party;
- assign slots;
- start a session;
- accept late joins;
- mark members interrupted;
- authenticate reconnect;
- resynchronize a member;
- expire reconnect grace;
- complete the game session;

without any UI.

System 20 consumes public semantic state from systems 07/08/06.

Those systems do not depend on system 20.

## Suggested presentation structure

Conceptually:

```text
MultiplayerPresentation
    PartyRosterPresenter
    SessionReadinessPresenter
    TeammateHudPresenter
    ConnectionRecoveryPresenter
    SessionNotificationPresenter
```

with projections such as:

```text
PartyMemberViewModel
    PartyMemberId
    DisplayName
    IsLocal
    IsLeader
    PlayerSlot
    CharacterId?
    PresenceState
    ReadinessState
    CharacterSummary?

PartySessionViewModel
    GameSessionId
    Lifecycle
    Members[]
    CanLocalRequestStart
```

These are local read projections, not authoritative session objects.

## Acceptance / reuse proof

### Fresh party

1. Create a forming session.
2. Add multiple members.
3. System 20 renders one row per stable `PartyMemberId`.
4. Members receive stable slots.
5. Readiness changes are reflected from system-07 state.
6. Authorized start becomes available only when session policy reports it valid.
7. Starting the game is requested through system 07, not by loading gameplay directly from UI.

### Remote disconnect and reconnect

1. Player B is shown connected.
2. B's transport connection drops unexpectedly.
3. B remains in the same roster row as `ConnectionInterrupted`.
4. B reconnects on a new transport connection.
5. The same row progresses through reconnect/resynchronization state.
6. The same `PartyMemberId`, `PlayerSlot`, and `CharacterId` relationship is restored.

### Local reconnect

1. Local connection is interrupted.
2. Gameplay controls become non-actionable.
3. System 20 presents semantic reconnect state.
4. Full resynchronization occurs.
5. GameplayReady is restored.
6. The recovery presentation clears and the same local party/member/character relationship resumes.

### Teammate vitality reuse

1. Two connected members control two characters.
2. System 20 resolves each member's current `CharacterId`.
3. It joins replicated system-02 vitality into the teammate presentation.
4. Damage one remote character.
5. Teammate HUD reflects the authoritative vitality change without adding health to party/session state.

### Late join

1. Session is already `InGame` with existing members.
2. A new client joins.
3. New client receives current party/session snapshot.
4. Existing roster appears immediately without event-history replay.
5. Existing clients gain one new semantic member.
6. No current player is reconstructed from transport connection history.

### Headless independence

Run party formation, session start, interruption/reconnect, and completion without system 20 loaded and verify session behavior is unchanged.

## Out of scope

- transport/network implementation
- gameplay-state replication — system 06
- party/session authority — system 07
- reconnect mechanics — system 08
- character vitality authority — system 02
- public matchmaking/server browser
- platform-specific lobby implementation
- voice/text chat
- teammate waypoint/minimap system
- host migration unless independently designed
- kick/promote/vote systems unless system 07 defines them
- generic menu/start/end screen navigation — system 23
- gameplay HUD shell — system 17
- gameplay outcome policy — system 15
- generic application-wide UI framework

## Architectural constraints

- Key multiplayer presentation by stable `PartyMemberId`, never transport connection id.
- Preserve `PartyMemberId -> PlayerSlot -> CharacterId` as distinct identities.
- Party membership, presence/reconnect state, readiness, and character state remain separate semantic dimensions.
- Consume one coherent party/session read snapshot rather than a chatty per-member query graph.
- Transport disconnection does not imply party departure.
- GameplayReady does not equal socket connected.
- Raw network repair/protocol mechanics do not leak into ordinary player-facing UI.
- Teammate vitality reuses system 02/system 06 state rather than duplicating health in party state.
- Snapshots establish current roster/session truth; semantic events drive transient notifications.
- Late join and reconnect fully reconstruct presentation from current state.
- System 20 owns multiplayer presentation semantics; systems 17/23 may host its widgets/screens.
- Party/session authority remains fully runnable on a headless server without system 20.
