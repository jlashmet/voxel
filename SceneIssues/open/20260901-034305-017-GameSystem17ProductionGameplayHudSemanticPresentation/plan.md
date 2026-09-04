# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: authoritative state remains in Sessions/Vitality/Encounters/Progression and interaction composition; physical bindings remain Input-owned.

## Observed behavior / acceptance

Current `KentridgePlayableSlice` owns prototype `OnGUI` state text and recomputes a nearby conversation NPC, while production input directly polls `KeyCode.E` and the HUD strings hardcode `E`, `WASD`, `Shift`, `Space`, `F10`, and `Esc`. System17 must replace gameplay HUD truth with semantic snapshot projections, isolate local-player identity, survive reconnect/rebuild, and own a module-local built-player validation scene.

## Hypotheses / discrimination

1. **Core authoritative APIs are sufficient for HUD state.** Supported: `Sessions.Api` exposes member/readiness/CharacterId, `Vitality.Api` exposes immutable vitality queries, and `Encounters.Api` exposes current membership/lifecycle. The first implementation projects these without Runtime dependencies.
2. **All presentation prerequisites already exist.** Falsified on master `e18efe82ce1b4aa069031165d40bac14a9269412`: Input has no semantic action-to-binding-label seam, and System19's compact tracked-objective HUD projection is still an open SceneIssue. Add only the minimal Input presentation seam required by System17; do not duplicate System19 journal/tracking state in Hud.

## Selected approach

`Hud.Api` defines immutable local-player, vitality, interaction, encounter, readiness, tracked-progression, and transient-event presentation contracts. `HudSnapshotProjector` resolves the local party member and controlled CharacterId on every projection, reads current snapshots directly, derives active encounter state, resolves prompt labels through `IInputBindingPresentation`, and retains only transient event IDs for presentation dedupe. `RebuildAfterReconnect` baselines old transient events while persistent state is immediately rebuilt from current queries. `UnityInputBindingService` supplies the minimal semantic Input binding/label seam and keeps physical key ownership outside Hud.

## Remaining gates / blockers

Build the production Unity HUD view and Kentridge adapter, remove prototype HUD labels/prompt duplication, add module-local Hud validation using the real presenter, and validate visual quality. System19 must provide its tracked-objective source before T17-014 can close; if production Kentridge still lacks authoritative Sessions local-player wiring, keep that integration blocked while completing independent Hud validation. Then run exact-SHA CI, reconcile current master, close directly open→closed, and promote only through PR + auto-merge with the required `affected` gate.
