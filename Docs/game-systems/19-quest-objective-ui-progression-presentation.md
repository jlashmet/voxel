# 19. Quest & objective UI / progression presentation

**Status:** Approved

## Purpose

Provide player-facing presentation for the unified quest/objective progression model from system 11 without introducing a second quest journal state machine.

The defining rule is:

> Progression decides what is active, completed, failed, or available; the UI decides how that authoritative progression state is presented.

Conceptually:

```text
authoritative unified progression
    -> system 06 replication where multiplayer requires it
        -> progression snapshot / semantic events
            -> quest-objective presentation model
                -> journal / tracked-objective HUD presentation
```

The UI never completes objectives merely because a checkbox was clicked or a row changed visually.

## 1. Reuse the unified system-11 progression model

System 19 must consume the unified progression authority established by system 11.

It must not create independent collections such as `UiActiveQuests`, `UiCompletedQuests`, `QuestJournalObjectives`, or `CampaignObjectiveUiState` that become another source of gameplay truth.

The underlying semantic state remains stable quest/objective identity, authoritative status, objective/step status, authoritative progression revision, and semantic progression events. The journal is a projection of that state.

## 2. Support quests and standalone objectives through one presentation path

System 11 deliberately unifies quests containing objectives/steps and standalone campaign objectives that do not require a parent quest.

System 19 should not rebuild the old distinction by having unrelated quest-journal and campaign-objective state implementations.

A shared presentation entry may contain semantic identity, optional parent quest identity, authoritative status, presentation metadata, and child objective presentation. A multi-step quest and a standalone objective may look different visually while consuming the same progression read model.

## 3. Semantic progression state and presentation metadata stay separate

The current quest definitions contain gameplay-oriented fields such as `QuestRef`, `QuestStepRef`, `TargetId`, and completion specification. Those are not automatically player-facing copy.

Do not display internal values such as NPC ids, WorldObject ids, or completion-spec strings as quest text.

Player-facing metadata should be resolved from stable semantic identity, conceptually:

```text
QuestRef("repair-the-gate")
    -> QuestPresentationDefinition
        Title
        Description
        optional icon/category

QuestStepRef("find-mechanism")
    -> ObjectivePresentationDefinition
        ObjectiveText
```

This presentation/content layer may later support localization without changing authoritative progression identity.

## 4. Do not put Unity UI assets into the progression runtime

Progression definitions should not accumulate Unity sprites, prefabs, fonts, panel references, UI colors, layout data, or localized final text.

The progression engine owns semantics. Presentation/content owns how those semantics are communicated to a player.

The dependency remains:

```text
progression semantic refs
    -> presentation metadata lookup
        -> local UI
```

not `QuestRuntime -> Unity UI assets`.

## 5. Snapshot establishes journal truth

System 19 should initialize from one coherent progression snapshot, conceptually:

```text
ProgressionSnapshot
    Revision
    Entries[]
```

containing the current state necessary to render all player-visible progression.

The journal should be able to discard its local model and reconstruct entirely from that snapshot. This is required for initial join, reconnect, session restoration, UI recreation, scene changes, and replication repair.

Events are not the database of record.

## 6. Avoid a chatty per-row API

The current first-slice runtime exposes `GetSnapshot(QuestRef)`. That is sufficient for its current scale, but the production UI should not evolve into a sequence that fetches every quest and then separately queries each objective every frame or refresh.

As system 11 is generalized, expose an appropriate deterministic batch/read snapshot for the progression state needed by networking, persistence, and UI, conceptually `GetProgressionSnapshot()` or an equivalent read contract.

System 19 should consume a coherent read model rather than turning API granularity into a chatty cross-module interaction.

## 7. Events explain changes; snapshots establish truth

Semantic events such as quest started, objective activated, objective completed, and quest completed may drive transient presentation such as "New Quest" or completion notifications.

But the persistent journal is reconstructed from current state.

If a completion event was missed during disconnect, reconnect still shows the objective as completed because the synchronized snapshot says so. Do not replay all historical progression events merely to reconstruct the journal.

## 8. Journal grouping is presentation policy

The UI may organize progression into groups such as Active, Completed, and Failed if useful. Those groups are derived from semantic status; they are not new gameplay states.

