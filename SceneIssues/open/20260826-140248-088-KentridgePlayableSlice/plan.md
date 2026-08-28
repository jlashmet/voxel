# Plan

## Evidence / target
- No marked circles; whole-frame acceptance is the note: land the `design/combat-input-modules` production seam and place three forest bandits that begin combat when the Kentridge player approaches.
- Refreshed master had no production `Assets/Game/Combat` or `Assets/Game/Input`; Kentridge remains one continuous world and its legacy controller reads Unity input directly.
- The authored Kentridge→Hightown theme map places PineForest from about 142–362 m Z; the captured pose at 155.2 m Z is inside that forest.

## Competing hypotheses / discriminators
1. **Supported — production migration missing.** Combat/Input production modules were absent while the prototype remained.
2. **Rejected — modules existed but Kentridge omitted wiring.** Refreshed-master path probes returned absent for both modules.
- Exact CI `fa782d3…` isolated a test-only Unity 6000.5 `GetInstanceID()` incompatibility; direct reference identity preserves the invariant.
- Exact CI `56980e4…` compiled production but exposed startup-only composition; a scene loaded after PlayMode startup had no encounter. Its 30 s replay never left the loading cover.
- Exact CI `102b9a1…` passed behavior and 60 s replay, but opened 1928×836 evidence showed bright-magenta runtime gear while the rigged actor rendered normally: built-in primitive material is incompatible with the URP player.

## Fix / behavioral regression
- Add device-neutral Input API/runtime and deterministic Combat API/runtime; Combat samples input then suppresses the legacy Unity frame so exploration cannot consume the same intent.
- Compose three persistent bandits from the semantic PineForest band with the repo's rigged character resource; proximity starts one in-place Combat session with the same actors and Combat input context.
- Install composition idempotently for every Kentridge scene load, not only process startup.
- Reuse the rigged actor's shipped material/shader for runtime outlaw gear. Regression proves three PineForest bandits, player-compatible gear shader, proximity activation, Combat context, same scene/object identity, and three enemy participants.

## Blast radius / cost
- New Combat/Input modules plus Kentridge composition/presentation and one PlayMode test; no other scene/capture changes.
- Pre-combat cost is three squared-distance checks/frame; ground probes only within 96 m. Material repair runs once at scene Start; active input/command dispatch is O(1).

## Verification
- Fresh exact-SHA PlayMode CI must pass the regression and a 60 s saved-pose replay. Reject promotion unless native 1928×836 evidence shows clean Kentridge forest, three readable grounded bandits with no magenta/error materials, and no debug/editor/replay overlays.
