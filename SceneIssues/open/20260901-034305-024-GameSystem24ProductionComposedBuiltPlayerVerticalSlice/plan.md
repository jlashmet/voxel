# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition and shared standalone-player validation. No new generic gameplay module, alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance

Prove FrontEnd -> New Game -> GameplayReady -> production movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Application owns input/session lifetime; Kentridge owns named content/adapters. Use canonical public subsystem authority and the shared player harness.

## Evidence / discriminating experiment

Compilation and upstream Structures fixture failures were corrected, including master merge `36ecce581846fe6b0e1c021f9980890c279ad3e4`. Runs `34000242054` and `34001710667` passed editor validation but stalled in `MoveToDestination` after different route attempts. Requiring `_openingGameplayReleased` did not resolve it: run `34004591329` on product `bcb317a986661265aee5ade5155bdf0f523e54bd` also failed that player milestone.

The player log records control at `(111.5,32.1,82.8)` and failure at `(101.2,22.0,55.9)`, without pub-exit/input diagnostics. Neither the blocked structure nor trajectory can be inferred. Captures are unreadable washed-out world views and close dark geometry, not production-quality proof.

Hypotheses: (1) input/physical opening access is blocked; (2) opening exit works but subsequent direct destination steering fails. The production exit-only `SceneRuntime/Validation/KentridgeOpeningControlValidation` scene observes device/input/kinematics and requires physical exit without teleports/collision bypass. No further speculative route fix before its evidence.

Discovery audit found this probe was not registered by structure: `module-validation-plan.py` discovers roots from owned `Tests` assemblies and scenes only in that root's `Validation`. SceneRuntime's existing test assembly lived under parent `Playable/Tests/EditMode/FarWorld`. Move that unchanged assembly/test content to `SceneRuntime/Tests/EditMode`, preserving existing metadata, so normal discovery owns both tests and the probe. No CI script or manual target list changes.

**Active request:** `1b08b40d1bad21e866a2d2095edf75887fdd8863`, run `34006180411`, product `37416dd7b1a35b8f9a181f8d3005cf35b103906b`. Preserve it; it predates the ownership correction and cannot prove the new local target.

## Restore correction / cost

T24-023 audit found omitted vitality: fresh composition reset health. T24-039 adds required `vitality` schema-1 persistence through current-graph `IVitalityService.Capture/Restore`, preserving current/maximum/defeated/revision without event replay. The canonical driver compares saved/restored health and logs exit/target diagnostics. Nine behavioral cases cover damaged/defeated round trips, fresh-registry writes, malformed counts/ids/defeat/truncation/trailing bytes. Unity validation remains pending; incomplete old saves fail closed for the missing required section.

No per-frame persistence work. One bounded snapshot array and validation set per operation (maximum 65,536 entries); payload is four count bytes plus each length-prefixed UTF-8 id and 17 state bytes.

## Remaining gates

Application, Input, Kentridge Playable and SceneRuntime own focused production-path scenes; pure domain portions use module tests. Finish every task and exact-SHA module/player gate, inspect captures, then close open -> closed, integrate current master, open/update the PR, enable auto-merge, monitor `affected`, and verify closure on master.