Likewise sorting, collapsing, filtering, selected quest, selected objective, and scroll position are local presentation state and do not belong in the authoritative progression runtime.

## 9. Tracking an objective is local presentation state

A player may choose an active quest/objective to keep visible during gameplay.

Conceptually:

```text
Track Objective X
    -> local presentation preference
        -> system 17 tracked-objective HUD widget
```

Tracking does not activate the objective or cause the campaign to prioritize it. Authoritative activity comes from system 11.

By default that choice should not replicate as shared campaign authority or be stored in authoritative system-16 persistence. If persistent personal UI preferences are later desired, that belongs to player/profile/settings persistence rather than game progression state.

## 10. System 17 hosts the small gameplay widget; system 19 owns its semantics

The dedicated journal belongs to system 19. A compact tracked-objective presentation may appear in the gameplay HUD from system 17.

The ownership should be:

```text
system 19
    -> produces tracked-objective presentation model

system 17
    -> provides HUD placement/rendering shell
```

This avoids implementing objective logic twice. Journal and HUD both derive from the same system-19 presentation projection.

## 11. UI does not complete progression

Clicking a quest entry, tracking it, expanding it, or opening the journal must never mutate authoritative completion state.

Gameplay still follows the system-11 pipeline:

```text
gameplay fact
    -> semantic progression observation
        -> progression evaluates completion
            -> authoritative transition
                -> progression event/snapshot
                    -> UI reacts
```

Do not implement `UI checkbox -> CompleteObjective()` unless a future explicitly designed gameplay mechanic makes manual completion itself a semantic game action.

## 12. Quest acceptance is not assumed

The existing architecture has story/campaign starting progression. There is no demonstrated generic player quest-acceptance subsystem.

Therefore system 19 should not invent Accept Quest, Decline Quest, Abandon Quest, or Restart Quest merely because RPG journals often contain those buttons.

If later content requires player acceptance or abandonment, progression/story must first define those semantic operations and rules. Only then may the UI expose them.

## 13. Failure presentation follows actual progression semantics

The current model contains failed statuses, but UI must not invent failure conditions or infer them from unrelated gameplay.

If system 11 exposes an authoritative failed objective/quest, system 19 presents it. If production progression has not yet implemented a semantic failure transition, a disabled or decorative Failed UI feature does not justify introducing one inside the presentation layer.

The same rule applies to skipped objectives.

## 14. Do not expose locked future objectives accidentally

A quest may contain objectives that are not yet active. The UI should display only what the authored presentation policy says is appropriate.

A locked future objective must not automatically expose internal target ids, completion rules, or story spoilers merely because those details are present in authoritative definitions.

The presentation projection determines appropriate visible text. This does not require a generic hidden-objective rules engine; it simply prevents internal gameplay configuration from becoming UI copy by accident.

## 15. Multiplayer progression remains authoritative/shared

System 11 currently defines progression as shared campaign/session state. Therefore all clients consume the same authoritative progression truth unless a future design explicitly introduces per-player progression scope.

Local players may have different selected journal entries, tracked objectives, scroll positions, filters, and panel layout. They must not independently decide whether an objective completed, whether a quest started or failed, or campaign progression.

If future content needs personal quests, that ownership/scope belongs in system 11 first. System 19 then presents it.

## 16. Reconnect rebuilds presentation completely

If a player disconnects while objective A is active, and A completes while B activates during disconnection, system 08 resynchronization should provide current progression where A is Completed and B is Active.

The journal and tracked-objective presentation must immediately reflect that current truth. They must not require receipt of missed historical events.

## 17. Session restore behaves the same way

System 16 restores semantic progression state into the normal authoritative runtime.

After load:

```text
restored system-11 progression
    -> replicated/current snapshot
        -> system 19 presentation
```

There is no special loaded-game quest journal. New sessions, reconnects, and restored sessions all use the same presentation path once authoritative progression is available.

## 18. Gameplay-ready is the interaction barrier

During Connecting, Restoring, or Synchronizing, previous journal state must not remain actionable as though it were current.

Once systems 08/14 establish gameplay-ready state, system 19 binds to the synchronized progression source. Transient connection/session status belongs primarily to systems 20/23, not to quest authority.

