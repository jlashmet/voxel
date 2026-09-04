# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: authoritative state remains in Sessions/Vitality/Encounters/Progression and interaction composition; physical bindings remain Input-owned.

## Observed behavior / acceptance

Current `KentridgePlayableSlice` owns prototype `OnGUI` state text and recomputes a nearby conversation NPC, while production input directly polls `KeyCode.E` and the HUD strings hardcode `E`, `WASD`, `Shift`, `Space`, `F10`, and `Esc`. System17 must replace gameplay HUD truth with semantic snapshot projections, isolate local-player identity, survive reconnect/rebuild, and own a module-local built-player validation scene.

## Hypotheses / discrimination

1. **Core authoritative APIs are sufficient for HUD state.** Supported: `Sessions.Api` exposes member/readiness/CharacterId, `Vitality.Api` exposes immutable vitality queries, and `Encounters.Api` exposes current membership/lifecycle. The first implementation projects these without Runtime dependencies.
2. **All presentation prerequisites already exist.** Falsified on master `e18efe82ce1b4aa069031165d40bac14a9269412`: Input has no semantic action-to-binding-label seam, and System19's compact tracked-objective HUD projection is still an open SceneIssue. Add only the minimal Input presentation seam required by System17; do not duplicate System19 journal/tracking state in Hud.
3. **Kentridge can immediately host the production HUD graph.** Falsified after reconciling master `39f9fea9992225a66e74b7aac9d00394fcc4daaf`: Kentridge still constructs scene-local campaign/input/world behavior and has no authoritative `PartySession`/Vitality/Encounter graph plus stable local-player -> party-member binding. System14 explicitly owns the one production graph (T14-010) and replacement of Kentridge scene-local construction (T14-019), so System17 must not invent parallel authorities to unblock composition.
4. **Exact-SHA module validation can currently execute for a production Input/Hud change.** Falsified by workflow run `33851138044` for request commit `be9badbc0e2a1f4ea07e3c0ad56b9e1b9f8b5cdc`: before Unity tests, `tools/module-validation-plan.py` aborts with `runtime assembly token has multiple module owners: Game.Kentridge.PlayableSlice`. Root cause is pre-existing overlapping module roots at `Assets/Game/Composition/Kentridge/Playable/Tests` and nested `Playable/SceneRuntime/Tests`; the planner recursively attributes the nested runtime asmdef to both. This is not a System17 gameplay defect, so do not mutate unrelated Kentridge test ownership on this branch. Retry only after master fixes that infrastructure/layout collision.

## Selected approach

`Hud.Api` defines immutable local-player, vitality, interaction, encounter, readiness, tracked-progression, and transient-event presentation contracts. `HudSnapshotProjector` resolves the local party member and controlled CharacterId on every projection, reads current snapshots directly, derives active encounter state, resolves prompt labels through `IInputBindingPresentation`, and retains only transient event IDs for presentation dedupe. `RebuildAfterReconnect` baselines old transient events while persistent state is immediately rebuilt from current queries. `UnityInputBindingService` supplies the minimal semantic Input binding/label seam and keeps physical key ownership outside Hud.

The production Unity presenter and module-local validation scene exercise the real Hud presentation path with deterministic semantic providers; player validation uses the real `UnityInputBindingService` as well as the real projector/presenter. A separate headless regression test assembly intentionally has no Hud dependency and drives real Sessions/Vitality runtime state. Production Kentridge hookup remains blocked until System14 supplies the production graph/local-player identity seam; tracked-objective hookup remains blocked until System19 supplies its compact projection.

## Do not build

No inventory journal or party-screen authority, no gameplay commands/mutation from HUD state, no scene-local duplicate source of authoritative truth, and no hardcoded physical key names in Hud. Do not create a second Kentridge production graph to work around System14, and do not repair unrelated Kentridge module ownership solely to make System17 CI green.

## Remaining gates / blockers

Current branch was reconciled with master through merge commit `b8627f3643c06c7fdd2155a1a3501ad4d3e2530a`. Independent Hud implementation, semantic regressions, headless dependency regression, production-grade presenter polish, and module-root validation assets are present. Exact executable verification is currently blocked before Unity tests by the Kentridge module-owner planner collision above. T17-014 also remains blocked on System19 T19-006/T19-015; T17-018/T17-030 remain blocked on System14 T14-010/T14-019. Re-fetch/reconcile current master before the next exact-SHA request; if the planner collision is fixed, run the full selected module tests and standalone Hud player validation and inspect built-player evidence at the repo-required quality bar. Only after every task and acceptance item is actually complete: close directly open→closed, populate closure fields, then promote through PR + auto-merge and the required `affected` gate; never update master directly.
