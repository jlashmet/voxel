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

### 2026-09-03 — T11-002 consumer/compatibility catalog

- `Game.Story.Api` is a direct contract consumer of both legacy identities: it references `Game.Quests.Api` for `QuestRef` and `Game.WorldBuilder.Api` for `ObjectiveRef`. `StoryEvent.QuestCompleted`, `IStoryStateView.IsQuestActive/IsQuestCompleted`, and `IStoryEffectSink.StartQuest/StartObjective` are the integration seam. This should migrate directly to Progression identities/events; Story does not need a stateful Quests runtime facade.
- `Game.Composition.Campaign.Content.KnownOpeningCampaignContent` authors both forms in one opening: `WellQuest` is a `QuestRef`, while `TravelObjective` is a WorldBuilder `ObjectiveRef`. Story starts the well quest on NewGame, starts the standalone travel objective after the intro cutscene, and gates the destination conversation on `ObjectiveActive`. This is the binding mixed-content reuse case for T11-031.
- `Game.Composition.Campaign.Runtime` directly references both `Game.Quests.Api` and `Game.Quests.Runtime`; it is the main runtime migration consumer and must switch atomically to one `Game.Progression.Runtime` instance. Its public quest-shaped convenience methods can be migrated with the campaign callers rather than preserving a second runtime.
- Upstream System06 work already established an engine-neutral `Game.Progression.Api` containing `ObjectiveId`, `QuestId`, lifecycle snapshots, `ProgressionSnapshot`, and `IProgressionQuery`; `Game.GameplayReplication.Adapters` already references this API. There is no `Assets/Game/Progression/Runtime` yet. GameSystem11 will extend/reuse this existing API rather than create a competing Progression contract.
- Existing `Game.Progression.Tests` currently cover API snapshot copy semantics only. Existing quest behavior remains in `Game.Quests` and must be ported to module-local Progression tests as required by T11-030.
- Persistence/continuity currently has no direct Progression dependency: `Game.Continuity.Runtime` references only Continuity, Sessions, and GameplayReplication APIs. The present campaign save snapshot also omits quest/objective truth. Therefore GameSystem11 supplies a coherent Progression snapshot/query/restore seam without adding a new persistence implementation.
- Replication already anticipates Progression through `Game.GameplayReplication.Adapters -> Game.Progression.Api`, so compatibility is API-level only; no replication-owned progression state should be added.
- No current quest/objective UI is part of the production path being migrated, and UI is explicitly a non-goal. The repository-level code search endpoint was incomplete during discovery, so absence of UI consumers is treated narrowly: no UI dependency is required by the known Story/Campaign/Progression/Replication assembly graph or binding content; the final boundary/repository audit will re-check for remaining legacy consumers before removing compatibility ownership.
- Compatibility classification: migrate Story, Campaign Runtime, campaign content, Progression tests, and replication adapters directly to Progression contracts. Preserve `Game.Quests` naming only if a compile-time caller outside this catalog requires it during the atomic change; any such layer must delegate to the single Progression runtime and is removed by T11-027/T11-041.
