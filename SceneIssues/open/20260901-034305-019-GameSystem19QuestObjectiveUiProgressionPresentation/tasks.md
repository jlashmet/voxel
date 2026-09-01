# 19 Quest & objective UI / progression presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.ProgressionPresentation.Api` / `Game.ProgressionPresentation.Runtime`
**Execution rule:** system 11 owns progression truth. This module owns local journal/tracking presentation only.

## API / model

- [ ] **T19-001 — Inventory current quest/objective UI.** Find campaign objective labels, quest lists, tracked state, local completion controls and direct CampaignRuntime/QuestRuntime reads.
- [ ] **T19-002 — Establish asmdefs.** Runtime consumes Progression.Api and replicated/current-state APIs; no Progression Runtime reference.
- [ ] **T19-003 — Define coherent journal read model.** Derive entries from one Progression snapshot/revision with semantic quest/objective ids, visible text metadata and authoritative state.
- [ ] **T19-004 — Define local tracking/selection model.** Track/collapse/filter/sort are local presentation preferences and cannot alter Progression state.
- [ ] **T19-005 — Define visibility/spoiler contract.** Presentation only exposes content marked visible/known by authoritative definition/state; hidden future objectives stay absent.
- [ ] **T19-006 — Define compact tracked-objective projection for HUD.** Small read-only seam consumed by system 17 without shared mutable UI state.

## Runtime / views

- [ ] **T19-010 — Consume unified Progression snapshots.** Remove logic piecing together CampaignRuntime objectives and QuestRuntime separately.
- [ ] **T19-011 — Build journal presenter/view model.** Stable ordering/grouping and state transitions from authoritative snapshot.
- [ ] **T19-012 — Implement local tracking.** User can select tracked objective without emitting a gameplay command or replicated mutation.
- [ ] **T19-013 — Implement local sorting/filtering/collapse.** Preserve across navigation according to local preference policy only.
- [ ] **T19-014 — Enforce spoiler visibility.** Activation/reveal is driven by Progression content/state; UI cannot enumerate hidden definitions for display.
- [ ] **T19-015 — Integrate HUD projection.** Publish current tracked summary to Hud API/presenter without Hud reading internal journal state.
- [ ] **T19-016 — Rebuild after reconnect/restore.** Recreate journal from current Progression snapshot and reconcile local tracking when tracked objective no longer exists/is visible.
- [ ] **T19-017 — Remove direct completion/debug UI from production.** Any test-only controls remain isolated from production assembly and cannot satisfy acceptance.

## Verification

- [ ] **T19-020 — Activation/completion transition tests.** View model follows one coherent revision and never displays impossible mixed states.
- [ ] **T19-021 — Visibility tests.** Hidden objectives/quests do not leak before authoritative reveal; reveal makes them available deterministically.
- [ ] **T19-022 — Local-tracking independence test.** Changing tracked objective leaves authoritative Progression snapshot unchanged.
- [ ] **T19-023 — Shared multiplayer progression test.** Different clients may track differently while viewing the same shared authoritative quest/objective completion state.
- [ ] **T19-024 — Reconnect/restore rebuild test.** Current progression reconstructs journal without replaying prior completion notifications.
- [ ] **T19-025 — Module-local built-player visual validation through shared harness.**

## Cleanup / close

- [ ] **T19-030 — Remove duplicate progression stores/direct completion controls.** Repository search in UI/presentation code.
- [ ] **T19-031 — Scope audit.** No generic accept/decline semantics, map/minimap system or gameplay mutation added.
- [ ] **T19-032 — Close with projection proof.** Journal/HUD tracking can be destroyed/recreated independently while system 11 remains the only progression authority.
