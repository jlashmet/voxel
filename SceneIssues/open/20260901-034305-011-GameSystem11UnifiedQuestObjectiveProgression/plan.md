# 11 Unified quest & objective progression — implementation plan

**Target module:** establish `Assets/Game/Progression/Api` / `Runtime` (`Game.Progression.Api`, `Game.Progression.Runtime`) and migrate the reusable core of existing `Game.Quests.Api/Runtime` into it. Keep a temporary Quests compatibility facade only if needed for an atomic migration.

## API

Stable objective/quest identities, definitions, semantic observations, activation/completion state, progression events, coherent snapshots, and query interfaces. Gameplay reports facts; it never marks completion directly.

## Runtime

1. Extract/generalize existing deterministic QuestRuntime mechanics into Progression.
2. Represent quest steps and standalone campaign objectives on one state machine/snapshot model.
3. Migrate CampaignRuntime's parallel active/completed objective sets and direct completion logic.
4. Route semantic interaction/site/encounter/item facts through typed observations only when real content requires each vocabulary item.
5. Emit completion events for Story; do not invoke cutscenes/encounters directly.

## Dependencies

Story consumes API events/state; #13/#05/#10 may supply observations through composition. Persistence/replication consume snapshots later.

## Tests / proof

Existing multi-step quest and standalone travel/interaction objective both run on the same runtime; deterministic snapshot/restore; no campaign-owned duplicate objective state.

## Do not build

No procedural quests, rewards framework, per-player divergence, arbitrary expression language, or UI.

## Execution notes

### 2026-09-03 — T11-001 current-owner inventory

- Synced `fixes/agent-8` to current `origin/master` `81ffa4bbc76c3feb6e0bde2376065b4144f3f10a` before GameSystem11 work; the previous feature head had no GameSystem11 commits, so this was a non-force fast-forward.
- Owner A is `Game.Quests.Runtime.QuestRuntime`. It stores immutable `QuestDefinition` references plus mutable `QuestStatus`, per-step `QuestStepStatus[]`, and `ActiveStepIndex` in one `RuntimeQuest` per quest. `Start` activates the first step; `Observe(QuestObservation)` advances matching active steps in definition order and emits deterministic `QuestEvent`s; `Complete(QuestRef)` is a public direct-completion escape hatch; `GetSnapshot` exposes one quest at a time. Current observation vocabulary is NPC interaction and generic subject interaction.
- Owner B is `Game.Composition.Campaign.Runtime.CampaignRuntime`. Independently of `QuestRuntime`, it maintains `_knownObjectives`, `_activeObjectives`, and `_completedObjectives` for authored campaign `ObjectiveSpec`s. `IStoryEffectSink.StartObjective` mutates campaign objective state through `StartObjective`, and `InteractWithNpc` calls `CompleteInteractionObjectives` after Story/Quest handling; that method directly removes matching objective refs from `_activeObjectives` and adds them to `_completedObjectives` when an `InteractWithNpcTriggerSpec` matches.
- Campaign currently embeds a separate `QuestRuntime _quests`, so quest and standalone-objective truth are two authoritative stores inside the same campaign host. Campaign exposes both `IsObjective*` and `IsQuest*` queries, while `CampaignProgressSnapshot` currently persists cutscene/member/spell state only and does not include either objective store.
- Competing hypotheses considered: (1) wrap Campaign objective sets behind a compatibility API and leave QuestRuntime intact, versus (2) generalize QuestRuntime mechanics into Progression and migrate campaign objectives into the same primitive. The discriminating requirement is T11-043's one-snapshot proof plus T11-040's deletion of duplicate campaign objective state; hypothesis (1) cannot satisfy those without retaining parallel authority. Selected direction is (2): Progression becomes the sole quest/objective state machine, with any Quests compatibility surface delegating to it rather than owning state.
