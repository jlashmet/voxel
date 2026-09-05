# 24 Production-composed built-player vertical slice — implementation plan

**Target ownership:** Kentridge production composition plus shared standalone-player validation. No new generic gameplay module.

## Observed behavior / acceptance

Current branch head before this plan refresh was `0b27b59391e8779610b1fab80a19ee9d5f33ae98`; current `origin/master` is `51797c954490425964e602d6bb2252a0d7a7c5aa`, and the branch is ahead with no missing master commits.

The production root now owns one `UnityPlayerInputReader`/input context, `ApplicationFlowCoordinator`, `GameSessionOrchestrator`, frontend, persistence bridge, and the single Application/session update loop. The raw `KentridgeUnityInputBridge` is removed and the production scene serializes frontend-first boot. The slice consumes the composed graph and no longer ticks `IGameSessionControl` itself.

Remaining acceptance is real built-player proof of FrontEnd -> New Game -> `GameplayReady` -> movement -> authored NPC/story progression -> WorldBuilder encounter -> Character/Vitality/Combat resolution -> WorldObject/Loot/Inventory pickup -> save -> ordered teardown -> Continue -> restored live gameplay.

## Hypotheses / material results

1. **Existing combat reward/well quest can stand in for the loot leg.** Falsified: T24-016 requires a real Systems 13/10/09 world pickup/container/drop/transfer action. The canonical `WorldObjectRegistry` + `ItemPickupObject` + `WorldObjectLootAdapter` path exists and must be composed instead.
2. **The current persistence contributors are sufficient for Continue.** Falsified for the representative pickup: campaign, inventory, player and encounter state are persisted, but T24-023 also requires relevant WorldObject state. The pickup state must round-trip with the same save.
3. **A thin session-scoped Kentridge interaction adapter plus generic local-character interaction entry point is sufficient.** Selected. Reuse the canonical WorldObject/Loot/Inventory services; keep named item/drop policy in Kentridge composition. The authored destination NPC already satisfies the interaction/story leg, so no new well-command path is required for System24.

## Validation / ownership

Affected roots are Kentridge Playable/Runtime, Application/Input/Persistence, and (only if required for semantic local interaction) WorldObjects. Application/Input/Kentridge Playable retain their focused module-local validation scenes. Kentridge Runtime and Persistence are headless/domain composition and use module-local EditMode coverage; no synthetic scene is appropriate. Any WorldObjects change must add module-local behavioral regression coverage.

The feature-specific assembled-player scenario must reuse `tools/player-validation.py` / `showcase-player-capture.sh`. In-player orchestration may observe read-only semantic snapshots and inject through the Input System, but may not mutate gameplay authority, teleport characters, or call completion setters. Synchronization must be semantic milestones with bounded timeouts and durable milestone/log/screenshot evidence.

## Remaining gates

Complete T24-003 onward, run repository-selected module tests/validation, then exact-SHA targeted CI on `ci-test/fixes/agent-2` without replacing queued/running work. After green exact-SHA proof, close open -> closed with resolution metadata, fetch/merge latest master, open/update the PR, enable auto-merge, and monitor required `affected` until merged and the closed SceneIssue is visible on master.
