# 17 Production gameplay HUD & semantic presentation — implementation plan

**Ownership:** `Game.Hud.Api` / `Game.Hud.Runtime`. HUD is presentation only: Sessions/Vitality/Encounters/Progression remain authoritative; physical bindings remain Input-owned; System19 owns journal/tracking selection.

## Acceptance / architecture

The production HUD projects semantic, local-player-safe vitality, interaction/binding, encounter/combat, readiness, tracked progression, and transient feedback. `HudSnapshotProjector` is read-only; `GameplayHudPresenter` is the sole production HUD visual path. Kentridge consumes the same path. No journal/party/inventory authority, gameplay mutation, hardcoded physical-input truth, duplicate progression state, or private-field reflection is introduced.

Affected player-visible ownership is `Assets/Game/Hud` with module-local `Validation/HudSemanticPresentationValidation.unity` + scenario. Kentridge is an integration consumer and owns its separate focused composition regression.

## Material results

- Earlier exact-SHA HUD validation proved projector semantics, local-player isolation, binding changes, reconnect rebuild, headless gameplay without Hud, module-local player validation, and production Kentridge HUD replacement.
- System19 publishes `ITrackedObjectiveProjection`; System17 consumes it only through read-only `TrackedObjectiveHudSource`. `Game.Hud.Runtime` needs API-only `Game.Progression.Api` because System19 summaries expose progression API value types; it does not depend on Progression Runtime or System19 Runtime.
- Kentridge now composes `KentridgeTrackedObjectiveProjection` from canonical System11 `IProgressionQuery`, feeds it through System19 presentation, then adapts it into the existing HUD projector. Each refresh re-reads current authority; HUD owns no progression truth.
- The final compile defect was isolated to the new regression missing `using Game.Input.Api;`; production behavior was unchanged. The test-only fix is commit `d967bc7ca30ac8f3c3672eb7c8cdc5e877795661`.
- Final verified feature SHA `6802b7dc8bcabb193e767e38f6e820a9aecf2848` was requested by CI commit `ef27f13b6e422dc00a9fcec2f08b4ec640b0da40`. Workflow `33915072026` passed the focused Kentridge projection regression (1/1), all repository-derived affected tests/module player validations, canonical Kentridge full-app validation, standalone SceneIssue replay, artifact upload, and final commit status.
- Direct artifact inspection classified the semantic HUD surface **production-quality for this feature acceptance**: vitality/readiness/combat/prompt/tracked-objective hierarchy is crisp and readable, the HUD validation reports `objective=Reach the gate source=System19`, and canonical Kentridge evidence visibly presents the tracked objective through the production path.

## Remaining gates

Issue-specific implementation and exact-SHA validation are complete. Close open→closed with closure fields, then merge current `origin/master` into `fixes/agent-9`, open/update the PR to `master`, enable auto-merge, and monitor required PR `affected` until merged. Completion is only when this closed SceneIssue is visible on `origin/master`.
