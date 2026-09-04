# 19 Quest & objective UI / progression presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.ProgressionPresentation.Api` / `Game.ProgressionPresentation.Runtime`
**Execution rule:** system 11 owns progression truth. This module owns local journal/tracking presentation only.

## API / model

- [x] **T19-001 — Inventory current quest/objective UI.** Current master has no ProgressionPresentation/HUD progression store; legacy `QuestRuntime` is a compatibility facade over System11 and no production completion/debug UI was found to migrate.
- [x] **T19-002 — Establish asmdefs.** Runtime consumes `Progression.Api`, `GameplayReplication.Api`, and `Sessions.Api`; no Progression Runtime reference.
- [x] **T19-003 — Define coherent journal read model.** Entries derive from one Progression snapshot/revision with semantic quest/objective ids, visible text metadata and authoritative state.
- [x] **T19-004 — Define local tracking/selection model.** Track/collapse/filter/sort are local presentation preferences and cannot alter Progression state.
- [x] **T19-005 — Define visibility/spoiler contract.** Presentation only exposes content visible by authoritative lifecycle or explicitly authored as known while inactive.
- [x] **T19-006 — Define compact tracked-objective projection for HUD.** `ITrackedObjectiveProjection` is a small read-only seam; no shared mutable UI state.

## Runtime / views

- [x] **T19-010 — Consume unified Progression snapshots.** Journal reads exactly one `IProgressionQuery.Snapshot()` per rebuild; no CampaignRuntime/QuestRuntime composition.
- [x] **T19-011 — Build journal presenter/view model.** Stable ordering/grouping and state transitions derive from the authoritative snapshot; equal authored order preserves snapshot order deterministically.
- [x] **T19-012 — Implement local tracking.** Selecting/tracking only updates `JournalLocalPreferences`; no gameplay command or replicated mutation is emitted.
- [x] **T19-013 — Implement local sorting/filtering/collapse.** Preferences survive presenter recreation when the same local preference object is retained.
- [x] **T19-014 — Enforce spoiler visibility.** Inactive hidden quests/objectives are absent until authority reveals them; authored known-inactive content may be shown explicitly.
- [x] **T19-015 — Integrate HUD projection.** System19 publishes `ITrackedObjectiveProjection`; current master has no System17 HUD assembly, so this semantic seam is the integration boundary without an unmerged dependency.
- [x] **T19-016 — Rebuild after reconnect/restore.** Presenter reconstructs from current Progression snapshot and clears local selection/tracking that no longer exists or is visible.
- [x] **T19-017 — Remove direct completion/debug UI from production.** Production module exposes no completion command/control; validation-only state changes are isolated under `Validation`.
- [x] **T19-018 — Include unified standalone campaign objectives.** Discovered acceptance-required work: project `ProgressionSnapshot.StandaloneObjectives` directly with semantic objective keys rather than synthetic quest state.

## Verification

- [ ] **T19-020 — Activation/completion transition tests.** View model follows one coherent revision and never displays impossible mixed states.
- [ ] **T19-021 — Visibility tests.** Hidden objectives/quests do not leak before authoritative reveal; reveal makes them available deterministically.
- [ ] **T19-022 — Local-tracking independence test.** Changing tracked objective leaves authoritative Progression snapshot unchanged.
- [ ] **T19-023 — Shared multiplayer progression test.** Different clients may track differently while viewing the same shared authoritative quest/objective completion state.
- [ ] **T19-024 — Reconnect/restore rebuild test.** Current progression reconstructs journal without replaying prior completion notifications.
- [ ] **T19-025 — Module-local built-player visual validation through shared harness.**

## Cleanup / close

- [ ] **T19-030 — Remove duplicate progression stores/direct completion controls.** Repository search in UI/presentation code.
- [x] **T19-031 — Scope audit.** No generic accept/decline semantics, map/minimap system or gameplay mutation added.
- [ ] **T19-032 — Close with projection proof.** Journal/HUD tracking can be destroyed/recreated independently while system 11 remains the only progression authority.
