# Plan

## Observed / acceptance
`KentridgePlayableSlice` owned reusable character runtime/presentation policy and Madeline resolved through placeholder presentation despite production `Resources/Characters/Madeline/Madeline.prefab` (model GUID `f593982524b374c80b946d9e4670471d`). This Architecture issue has `captures: []`, so there are no marked image regions or poses to inspect/replay.

Acceptance: scene code selects scenario/spawn/input; a Game-owned host owns movement, campaign actors, model/animation binding and grounding. Madeline resolves to the production Humanoid prefab. Opening choreography, gameplay handoff, pub traversal and destination interaction remain behavioral invariants, and the exact built Kentridge player launches without runtime exceptions.

## Hypotheses / discriminators
1. **Falsified:** a reusable Game-owned implementation already existed and Kentridge only bypassed it. Audit found the campaign interface/lower-level seams but no reusable host.
2. **Selected:** Kentridge had correct lower-level behavior but scene-local ownership. Extract to `KentridgeCharacterHost`; falsifier is changed actor choreography, model binding, traversal, handoff, or startup.
3. **CI-4 camera failure falsified as host regression:** exact-SHA run `33218551844` captured opening vertical separation `1.72000122 m` against a test threshold `>2.5 m`. Pre-extraction source at `a3acc64d...` has the identical camera formula: focus is `floor + 0.9 m`, while camera height is intentionally capped below the generated pub first-floor slab. The old threshold therefore contradicted production before this issue. The regression now asserts the architecture-derived `>1.3 m` separation plus the existing downward-facing/fixed-camera checks.

## Fix / regression
- `KentridgeCharacterHost` owns motor, campaign actors, model selection and animation driving; `KentridgePlayableSlice` delegates to one host.
- Generic foot alignment is `CharacterVisualFootGrounding`; Madeline instantiates the production prefab intact, with Humanoid avatar/controller present and root motion disabled.
- `KentridgePlayableScenePlayTests.LaunchScene_NewGameCutscene_ReleasesControl_AndPlayerWalksOutIntoKentridge` is the behavioral regression: production scene, shared-host identity, production Madeline, opening movement/camera, generated pub exit, destination interaction and control handoffs. Its configured profile also builds/launches the exact Kentridge player.

## Blast radius / cost
Only Kentridge playable character composition, reusable visual grounding, Madeline presentation, and this existing production acceptance are touched; other placeholders remain unchanged. Runtime cost is the existing per-character host/animation tick plus one production Madeline prefab; grounding replaces the former Kentridge-only behavior and adds no world/streaming scan.

## Remaining gates
- [ ] Fresh exact-SHA targeted PlayMode + configured built-player validation.
- [ ] After green, set pending metadata/move open -> pending, then close/fix with `resolvedUtc`.
- [ ] Merge latest `master`; non-force push the exact feature head to `master`.
