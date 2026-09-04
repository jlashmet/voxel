# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: authoritative state remains in Sessions/Vitality/Encounters/Progression and interaction composition; physical bindings remain Input-owned.

## Observed behavior / acceptance

System17 replaces prototype gameplay HUD truth with semantic projections for the controlled character: vitality, interaction affordance + semantic action binding, encounter/combat state, readiness, tracked progression, and transient feedback. HUD must remain rebuildable from current state, independent from gameplay authority, local-player safe, and proven through module-local built-player validation. Canonical Kentridge production composition must use the same Hud path and remove overlapping prototype labels/prompts.

## Material findings

1. Core Sessions/Vitality/Encounters APIs are sufficient for the reusable projector and have green regression coverage.
2. Input originally lacked an action-to-display-binding seam; System17 added the minimal semantic `IInputBindingPresentation` + `UnityInputBindingService` without moving physical binding ownership into Hud.
3. System14 was initially an external blocker because Kentridge lacked a single production gameplay graph. That blocker is now resolved on master `75e2fae9e76484103b0a12495914bce7135f9d82`; agent-9 has reconciled that production graph and can complete T17-018/T17-030 against it.
4. System19 remains open on current master and its T19-006/T19-015 compact tracked-objective publisher is still unavailable. T17-014 must remain blocked rather than duplicating progression/journal authority.
5. Exact-SHA run `33864869873` proved the reusable Hud implementation: `Game.Hud.Tests` 11/11, headless/no-Hud regression 1/1, module-local Hud standalone player validation green, canonical Kentridge integration green, and settled Hud evidence classified production-quality.
6. The earlier Kentridge nested-test ownership planner collision was isolated after two identical failures. A temporary branch-local repair enabled proof, but System14 subsequently removed the nested ownership layout on master; the temporary tooling diff is intentionally not carried forward.

## Selected approach

Keep `HudSnapshotProjector` as a read-only projector over semantic query contracts and keep `GameplayHudPresenter` as the production visual path. For Kentridge, expose only the minimum canonical query surfaces from the System14-owned production graph/extension and compose Hud through Kentridge-specific adapters; do not create another session, vitality, encounter, combat, or progression authority. Reuse the Input semantic reader/binding service for both gameplay action input and prompt labels where practical, so displayed bindings and actual interaction input cannot drift.

Production Kentridge cleanup removes only overlapping prototype HUD/prompt truth after the real Hud presenter has parity. Non-HUD developer/validation diagnostics remain only when they are not player-facing gameplay truth. Tracked progression integration waits for System19’s production compact publisher.

## Do not build

No inventory journal or party-screen authority; no gameplay command/mutation from Hud; no scene-local duplicate session/vitality/encounter/progression authority; no hardcoded physical key names in Hud; no bespoke validation-only visual path; no unrelated CI/planner refactor now that master has the canonical ownership layout.

## Remaining gates

Current master reconciliation target is `75e2fae9e76484103b0a12495914bce7135f9d82`. Finish T17-018 production Kentridge composition and T17-030 cleanup, add/update focused regression only where required for that integration, then run exact-SHA targeted CI and inspect both Hud module and Kentridge built-player evidence. T17-014 remains blocked until System19 lands. Do not close while that required checkbox is incomplete. Once System19 is available: merge current master again, add the thin tracked-progression adapter, rerun exact-SHA validation, complete all remaining checkboxes, move only this SceneIssue open→closed with closure fields, then PR + auto-merge and monitor the required `affected` gate until the closed issue is visible on master.
