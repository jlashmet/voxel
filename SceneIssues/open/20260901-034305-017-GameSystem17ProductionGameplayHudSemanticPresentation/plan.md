# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: Sessions/Vitality/Encounters/Progression remain authoritative; physical bindings remain Input-owned; System19 owns journal/tracking selection.

## Acceptance / approach

Replace prototype HUD truth with semantic, local-player-safe projections for vitality, interaction + binding, encounter/combat, readiness, tracked progression, and transient feedback. Keep `HudSnapshotProjector` read-only and `GameplayHudPresenter` as the sole production HUD visual path. Kentridge consumes the same production path. No journal/party/inventory authority, gameplay mutation, hardcoded physical input, duplicate progression state, private-field reflection, or auto-tracking policy.

For the final System19 integration, the competing hypotheses were: (A) the adapter/assembly dependency graph was incomplete; (B) the System19 projection contract itself was incompatible with the HUD seam. Exact compiler artifacts discriminate these directly.

## Material findings

1. Existing Sessions/Vitality/Encounters APIs support the projector; Input owns `IInputBindingPresentation` / semantic action reading. Production Kentridge now uses the reusable projector/presenter and no longer renders overlapping prototype state/prompt GUI or hardcoded gameplay key labels.
2. Workflow `33869572240` on source `3775c5816af10e1d5d4b6253898068ac00de3a5a` passed repository-derived module/player validation, canonical Kentridge full-app validation, and standalone replay. Hud/Kentridge captures were directly inspected and classified production-quality.
3. System19 merged as `283b512cf6dac4feba5f1cfd5b9d79ef0b3075e8`, publishing `ITrackedObjectiveProjection` / `TrackedObjectiveSummary`. System17’s `TrackedObjectiveHudSource` is a read-only adapter; it cannot select, track, rebuild, or mutate progression. Focused tests cover mapping and local-player isolation. Validation exercises the real adapter.
4. Request `0efa0d87193cf695555cc635f1c72b96f3d76e34`, workflow `33886339957`, failed compilation because `TrackedObjectiveHudSource` lacked `Game.Input.Api` for `LocalPlayerId`; fixed in `b627a7daaab37bd9f68fd56cd074a788695bdd60`.
5. After syncing System18-only master `d08612dfe2f4a99aff34897717569744565bc642`, request `d35dbaaad150c1eea13a28790acc424db7be807a`, workflow `33887634857`, failed compilation with CS0012: System19 summaries expose `ObjectiveId`, `QuestId`, and `ProgressionLifecycleState` from `Game.Progression.Api`. This proves hypothesis A; the adapter contract itself is not the failure. `Game.Hud.Runtime` therefore requires the API-only `Game.Progression.Api` reference in addition to `Game.ProgressionPresentation.Api`; it still references neither System19 Runtime nor Progression Runtime.
6. Request `0a603607cfd6ee6b8b75dd5afda3420ffedc863f`, workflow `33898731613`, compiled past the production adapter dependency but failed because the new `TrackedObjectiveHudSourceTests.cs` regression itself used `LocalPlayerId` without importing `Game.Input.Api`. The test assembly already references `Game.Input.Api`; artifact `single-test-33898731613` isolates CS0246 to that test file. The fix is therefore a test-only namespace import, with no production behavior or ownership change.

## Remaining gates

Run exact-SHA targeted CI on the post-fix feature head. Require focused regression, repository-derived module tests/player validations, canonical Kentridge full-app validation, and standalone SceneIssue replay to pass; inspect durable Hud/Kentridge built-player evidence directly. Then check T17-014, confirm every task/acceptance item, close open→closed with closure fields, refresh/merge current master if needed, open/update PR, enable auto-merge, monitor required `affected`, and finish only after the closed SceneIssue is visible on master.
