# 04 Character AI, autonomous life, perception & intent — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.CharacterAI.Api` / `Game.CharacterAI.Runtime`
**Execution rule:** AI chooses semantic intent; owning gameplay systems execute and validate that intent. Do not clone the existing tactical AI into a parallel framework.

## Baseline / API

- [ ] **T04-001 — Map existing AI ownership.** Inventory tactical combat AI, behavior trees/planners, perception sources, enemy controllers, NPC schedules, and scene-specific decision code; identify reusable mechanics versus content policy.
- [ ] **T04-002 — Establish asmdefs and dependency direction.** `CharacterAI.Runtime` may depend on Characters/Encounters/world-query APIs; gameplay modules must not depend on CharacterAI.Runtime.
- [ ] **T04-003 — Define semantic perception observations.** Represent characters, world objects, sites, encounter/combat context and relevant facts by stable ids/data, never scene objects.
- [ ] **T04-004 — Define goal/intent contract.** Capture what an AI wants to do, not concrete Runtime calls; include deterministic priority/tie-break data only if demonstrated by current AI.
- [ ] **T04-005 — Define AI control/read state.** Expose enabled/control mode/current intent/diagnostic information needed by tests/presentation without leaking planner internals.
- [ ] **T04-006 — Define policy seams.** Keep reusable planner/behavior policy interfaces semantic/config-driven and keep named character schedules/content outside the generic module.

## Runtime / migration

- [ ] **T04-010 — Adapt tactical AI to the common intent seam.** Wrap/reuse current combat decision logic rather than rewriting it; prove it can emit semantic intents.
- [ ] **T04-011 — Build perception adapters.** Consume Characters, Encounters and world-query APIs to produce observations; prohibit direct Runtime/scene traversal for domain truth.
- [ ] **T04-012 — Add persistent non-combat intent execution.** Support at least one authored autonomous behavior outside Combat through the same planner seam.
- [ ] **T04-013 — Implement combat-context transition.** Enter/leave tactical intent based on semantic encounter/combat state while preserving the same CharacterId and AI owner.
- [ ] **T04-014 — Route intents through owner APIs.** Movement, interaction, combat participation, etc. must be requested through their public APIs and may be rejected normally.
- [ ] **T04-015 — Add deterministic update/order policy.** Make equal inputs produce equal intent selection and record any required stable ordering.
- [ ] **T04-016 — Add simulation-LOD hook only if demonstrated.** If far simulation is already required, lower fidelity without changing semantic outcomes; otherwise leave this task satisfied by documenting no current need.

## Verification

- [ ] **T04-020 — Tactical reuse regression.** Existing enemy combat behavior continues through the new intent seam with no parallel tactical planner.
- [ ] **T04-021 — Non-combat reuse fixture.** An autonomous NPC uses the same perception/intent/runtime path outside combat.
- [ ] **T04-022 — Determinism tests.** Same observations/configuration must select the same intent and stable tie break.
- [ ] **T04-023 — Rejection handling test.** When an owning system rejects an AI intent, AI updates from semantic truth rather than mutating the domain directly.
- [ ] **T04-024 — Headless/core tests.** Core planner/perception tests run without Unity scene objects.
- [ ] **T04-025 — Run module and dependent Encounter/Combat tests automatically.**

## Cleanup / close

- [ ] **T04-030 — Remove bypassing AI controllers.** Search for enemy/NPC decision paths that directly mutate Characters/Combat/WorldObjects and migrate or justify them.
- [ ] **T04-031 — Boundary audit.** No quest/story ownership, named schedules, GameObjects, or other module Runtime types in CharacterAI.Api.
- [ ] **T04-032 — Close with two-consumer proof.** One tactical and one non-combat character must demonstrably share the module.
