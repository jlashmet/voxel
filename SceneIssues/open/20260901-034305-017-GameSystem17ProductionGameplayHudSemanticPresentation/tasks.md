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
- [x] **T17-011 — Implement vitality presenter/view.** Resolve controlled CharacterId and update from current vitality snapshot.
  - Evidence: every `GameplayHudPresenter.OnGUI` projection re-resolves the member's current CharacterId; `HudSnapshotProjector` then queries `IVitalityQuery` for that CharacterId and the presenter renders current/maximum/defeat state.
- [x] **T17-012 — Implement interaction prompt presenter/view.** Show/hide based on semantic current interaction candidate/capability and resolve binding/glyph through Input seam.
  - Evidence: the projector accepts only `HudInteractionCandidate` semantic action/capability data, resolves its display label through `IInputBindingPresentation`, and the presenter renders that projected label/action/capability without polling a physical key.
- [x] **T17-013 — Implement combat/encounter HUD presenter.** Present approved current state without driving Combat/Encounter commands directly.
  - Evidence: the projector reads `IEncounterQuery` snapshots for the controlled CharacterId; the presenter renders encounter kind/lifecycle/combat-required state and exposes no Encounter or Combat mutation interface.
- [ ] **T17-014 — Integrate tracked progression summary.** Small read-only projection from system 19; no journal ownership.
  - Blocked: System19 is still open on current master; its T19-006/T19-015 own the compact tracked-objective publisher consumed by `IHudTrackedProgressionSource`.
- [x] **T17-015 — Integrate connection/readiness presentation.** Distinguish reconnecting/resynchronizing/GameplayReady using systems 06/08/20 as appropriate.
  - Evidence: current Sessions presence/readiness is projected as Disconnected -> Reconnecting, Connected but not GameplayReady -> Resynchronizing, and GameplayReady -> GameplayReady. Parameterized regression coverage was added for Connected, Synchronized, Disconnected, and GameplayReady states.
- [x] **T17-016 — Handle InputContext changes.** HUD remains visible/appropriate while Exploration/Combat/Ui/Disabled contexts change; no raw key polling.
  - Evidence: persistent vitality/encounter/readiness remains projected in every context; interaction is visible only in Exploration/Combat and hidden in Ui/Disabled. Regressions cover Combat, Ui, and Disabled.
- [x] **T17-017 — Rebuild after reconnect/restore.** Clear stale transient state and reconstruct persistent HUD entirely from current semantic snapshots.
  - Evidence: `RebuildAfterReconnect` baselines already-present transient ids; the next `Project` re-resolves member/CharacterId and all persistent snapshots from sources.
- [ ] **T17-018 — Replace prototype/Kentridge hardcoded GUI.** Production composition uses Hud module; remove duplicate labels/prompts after parity.
  - System14 production graph is now present on current master (`75e2fae...`). Next: wire the canonical Kentridge production services into Hud via semantic query adapters, then remove the overlapping prototype `OnGUI` state/prompt presentation. Do not introduce another gameplay/session authority.

## Verification

- [x] **T17-020 — Presenter unit tests without Unity views.** Snapshot -> deterministic view model for vitality/prompt/combat/readiness.
  - Exact-SHA feature `1ca523f2fedb05599a445bb1a539d04fbf3e7774`, request `e3fb4b46000882770b983a6653c6d4b5293b3a43`, workflow `33864869873`: repository-selected `Game.Hud.Tests` passed 11/11 with 0 failed/skipped/inconclusive; requested `Game.Hud.Tests.HudSnapshotProjectorTests` also passed 11/11.
- [x] **T17-021 — Binding-change regression.** Change Input binding and prove prompt text/glyph updates without gameplay code changes.
  - Verified in the green 11/11 projector suite: real `UnityInputBindingService.Rebind(Interact, KeyCode.F)` changes only the projected binding label to `F` while target/action semantics remain unchanged.
- [x] **T17-022 — Local-player identity test.** Two local/represented players cannot display each other's controlled-character vitality/prompt state.
  - Verified in the green projector suite with two independent local/member/CharacterId bindings and distinct vitality/interaction state.
- [x] **T17-023 — Reconnect rebuild test.** Current state reappears correctly and old transient events are not replayed.
  - Verified in the green projector suite: updated authoritative vitality is reprojected after rebuild, pre-reconnect transient IDs are baselined, and only newly arriving events display afterward.
- [x] **T17-024 — Headless gameplay regression.** Authoritative gameplay runs with Hud assembly absent.
  - Exact-SHA workflow `33864869873`: independent `Game.Hud.HeadlessRegression.Tests` passed 1/1. The assembly has no Hud reference, drives real `PartySession` through readiness/start and real `VitalityRegistry` through damage, and asserts Sessions/Vitality/test assemblies do not reference `Game.Hud`.
- [x] **T17-025 — Module-local built-player visual validation.** Use shared validation harness and semantic milestones; no bespoke screenshot driver.
  - Exact-SHA workflow `33864869873`: `Assets/Game/Hud/Validation/HudSemanticPresentationValidation.unity` passed in 30.1s using the real `HudSnapshotProjector`, production `GameplayHudPresenter`, and real `UnityInputBindingService`; player log emitted `HUD_SEMANTIC_PRESENTATION_VALIDATION PASS vitality=73/100 prompt=E combat=true readiness=GameplayReady objective=Reach the gate`. Durable settled screenshot evidence is classified **production-quality**: crisp readable hierarchy, consistent panel alignment, semantic state accents, immediate vitality/prompt readability, and no placeholder or validation-only presentation path. Canonical Kentridge integration validation and the separate SceneIssue standalone replay also passed.
- [x] **T17-026 — Isolate exact-SHA planner blocker and prove the cause.** Nested Kentridge module roots were assigning one runtime asmdef to multiple module owners.
  - Evidence: after two identical CI failures, a minimal repro proved nearest/deepest ownership was required; the temporary agent-9 planner repair enabled green workflow `33864869873`. Current master subsequently removed the nested Kentridge test-root collision in System14, so the temporary tooling change was intentionally dropped while merging master and canonical repository tooling is retained.

## Cleanup / close

- [ ] **T17-030 — Remove hardcoded physical prompt strings and duplicate HUD truth.** Repository search for `Press E`/key-name equivalents and HUD-owned gameplay values.
  - Kentridge cleanup is coupled to T17-018. With System14 now merged, remove the prototype prompt/state surface only after the production Hud composition consumes the same canonical gameplay sources.
- [x] **T17-031 — Boundary audit.** No Inventory journal/party screen authority and no commands that mutate gameplay directly from Hud state.
  - Evidence: Hud.Api exposes read/presentation contracts only; Hud.Runtime references Query interfaces (`IPartySessionQuery`, `IVitalityQuery`, `IEncounterQuery`) plus Input presentation/context seams. Neither Hud assembly references Inventory, journal/party UI authority, or gameplay mutation services.
- [x] **T17-032 — Close with rebuild proof.** HUD is a pure projection that can be destroyed/recreated from current semantic state at any time.
  - Evidence: `GameplayHudPresenter` owns no gameplay state and calls the provider for a fresh snapshot each draw. `HudSnapshotProjector` owns no persistent gameplay snapshot; every `Project` re-resolves member/controlled CharacterId and current Sessions/Vitality/Encounter/interaction/progression state. The exact-SHA reconnect regression proves current state changes are reprojected and only transient event IDs require presentation dedupe/baselining.
