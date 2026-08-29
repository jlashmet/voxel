# Plan

## Observed / acceptance
`KentridgePlayableSlice` owned reusable character runtime/presentation policy and Madeline resolved through placeholder presentation despite production `Resources/Characters/Madeline/Madeline.prefab` (model GUID `f593982524b374c80b946d9e4670471d`). This Architecture issue has `captures: []`, so there are no marked image regions or poses to inspect/replay.

Acceptance: scene code selects scenario/spawn/input; a Game-owned host owns movement, campaign actors, model/animation binding and grounding. Madeline resolves to the production Humanoid prefab. Opening choreography, gameplay handoff, pub traversal and destination interaction remain behavioral invariants, and the exact built Kentridge player launches without runtime exceptions.

## Hypotheses / discriminators
1. **Falsified:** a reusable Game-owned implementation already existed and Kentridge only bypassed it. Audit found the campaign interface/lower-level seams but no reusable host.
2. **Selected:** Kentridge had correct lower-level behavior but scene-local ownership. Extract to `KentridgeCharacterHost`; falsifier is changed actor choreography, model binding, traversal, handoff, or startup.
3. **CI-4 stale camera threshold:** exact-SHA run `33218551844` captured opening vertical separation `1.72000122 m` against `>2.5 m`. Pre-extraction source at `a3acc64d...` uses the same camera formula and intentionally caps the camera below the pub first-floor slab, so the regression now asserts the architecture-derived `>1.3 m` separation.
4. **CI-5 product failure:** exact-SHA run `33220289826` captured the establishing driver moving `5.5184145 m` while the opening camera remained active; the same run's standalone Kentridge player passed. The strict `<=0.01 m` fixed-shot assertion already existed at pre-extraction `a3acc64d...`, so weakening/removing it was rejected. Host tick parity also falsifies direct host mutation of the scene driver. The scene-flow coordinator now reapplies its stored opening pose each active-camera frame, making the authored fixed-shot state authoritative.

## Fix / regression
- `KentridgeCharacterHost` owns motor, campaign actors, model selection and animation driving; `KentridgePlayableSlice` delegates to one host.
- Generic foot alignment is `CharacterVisualFootGrounding`; Madeline instantiates the production prefab intact, with Humanoid avatar/controller present and root motion disabled.
- `KentridgePlayableScenePlayTests.LaunchScene_NewGameCutscene_ReleasesControl_AndPlayerWalksOutIntoKentridge` remains the behavioral regression: production scene, shared-host identity, production Madeline, opening actor movement with fixed camera, generated pub exit, destination interaction and control handoffs. Its configured profile also builds/launches the exact Kentridge player.

## Blast radius / cost
Only Kentridge playable character composition, reusable visual grounding, Madeline presentation, scene-flow fixed-camera enforcement, and this existing production acceptance are touched; other placeholders remain unchanged. Runtime cost is the existing per-character host/animation tick plus one production Madeline prefab and two transform assignments per frame only while the opening establishing camera is active; no additional world/streaming scan.

## Remaining gates
- [ ] Fresh exact-SHA targeted PlayMode + configured built-player validation.
- [ ] After green, set pending metadata/move open -> pending, then close/fix with `resolvedUtc`.
- [ ] Merge latest `master`; non-force push the exact feature head to `master`.
