# 26. Authored full-run campaign progression & completion

**Status:** Approved

## Purpose

Complete the production campaign as an authored semantic path from normal session start to one authoritative terminal `GameOutcome`, without creating a parallel `GameLoop`, chapter manager, universal game-state machine, or replacement progression engine.

The repository already has the required ownership boundaries:

- system 11 owns unified quest/objective progression;
- Story/campaign content owns authored sequencing and consequences;
- system 14 assembles and coordinates the running authoritative gameplay graph;
- system 15 commits exactly one authoritative terminal result;
- system 16 persists/restores authoritative session state;
- system 23 owns application/front-end transition around the run.

System 26 is therefore primarily production campaign composition plus the minimum semantic integration needed for that authored content to reach a real ending.

## Defining rule

**A complete run is an authored chain of semantic gameplay consequences from session start to one configured terminal outcome; no scene, quest, encounter, combat, UI, or generic "game loop" object owns that chain.**

Conceptually:

`#23 New Game / Continue`

→ `#14 authoritative run`

→ campaign/story activates authored goals and situations

→ players perform real gameplay

→ authoritative domain systems publish semantic facts

→ system 11 progression and Story observe those facts

→ authored consequences activate subsequent goals/situations

→ repeat

→ authored terminal condition

→ `#15 GameOutcome`

→ `#14` aftermath/shutdown coordination

→ `#23` outcome/front-end flow.

## 1. Do not add another global progression state machine

Do not introduce a generic model such as:

`Intro -> Explore -> Fight -> Town -> Boss -> Victory`

or a universal `CurrentChapter` counter merely to represent campaign position.

Campaign position is already represented by authoritative semantic facts such as completed/active progression, completed one-shot story content, persistent campaign effects, world state, encounters, and other domain-owned state.

A run advances because authored rules respond to those facts.

## 2. Reuse the existing Story / progression pattern

The existing campaign composition already demonstrates the correct form:

- new game may trigger an intro;
- cutscene completion may start an objective;
- interaction/proximity may trigger later content when prior conditions are satisfied;
- quest completion may drive further story effects;
- authored effects may persist semantic progression such as a party addition or granted capability.

Extend this pattern to a complete production run rather than replacing it.

Gameplay reports facts. Unified progression evaluates goals. Story decides consequences.

## 3. System 26 is primarily campaign content/composition

System 14 deliberately does not own campaign sequencing. System 26 should not push that policy downward into shared orchestration.

The production campaign should compose the actual authored run over reusable systems. Shared infrastructure should grow only when concrete campaign content demonstrates a missing semantic contract.

Examples:

- if actual content requires an encounter result, route a semantic encounter-completion fact from system 05;
- if it requires a world-object result, route the semantic system-13 result;
- if it requires inventory acquisition, route the appropriate system-09/10/11 observation;
- if it requires another domain fact, add the narrow semantic integration at that owner boundary.

Do not prebuild a generic workflow expression language.

## 4. Story vocabulary grows only from demonstrated content needs

Existing semantic story/progression concepts should be reused wherever possible.

New trigger, condition, observation, or effect types are added only when production campaign content proves they are needed.

Do not add speculative families of triggers merely because a generic RPG might someday use them.

## 5. Outcome is the final authored consequence

A complete campaign route eventually satisfies an authored terminal policy that asks system 15 to resolve the game.

Conceptually:

`final semantic progression condition`

→ `ResolveGameOutcome(Success, "campaign-completed")`

The exact semantic reason belongs to content/configuration.

Do not encode terminality as reusable-domain flags such as:

- `IsFinalBoss`;
- `IsLastQuest`;
- `FinalSceneName`;
- ordinary combat victory;
- ordinary character defeat.

System 15 remains the only owner of committed gameplay outcome.

## 6. Recovered world/content evidence does not automatically define sequence

The recovered production material includes multiple semantic regions and settlements beyond the opening, plus source evidence for additional locations and end/epilogue material.

That evidence proves the campaign footprint is larger than the current opening, but filenames, map names, or catalog ordering must not be promoted into authoritative campaign order without source/design evidence.

