# 17 Production gameplay HUD & semantic presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Hud.Api` / `Game.Hud.Runtime`
**Execution rule:** HUD projects current replicated/authoritative truth for the local player. It never owns gameplay state, combat sequencing or hardcoded physical input.

## API / presentation model

- [ ] **T17-001 — Inventory current HUD/debug UI.** Find Kentridge/prototype health labels, prompts, combat/encounter indicators, objective text, connection/loading status and hardcoded key strings.
- [ ] **T17-002 — Establish asmdefs.** Hud.Api remains engine-neutral; Hud.Runtime may contain Unity presenters/views and depends only on semantic APIs/presentation binding seams.
- [ ] **T17-003 — Define local-player HUD input model.** Stable local player/member/CharacterId resolution plus current vitality/readiness/encounter/combat summaries actually needed by approved HUD.
- [ ] **T17-004 — Define semantic interaction prompt model.** WorldObject/action/capability text data plus semantic input action id; physical key/glyph resolution remains Input presentation responsibility.
- [ ] **T17-005 — Define tracked-progression projection seam.** Consume compact system 19 model rather than owning journal/progression state.
- [ ] **T17-006 — Define transient semantic event handling/dedupe.** State comes from snapshots; temporary feedback consumes event identity and never reconstructs current truth.
- [ ] **T17-007 — Define squad-beat combat projection.** Consume Combat-owned current active member, bounded upcoming member sequence, semantic action choices, beat timing/commit state and transient combo-opportunity data without recreating combat logic in Hud.
- [ ] **T17-008 — Define predictive combo-preview contract.** Preview/forecast data is explicitly non-authoritative, distinguishes likely/eligible event interactions from confirmed results, and contains no mutation capability.

## Runtime / views

- [ ] **T17-010 — Build snapshot adapters.** Project GameplayReplication/current APIs into stable HUD view models without caching a second authoritative copy.
- [ ] **T17-011 — Implement vitality presenter/view.** Resolve controlled CharacterId and update from current vitality snapshot.
- [ ] **T17-012 — Implement interaction prompt presenter/view.** Show/hide based on semantic current interaction candidate/capability and resolve binding/glyph through Input seam.
- [ ] **T17-013 — Implement combat/encounter HUD presenter.** Present approved current state without driving Combat/Encounter commands directly.
- [ ] **T17-014 — Integrate tracked progression summary.** Small read-only projection from system 19; no journal ownership.
- [ ] **T17-015 — Integrate connection/readiness presentation.** Distinguish reconnecting/resynchronizing/GameplayReady using systems 06/08/20 as appropriate.
- [ ] **T17-016 — Handle InputContext changes.** HUD remains visible/appropriate while Exploration/Combat/Ui/Disabled contexts change; no raw key polling.
- [ ] **T17-017 — Rebuild after reconnect/restore.** Clear stale transient state and reconstruct persistent HUD entirely from current semantic snapshots.
- [ ] **T17-018 — Replace prototype/Kentridge hardcoded GUI.** Production composition uses Hud module; remove duplicate labels/prompts after parity.
- [ ] **T17-019 — Implement squad-beat action/forecast presentation.** Make `current active member -> available move -> upcoming members` readable at a glance, show teammate beat/commit state only where useful, and visualize event-driven combo opportunities/trajectories without requiring a dense list of status reaction types.

## Verification

- [ ] **T17-020 — Presenter unit tests without Unity views.** Snapshot -> deterministic view model for vitality/prompt/combat/readiness.
- [ ] **T17-021 — Binding-change regression.** Change Input binding and prove prompt text/glyph updates without gameplay code changes.
- [ ] **T17-022 — Local-player identity test.** Two local/represented players cannot display each other's controlled-character vitality/prompt/action state.
- [ ] **T17-023 — Reconnect rebuild test.** Current state reappears correctly and old transient events are not replayed.
- [ ] **T17-024 — Headless gameplay regression.** Authoritative gameplay runs with Hud assembly absent.
- [ ] **T17-025 — Module-local built-player visual validation.** Use shared validation harness and semantic milestones; no bespoke screenshot driver.
- [ ] **T17-026 — Beat-action affordance regression.** Only the system-selected active member receives deliberate-action affordances for the current beat; non-active squad members remain visible/meaningful without presenting extra turns.
- [ ] **T17-027 — Combo-preview readability/isolation proof.** A representative movement/projectile/impact opportunity shows which character/event can join, redirect or transform it, while disabling/removing the HUD leaves authoritative resolution unchanged.

## Cleanup / close

- [ ] **T17-030 — Remove hardcoded physical prompt strings and duplicate HUD truth.** Repository search for `Press E`/key-name equivalents and HUD-owned gameplay values.
- [ ] **T17-031 — Boundary audit.** No Inventory journal/party screen authority, Combat beat/sequence authority or commands that mutate gameplay directly from Hud state.
- [ ] **T17-032 — Close with rebuild proof.** HUD is a pure projection that can be destroyed/recreated from current semantic state at any time, including mid-combat beat.
