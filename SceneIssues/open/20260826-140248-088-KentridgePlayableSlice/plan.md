# Plan

## Evidence / target
- The capture has no marked circles; the whole-frame note is the acceptance target: implement `design/combat-input-modules`, then place three forest bandits that start combat when the Kentridge player approaches.
- Refreshed production has neither `Assets/Game/Combat` nor `Assets/Game/Input`; combat still lives under `Assets/CombatPrototype`. The playable scene is a single in-place world rooted on `Kentridge Player Camera`.
- The design’s first vertical slice requires production Combat/Input module boundaries, a command validation boundary, normal actors remaining in the same world, automatic encounter lifecycle, and Combat input ownership.

## Competing hypotheses / discriminator
1. **Supported — migration never landed.** Direct path probes for `Assets/Game/Combat` and `Assets/Game/Input` return absent while the prototype remains; Kentridge therefore has no production `ICombatService` to wire.
2. **Rejected — modules exist but Kentridge omitted composition.** Refreshed master contains no production Combat/Input modules, so scene-only wiring cannot satisfy the design.

## Fix / behavioral regression
- Add device-neutral `Game.Input.Api` plus Unity-owned `Game.Input.Runtime` context/snapshot implementation.
- Add `Game.Combat.Api` lifecycle contracts and a deterministic `Game.Combat.Runtime` command dispatcher; simulation never reads Unity input.
- Add a Kentridge playable composition component: spawn three persistent bandit world actors on terrain ahead of the player, detect proximity, begin one in-place combat session containing the same player/bandit identities, push Combat input context, and drive player movement intents through validated Combat commands.
- Regression: load `KentridgePlayableSlice`, require exactly three bandits, move the real player camera inside one bandit’s trigger radius, and assert one active production Combat session + Combat input context with no scene change or bandit replacement.

## Blast radius / cost
- New Combat/Input modules plus one component on `KentridgePlayableSlice`; no other scene/capture changes.
- Proximity check is three squared-distance comparisons/frame until encounter start; combat input sampling/command dispatch is O(1) per frame. No per-frame allocations are required after spawn.

## Verification
- Exact-SHA PlayMode CI must pass the regression and saved-pose replay. Visually compare replay with the original whole frame: clean Kentridge world, three readable forest bandits present, no debug/editor overlays.
