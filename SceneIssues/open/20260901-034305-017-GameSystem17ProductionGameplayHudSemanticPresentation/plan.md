# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: authoritative state stays in Sessions/Vitality/Encounters/Progression and interaction composition; physical bindings stay Input-owned.

## Observed behavior / acceptance

`KentridgePlayableSlice` still owns prototype `OnGUI` state/prompt text and raw physical-key handling. System17 must replace gameplay HUD truth with semantic local-player projections, isolate controlled-character state, survive reconnect/rebuild, avoid gameplay authority, and own a focused built-player validation scene under `Assets/Game/Hud/Validation/`.

## Hypotheses / results

1. **Core APIs are sufficient for HUD state.** Supported: Sessions supplies member/readiness/CharacterId, Vitality supplies current health, Encounters supplies lifecycle/membership. `HudSnapshotProjector` reads them without Runtime authority dependencies.
2. **All presentation prerequisites already exist.** Falsified: Input lacked binding-label presentation and System19's compact tracked-objective publisher is still open. System17 added only the minimal Input presentation seam and does not duplicate Progression ownership.
3. **Kentridge can immediately host production HUD composition.** Falsified on reconciled master `39f9fea9992225a66e74b7aac9d00394fcc4daaf`: System14 still owns the one production graph/local-player binding and Kentridge replacement. System17 must not create parallel authorities.
4. **Module validation failure is a HUD product failure.** Falsified twice by runs `33851138044` and `33852244050`: both abort before Unity module tests with `runtime assembly token has multiple module owners: Game.Kentridge.PlayableSlice`; the second run still passes standalone Kentridge replay. Minimal repro shows nested test roots make the planner attribute one runtime asmdef to both parent and child modules. The narrow fix is planner ownership by nearest/deepest module root, covered by `tools/tests/test_module_validation_nested_roots.py`; do not rearrange unrelated Kentridge tests.

## Selected approach

`Hud.Api` defines immutable local-player, vitality, interaction, encounter, readiness, tracked-progression, and transient-event presentation contracts. `HudSnapshotProjector` re-resolves current semantic state every projection, uses `IInputBindingPresentation` for prompt labels, and retains only transient event IDs for dedupe. `RebuildAfterReconnect` baselines old transients while persistent state rebuilds from current queries. `GameplayHudPresenter` is the real production view used by module-local validation; headless regression runs real Sessions/Vitality with no Hud dependency.

## Remaining gates / blockers

Branch includes the nested-module planner regression/fix through `aa1a1726ee0ac9a4e120d2701c086725f2540726`; local minimal regression passes. Run a new exact-SHA targeted request and require selected Hud/Input tests plus Hud module player validation and canonical Kentridge replay to pass; inspect durable Hud screenshot evidence as `production-quality`. T17-014 remains blocked until System19 lands its compact tracked-objective source. T17-018/T17-030 remain blocked until System14 lands production composition/local-player identity. Reconcile current master before final validation/closure. Close only after every checkbox is complete, then PR + auto-merge; never push the feature head directly to master.
