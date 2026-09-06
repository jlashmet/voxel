# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition and shared standalone-player validation. No new generic gameplay module, alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance

Prove FrontEnd -> New Game -> GameplayReady -> movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Application owns input/session lifetime; Kentridge owns named content/adapters. Use canonical public subsystem authority and the shared player harness.

## Evidence / next experiment

Compilation/upstream fixture defects were corrected, including master merge `36ecce581846fe6b0e1c021f9980890c279ad3e4`. Runs `34000242054`, `34001710667`, `34004591329`, and `34006180411` passed editor validation but stalled in MoveToDestination. The `_openingGameplayReleased` readiness gate is not sufficient. Latest completed request `1b08b40d1bad21e866a2d2095edf75887fdd8863` tested `37416dd7b1a35b8f9a181f8d3005cf35b103906b`; artifact `9981502669` confirms the same control `(111.5,32.1,82.8)` and failure `(101.2,22.0,55.9)` positions, without exit/input diagnostics. Captures are not production-quality traversal proof.

Hypotheses: (1) input/physical opening access is blocked; (2) opening exit works but later direct destination steering fails. Discriminate with the production exit-only SceneRuntime/Validation/KentridgeOpeningControlValidation scene: read device/input/kinematics and opening focus, require movement/public-exit state, then inspect actual trajectory/captures. A marker alone does not prove a walkable doorway. No speculative route fix before evidence.

Run `34006180411` did not discover this probe. The planner defines roots from owned Tests assemblies and reads only each root's Validation. SceneRuntime tests were incorrectly under parent Playable/Tests/EditMode/FarWorld. Commit `fb46d3f331fe3c960f703f103465b3b329eb194f` relocates the unchanged assembly/test contents into SceneRuntime/Tests/EditMode, preserving existing metadata. The next derived plan must include the local scene. No CI script/manual registration changes.

## Restore correction / cost

T24-023 audit found omitted vitality: fresh composition reset health. Commit `a356eafa6fafbb781f31e1741423de3545548d8a` adds required vitality schema-1 persistence via current-graph IVitalityService.Capture/Restore, preserving current/maximum/defeated/revision without event replay. Canonical validation now compares saved/restored health and logs exit/target state. Nine behavioral cases cover damaged/defeated round trips, fresh-registry writes, and malformed payloads. These additions remain Unity-unvalidated; incomplete old saves fail closed for the missing required section.

No per-frame persistence work. One bounded array and validation set per operation, maximum 65,536 entries. Payload is four count bytes plus each length-prefixed UTF-8 id and 17 state bytes; no budget changes.

## Remaining gates

Submit a new exact-head request on the existing ci-test/fixes/agent-2 transport now that the prior run is terminal. Require discovery/execution of the local probe, health regressions, all selected modules and canonical route. Domain-only portions use module tests; runtime assemblies own production-path scenes. Finish every task, inspect captures, close open -> closed, integrate current master, open/update PR, enable auto-merge, monitor affected, and verify closure on master.
