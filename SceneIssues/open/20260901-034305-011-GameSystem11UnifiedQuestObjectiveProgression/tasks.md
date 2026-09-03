# 11 Unified quest & objective progression — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** new `Game.Progression.Api` / `Game.Progression.Runtime`, migrating reusable `Game.Quests` mechanics and CampaignRuntime's duplicate objective state.
**Execution rule:** gameplay reports semantic facts; Progression evaluates goals; Story chooses consequences. Do not preserve two authoritative progression stores.

## Baseline / migration design

- [x] **T11-001 — Inventory both current progression owners.** `QuestRuntime` owns quest definitions/status/step state/active index/observations/events/per-quest snapshots and exposes direct `Complete`; `CampaignRuntime` separately owns known/active/completed standalone objective sets and directly completes matching NPC-interaction objectives after Story dispatch. Plan notes record the competing-authority evidence and selected one-runtime migration direction.
- [x] **T11-002 — Catalog all current objective/quest consumers.** Story contracts, known campaign content, CampaignRuntime, existing Progression API/tests, replication adapters and continuity/persistence seams are mapped in `plan.md`; direct Progression migration is selected, with legacy naming allowed only as a stateless/delegating compile-time bridge if an uncatalogued caller requires it.
- [x] **T11-003 — Establish Progression asmdefs.** Reused upstream engine-neutral `Game.Progression.Api`; added `Game.Progression.Runtime` with `noEngineReferences=true` and only `Game.Progression.Api` as a dependency.
- [x] **T11-004 — Decide atomic migration/compatibility facade.** Known callers can migrate directly in this feature. No parallel/stateful Quests facade will be created; any temporary legacy naming must delegate to Progression and be removed by T11-027/T11-041.

## API

- [ ] **T11-010 — Define stable Objective/Quest identities and definitions.** Quest composes objective definitions; standalone objectives use the same primitive.
- [ ] **T11-011 — Define activation/progress/completion state.** One state model for quest steps and campaign standalone objectives, with deterministic ordering/revisions.
- [ ] **T11-012 — Define typed semantic observations.** Start with only demonstrated facts (interaction/site/etc.); add encounter/item vocabulary only when a current consumer requires it.
- [ ] **T11-013 — Define coherent progression snapshot/query interface.** One snapshot contains all authoritative quest/objective state needed by Story, replication, persistence and UI.
- [ ] **T11-014 — Define progression events.** Activation/progress/completion transitions are semantic facts; events do not directly play cutscenes/start encounters/grant rewards.

## Runtime

- [ ] **T11-020 — Extract/generalize QuestRuntime mechanics.** Preserve deterministic behavior while moving reusable state/evaluation into Progression.Runtime.
- [ ] **T11-021 — Run existing quest steps on common objective primitive.** Verify quest composition does not own a separate completion engine.
- [ ] **T11-022 — Run standalone campaign objectives on the same primitive.** Replace CampaignRuntime active/completed objective sets.
- [ ] **T11-023 — Route semantic observations through Progression.** Remove direct `CompleteObjective`/interaction-completion mutations from campaign/gameplay callers.
- [ ] **T11-024 — Emit completion facts to Story.** Story consumes Progression API events/state and decides authored consequences; Progression does not invoke Story Runtime.
- [ ] **T11-025 — Build deterministic snapshot capture/restore.** Active/completed quest/objective state round-trips as one coherent revision.
- [ ] **T11-026 — Provide replication projection seam.** System 06 consumes current progression truth through API/adapters.
- [ ] **T11-027 — Remove compatibility facade once all callers migrate.** If no facade was needed, explicitly mark this task satisfied.

## Verification

- [ ] **T11-030 — Port existing QuestRuntime regression suite.** Behavior remains deterministic under Progression.
- [ ] **T11-031 — Unified travel/interaction objective test.** Known opening standalone objective and a multi-step quest coexist in one runtime/snapshot.
- [ ] **T11-032 — Fact-driven completion test.** Gameplay observation advances the appropriate objective; no caller can directly mark completion through public API.
- [ ] **T11-033 — Snapshot/restore test.** Restore mixed active/completed quests/objectives without replaying completion events.
- [ ] **T11-034 — Story integration test.** Completion event enables authored Story rule/effect while ownership stays separated.
- [ ] **T11-035 — Shared-session progression test.** Current architecture exposes one shared authoritative campaign progression state, not divergent per-player copies.
- [ ] **T11-036 — Run automatic Progression/Story/Campaign dependent tests.**

## Cleanup / close

- [ ] **T11-040 — Delete duplicate CampaignRuntime objective state/direct completion.** Repository search must find no second authoritative objective collection.
- [ ] **T11-041 — Remove/deprecate superseded Quests runtime ownership.** Keep only compatibility naming/API if still intentionally required, not a parallel state machine.
- [ ] **T11-042 — Boundary audit.** No rewards/procedural quest/expression DSL/UI/per-player progression added.
- [ ] **T11-043 — Close with one-snapshot proof.** All quest and standalone objective truth is represented by one deterministic Progression runtime/snapshot.
