# 24 Production-composed built-player vertical slice — implementation plan

**Target ownership:** Kentridge production composition plus shared standalone-player validation. No new generic gameplay module.

## Observed behavior / acceptance

The production root owns one `UnityPlayerInputReader`/input context, `ApplicationFlowCoordinator`, `GameSessionOrchestrator`, frontend, persistence bridge, and the single Application/session update loop. The raw `KentridgeUnityInputBridge` is removed and the production scene serializes frontend-first boot. The slice consumes the composed graph and no longer ticks `IGameSessionControl` itself.

Remaining acceptance is exact-SHA built-player proof of FrontEnd -> New Game -> `GameplayReady` -> movement -> authored NPC/story progression -> WorldBuilder encounter -> Character/Vitality/Combat resolution -> WorldObject/Loot/Inventory pickup -> save -> ordered teardown -> Continue -> restored live gameplay, plus direct inspection of durable built-player visual evidence.

## Hypotheses / material results

1. **Existing combat reward/well quest can stand in for the loot leg.** Falsified: T24-016 requires a real Systems 13/10/09 world pickup/container/drop/transfer action. The canonical `WorldObjectRegistry` + `ItemPickupObject` + `WorldObjectLootAdapter` path exists and must be composed instead.
2. **The current persistence contributors are sufficient for Continue.** Falsified for the representative pickup: campaign, inventory, player and encounter state are persisted, but T24-023 also requires relevant WorldObject state. The pickup state must round-trip with the same save.
3. **A thin session-scoped Kentridge interaction adapter plus generic local-character interaction entry point is sufficient.** Selected. Reuse the canonical WorldObject/Loot/Inventory services; keep named item/drop policy in Kentridge composition. The authored destination NPC already satisfies the interaction/story leg, so no new well-command path is required for System24.
4. **Exact-SHA `f6b3ace316f7122b48135ea04c0f04078049d9a5` is ready for runtime validation.** Falsified by targeted run `33984774287` attempt 2: repository-owned module validation reached Unity and failed compilation on exactly three product errors before any player scenario ran. The selected corrective fix was narrow assembly/type qualification: add the missing `Game.Combat.Api` reference, import `Game.Composition.Kentridge.Runtime` in the validation driver, and disambiguate `Game.SessionOrchestration.Api.GameSessionSnapshot` in the production root.
5. **Corrected exact-SHA `d0971511dee5affc0064adcc83f6c2e9d7b7b050` is ready for runtime validation.** Falsified by targeted run `33988814815`: Unity progressed farther but compilation still failed. Adding Application references made unqualified `Application.isPlaying`/`Application.Quit` bind to the sibling `Game.Application` namespace in Kentridge scene-runtime files, so those Unity lifecycle calls must be explicitly qualified. The WorldObject loot adapter also needs an unambiguous binding to the intended Kentridge runtime loot-item id.
6. **The deterministic battle driver can stand in for the player's combat leg.** Falsified by ownership audit: `CombatAiBattleDriver.Step()` executes the active participant regardless of team, while the System24 driver never emits a combat attack input. That lets AI substitute for the player and does not satisfy T24-015. The production path must map existing `PlayerInputSnapshot.PrimaryPressed` to a legal local-player attack, run deterministic AI only for enemy turns, and have the built-player driver emit the normal physical Primary input rather than calling Combat authority directly.

## Validation / ownership

Affected roots are Kentridge Playable/Runtime, Application/Input/Persistence, Combat, and (only where required for semantic local interaction) WorldObjects. Application/Input/Kentridge Playable retain their focused module-local validation scenes. Kentridge Runtime and Persistence are headless/domain composition and use module-local EditMode coverage; no synthetic scene is appropriate. Combat player-input behavior gets focused EditMode regression coverage, and any WorldObjects change must retain module-local behavioral regression coverage.

The feature-specific assembled-player scenario reuses `tools/player-validation.py` / `showcase-player-capture.sh`. In-player orchestration may observe read-only semantic snapshots and inject through the Input System, but may not mutate gameplay authority, teleport characters, or call completion setters. Synchronization is semantic milestones with bounded timeouts and durable milestone/log/screenshot evidence.

## Remaining gates

Correct the remaining compile namespace/item binding faults, make the combat proof player-input-driven with enemy-only AI stepping, add focused Combat regression coverage, and finish the ownership/shortcut audit. Then issue a fresh exact-SHA targeted request on `ci-test/fixes/agent-2`. Automatic module tests/validation must compile and pass, including the canonical Kentridge game-integration player scenario. Inspect exact built-player evidence for presentation quality, complete every remaining task, close open -> closed with resolution metadata, fetch/merge latest master, open/update the PR, enable auto-merge, and monitor required `affected` until merged and the closed SceneIssue is visible on master.
