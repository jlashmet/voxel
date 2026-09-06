# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition and shared standalone-player validation. No new generic gameplay module, alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance and boundaries

Prove FrontEnd -> New Game -> GameplayReady -> production movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Application owns input and session lifetime; Kentridge owns named content and adapters. Use canonical public subsystem authority and the shared player harness.

## Material evidence / next experiment

Compilation and upstream Structures fixture failures were corrected, including master integration `36ecce581846fe6b0e1c021f9980890c279ad3e4`. Runs `34000242054` and `34001710667` passed editor validation but stalled in `MoveToDestination` after different route attempts. The subsequent readiness fix requires `_openingGameplayReleased`; run `34004591329` on product `bcb317a986661265aee5ade5155bdf0f523e54bd` also failed that player milestone after editor tests passed. Therefore readiness alone is not a sufficient movement fix.

The last player log records control at `(111.5,32.1,82.8)` and failure at `(101.2,22.0,55.9)`. It does not log the pub-exit flag or decoded movement; neither the precise blocked structure nor release-to-failure motion may be inferred from those two points. Direct captures show unreadable washed-out world views and later close dark geometry, not production-quality traversal proof.

Two remaining hypotheses: (1) input delivery or physical opening access is blocked; (2) opening exit works, but direct destination steering is blocked later. The next discriminating experiment is the production exit-only `SceneRuntime/Validation/KentridgeOpeningControlValidation` scene: observe device/input/kinematics and require physical exit without teleports or collision bypass. Do not make another speculative route fix before its evidence.

**Exact request:** `1b08b40d1bad21e866a2d2095edf75887fdd8863`, run `34006180411`, product `37416dd7b1a35b8f9a181f8d3005cf35b103906b`. Preserve it while queued/running. Later source changes require later exact-SHA validation.

## Required independent correction

The restore audit found omitted vitality: the persistence bridge saves player identity/lifecycle/kinematics but no `IVitalityService` state; fresh forest composition registers full vitality. T24-039 must preserve current/maximum/defeated/revision through the existing public Capture/Restore contract and strengthen restored-state assertions. This is T24-023 correctness, not a new gameplay system. Add behavioral round-trip coverage before accepting it.

## Validation / cost / remaining gates

Application, Input, Kentridge Playable and its SceneRuntime assembly own production-path local validation scenes. Kentridge Runtime, Persistence and Combat use module-local domain tests where behavior is headless; canonical Kentridge remains integration proof. Keep test orchestration read-only except ordinary player input and public lifecycle intents. Measure added persistence payload/allocation; do not weaken budgets.

Finish every task and required module/exact-SHA player gate; inspect durable captures directly. Only then close open -> closed with resolution fields, integrate current master, open/update the feature PR, enable auto-merge, monitor `affected`, and verify the closed issue on `origin/master`.
