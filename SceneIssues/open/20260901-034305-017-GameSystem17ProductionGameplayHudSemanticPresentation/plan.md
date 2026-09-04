# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: authoritative state remains in Sessions/Vitality/Encounters/Progression and interaction composition; physical bindings remain Input-owned.

## Observed behavior / acceptance

System17 replaces prototype gameplay HUD truth with semantic projections for the controlled character: vitality, interaction affordance + semantic action binding, encounter/combat state, readiness, tracked progression, and transient feedback. HUD must remain rebuildable from current state, independent from gameplay authority, local-player safe, and proven through module-local built-player validation. Canonical Kentridge production composition must use the same Hud path and remove overlapping prototype labels/prompts.

## Material findings

1. Core Sessions/Vitality/Encounters APIs are sufficient for the reusable projector and have green regression coverage.
2. Input originally lacked an action-to-display-binding seam; System17 added the minimal semantic `IInputBindingPresentation` + `UnityInputBindingService` without moving physical binding ownership into Hud.
3. System14 is resolved on master and the production Kentridge graph is now integrated with System17. `KentridgeGameplayHudInstaller` consumes only canonical read/query surfaces from the character/vitality/encounter/input-context owners; no parallel gameplay authority was added.
4. System19 remains open on current master `aa61895f28d70f35c67d07db6a4fa93beee635eb`. Its T19-006/T19-015 compact tracked-objective publisher is still unavailable, so T17-014 remains the only unchecked feature item.
5. Exact-SHA workflow `33869572240` validated feature SHA `3775c5816af10e1d5d4b6253898068ac00de3a5a` after the production Kentridge integration: repository-derived module tests passed, Hud module player validation passed, Kentridge module player validation passed, canonical full-application Kentridge integration passed, and standalone SceneIssue replay passed. Direct screenshot inspection confirms the real production HUD is visible in both focused Hud and Kentridge application evidence with readable hierarchy/alignment and no validation-only visual path.
6. The earlier Kentridge nested-test ownership planner collision was isolated after two identical failures. A temporary branch-local repair enabled proof, but System14 subsequently removed the nested ownership layout on master; the temporary tooling diff is intentionally not carried forward.
7. Master advanced once after that validation with closed System20 SessionPresentation work only; it does not satisfy System19's tracked-progression contract.

## Selected approach

Keep `HudSnapshotProjector` as a read-only projector over semantic query contracts and keep `GameplayHudPresenter` as the production visual path. Kentridge now exposes only the minimum canonical query surfaces from the System14-owned production graph/extension and composes Hud through Kentridge-specific adapters; it creates no second session, vitality, encounter, combat, or progression authority. Input semantic reading/binding is shared between gameplay actions and prompt labels, preventing displayed bindings from drifting from interaction input.

The overlapping Kentridge prototype state/prompt GUI has been removed after parity proof. Non-HUD loading/dialogue and developer-only diagnostics remain outside Hud when they are not player-facing gameplay truth. Tracked progression integration waits for System19's production compact publisher.

## Do not build

No inventory journal or party-screen authority; no gameplay command/mutation from Hud; no scene-local duplicate session/vitality/encounter/progression authority; no hardcoded physical key names in Hud; no bespoke validation-only visual path; no unrelated CI/planner refactor.

## Remaining gates

Current branch bookkeeping head is `f7dcaebaf545bced2ac3ae112430eae060351175`; current master is `aa61895f28d70f35c67d07db6a4fa93beee635eb`. Merge current master into `fixes/agent-9` so the branch remains current, but do not run a replacement exact request merely for unrelated System20 changes. T17-014 remains blocked until System19 lands. Once System19 is available: fetch and merge current master, adapt its compact tracked-objective publisher to `IHudTrackedProgressionSource`, add focused regression only if the new adapter needs it, rerun exact-SHA validation on the then-current feature SHA, complete every remaining checkbox, move only this SceneIssue open→closed with closure fields, then open the final PR, enable auto-merge, and monitor the required `affected` gate until the closed issue is visible on master.
