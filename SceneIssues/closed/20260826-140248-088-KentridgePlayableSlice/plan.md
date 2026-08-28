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
- Exact CI `102b9a1…` passed behavior/replay but native evidence exposed magenta runtime gear, isolating an incompatible primitive material path.

## Fix / regression
- Added device-neutral Input API/runtime and deterministic Combat API/runtime; Combat owns input while active so exploration cannot consume the same frame.
- Composed three persistent rigged bandits from the semantic PineForest band; proximity starts one in-place Combat session with the same actors.
- Installed composition idempotently on every Kentridge scene load and reused the rigged actor’s shipped shader/material for runtime outlaw gear.
- Regression loads the real slice and proves three PineForest bandits, player-compatible gear shader, proximity activation, Combat context, same scene/object identity, and three enemy participants.

## Blast radius / cost
- New Combat/Input modules plus Kentridge composition/presentation and one PlayMode test; no other scene/capture changes.
- Pre-combat cost is three squared-distance checks/frame; ground probes only within 96 m. Material repair runs once at scene Start; active input/command dispatch is O(1).

## Verification
- Final exact request `1a1c4fcd9951dd046611175705a3913e982ec257`, run `33133874979`, passed the focused PlayMode test and 60 s real-player replay.
- Opened native 1928×836 replay: three readable grounded PineForest bandits, no magenta/error materials, no editor/debug/replay overlay obscuring them.
- Repository image commit intentionally skipped per coordinator instruction; durable replay details are in `verification-replay.txt`.
