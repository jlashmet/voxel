# 11. Unified quest & objective progression

**Status:** Approved

## Purpose

Unify the repository's existing deterministic quest runtime and campaign-level objective tracking into one authoritative progression model. This is an integration/consolidation task, not a replacement quest system.

The existing quest runtime already provides stable semantic identities, immutable definitions, deterministic mutable progression state, semantic observations, progression events, and snapshots. Preserve and generalize that foundation.

## Current duplication to remove

`QuestRuntime` owns quest/step state, but `CampaignRuntime` also maintains separate active/completed objective sets and directly completes interaction objectives. Production progression should not have two independent state machines for the same concept.

The standalone campaign-objective path should move onto the same authoritative progression machinery used by quests.

## Core model

- An **objective** is the atomic gameplay goal with stable identity, completion specification, and progression state.
- A **quest** composes one or more objectives/steps into a larger authored progression unit.
- Existing `QuestRef` and `QuestStepRef` identities may remain; architecturally, a quest step is an objective within a quest.
- A standalone campaign objective is an objective not necessarily presented as part of a larger quest.

Do not require callers to maintain separate quest and objective state implementations.

## Gameplay observations

Gameplay systems report semantic facts; they do not complete progression state directly.

Examples of facts that may become supported as content requires them include:

- character/NPC interaction;
- world-object interaction;
- encounter outcome;
- loot/item acquisition;
- site/location arrival.

The progression integration seam translates authoritative gameplay results into semantic observations understood by the progression runtime. Extend completion-spec/observation types only when real content requires them.

## Progression events

Authoritative transitions emit semantic events such as:

- objective/step activated;
- objective/step completed;
- quest started;
- quest completed.

Story/campaign, replication, UI, audio, and persistence react independently. The progression runtime does not directly play cutscenes, spawn encounters, grant presentation effects, or manipulate UI.

## Story / campaign boundary

Story decides what progression to start and what should happen after progression events.

Examples:

- story starts quest X;
- gameplay satisfies an objective;
- progression emits completion;
- story observes completion and starts a cutscene, encounter, subsequent quest, or other authored effect.

Story must not manually mutate internal quest-step/objective state.

## Snapshot and authority

All quest and standalone-objective state participates in one deterministic snapshot model so networking and save/session persistence do not reconstruct progression from unrelated collections.

The authoritative gameplay host/server owns progression mutation. Multiplayer clients consume replicated progression state/events rather than independently deciding completion.

For now, progression is shared campaign/session state. Do not introduce per-player versus party quest divergence until actual content requires explicit progression ownership/scope.

## Migration from campaign-owned objectives

Once equivalent unified progression behavior exists:

- remove the campaign-owned active/completed objective state collections;
- remove direct interaction-objective completion logic from `CampaignRuntime`;
- route those authored objectives through the shared progression runtime;
- preserve story conditions/effects through semantic progression queries/events rather than parallel state.

## Deliberately not assumed

This system does not automatically add:

- procedural quest generation;
- daily/repeatable quest infrastructure;
- reputation/faction progression;
- achievement systems;
- branching-dialogue graphs;
- arbitrary counter-expression languages;
- hidden-objective frameworks;
- quest reward subsystems;
- per-player quest divergence.

Add these only when demonstrated by game content.

## Reuse proof / acceptance

Prove both existing progression styles use the same runtime:

1. A multi-step quest starts, consumes semantic interaction observations, advances steps deterministically, completes, and emits quest completion for story to observe.
2. A standalone travel/interaction objective activates, consumes a semantic gameplay observation through the same progression machinery, completes, and becomes observable by story.
3. `CampaignRuntime` no longer needs a second objective-state implementation for those cases.
4. Snapshots deterministically include both quest and standalone-objective progression.

## Architectural constraints

- Reuse/generalize the existing quest runtime rather than creating another progression engine.
- Gameplay reports facts; progression evaluates goals; story decides consequences.
- Keep progression engine-independent and based on stable semantic identities.
- Keep campaign/place-specific completion policy in authored content/composition.
- Networking and UI consume progression state; they do not own it.
