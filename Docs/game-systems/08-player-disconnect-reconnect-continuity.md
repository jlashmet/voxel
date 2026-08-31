# 08. Player disconnect, reconnect & continuity

## Status

Approved design direction.

## Purpose

Preserve player identity and gameplay continuity across temporary transport failures. Connections are disposable; party members, player slots, and controlled characters are durable gameplay identities.

## Existing foundation

The networking runtime already contains lower-level late-join, reconnect, and session-lifecycle support for the voxel world. `Reconnect` can choose between no/repair/full-region recovery using authoritative hashes, while `LateJoin` reconstructs current authoritative state instead of replaying history. This system extends those mechanics to the full gameplay model rather than replacing them.

## Core identity rule

Do not equate:

- transport connection
- party member
- player slot
- gameplay character

The intended binding is:

`PartyMemberId -> PlayerSlot -> CharacterId`

with a currently attached transport connection that may disappear and later be replaced.

An unexpected connection loss must not immediately remove the party member or destroy/recreate the controlled character.

## Lifecycle

A member can move through semantic states such as:

`Connected -> ConnectionInterrupted -> Reconnecting -> Resynchronizing -> Connected`

and, when recovery fails or policy expires:

`ConnectionInterrupted -> ReconnectExpired -> Left`

The exact grace duration is game/session policy rather than a hard-coded networking constant.

## Network repair window vs gameplay grace

The existing short reconnect tolerance used by voxel-state repair is an optimization boundary, not the player's full opportunity to return to the session.

Separate:

- **network fast-repair tolerance**: whether cached state can be repaired cheaply
- **member continuity grace**: how long the game reserves the member, slot, and character

A reconnect outside the fast-repair window can still recover through a full authoritative resynchronization while preserving the same member/slot/character identity.

## Character continuity

While disconnected, the controlled `CharacterId` remains an authoritative world character.

It may still:

- receive damage
- be defeated
- remain in an encounter
- be moved or affected by authoritative world rules

The initial disconnected-control policy should be conservative: no new player commands are generated. Optional temporary AI takeover can later be supplied by the character-AI system as an explicit policy rather than being implicit reconnect behavior.

On successful reconnect, the new connection rebinds to the same `PartyMemberId`, `PlayerSlot`, and `CharacterId`.

## Authentication

Reconnect must prove ownership of the durable party-member identity. A client cannot claim another member's character by supplying an ID.

The session layer authenticates the new transport connection and restores its existing member/slot binding before gameplay resumes.

## Recovery paths

### Fast reconnect

When cached client state is sufficiently current:

1. reconnect/authenticate the same `PartyMemberId`
2. use the existing voxel reconnect/repair mechanisms where applicable
3. apply gameplay-state deltas/current snapshots through system 06
4. restore input authority only after synchronization completes

### Full resynchronization

When cached state is stale or unavailable:

1. reconnect/authenticate the same `PartyMemberId`
2. reconstruct current authoritative world state using the existing late-join style snapshot path
3. synchronize current character, vitality, encounters/combat, inventory, quest, and campaign state
4. restore the existing `PlayerSlot -> CharacterId` relationship
5. enable gameplay input after the synchronization barrier

Do not replay the entire disconnected history merely to reconstruct present truth.

## Gameplay-ready barrier

A re-established socket is not sufficient for gameplay readiness.

Input authority resumes only after:

- member identity is restored
- player slot is restored
- controlled character is resolved
- required world state is synchronized
- gameplay-state replication has converged sufficiently for play

Expose an explicit `GameplayReady`/equivalent state to composition and UI.

## Presence semantics

Other players should be able to distinguish:

- connected
- temporarily interrupted/reconnecting
- deliberately left
- reconnect expired

An unexpected outage should initially appear as an interruption rather than an immediate party departure.

## Explicit leave

A deliberate `LeaveGame` is different from an unexpected disconnect. Explicit leave may immediately run session policy for roster removal, player-slot release, and character handling without waiting for reconnect grace.

## Session shutdown

Keep three independent lifecycles:

- transport connection lifecycle
- party-member continuity lifecycle
- overall game-session lifecycle

Server/session shutdown is not modeled as an individual player's reconnect failure.

## Player-facing recovery state

This system owns semantic recovery states, not the final UI presentation. It should expose enough state for later UI to present messages such as:

- Connection interrupted
- Reconnecting
- Synchronizing game
- Reconnected
- Unable to reconnect

Full teammate/session UI belongs to system 20 and menu presentation belongs to system 23.

## Example

Player 2 controls `CharacterId 12` at 5 HP during an encounter. Their connection drops. Character 12 remains in the encounter and is hit, reaching 3 HP. The player reconnects on a new transport connection. Authentication restores the same party member and player slot; gameplay synchronization reports the current authoritative 3 HP state; control resumes on `CharacterId 12`.

No duplicate actor is created and no stale state is restored.

## Acceptance / reuse proofs

### Brief outage

1. player disconnects unexpectedly
2. member, slot, and character remain reserved
3. authoritative gameplay continues
4. player reconnects within the fast-repair path
5. state converges
6. the same `CharacterId` resumes control

### Longer recoverable outage

1. player disconnects beyond fast network-repair tolerance but within member-continuity grace
2. the controlled character changes authoritative state while disconnected
3. player reconnects
4. full authoritative synchronization occurs
5. the same member, slot, and character are restored with current state

## Explicitly out of scope

- transport/networking rewrite
- party creation and fresh join flow (system 07)
- gameplay snapshot contract ownership (system 06)
- persistence across separate game sessions (system 16)
- teammate/session HUD presentation (system 20)
- menu implementation (system 23)

## Architectural principle

**Connections are temporary. Party members, player slots, and gameplay characters are durable identities.**
