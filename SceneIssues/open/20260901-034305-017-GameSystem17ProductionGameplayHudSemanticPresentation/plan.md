# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: authoritative state stays in Sessions/Vitality/Encounters/Progression and interaction composition; physical bindings stay Input-owned.

## Observed behavior / acceptance

`KentridgePlayableSlice` still owns prototype `OnGUI` state/prompt text and raw physical-key handling. System17 replaces gameplay HUD truth with semantic local-player projections, isolates controlled-character state, survives reconnect/rebuild, avoids gameplay authority, and owns a focused built-player validation scene under `Assets/Game/Hud/Validation/`.

## Hypotheses / results

1. **Core APIs are sufficient for HUD state.** Supported: Sessions supplies member/readiness/CharacterId, Vitality supplies current health, Encounters supplies lifecycle/membership. `HudSnapshotProjector` reads them without Runtime authority dependencies.
2. **All presentation prerequisites already exist.** Falsified: Input lacked binding-label presentation and System19's compact tracked-objective publisher is still open. System17 added only the minimal Input presentation seam and does not duplicate Progression ownership.
3. **Kentridge can immediately host production HUD composition.** Falsified on reconciled master `39f9fea9992225a66e74b7aac9d00394fcc4daaf`: System14 still owns the one production graph/local-player binding and Kentridge replacement. System17 must not create parallel authorities.
4. **Module validation failure was a HUD product failure.** Falsified twice by runs `33851138044` and `33852244050`. Minimal repro proved nested test roots made the planner claim one runtime asmdef for both parent and child modules. Nearest/deepest module ownership plus focused regression fixed the tooling defect without rearranging Kentridge tests.

## Selected approach

`Hud.Api` defines immutable local-player, vitality, interaction, encounter, readiness, tracked-progression, and transient-event presentation contracts. `HudSnapshotProjector` re-resolves current semantic state every projection, uses `IInputBindingPresentation` for prompt labels, and retains only transient event IDs for dedupe. `RebuildAfterReconnect` baselines old transients while persistent state rebuilds from current queries. `GameplayHudPresenter` is the real production view used by module-local validation; headless regression runs real Sessions/Vitality with no Hud dependency.

## Validation result

Exact feature SHA `1ca523f2fedb05599a445bb1a539d04fbf3e7774` was requested by CI commit `e3fb4b46000882770b983a6653c6d4b5293b3a43`; workflow `33864869873` completed green. Automatic plan derivation passed; `Game.Hud.Tests` passed 11/11, requested projector tests passed 11/11, and `Game.Hud.HeadlessRegression.Tests` passed 1/1. Hud built-player validation passed in 30.1s and emitted its semantic PASS marker. Settled durable screenshot evidence is classified `production-quality`. Canonical Kentridge integration validation and SceneIssue standalone replay also passed. The aggregate module step was 6m34 because it contained 39 assemblies plus five player validations; individual Hud tests/player target were comfortably below the five-minute target limit.

## Remaining gates / blockers

Current documentation head follows the verified implementation; final promotion still requires revalidation after upstream integration. T17-014 is blocked until System19 lands its compact tracked-objective production source. T17-018 and coupled T17-030 are blocked until System14 lands the production graph/stable local-player identity and replaces Kentridge scene-local construction. `origin/master` remains `39f9fea9992225a66e74b7aac9d00394fcc4daaf`, so neither prerequisite is available yet. Keep the SceneIssue in `open/`; do not create substitute authority or weaken acceptance. Once those prerequisites land: fetch/merge current master, integrate only the production Hud adapters/cleanup, rerun exact-SHA module + built-player validation, complete every checkbox, populate closure fields, move directly `open/` -> `closed/`, then PR + auto-merge and monitor required `affected` gate. Never push the feature head directly to master.
