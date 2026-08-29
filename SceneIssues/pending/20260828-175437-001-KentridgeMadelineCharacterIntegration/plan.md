# Plan

## Observed / acceptance
`KentridgePlayableSlice` owned reusable character runtime/presentation policy and Madeline resolved through placeholder presentation despite production `Resources/Characters/Madeline/Madeline.prefab` (model GUID `f593982524b374c80b946d9e4670471d`). This Architecture issue has `captures: []`, so there are no marked image regions or poses to inspect/replay.

Acceptance: scene code selects scenario/spawn/input; a Game-owned host owns movement, campaign actors, model/animation binding and grounding. Madeline resolves to the production Humanoid prefab. Opening choreography, gameplay handoff, pub traversal and destination interaction remain behavioral invariants, and the exact built Kentridge player launches without runtime exceptions.

## Hypotheses / discriminators
1. **Falsified:** a reusable Game-owned implementation already existed and Kentridge only bypassed it. Audit found the campaign interface/lower-level seams but no reusable host.
2. **Selected:** Kentridge had correct lower-level behavior but scene-local ownership. Extract to `KentridgeCharacterHost`; falsifier is changed actor choreography, model binding, traversal, handoff, or startup.
3. **CI-4 stale camera threshold:** run `33218551844` captured `1.72000122 m` vertical separation against inherited `>2.5 m`; pre-extraction source used the same camera formula capped below the pub slab, so the regression now asserts the architecture-derived `>1.3 m` separation.
4. **CI-5 product failure:** run `33220289826` captured the establishing driver moving `5.5184145 m` while the opening camera remained active. The strict `<=0.01 m` fixed-shot assertion predates the extraction, so it stayed strict; scene flow now reapplies the stored opening pose each active-camera frame.

## Fix / regression
- `KentridgeCharacterHost` owns motor, campaign actors, model selection and animation driving; `KentridgePlayableSlice` delegates to one host.
- Generic foot alignment is `CharacterVisualFootGrounding`; Madeline instantiates the production prefab intact, with Humanoid avatar/controller present and root motion disabled.
- `KentridgePlayableScenePlayTests.LaunchScene_NewGameCutscene_ReleasesControl_AndPlayerWalksOutIntoKentridge` covers production scene, shared-host identity, production Madeline, opening movement/fixed camera, generated pub exit, destination interaction and control handoffs.

## Runtime evidence / cost
Exact source `fb962fe2f055e1b31537e737a9c4493667fc5362` passed final run `33222884817`: focused PlayMode plus configured real-player build/launch/capture of `Assets/Scenes/KentridgePlayableSlice.unity`. Blast radius is limited to Kentridge composition, reusable visual grounding, Madeline presentation and the acceptance test. Runtime cost is one production Madeline prefab plus the existing host/animation tick and two transform assignments per active opening-camera frame; no extra world/streaming scan.

## Remaining gates
- [x] Fresh exact-SHA targeted PlayMode + configured built-player validation.
- [x] Complete pending metadata and move open -> pending.
- [ ] Move pending -> closed with `resolvedUtc`.
- [ ] Merge latest `master`; non-force push the exact feature head to `master`.
