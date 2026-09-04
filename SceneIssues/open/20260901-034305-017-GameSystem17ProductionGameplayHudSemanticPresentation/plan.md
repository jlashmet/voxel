# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: authoritative state remains in Sessions/Vitality/Encounters/Progression and interaction composition; physical bindings remain Input-owned; quest/journal tracking remains System19-owned local presentation state.

## Observed behavior / acceptance

System17 replaces prototype gameplay HUD truth with semantic projections for the controlled character: vitality, interaction affordance + semantic binding, encounter/combat state, readiness, tracked progression, and transient feedback. HUD must be rebuildable from current state, local-player safe, independent from gameplay authority, proven by focused regressions and module-local built-player validation, and used by the canonical Kentridge production composition for its applicable gameplay surfaces.

## Material findings

1. Sessions/Vitality/Encounters APIs support the reusable projector; Input needed and now owns the minimal `IInputBindingPresentation` / semantic action reader seam.
2. Production Kentridge uses the reusable `HudSnapshotProjector` + `GameplayHudPresenter`, with canonical character/vitality/encounter/input-context queries. The overlapping prototype state/prompt GUI and hardcoded gameplay key labels were removed.
3. Exact-SHA workflow `33869572240` on production source `3775c5816af10e1d5d4b6253898068ac00de3a5a` passed repository-derived module tests/player validation, canonical Kentridge full-app validation, and standalone SceneIssue replay; durable Hud/Kentridge captures were directly inspected and classified production-quality.
4. System19 merged to master as `283b512cf6dac4feba5f1cfd5b9d79ef0b3075e8`; agent-9 merged it as true two-parent commit `f2af015d2b0d8fa1fc7c3bdfa70cc56c96ec4d76`. System19 publishes `ITrackedObjectiveProjection` / `TrackedObjectiveSummary` and retains all local track/select/journal ownership.
5. System17 now adds `TrackedObjectiveHudSource`, a read-only System19-API-to-Hud adapter. It maps only the local player's supplied tracked summary into `HudTrackedProgressionView`; it cannot select, track, rebuild, or mutate progression. Focused tests cover mapping and local-player isolation. Hud module validation now feeds the real adapter from an `ITrackedObjectiveProjection`, so standalone visual proof exercises the production integration boundary rather than a parallel `IHudTrackedProgressionSource` fixture.
6. A proposed Kentridge auto-selection adapter was rejected and removed before validation because choosing an objective would duplicate System19 tracking policy. A Kentridge scene with no composed journal selection legitimately supplies no tracked objective; Hud remains a consumer only.

## Selected approach / non-goals

Keep `HudSnapshotProjector` read-only and `GameplayHudPresenter` as the sole production HUD visual path. `Game.Hud.Runtime` references `Game.ProgressionPresentation.Api` only, never System19 Runtime or Progression Runtime. Do not add journal/party/inventory authority, gameplay mutation, hardcoded physical input, scene-local duplicate gameplay/progression state, private-field reflection, auto-tracking policy, or unrelated refactors.

## Remaining gates

Fetch/merge any newly advanced master, run a new exact-SHA targeted request on the current production/test branch head, inspect Hud module and canonical Kentridge built-player evidence directly, and require all selected regressions/module gates to pass. Then check T17-014 with exact evidence, confirm every task/acceptance item, close open→closed with closure fields, merge current master again if needed, open/update the final PR, enable auto-merge, and monitor the required `affected` gate until merged and the closed SceneIssue is visible on master.
