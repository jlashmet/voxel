# 24 Production-composed built-player vertical slice — implementation plan

**Target ownership:** Kentridge production composition plus shared standalone-player validation. No new generic gameplay module.

## Observed behavior / acceptance

The production root owns the single Application/input/session lifecycle. The raw Kentridge input bridge is removed, frontend-first boot is serialized, and Kentridge consumes the composed session graph rather than ticking session authority itself.

Remaining acceptance is exact-SHA built-player proof of FrontEnd -> New Game -> `GameplayReady` -> movement -> authored NPC/story progression -> WorldBuilder encounter -> player-input-driven Combat/Vitality resolution -> WorldObject/Loot/Inventory pickup -> save -> ordered teardown -> Continue -> restored live gameplay, plus direct inspection of durable player evidence.

## Hypotheses / material results

1. **Existing reward/well flow can stand in for loot.** Falsified: T24-016 requires a real WorldObject/Loot/Inventory transfer. Selected path is `WorldObjectRegistry` + `ItemPickupObject` + `WorldObjectLootAdapter`, with named loot policy in Kentridge composition.
2. **Existing persistence contributors are sufficient.** Falsified for the representative pickup: WorldObject state must round-trip with campaign, inventory, player and encounter state.
3. **A session-scoped Kentridge interaction adapter is sufficient.** Selected: reuse canonical authority; validation may inject physical Input System events and observe read-only semantic state only.
4. **AI may execute the representative player combat leg.** Falsified by ownership audit. Production now maps `PrimaryPressed` through `CombatInputController`; deterministic AI advances enemy turns only; the System24 driver emits physical primary input.
5. **Exact-SHA `e724343d0f2a2d05631e1305f344ba94f832e94f` is runtime-ready.** Falsified by targeted run `33995706164`: repository-owned validation reached Unity but failed compilation on six `Application.isPlaying` references resolving to sibling namespace `Game.Application`. No standalone player ran. The narrow fix is explicit `UnityEngine.Application.isPlaying` qualification; no gameplay behavior changes.

## Validation / ownership

Affected player/runtime roots Application, Input and Kentridge Playable retain module-local validation scenes/scenarios. Kentridge Runtime, Persistence and Combat are headless/domain/runtime authority here; they use module-local EditMode coverage, with Combat receiving focused player-input regression coverage. WorldObjects retains its behavioral regression surface. The feature scenario reuses the shared player harness and synchronizes on semantic milestones with bounded timeouts.

**Current feature commit:** `ffa28a1cc342a99ab6a31e0a77d5feb3c034595d` after qualifying the six Unity lifecycle checks exposed by run `33995706164`.

## Remaining gates

Update durable task notes, issue a new exact-SHA request on `ci-test/fixes/agent-2`, and require repository-selected module tests/player validations plus canonical Kentridge integration to pass. Inspect durable built-player evidence directly. Only then complete remaining checkboxes, close open -> closed with resolution metadata, merge current master into the feature branch, open/update the PR, enable auto-merge, monitor required `affected`, and verify the closed SceneIssue on `origin/master`.
