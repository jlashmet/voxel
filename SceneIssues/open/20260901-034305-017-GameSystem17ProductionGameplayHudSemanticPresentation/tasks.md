# 17 Production gameplay HUD & semantic presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Hud.Api` / `Game.Hud.Runtime`
**Execution rule:** HUD projects current replicated/authoritative truth for the local player. It never owns gameplay state or hardcoded physical input.

## API / presentation model

- [x] **T17-001 — Inventory current HUD/debug UI.** Find Kentridge/prototype health labels, prompts, combat/encounter indicators, objective text, connection/loading status and hardcoded key strings.
  - Evidence: `KentridgePlayableSlice` owns prototype `OnGUI` state/loading labels, hardcoded `WASD`/`E`/`Shift`/`Space`/`F10`/`Esc` strings, and locally scans campaign NPC positions to produce `E talk to ...`; `HandleKeys` independently polls `KeyCode.E` for the same interaction.
- [x] **T17-002 — Establish asmdefs.** Hud.Api remains engine-neutral; Hud.Runtime may contain Unity presenters/views and depends only on semantic APIs/presentation binding seams.
  - Evidence: `Game.Hud.Api` is engine-neutral and references only Characters/Input/Sessions APIs; `Game.Hud.Runtime` references Hud/Input/Sessions/Characters/Vitality/Encounters APIs and no gameplay Runtime assembly.
- [x] **T17-003 — Define local-player HUD input model.** Stable local player/member/CharacterId resolution plus current vitality/readiness/encounter/combat summaries actually needed by approved HUD.
  - Evidence: `IHudLocalPlayerResolver` resolves stable `LocalPlayerId -> PartyMemberId`; each projection re-queries `IPartySessionQuery` and uses its current `CharacterId`, then projects vitality/readiness/encounter state.
- [x] **T17-004 — Define semantic interaction prompt model.** WorldObject/action/capability text data plus semantic input action id; physical key/glyph resolution remains Input presentation responsibility.
  - Evidence: `HudInteractionCandidate` carries semantic target/action/capability plus `InputActionId`; `IInputBindingPresentation` and `UnityInputBindingService` keep physical binding/display labels in Input.
- [x] **T17-005 — Define tracked-progression projection seam.** Consume compact system 19 model rather than owning journal/progression state.
  - Evidence: `IHudTrackedProgressionSource` is a compact read-only consumer seam in Hud.Api. System19 remains open and must supply the production source; Hud does not read `IProgressionQuery` or own tracking state.
- [x] **T17-006 — Define transient semantic event handling/dedupe.** State comes from snapshots; temporary feedback consumes event identity and never reconstructs current truth.
  - Evidence: `HudSnapshotProjector` reads persistent state only from current queries; its only retained presentation state is a per-local-player set of consumed transient event ids.

## Runtime / views

- [x] **T17-010 — Build snapshot adapters.** Project GameplayReplication/current APIs into stable HUD view models without caching a second authoritative copy.
  - Evidence: `HudSnapshotProjector` projects current Sessions/Vitality/Encounters plus semantic interaction/input/progression seams on each call; it caches no gameplay snapshot.
- [ ] **T17-011 — Implement vitality presenter/view.** Resolve controlled CharacterId and update from current vitality snapshot.
- [ ] **T17-012 — Implement interaction prompt presenter/view.** Show/hide based on semantic current interaction candidate/capability and resolve binding/glyph through Input seam.
- [ ] **T17-013 — Implement combat/encounter HUD presenter.** Present approved current state without driving Combat/Encounter commands directly.
- [ ] **T17-014 — Integrate tracked progression summary.** Small read-only projection from system 19; no journal ownership.
  - Blocked: System19 is still open on current master; its T19-006/T19-015 own the compact tracked-objective publisher consumed by `IHudTrackedProgressionSource`.
- [ ] **T17-015 — Integrate connection/readiness presentation.** Distinguish reconnecting/resynchronizing/GameplayReady using systems 06/08/20 as appropriate.
- [ ] **T17-016 — Handle InputContext changes.** HUD remains visible/appropriate while Exploration/Combat/Ui/Disabled contexts change; no raw key polling.
- [x] **T17-017 — Rebuild after reconnect/restore.** Clear stale transient state and reconstruct persistent HUD entirely from current semantic snapshots.
  - Evidence: `RebuildAfterReconnect` baselines already-present transient ids; the next `Project` re-resolves member/CharacterId and all persistent snapshots from sources.
- [ ] **T17-018 — Replace prototype/Kentridge hardcoded GUI.** Production composition uses Hud module; remove duplicate labels/prompts after parity.

## Verification

- [ ] **T17-020 — Presenter unit tests without Unity views.** Snapshot -> deterministic view model for vitality/prompt/combat/readiness.
  - Tests added under `Assets/Game/Hud/Tests`; pending exact-SHA execution.
- [ ] **T17-021 — Binding-change regression.** Change Input binding and prove prompt text/glyph updates without gameplay code changes.
  - Regression added using the Input presentation seam; pending exact-SHA execution.
- [ ] **T17-022 — Local-player identity test.** Two local/represented players cannot display each other's controlled-character vitality/prompt state.
  - Regression added with two independent local/member/CharacterId bindings; pending exact-SHA execution.
- [ ] **T17-023 — Reconnect rebuild test.** Current state reappears correctly and old transient events are not replayed.
  - Regression added; pending exact-SHA execution.
- [ ] **T17-024 — Headless gameplay regression.** Authoritative gameplay runs with Hud assembly absent.
- [ ] **T17-025 — Module-local built-player visual validation.** Use shared validation harness and semantic milestones; no bespoke screenshot driver.

## Cleanup / close

- [ ] **T17-030 — Remove hardcoded physical prompt strings and duplicate HUD truth.** Repository search for `Press E`/key-name equivalents and HUD-owned gameplay values.
- [ ] **T17-031 — Boundary audit.** No Inventory journal/party screen authority and no commands that mutate gameplay directly from Hud state.
- [ ] **T17-032 — Close with rebuild proof.** HUD is a pure projection that can be destroyed/recreated from current semantic state at any time.
