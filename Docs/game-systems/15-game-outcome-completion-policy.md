# 15. Game outcome & completion policy

**Status:** Approved

## Purpose

Provide one authoritative gameplay-level terminal result for a running game without conflating combat completion, campaign completion, gameplay-session orchestration, or technical network-session shutdown.

The existing systems already model different kinds of completion:

- combat has its own `Idle → Active → Completed` lifecycle and may record a `WinningTeam`; that is a combat result only;
- network `SessionLifecycle` has `Active → Ending → Ended` with technical reasons such as `ServerShutdown`, `DurationLimit`, and `AdminTerminate`; that is technical session-transition semantics;
- system 14 owns high-level gameplay-session orchestration.

System 15 fills the missing gameplay-outcome layer:

`combat / encounter / character / objective / campaign facts`

→ **authored terminal policy**

→ **authoritative GameOutcome**

→ system 14 session orchestration

## 1. One authoritative terminal result

A running game has one terminal lifecycle:

`Running → Resolved`

Resolution commits one immutable result containing:

- a broad disposition useful to generic flow/presentation, such as `Success` or `Failure`;
- a semantic outcome reference explaining why, such as `campaign-completed` or `party-defeated`.

Conceptually:

`GameOutcome = Disposition + OutcomeRef`

Campaign-specific meanings belong in semantic content references rather than an ever-growing engine enum.

## 2. Resolution is immutable and idempotent

Once `GameOutcome` is resolved, it cannot change.

Duplicate terminal requests must not create duplicate endings, rewards, persistence actions, orchestration transitions, or conflicting results.

Late/stale terminal facts likewise cannot reverse an already committed outcome. The first deterministically committed valid terminal result wins.

## 3. Content decides what actually ends the game

System 15 must not hardcode either:

- winning combat means game victory; or
- losing combat means game failure.

Gameplay systems report semantic facts. Campaign/story/progression composition determines which facts are terminal.

An ordinary roadside encounter may end with the enemy winning and the campaign may continue. A final configured progression condition may instead resolve `Success / campaign-completed`. A campaign may also complete without any final combat.

The terminal meaning belongs in authored composition rather than encounter/combat implementation.

## 4. Character defeat is not automatically game failure

System 02 owns character vitality/defeat. `CharacterDefeated` is a character-domain fact, not a game-over command.

Individual character defeat does not imply `Failure` unless configured terminal policy says so.

If the game later defines a condition such as all required active-party characters being defeated, system 15 may consume that authoritative condition. Party-defeat failure remains configured completion policy rather than vitality behavior, leaving room for revival, rescue, incapacitation, retreat, or other mechanics.

## 5. Domain systems produce facts, not game outcomes

The intended chain is conceptually:

`CharacterDefeated` → vitality / system 02

`EncounterCompleted` → system 05

`QuestCompleted / ObjectiveCompleted` → system 11

`Campaign semantic event` → story/content

Only when configured terminal policy is satisfied:

`ResolveGameOutcome(...)` → system 15

System 05 therefore does not need concepts such as `IsFinalBoss`. Campaign composition supplies that meaning.

## 6. Story/progression integration stays semantic

Story/content may request a semantic terminal effect such as:

`ResolveGameOutcome("campaign-complete", Success)`

A reusable run policy may independently observe a generic authoritative condition such as required-party defeat and request:

`ResolveGameOutcome("party-defeated", Failure)`

System 15 must not become another general-purpose rule engine. Story/progression owns authored sequencing and conditions; system 15 validates and records the terminal result.

## 7. System 14 controls what happens after resolution

After system 15 emits `GameOutcomeResolved`, system 14 coordinates the gameplay runtime transition, for example stopping ordinary gameplay commands and completing/shutting down the running gameplay graph.

System 15 itself does not:

- shut down sockets or servers;
- unload scenes;
- save files;
- open menus;
- return players to a lobby;
- run credits or victory presentation.

The responsibility split is:

- **15:** the game is over, and this is why;
- **14:** coordinate the gameplay runtime transition because the game is over;
- **Network SessionLifecycle:** perform technical server-session transition when instructed or for independent technical reasons.

A server can therefore terminate administratively without manufacturing a gameplay victory or failure.

## 8. Multiplayer authority

The authoritative server alone resolves `GameOutcome`.

Clients do not declare victory/failure. They receive the committed semantic result through system 06 replication so all players converge on the same disposition and reason.

A disconnected player under system 08 does not count as defeated merely because its connection disappeared.

## 9. Deterministic conflicting terminal conditions

Potentially terminal facts may occur near the same time, for example final-objective completion and party defeat.

Authoritative processing order must be deterministic. Whichever valid terminal request is committed first resolves the game; all later requests are ignored as duplicate/late terminal facts.

System 14 should stop ordinary gameplay processing promptly after resolution, but system 15 must remain idempotent even if late events still arrive.

## 10. Persistence boundary

System 16 may store the committed `GameOutcome` in the authoritative session snapshot so completed runs remain inspectable/recoverable where required.

System 15 does not serialize itself.

Boundary:

- **15 = outcome state/policy**
- **16 = persistence**

## 11. Presentation is downstream

The outcome service emits semantics, not presentation commands.

A result such as:

`Success / campaign-completed`

may later map to a victory screen, localized text, animation, music, credits, or menus, but those concerns belong to systems 17–23 or later composition.

System 15 must not contain screen names, scene transitions, animation triggers, or music cues.

## Reuse / integration proof

### Ordinary combat loss

1. Resolve an ordinary encounter with the enemy winning.
2. No configured terminal policy matches.
3. Verify `GameOutcome` remains `Running` and no terminal event is emitted.

This proves encounter/combat completion is not implicitly game completion.

### Campaign completion

1. Satisfy the configured final progression fact.
2. Resolve `Success / campaign-completed`.
3. Deliver the same terminal request again.
4. Verify exactly one immutable outcome and one `GameOutcomeResolved` publication.

### Configured party defeat

1. Configure required-party defeat as terminal failure policy.
2. Defeat an individual character and verify the run continues.
3. Satisfy the authoritative required-party defeat condition.
4. Verify exactly one `Failure / party-defeated` result.

### Technical shutdown

1. Leave gameplay outcome unresolved.
2. End network `SessionLifecycle` for `ServerShutdown`, `DurationLimit`, or `AdminTerminate`.
3. Verify the technical session can end without creating a gameplay `Success` or `Failure`.

The first and fourth scenarios are the critical reuse proofs because they demonstrate that unrelated kinds of "finished" remain separate.

## Out of scope

- checkpoint/retry systems
- respawn/revive/rescue mechanics
- score screens
- return-to-lobby flow
- save deletion
- server shutdown mechanics
- final-boss systems
- credits sequences
- special victory scenes
- presentation/audio/VFX for outcomes

These belong to later gameplay design, system 14 orchestration, system 16 persistence, network lifecycle, or presentation/UI systems.

## Architectural constraints

- Combat completion, gameplay completion, and technical session termination remain separate concepts.
- Exactly one authoritative immutable `GameOutcome` may be committed per run.
- Terminal resolution is deterministic, idempotent, and server-authoritative.
- Domain systems report semantic facts rather than declaring game victory/failure.
- Campaign-specific terminal meaning stays in authored content/composition.
- System 15 records the terminal semantic result; it does not orchestrate shutdown, persistence, replication transport, or presentation.
- System 14 reacts to `GameOutcomeResolved`; system 16 may persist the result; system 06 may replicate it; network `SessionLifecycle` remains independently technical.