## 19. Input context and pause behavior

Opening the full journal should use the existing `Ui` input context, just like the inventory screen. It must not disable gameplay through unrelated component toggles.

And:

```text
Open Quest Journal != Pause Game
```

In multiplayer especially, one player's journal cannot implicitly pause the authoritative session. Any single-player pause policy belongs to system 23/session composition.

## 20. Navigation/wayfinding is not silently part of quest UI

A `TargetId` does not automatically imply a minimap marker, compass marker, GPS route, glowing world outline, objective beam, or waypoint pathfinding.

Those require demonstrated navigation/presentation capabilities.

If such a system is later introduced, system 19 may expose the semantic target association necessary for it. Do not have Quest UI inspect world coordinates or scene objects to invent navigation behavior.

## 21. Headless-server independence

The progression runtime must behave identically without system 19 loaded.

A headless server can start progression, consume gameplay observations, activate objectives, complete objectives, complete quests, replicate state, and persist/restore state with no quest UI.

System 19 depends on public progression read contracts. Progression does not depend on system 19.

## Suggested presentation structure

Conceptually:

```text
ProgressionPresentation
    ProgressionJournalPresenter
    ProgressionEntryPresenter
    TrackedObjectivePresenter
    ProgressionNotificationPresenter
```

with local view projections such as:

```text
QuestViewModel
    QuestRef
    Title
    Description
    Status
    Objectives[]

ObjectiveViewModel
    ObjectiveRef
    Text
    Status
    IsTracked
```

These are read projections. They are never authoritative quest state.

## Acceptance / reuse proof

### Multi-step quest

1. Start a multi-step quest through the normal story/progression path.
2. Journal snapshot shows the quest active and only the appropriate current objective active.
3. Gameplay satisfies the first objective.
4. Authority completes it and activates the next objective.
5. Journal updates from semantic progression state.
6. No UI mutation was required to advance progression.

### Standalone objective

1. Activate a standalone campaign objective through system 11.
2. Present it using the same objective presentation path used for a quest step.
3. Complete it from an authoritative gameplay observation.
4. Verify there is no separate campaign-objective UI state machine.

### Tracked-objective HUD reuse

1. Track an active objective locally.
2. System 19 produces its compact presentation model.
3. System 17 renders it.
4. Complete that objective authoritatively.
5. HUD presentation updates to the next valid state without maintaining independent progression truth.

### Reconnect

1. Open a quest with objective A active.
2. Disconnect.
3. A completes and B activates while disconnected.
4. Reconnect/resynchronize.
5. Journal immediately shows A completed and B active from current snapshot.

### Two clients

1. Two clients observe the same shared campaign progression.
2. Client A tracks objective X.
3. Client B tracks objective Y.
4. Both retain identical authoritative quest/objective state.
5. Their tracking choices remain local presentation preferences.

### Headless independence

Execute the same progression scenario without loading system 19 and verify authoritative results remain identical.

## Out of scope

- new quest/progression authority — system 11
- procedural quest generation
- quest rewards
- quest acceptance/abandonment unless separately designed
- per-player progression divergence
- achievements
- reputation/factions
- map/minimap/navigation system
- arbitrary waypoint/pathfinding UI
- dialogue UI
- HUD shell — system 17
- inventory UI — system 18
- party/session UI — system 20
- pause/menu policy — system 23
- generic application-wide UI framework

## Architectural constraints

- System 19 consumes the unified system-11 progression model.
- It never owns authoritative quest/objective status.
- Quests and standalone objectives use one presentation path.
- Semantic refs/statuses remain separate from player-facing titles/descriptions/icons.
- Internal `TargetId` and completion specs are not player-facing text.
- Prefer one coherent progression snapshot/read projection over chatty per-entry cross-module queries.
- Snapshots establish truth; events drive transient notifications.
- Tracking, selection, sorting, filtering, and scrolling remain local presentation state.
- Tracking does not activate or prioritize authoritative progression.
- UI does not invent acceptance, abandonment, failure, rewards, or navigation mechanics.
- Reconnect and restore reconstruct presentation from current authoritative state.
- Progression remains runnable on a headless server without system 19.