Do not infer a linear route from recovered asset names.

Recovered or newly authored sequencing should be recorded explicitly in campaign/story composition.

## 7. Decompose the campaign instead of growing one monolithic builder

The production campaign should not become a single enormous `MainCampaign.Build()` containing every site, NPC, cutscene, quest, encounter, binding, and story rule.

Compose it from understandable authored slices/content modules that return or consume stable semantic handles.

Conceptually:

`MainCampaign`

- opening content
- subsequent progression slice
- subsequent progression slice
- terminal progression

Do not require an `ICampaignChapter` abstraction before repeated implementation evidence justifies one. Plain composition functions/classes are sufficient initially.

## 8. Geography is not campaign progression

WorldBuilder/world catalog owns what locations exist and how they can be realized.

Story owns what entering, approaching, interacting with, or completing content at those locations means at the current semantic progression state.

Entering a town must not implicitly mean `CurrentChapter++`.

The same site or interaction may have different authored consequences depending on current progression facts without changing its world identity.

## 9. Optional content remains optional unless terminal policy requires it

A complete run does not require consuming every quest, NPC, secret, encounter, site, or WorldObject.

The production campaign must provide at least one valid authored route from new-game semantics to terminal outcome.

Optional content may branch, rejoin, alter later conditions, grant capabilities, or remain entirely optional.

Do not introduce a generic `IsMainQuest` flag merely to distinguish required content. Terminal/progression conditions themselves define what is required.

If future authored content contains multiple endings, each route can resolve its own semantic system-15 outcome without requiring a speculative branching-ending framework beforehand.

## 10. Shared campaign progression remains authoritative session state

System 11 currently defines progression as shared authoritative campaign/session state.

Preserve that model for the production run unless real content demonstrates a need for per-player progression divergence.

One player's authoritative action may advance shared progression, after which system 06 replicates the resulting current state to the party.

Do not model different players as independently occupying different campaign chapters by default.

Reconnect and late join reconstruct current shared truth rather than replaying the historical event stream merely to rebuild campaign position.

## 11. Depend on the unified progression snapshot, not transitional duplication

The current campaign runtime contains transitional campaign-owned objective state alongside the quest runtime and a campaign progress snapshot that does not yet represent all unified progression.

System 11 already requires migrating standalone objectives onto the same authoritative progression machinery and deterministic snapshot model used by quests.

System 26 depends on that consolidation.

Do not institutionalize the transitional duplicate objective state as the final full-run persistence model.

## 12. Save/restore must preserve campaign position without replay

A mid-run system-16 snapshot must restore enough authoritative semantic state to recreate the same campaign position through the normal system-14 runtime graph.

A restored run must preserve, as applicable:

- unified quest/objective progression;
- completed one-shot story/cutscene facts;
- durable campaign effects;
- party/gameplay progression owned by their respective systems;
- required world/domain state;
- committed outcome if the run is already resolved.

Resume must not call the new-game path or replay historical one-shot events to reconstruct current truth.

## 13. Pacing belongs to authored structure, not a generic pacing manager

Full-run pacing is expressed by the structure of authored goals, gameplay opportunities, consequences, transitions, and subsequent goals.

Do not add a generic pacing timer, periodic encounter injector, or automatic chapter advancement merely to make the run feel structured.

Timed transitions or pacing mechanics are added only when actual game content requires them.

## 14. Story must not become the owner of every gameplay domain

Story effects should remain semantic requests/consequences at the proper owning boundary.

Do not expand Story into low-level commands such as:

- set health;
- damage character directly;
- add arbitrary inventory quantities directly;
- set a door's Unity state;
- spawn an enemy at a concrete Unity vector;
- mutate scene objects;
- teleport transforms.

When authored content needs one of those outcomes, Story requests the semantic action/capability and the owning gameplay system validates and mutates its authoritative state.

## 15. Full-run validation must advance through real semantic gameplay paths

Provide a canonical production full-run validation route.

The scenario driver may perform player/application actions and wait for semantic milestones, for example:

