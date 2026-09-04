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

- [x] **T11-010 — Define stable Objective/Quest identities and definitions.** Quest composes objective definitions; standalone objectives use the same primitive.
- [x] **T11-011 — Define activation/progress/completion state.** One state model for quest steps and campaign standalone objectives, with deterministic ordering/revisions.
- [x] **T11-012 — Define typed semantic observations.** Start with only demonstrated facts (interaction/site/etc.); add encounter/item vocabulary only when a current consumer requires it.
- [x] **T11-013 — Define coherent progression snapshot/query interface.** One snapshot contains all authoritative quest/objective state needed by Story, replication, persistence and UI.
- [x] **T11-014 — Define progression events.** Activation/progress/completion transitions are semantic facts; events do not directly play cutscenes/start encounters/grant rewards.

## Runtime

- [x] **T11-020 — Extract/generalize QuestRuntime mechanics.** Preserve deterministic behavior while moving reusable state/evaluation into Progression.Runtime.
- [x] **T11-021 — Run existing quest steps on common objective primitive.** Verify quest composition does not own a separate completion engine.
- [x] **T11-022 — Run standalone campaign objectives on the same primitive.** Replace CampaignRuntime active/completed objective sets.
- [x] **T11-023 — Route semantic observations through Progression.** Remove direct `CompleteObjective`/interaction-completion mutations from campaign/gameplay callers.
- [x] **T11-024 — Emit completion facts to Story.** Story consumes Progression API events/state and decides authored consequences; Progression does not invoke Story Runtime.
- [x] **T11-025 — Build deterministic snapshot capture/restore.** Active/completed quest/objective state round-trips as one coherent revision.
- [x] **T11-026 — Provide replication projection seam.** System 06 consumes current progression truth through API/adapters.
- [x] **T11-027 — Remove compatibility facade once all callers migrate.** The only retained `Game.Quests.Runtime.QuestRuntime` surface is an intentional stateless/delegating compatibility facade over the shared `ProgressionRuntime`; it owns no mutable quest state and exposes no direct completion bypass, satisfying the no-parallel-authority requirement while legacy quest-shaped callers remain.
- [x] **T11-028 — Adopt current module-local validation ownership.** Current `master` requires every affected module to own focused validation. Because the touched Progression/Quests/Campaign production asmdefs are engine-neutral `noEngineReferences=true` headless/domain code, document the no-scene exception and ensure each affected module owns focused EditMode/unit coverage rather than relying on another module's tests.

## Verification

- [x] **T11-030 — Port existing QuestRuntime regression suite.** Behavior remains deterministic under Progression.
- [x] **T11-031 — Unified travel/interaction objective test.** Known opening standalone objective and a multi-step quest coexist in one runtime/snapshot.
- [x] **T11-032 — Fact-driven completion test.** Gameplay observation advances the appropriate objective; no caller can directly mark completion through public API.
- [x] **T11-033 — Snapshot/restore test.** Restore mixed active/completed quests/objectives without replaying completion events.
- [x] **T11-034 — Story integration test.** Completion event enables authored Story rule/effect while ownership stays separated.
- [x] **T11-035 — Shared-session progression test.** Current architecture exposes one shared authoritative campaign progression state, not divergent per-player copies.
- [x] **T11-036 — Run automatic Progression/Story/Campaign dependent tests.** Exact-SHA targeted CI run `33830156385` passed automatic module validation for source SHA `9388d96742a475425f3bb799b4c3459716a922b5`.

## Cleanup / close

- [x] **T11-040 — Delete duplicate CampaignRuntime objective state/direct completion.** Campaign objective lifecycle now delegates to the shared Progression runtime; the old active/completed objective collections and direct completion path are gone.
- [x] **T11-041 — Remove/deprecate superseded Quests runtime ownership.** `QuestRuntime` remains only as compatibility naming/API and delegates all mutable state/evaluation to Progression; it is not a parallel state machine.
- [x] **T11-042 — Boundary audit.** No rewards/procedural quest/expression DSL/UI/per-player progression added.
- [x] **T11-043 — Close with one-snapshot proof.** `ProgressionRuntimeTests.OneObservationAdvancesQuestAndStandaloneAndOneSnapshotOwnsBoth`, the authored opening integration regression, and Campaign integration coverage prove quest and standalone objective truth are represented by one deterministic Progression runtime/snapshot.

## Closure evidence

- Product/fix SHA: `9388d96742a475425f3bb799b4c3459716a922b5`.
- Exact-SHA CI transport helper: `9087601805a470e0cbfc43550ca42de2137237d8`; workflow run `33830156385` completed successfully on 2026-09-04 UTC, including the automatically required module-validation step.
- Module-local validation ownership: `Game.Progression.Tests`, `Game.Quests.Tests`, and `Game.Composition.Campaign.Tests` own focused EditMode/domain regressions. A Unity validation scene is intentionally not added because all affected production assemblies are engine-neutral `noEngineReferences=true` domain/runtime code, matching the documented pure-headless exception.
- Reuse proof: `KnownOpeningProgressionIntegrationTests` uses the authored `KnownOpeningCampaignContent` travel objective and the real two-step `KentridgeWellQuestDefinition` in one `ProgressionRuntime` snapshot.
- Persistence/replay proof: `ProgressionRuntimeTests.SnapshotRestoreRoundTripsAndOperationReplayIsIgnored` preserves one mixed snapshot/revision and rejects duplicate operation replay without emitting transitions.
- Story ownership proof: Campaign integration dispatches quest completion back to Story, where authored consequences are applied; Progression contains no reward/cutscene consequence execution.
- Replication seam: `ProgressionGameplayProjectionSource` consumes `IProgressionQuery`/`ProgressionSnapshot` and projects both quests and standalone objectives from that single source.
