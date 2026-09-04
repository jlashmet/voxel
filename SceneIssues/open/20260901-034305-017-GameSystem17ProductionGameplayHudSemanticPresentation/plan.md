# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: authoritative state remains in Sessions/Vitality/Encounters/Progression and interaction composition; physical bindings remain Input-owned.

## Observed behavior / acceptance

Current `KentridgePlayableSlice` owns prototype `OnGUI` state text and recomputes a nearby conversation NPC, while production input directly polls `KeyCode.E` and the HUD strings hardcode `E`, `WASD`, `Shift`, `Space`, `F10`, and `Esc`. System17 must replace gameplay HUD truth with semantic snapshot projections, isolate local-player identity, survive reconnect/rebuild, and own a module-local built-player validation scene.

## Hypotheses / discrimination

1. **Core authoritative APIs are sufficient for HUD state.** Supported: `Sessions.Api` exposes member/readiness/CharacterId, `Vitality.Api` exposes immutable vitality queries, and `Encounters.Api` exposes current membership/lifecycle. The first implementation projects these without Runtime dependencies.
2. **All presentation prerequisites already exist.** Falsified on master `e18efe82ce1b4aa069031165d40bac14a9269412`: Input has no semantic action-to-binding-label seam, and System19's compact tracked-objective HUD projection is still an open SceneIssue. Add only the minimal Input presentation seam required by System17; do not duplicate System19 journal/tracking state in Hud.
3. **Kentridge can immediately host the production HUD graph.** Falsified after reconciling master `39f9fea9992225a66e74b7aac9d00394fcc4daaf`: Kentridge still constructs scene-local campaign/input/world behavior and has no authoritative `PartySession`/Vitality/Encounter graph plus stable local-player -> party-member binding. System14 explicitly owns the one production graph (T14-010) and replacement of Kentridge scene-local construction (T14-019), so System17 must not invent parallel authorities to unblock composition.

## Selected approach

`Hud.Api` defines immutable local-player, vitality, interaction, encounter, readiness, tracked-progression, and transient-event presentation contracts. `HudSnapshotProjector` resolves the local party member and controlled CharacterId on every projection, reads current snapshots directly, derives active encounter state, resolves prompt labels through `IInputBindingPresentation`, and retains only transient event IDs for presentation dedupe. `RebuildAfterReconnect` baselines old transient events while persistent state is immediately rebuilt from current queries. `UnityInputBindingService` supplies the minimal semantic Input binding/label seam and keeps physical key ownership outside Hud.

The production Unity presenter and module-local validation scene exercise the real Hud presentation path with deterministic semantic providers. Production Kentridge hookup remains blocked until System14 supplies the production graph/local-player identity seam; tracked-objective hookup remains blocked until System19 supplies its compact projection. Independent Hud implementation, regression tests, visual validation, and boundary audits continue while those upstream tasks are open.

## Remaining gates / blockers

Current branch was reconciled with master through merge commit `b8627f3643c06c7fdd2155a1a3501ad4d3e2530a`. Run exact-SHA CI for the independent Hud tests first and fix product failures. Then run the module-local standalone-player visual validation and inspect evidence at the repo-required quality bar. Keep T17-014 blocked on System19 T19-006/T19-015 and T17-018/T17-030 Kentridge replacement blocked on System14 T14-010/T14-019 until those production seams exist. Reconcile current master again before final validation/closure. Only after every task and acceptance item is actually complete: close directly open→closed, then promote through PR + auto-merge and the required `affected` gate; never update master directly.