- move/navigate through the production input path;
- interact with characters or WorldObjects;
- perform combat intent;
- pick up/transfer an item;
- wait for objective, quest, encounter, cutscene, or outcome state.

It must not directly force campaign progress through shortcuts such as:

- `CompleteQuest()`;
- `MarkCutsceneCompleted()`;
- `GrantSpell()`;
- `SetCurrentChapter()`;
- `SetOutcome(Success)`.

The validation must prove the authored production chain rather than mutate the answer into existence.

## 16. Use complementary deterministic and built-player proofs

### Deterministic semantic full-run proof

Provide a fast engine-independent test that feeds real semantic domain facts through production progression/story contracts and proves at least one authored route can advance from new-game semantics to the configured terminal outcome without an unintended dead end.

This test should exercise the real rule/progression implementations rather than duplicate the campaign logic in the test.

### Built-player full-run proof

Use the shared built-player infrastructure established by systems 24 and 25 for a slower scheduled/release proof through the actual application/session/input/gameplay composition.

The built-player scenario proves that the semantic route is reachable through production wiring rather than only in isolated rule tests.

Do not build a separate full-run test runtime.

## 17. Failure completion is authored, not inferred

When production content defines a real failure route, prove that its semantic facts resolve the configured system-15 failure result and flow through system 14/system 23 aftermath.

Do not invent a generic failure condition merely to satisfy this system.

Individual character defeat, ordinary combat loss, disconnect, and technical server shutdown remain non-terminal unless authored terminal policy says otherwise.

## 18. Gameplay completion remains separate from technical session shutdown

The intended end-of-run chain is:

`final gameplay fact`

→ authored story/terminal policy

→ system 15 `GameOutcomeResolved`

→ system 14 gameplay aftermath/shutdown coordination

→ persistence/replication where policy requires

→ system 23 outcome/front-end presentation

→ eventual technical network/session teardown.

Do not manufacture a gameplay victory/failure merely because the network session is ending.

## Acceptance / reuse proof

System 26 is complete when the production campaign has at least one evidence-backed authored route that:

1. starts through the normal system-23/system-14 new-game path;
2. advances through real semantic gameplay facts and the unified system-11 progression/story boundaries;
3. extends beyond the opening demonstration into the actual production campaign;
4. crosses multiple owning domains where the authored content requires them without bypassing those owners;
5. reaches exactly one configured system-15 terminal `GameOutcome`;
6. flows through normal system-14/system-23 completion handling;
7. survives a mid-run system-16 save/teardown/restore without replaying prior one-shot content or losing campaign position;
8. remains shared authoritative progression in multiplayer, with clients converging through system 06;
9. is covered by a deterministic semantic full-run test;
10. is covered by a slower production built-player full-run scenario using the shared validation harness.

Where an actual authored failure route exists, add a corresponding failure proof through the same ownership boundaries.

## Explicitly out of scope

- a generic chapter engine;
- a universal `GameState` or `ProgressionManager` replacement;
- procedural quest generation;
- a generic pacing/timer manager;
- arbitrary difficulty progression;
- generic skill-tree infrastructure;
- mandatory traversal of every recovered map;
- inferred campaign sequencing from filenames/catalog order;
- per-player campaign divergence without demonstrated content need;
- replacement Story or Quest engines;
- a special-case final-boss subsystem;
- direct ownership of combat, encounters, inventory, WorldObjects, networking, persistence, UI, audio, or VFX.

## Architectural constraints

- The production campaign is authored content composed over reusable domain systems; it is not another gameplay domain.
- Gameplay produces semantic facts.
- Unified progression evaluates authored goals.
- Story chooses authored consequences.
- System 15 alone commits terminal gameplay outcome.
- System 14 coordinates the running authoritative graph and aftermath.
- Campaign-specific sequencing stays in campaign/content composition.
- Shared systems grow only when real production progression demonstrates a reusable missing semantic boundary.
- World/location identity is not progression identity.
- Save/restore reconstructs current semantic truth rather than replaying historical one-shot events.
- Validation must prove a real semantic path from `NewGame` to `GameOutcomeResolved` without privileged progression shortcuts.
