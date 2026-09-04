# 17 Production gameplay HUD & semantic presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Hud.Api` / `Game.Hud.Runtime`
**Execution rule:** HUD projects current replicated/authoritative truth for the local player. It never owns gameplay state or hardcoded physical input.

## API / presentation model

- [x] **T17-001 — Inventory current HUD/debug UI.** Audited Kentridge prototype labels/prompts/status/objective text and hardcoded physical key strings.
- [x] **T17-002 — Establish asmdefs.** `Game.Hud.Api` is engine-neutral; `Game.Hud.Runtime` depends only on semantic API/presentation seams, not gameplay Runtime authority.
- [x] **T17-003 — Define local-player HUD input model.** Stable `LocalPlayerId -> PartyMemberId -> CharacterId` resolution projects current vitality/readiness/encounter state.
- [x] **T17-004 — Define semantic interaction prompt model.** HUD receives semantic action/capability plus `InputActionId`; Input owns physical binding/glyph presentation.
- [x] **T17-005 — Define tracked-progression projection seam.** `IHudTrackedProgressionSource` is compact/read-only; System19 owns tracking/journal presentation state.
- [x] **T17-006 — Define transient semantic event handling/dedupe.** Persistent state comes from current snapshots; only transient event IDs are retained for presentation dedupe.

## Runtime / views

- [x] **T17-010 — Build snapshot adapters.** `HudSnapshotProjector` re-queries current Sessions/Vitality/Encounters/Input/progression sources and caches no gameplay snapshot.
- [x] **T17-011 — Implement vitality presenter/view.** Controlled CharacterId is re-resolved and current vitality is rendered.
- [x] **T17-012 — Implement interaction prompt presenter/view.** Prompt visibility is semantic/context-driven and display binding comes from Input presentation.
- [x] **T17-013 — Implement combat/encounter HUD presenter.** Current encounter/combat state is read-only presentation; HUD exposes no gameplay mutation interface.
- [x] **T17-014 — Integrate tracked progression summary.** Kentridge composes canonical System11 `IProgressionQuery` -> System19 `KentridgeTrackedObjectiveProjection` -> System17 `TrackedObjectiveHudSource` -> `HudSnapshotProjector`; no journal/progression ownership was added. Exact feature SHA `6802b7dc8bcabb193e767e38f6e820a9aecf2848`, request commit `ef27f13b6e422dc00a9fcec2f08b4ec640b0da40`, workflow `33915072026`: focused `Game.Kentridge.PlayableSlice.Tests.KentridgeTrackedObjectiveProjectionTests` passed 1/1, repository-derived affected validation passed, HUD player validation emitted `objective=Reach the gate source=System19`, canonical Kentridge full-app validation passed, standalone replay finished with zero harness assertion failures, and durable HUD/Kentridge evidence was directly inspected.
- [x] **T17-015 — Integrate connection/readiness presentation.** Disconnected/connected-not-ready/gameplay-ready states map to reconnecting/resynchronizing/gameplay-ready presentation with regression coverage.
- [x] **T17-016 — Handle InputContext changes.** Persistent HUD remains appropriate across Exploration/Combat/Ui/Disabled; interaction visibility is context-sensitive without raw key polling.
- [x] **T17-017 — Rebuild after reconnect/restore.** Persistent HUD reconstructs from current semantic snapshots while old transient IDs are baselined.
- [x] **T17-018 — Replace prototype/Kentridge hardcoded GUI.** Production Kentridge uses `KentridgeGameplayHudInstaller` + reusable projector/presenter; duplicate prototype state/prompt GUI and hardcoded gameplay key-label truth were removed.

## Verification

- [x] **T17-020 — Presenter unit tests without Unity views.** Deterministic snapshot-to-view-model coverage for vitality/prompt/combat/readiness passed in exact-SHA validation.
- [x] **T17-021 — Binding-change regression.** Rebinding Interact updates projected binding label without changing gameplay semantics.
- [x] **T17-022 — Local-player identity test.** Two local/represented players cannot display each other's controlled-character HUD state.
- [x] **T17-023 — Reconnect rebuild test.** Updated authoritative state reappears after rebuild and pre-reconnect transients are not replayed.
- [x] **T17-024 — Headless gameplay regression.** Independent authoritative gameplay runs and passes without a Hud assembly dependency.
- [x] **T17-025 — Module-local built-player visual validation.** `Assets/Game/Hud/Validation/HudSemanticPresentationValidation.unity` uses production projector/presenter/input paths; final artifact inspection classified the relevant HUD surface production-quality for this feature acceptance.
- [x] **T17-026 — Isolate exact-SHA planner blocker and prove the cause.** Prior nested-module ownership failure was root-caused via minimal repro; the temporary agent repair was later dropped after master contained the canonical repository fix.

## Cleanup / close

- [x] **T17-030 — Remove hardcoded physical prompt strings and duplicate HUD truth.** Shipped Kentridge gameplay HUD no longer owns hardcoded movement/interaction prompt truth or duplicate gameplay state panels.
- [x] **T17-031 — Boundary audit.** Hud owns read/presentation contracts only and has no Inventory/journal/party authority or gameplay mutation services.
- [x] **T17-032 — Close with rebuild proof.** Presenter/projector can be destroyed/recreated from current semantic state; exact-SHA reconnect and final production-hook validation are green.

## Final validation record

- Verified feature SHA: `6802b7dc8bcabb193e767e38f6e820a9aecf2848`
- Targeted-CI request commit: `ef27f13b6e422dc00a9fcec2f08b4ec640b0da40`
- Workflow: `33915072026` — **success**
- Focused regression: 1 passed, 0 failed/skipped/inconclusive
- Repository-derived module validation: **passed**
- HUD module built-player validation: **passed**, `source=System19`
- Canonical Kentridge full-application validation: **passed**
- Standalone SceneIssue replay: **passed**, zero harness assertion failures
- Durable HUD/Kentridge screenshots: directly inspected; relevant semantic HUD classified **production-quality** for this feature acceptance.
