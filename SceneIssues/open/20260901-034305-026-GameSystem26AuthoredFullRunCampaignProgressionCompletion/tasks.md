# 26 Authored full-run campaign progression & completion — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** production campaign Story/Progression content under composition/content assemblies; reuse existing Story plus system 11 Progression. No generic GameLoop/Chapter runtime.
**Execution rule:** recover/author only evidence-backed progression. Gameplay produces facts, Progression evaluates goals, Story chooses consequences, system 15 commits terminal outcome.

## Evidence and route definition

- [x] **T26-001 — Inventory current production campaign content.** Mapped `KnownOpeningCampaignContent`, Story rules/events/effects, unified objectives/quests, cutscenes, NPC/site bindings, persistence snapshot, and the opening-only endpoint in `route-evidence.md`.
- [x] **T26-002 — Inventory recovered source evidence beyond opening.** Recorded normalized regions/sites and verified upstream positive dependency chains separately from inferred filename/quest-label guidance in `route-evidence.md`.
- [x] **T26-003 — Define one evidence-backed completion route.** Canonical opening -> church -> Rorik/Moordell/Rossdam -> Logan-castle terminal spine is documented, with disconnected-component bridges explicitly labeled authored design rather than recovered chronology.
- [x] **T26-004 — Mark optional content.** Optional recovered branches are listed in `route-evidence.md` and are explicitly non-gating.
- [x] **T26-005 — Identify missing semantic vocabulary.** Gap list is limited to owning-domain encounter-resolution input and Outcome-condition effect; existing site/NPC/cutscene/Progression semantics cover the rest.

## Campaign content decomposition

- [ ] **T26-010 — Decompose `KnownOpeningCampaignContent.Build` responsibilities.** Separate manageable authored content slices/helpers for sites, NPC roles, objectives/quests, cutscenes/bindings and Story rules while preserving existing opening behavior.
- [ ] **T26-011 — Avoid premature chapter abstraction.** Use plain content composition functions/classes; introduce a reusable chapter/slice interface only if at least two concrete slices demonstrate the same contract.
- [ ] **T26-012 — Author/recover the next progression slice.** Add semantic site/NPC/encounter/objective/cutscene rules backed by evidence and route through existing owning APIs.
- [ ] **T26-013 — Continue authored slices to terminal route.** Each slice advances only from real semantic facts and has a deterministic next-condition or intentionally optional branch.
- [ ] **T26-014 — Keep geography separate from progression.** Entering a region/site generates semantic facts; Story rules decide consequences based on progression state, with no `CurrentChapter++`/map-index phase ownership.
- [ ] **T26-015 — Extend Story event/condition vocabulary minimally.** Add facts such as EncounterCompleted/WorldObject interaction/item acquisition only when T26-005 demonstrates the route requires them, and source each from the owning API.
- [ ] **T26-016 — Extend Story effects minimally.** Effects request semantic owning-domain actions; do not add generic damage/inventory/world mutation/teleport setters to Story.
- [ ] **T26-017 — Integrate system 11 unified Progression.** All quest/standalone objective truth uses Progression snapshot/events; remove any campaign-local objective bookkeeping encountered during route work.

## Terminal outcome / lifecycle

- [ ] **T26-020 — Define authored terminal rule.** The final evidence-backed semantic condition requests `GameOutcome` resolution with configured disposition/OutcomeRef; no final-boss/last-scene special case.
- [ ] **T26-021 — Route terminal request through system 15.** Story/composition requests outcome; Outcomes commits exactly once and system 14 coordinates aftermath.
- [ ] **T26-022 — Integrate frontend aftermath.** System 23 observes resolved run/application flow through approved presentation/orchestration seams; campaign content does not load outcome scenes directly.
- [ ] **T26-023 — Verify ordinary losses remain nonterminal unless authored.** Character/combat/encounter failures only end the run where explicit campaign policy maps them to an outcome.

## Fast semantic route proof

- [ ] **T26-030 — Build engine-independent canonical route test.** Start production campaign through `NewGame` semantic path and drive real domain facts/Story/Progression APIs to terminal outcome.
- [ ] **T26-031 — Prohibit privileged progression shortcuts in the test.** No direct `CompleteQuest`, `CompleteObjective`, `MarkCutsceneCompleted` without actual cutscene completion path, `GrantSpell`, `SetOutcome`, chapter setters or private state mutation.
- [ ] **T26-032 — Assert every route milestone.** Opening, multiple later authored consequences, objective/quest transitions, encounter/interactions as applicable and exactly one `GameOutcomeResolved`.
- [ ] **T26-033 — Add dead-end regression.** Canonical route test fails with a diagnostic naming the last semantic milestone if no authored rule can advance the required route.
- [ ] **T26-034 — Verify optional content does not gate canonical completion.** Skip at least one optional branch and still reach the terminal outcome.

## Persistence / multiplayer / built-player proof

- [ ] **T26-040 — Choose a meaningful mid-run restore point.** Save after multiple authored consequences beyond opening, including unified Progression state.
- [ ] **T26-041 — Restore through systems 16/14.** Fresh graph resumes current campaign state with completed one-shots/cutscenes/progression represented correctly and no historical replay.
- [ ] **T26-042 — Continue canonical route after restore.** Real semantic player/gameplay facts advance to terminal outcome from restored state.
- [ ] **T26-043 — Verify shared multiplayer progression/outcome.** **BLOCKED external prerequisite:** current master still has the single-process `tools/player-validation.py`; reuse system 25 infrastructure when it lands. Do not create an alternate transport/process harness.
- [ ] **T26-044 — Add canonical built-player full-run scenario.** Start through system 23/14 production path, drive only real player/semantic actions through the shared harness and reach the terminal outcome.
- [ ] **T26-045 — Make full-run scenario milestone-driven.** Bounded waits on semantic current state; no long blind sleeps or direct authority setters.
- [ ] **T26-046 — Classify full-run validation appropriately.** Keep fast semantic route tests in normal affected-module coverage; expensive complete built-player route uses scheduled/release tier while still automatically selected by repository conventions.

## Cleanup / close

- [ ] **T26-050 — Search for parallel progression/game-loop state.** Remove `CurrentChapter`, generic phase counters, final-boss completion flags or campaign-local objective stores introduced/left by prior code where they duplicate approved owners.
- [ ] **T26-051 — Search Story effects for domain-god operations.** Ensure Story only coordinates semantic actions/facts and does not directly mutate vitality/inventory/world/transport/presentation internals.
- [ ] **T26-052 — Verify recovered-map ordering claims.** Every required route ordering decision has repository/source evidence or explicit authored design evidence; filenames alone are not accepted proof.
- [ ] **T26-053 — Run automatic domain/campaign tests plus built-player full-run gate.** Include mid-run restore and shared outcome evidence.
- [ ] **T26-054 — Close with end-to-end semantic proof.** There is at least one real, evidence-backed, testable production path from normal New Game to immutable `GameOutcomeResolved`, crossing beyond the opening and using no parallel game-loop runtime.
