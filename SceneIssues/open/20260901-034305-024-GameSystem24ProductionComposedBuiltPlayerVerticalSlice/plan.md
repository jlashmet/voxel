# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition and shared standalone-player validation. No new generic gameplay module, alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance and boundaries

Prove FrontEnd -> New Game -> GameplayReady -> production movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Application owns input/session lifetime; Kentridge owns named content/adapters. Use canonical public subsystem authority and the shared player harness.

## Evidence / next experiment

Compilation and upstream Structures fixture failures were corrected, including master integration `36ecce581846fe6b0e1c021f9980890c279ad3e4`. Runs `34000242054` and `34001710667` passed editor validation but stalled in `MoveToDestination` after different route attempts. Requiring `_openingGameplayReleased` did not resolve it: run `34004591329` on product `bcb317a986661265aee5ade5155bdf0f523e54bd` also failed that player milestone after editor tests passed.

The last player log records control at `(111.5,32.1,82.8)` and failure at `(101.2,22.0,55.9)`. It does not log pub exit or decoded movement. Neither the blocked structure nor release-to-failure trajectory can be inferred from those two points. Captures show unreadable washed-out world views and later close dark geometry, not production-quality traversal proof.

Remaining hypotheses: (1) input delivery/physical opening access is blocked; (2) opening exit works but direct destination steering is blocked later. The discriminating experiment is the production exit-only `SceneRuntime/Validation/KentridgeOpeningControlValidation` scene: observe device/input/kinematics and require physical exit without teleports/collision bypass. No further speculative route fix before its evidence.

**Active exact request:** `1b08b40d1bad21e866a2d2095edf75887fdd8863`, run `34006180411`, product `37416dd7b1a35b8f9a181f8d3005cf35b103906b`. Preserve while queued/running. Subsequent code requires later exact-SHA validation.

## Required independent correction

The T24-023 audit found omitted vitality: saves included identity/lifecycle/kinematics, but fresh composition reset health. T24-039 now adds required `vitality` schema-1 persistence through the current graph's public `IVitalityService.Capture/Restore`, preserving current/maximum/defeated/revision without event replay. The canonical driver compares saved/restored health and adds read-only exit/target diagnostics. Nine module-local behavioral cases cover damaged/defeated round trips, fresh-registry reuse, invalid counts, duplicate/missing ids, inconsistent defeat, truncation and trailing bytes. Implementation is not yet Unity-validated; old incomplete saves fail closed for the missing required section.

Cost: no per-frame persistence work; one bounded snapshot array plus validation set per operation, at most 65,536 entries. Payload is four count bytes plus each length-prefixed UTF-8 character id and 17 state bytes. Do not weaken budgets.

## Remaining gates

Application, Input, Kentridge Playable and SceneRuntime own production-path local validation scenes; pure domain portions use module-local tests. Finish every task and required module/exact-SHA player gate; inspect durable captures. Then close open -> closed with resolution fields, integrate current master, open/update the feature PR, enable auto-merge, monitor `affected`, and verify closure on `origin/master`.
