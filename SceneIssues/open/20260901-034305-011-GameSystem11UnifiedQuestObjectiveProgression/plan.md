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
