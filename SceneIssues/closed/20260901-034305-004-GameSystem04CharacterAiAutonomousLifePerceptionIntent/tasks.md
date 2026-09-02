# 04 Character AI, autonomous life, perception & intent — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.CharacterAI.Api` / `Game.CharacterAI.Runtime`
**Execution rule:** AI chooses semantic intent; owning gameplay systems execute and validate that intent. Do not clone the existing tactical AI into a parallel framework.

## Baseline / API

- [x] **T04-001 — Map existing AI ownership.** Production tactical mechanics are `CombatAiBattleDriver`; `ChainEnemyTacticalAI` is an isolated older Combat-board prototype; Kentridge drives the production driver directly. No shared behavior-tree or named NPC schedule framework was found.
- [x] **T04-002 — Establish asmdefs and dependency direction.** `CharacterAI.Api` is engine-neutral; generic Runtime references only CharacterAI.Api + Characters.Api. Combat-specific Runtime dependencies are isolated in `Game.CharacterAI.CombatAdapter`.
- [x] **T04-003 — Define semantic perception observations.** Characters, world objects, sites, encounters/combat context and facts use `CharacterId`/semantic ids/data only.
- [x] **T04-004 — Define goal/intent contract.** `AiIntent` captures semantic desired action/target plus deterministic priority/tie-break metadata, not component calls.
- [x] **T04-005 — Define AI control/read state.** Enabled/control mode/current intent/last acceptance/diagnostic are exposed without planner internals.
- [x] **T04-006 — Define policy seams.** `IAiPerceptionSource`, `IAiIntentPolicy`, and `IAiIntentExecutor` are semantic; `SemanticIntentPolicy` is configuration-driven with no named-character schedule policy.

## Runtime / migration

- [x] **T04-010 — Adapt tactical AI to the common intent seam.** Combat adapter emits/consumes `TacticalCombat` intent and delegates actual tactical selection/execution to the existing `CombatAiBattleDriver`; no parallel tactical planner was added.
- [x] **T04-011 — Build perception adapters.** `CombatPerceptionSource` translates Combat public authority to semantic combat/character observations with no scene traversal.
- [x] **T04-012 — Add persistent non-combat intent execution.** Independent autonomous NPC fixture uses the same controller/rule/executor path for semantic movement intent outside Combat.
- [x] **T04-013 — Implement combat-context transition.** Regression preserves one CharacterId/controller as semantic observations switch autonomous to tactical context.
- [x] **T04-014 — Route intents through owner APIs.** Generic Runtime only calls `IAiIntentExecutor`; Combat adapter delegates to Combat's existing command-owning driver; rejection is a normal result.
- [x] **T04-015 — Add deterministic update/order policy.** Rule priority is descending and equal priority uses ordinal `TieBreakKey`; each tick starts with a fresh semantic observation.
- [x] **T04-016 — Add simulation-LOD hook only if demonstrated.** No current far-simulation requirement is demonstrated, so no parallel LOD planner is added; composition owns tick frequency while semantics remain unchanged.

## Verification

- [x] **T04-020 — Tactical reuse regression.** Headless test routes existing `CombatAiBattleDriver` behavior through common CharacterAI intent/controller seams and verifies Combat action execution.
- [x] **T04-021 — Non-combat reuse fixture.** Independent non-Kentridge market-going NPC fixture uses the same perception/intent/runtime path.
- [x] **T04-022 — Determinism tests.** Same observations/config select the same intent and ordinal tie-break deterministically.
- [x] **T04-023 — Rejection handling test.** Rejected move intent is followed by fresh semantic perception; changed truth produces Idle rather than direct domain mutation.
- [x] **T04-024 — Headless/core tests.** CharacterAI API/Runtime/CombatAdapter are engine-neutral and regressions require no scene objects.
- [x] **T04-025 — Run module and dependent Encounter/Combat tests automatically.** Exact feature SHA `0b2537735738aadab770f2e423ba3c0984fff053` passed targeted request `4926ca7399aa9ffefb72cf3b6d82f9c60f5b0a6d`, workflow run `33485434902`, job `99784291857`: focused CharacterAI tests, automatic module validation, and standalone SceneIssue replay all succeeded.

## Cleanup / close

- [x] **T04-030 — Remove bypassing AI controllers.** No new per-scene AI registry/controller bypass was introduced. Existing Kentridge direct Combat driver and legacy `ChainEnemyTacticalAI` remain Combat-owned tactical mechanics; CharacterAI wraps the production driver rather than duplicating/moving Combat authority.
- [x] **T04-031 — Boundary audit.** CharacterAI.Api contains no quest/story ownership, named schedules, GameObjects, or other module Runtime types; generic CharacterAI.Runtime has no Combat/scene dependency.
- [x] **T04-032 — Close with two-consumer proof.** Tactical Combat adapter and independent non-combat NPC fixture demonstrably share `CharacterAiController` + semantic contracts.
