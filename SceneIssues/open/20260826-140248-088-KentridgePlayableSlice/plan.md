# Plan

## Evidence / target
- No marked circles; whole-frame acceptance is the note: land the `design/combat-input-modules` production seam and place three forest bandits that begin combat when the Kentridge player approaches.
- Refreshed master has no production `Assets/Game/Combat` or `Assets/Game/Input`; the live Kentridge scene remains one continuous world and its legacy controller reads Unity input directly.
- The authored Kentridge→Hightown theme map places PineForest from about 142–362 m Z; the captured pose at 155.2 m Z is inside that forest.

## Competing hypotheses / discriminators
1. **Supported — production migration missing.** Combat/Input production modules are absent while the prototype remains.
2. **Rejected — modules exist but Kentridge omitted wiring.** Direct refreshed-master path probes return absent for both modules.
- Exact CI `fa782d3…` isolated a test-only Unity 6000.5 `GetInstanceID()` compile incompatibility; direct reference identity preserves the invariant.
- Exact CI `56980e4…` then compiled production but exposed startup-only scene composition: a scene loaded after PlayMode startup had no encounter. Its 30 s replay also remained behind the existing loading cover.

## Fix / behavioral regression
- Add device-neutral Input API/runtime and deterministic Combat API/runtime; Combat samples input then suppresses the legacy Unity frame so exploration cannot consume the same intent.
- Compose three persistent bandits from the semantic PineForest band, using the repo's rigged character resource plus readable outlaw gear; proximity starts one in-place Combat session with the same actors and Combat input context.
- Install composition idempotently for every Kentridge scene load, not only process startup.
- Regression loads the real slice and proves exactly three PineForest bandits, proximity activation, Combat context, same scene/object identity, and three enemy participants.

## Blast radius / cost
- New Combat/Input modules plus one Kentridge composition component/test; no other scene/capture changes.
- Pre-combat cost is three squared-distance checks/frame; ground probes only within 96 m. Active input/command dispatch is O(1); presentation allocations occur only at spawn.

## Verification
- Fresh exact-SHA PlayMode CI must pass the regression and a 60 s saved-pose replay. Reject promotion unless the 1928×836 original pose shows clean Kentridge forest, three readable grounded bandits, and no debug/editor/replay overlays.
